using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Eto.Forms;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using RhinoCopilotForMakers.Contracts;

namespace RhinoCopilotForMakers.Services;

internal sealed class PlanExecutionCoordinator
{
  private ExecutionPlanPayload? _currentPlan;
  private int _nextStepIndex;
  private bool _isRunningStep;
  private bool _isStepRunQueued;
  private readonly Dictionary<string, IReadOnlyList<Guid>> _stepObjectIds = new(StringComparer.OrdinalIgnoreCase);

  public event Action<UI.PlanExecutionState>? StateChanged;
  public event Action<string>? AssistantMessageGenerated;

  public ExecutionPlanPayload? CurrentPlan => _currentPlan;
  public ExecutionStepPayload? CurrentStep =>
    _currentPlan is not null && _nextStepIndex < _currentPlan.Steps.Count
      ? _currentPlan.Steps[_nextStepIndex]
      : null;

  public bool HasPendingPlan => _currentPlan is not null;

  public void LoadPlan(TurnResponse response)
  {
    if (response.Plan is null)
      throw new InvalidOperationException("Plan response did not contain a plan payload.");

    _currentPlan = response.Plan;
    _nextStepIndex = 0;
    _isRunningStep = false;
    _isStepRunQueued = false;
    _stepObjectIds.Clear();

    PublishState(BuildAwaitingApprovalState());
  }

  public void ApprovePlan(bool autoStartFirstStep = true)
  {
    if (_currentPlan is null)
      return;

    AssistantMessageGenerated?.Invoke($"Plan approved. I will start Step 1 of {_currentPlan.Steps.Count} automatically: {CurrentStep?.CommandName}.");
    PublishState(BuildReadyState(detailOverride: "Plan approved. Starting the first Rhino step automatically."));

    if (autoStartFirstStep)
      QueueNextStepRun();
  }

  public void RejectPlan()
  {
    if (_currentPlan is null)
      return;

    AssistantMessageGenerated?.Invoke("Plan rejected. I have not run any Rhino commands.");
    ClearPlan();
  }

  public void Reset()
  {
    _stepObjectIds.Clear();
    ClearPlan();
  }

  public void RequestRunNextStep()
  {
    if (_currentPlan is null || _isRunningStep || CurrentStep is null)
      return;

    QueueNextStepRun();
  }

  private void QueueNextStepRun()
  {
    if (_isStepRunQueued)
      return;

    _isStepRunQueued = true;

    EventHandler? idleHandler = null;
    idleHandler = (_, _) =>
    {
      RhinoApp.Idle -= idleHandler;
      _isStepRunQueued = false;

      if (_currentPlan is null || CurrentStep is null)
        return;

      var started = TryStartPlanRunner();
      if (!started)
      {
        AssistantMessageGenerated?.Invoke("I could not start the Rhino plan runner command.");
        PublishState(BuildReadyState(detailOverride: "Step runner failed to start. Try again."));
      }
    };

    RhinoApp.Idle += idleHandler;
  }

  public Result ExecuteCurrentStep(RhinoDoc doc)
  {
    if (_currentPlan is null || CurrentStep is null)
      return Result.Failure;

    var step = CurrentStep;
    _isRunningStep = true;
    PublishState(BuildRunningState(step));
    AssistantMessageGenerated?.Invoke($"Running Step {step.Sequence} of {_currentPlan.Steps.Count}: {step.CommandName}.");

    var result = step.Type == StepType.DirectGeometryAction || step.Strategy == StepStrategy.DeterministicGeometryWrite
      ? ExecuteDirectGeometryStep(doc, step)
      : ExecuteRhinoCommandStep(doc, step);

    _isRunningStep = false;

    if (result != Result.Success)
    {
      AssistantMessageGenerated?.Invoke($"Step {step.Sequence} did not complete. You can retry {step.CommandName} when ready.");
      PublishState(BuildReadyState(
        titleOverride: "Plan Paused",
        detailOverride: $"Step {step.Sequence} was cancelled or failed. You can retry it when ready.",
        canRunNextStep: true));
      return result;
    }

    AssistantMessageGenerated?.Invoke($"Step {step.Sequence} completed: {step.CommandName}.");
    _nextStepIndex++;

    if (_currentPlan is not null && _nextStepIndex >= _currentPlan.Steps.Count)
    {
      AssistantMessageGenerated?.Invoke("Plan complete. Rhino has finished all mocked steps.");
      ClearPlan();
      return Result.Success;
    }

    var plan = _currentPlan!;
    var nextStep = CurrentStep;
    AssistantMessageGenerated?.Invoke($"Continuing automatically with Step {nextStep?.Sequence} of {plan.Steps.Count}: {nextStep?.CommandName}.");
    PublishState(BuildReadyState(detailOverride: $"Step {step.Sequence} finished. Starting Step {nextStep?.Sequence} automatically."));
    QueueNextStepRun();
    return Result.Success;
  }

  private Result ExecuteRhinoCommandStep(RhinoDoc doc, ExecutionStepPayload step)
  {
    ApplyDependencySelection(doc, step);

    var macro = ResolveMacro(step);
    var ok = RhinoApp.RunScript(macro, false);
    if (!ok)
      return Result.Cancel;

    CaptureMostRecentObject(doc, step.StepId);
    return Result.Success;
  }

  private Result ExecuteDirectGeometryStep(RhinoDoc doc, ExecutionStepPayload step)
  {
    switch (step.CommandName)
    {
      case "CreateRectangle":
        return ExecuteCreateRectangle(doc, step);
      case "CreateCircle":
        return ExecuteCreateCircle(doc, step);
      case "ExtrudeCurve":
        return ExecuteExtrudeCurve(doc, step);
      default:
        AssistantMessageGenerated?.Invoke($"Direct geometry step '{step.CommandName}' is not implemented yet.");
        return Result.Failure;
    }
  }

  private Result ExecuteCreateRectangle(RhinoDoc doc, ExecutionStepPayload step)
  {
    var width = GetDouble(step, "width");
    var height = GetDouble(step, "height");
    var centered = GetBool(step, "centered");

    if (width <= 0 || height <= 0)
    {
      AssistantMessageGenerated?.Invoke("Rectangle step is missing width/height values.");
      return Result.Failure;
    }

    var plane = GetActivePlane(doc);
    var rectangle = new Rectangle3d(plane, width, height);
    if (centered)
      rectangle.RecenterPlane(plane.Origin);

    var curve = rectangle.ToNurbsCurve();
    if (curve is null)
      return Result.Failure;

    var id = doc.Objects.AddCurve(curve);
    if (id == Guid.Empty)
      return Result.Failure;

    doc.Objects.UnselectAll();
    doc.Objects.Select(id);
    doc.Views.Redraw();

    _stepObjectIds[step.StepId] = new[] { id };
    AssistantMessageGenerated?.Invoke($"Created a {width:0.###} x {height:0.###} rectangle on the active CPlane.");
    return Result.Success;
  }

  private Result ExecuteCreateCircle(RhinoDoc doc, ExecutionStepPayload step)
  {
    var radius = GetDouble(step, "radius");
    var centerX = GetDouble(step, "center_x");
    var centerY = GetDouble(step, "center_y");

    if (radius <= 0)
    {
      AssistantMessageGenerated?.Invoke("Circle step is missing a valid radius.");
      return Result.Failure;
    }

    var plane = GetActivePlane(doc);
    var center = plane.PointAt(centerX, centerY);
    var circle = new Circle(plane, center, radius);
    var curve = circle.ToNurbsCurve();
    if (curve is null)
      return Result.Failure;

    var id = doc.Objects.AddCurve(curve);
    if (id == Guid.Empty)
      return Result.Failure;

    doc.Objects.UnselectAll();
    doc.Objects.Select(id);
    doc.Views.Redraw();

    _stepObjectIds[step.StepId] = new[] { id };
    AssistantMessageGenerated?.Invoke($"Created a circle at {centerX:0.###},{centerY:0.###} with radius {radius:0.###}.");
    return Result.Success;
  }

  private Result ExecuteExtrudeCurve(RhinoDoc doc, ExecutionStepPayload step)
  {
    var distance = GetDouble(step, "distance");
    var cap = GetBool(step, "cap", defaultValue: true);
    if (distance <= 0)
    {
      AssistantMessageGenerated?.Invoke("Extrude step is missing a valid distance.");
      return Result.Failure;
    }

    var sourceId = ResolvePrimaryDependencyId(step);
    if (sourceId == Guid.Empty)
    {
      AssistantMessageGenerated?.Invoke("Extrude step could not find a source curve from the previous step.");
      return Result.Failure;
    }

    var sourceObject = doc.Objects.FindId(sourceId);
    var sourceCurve = sourceObject?.Geometry as Curve;
    if (sourceCurve is null)
    {
      AssistantMessageGenerated?.Invoke("Extrude step expected a curve from the previous step.");
      return Result.Failure;
    }

    var extrusion = Extrusion.Create(sourceCurve, distance, cap);
    if (extrusion is null)
      return Result.Failure;

    var extrusionId = doc.Objects.AddExtrusion(extrusion);
    if (extrusionId == Guid.Empty)
      return Result.Failure;

    doc.Objects.UnselectAll();
    doc.Objects.Select(extrusionId);
    doc.Views.Redraw();

    _stepObjectIds[step.StepId] = new[] { extrusionId };
    AssistantMessageGenerated?.Invoke($"Extruded the curve by {distance:0.###}.");
    return Result.Success;
  }

  private void ApplyDependencySelection(RhinoDoc doc, ExecutionStepPayload step)
  {
    var dependencyIds = ResolveDependencyIds(step).ToArray();
    if (dependencyIds.Length == 0)
      return;

    doc.Objects.UnselectAll();
    doc.Objects.Select(dependencyIds);
    doc.Views.Redraw();
  }

  private void CaptureMostRecentObject(RhinoDoc doc, string stepId)
  {
    var recent = doc.Objects.MostRecentObject();
    if (recent is null)
      return;

    _stepObjectIds[stepId] = new[] { recent.Id };
  }

  private string ResolveMacro(ExecutionStepPayload step)
  {
    var macro = string.IsNullOrWhiteSpace(step.Macro) ? "_" + step.CommandName : step.Macro!;
    foreach (var dependency in step.DependsOn)
    {
      var token = $"__STEP_RESULT__:{dependency}";
      if (!macro.Contains(token, StringComparison.OrdinalIgnoreCase))
        continue;

      var replacement = ResolvePrimaryDependencyId(dependency).ToString("B");
      macro = macro.Replace(token, replacement, StringComparison.OrdinalIgnoreCase);
    }

    return macro;
  }

  private IEnumerable<Guid> ResolveDependencyIds(ExecutionStepPayload step)
  {
    foreach (var dependency in step.DependsOn)
    {
      if (_stepObjectIds.TryGetValue(dependency, out var ids))
      {
        foreach (var id in ids)
          yield return id;
      }
    }
  }

  private Guid ResolvePrimaryDependencyId(ExecutionStepPayload step) =>
    step.DependsOn.Count == 0 ? Guid.Empty : ResolvePrimaryDependencyId(step.DependsOn[0]);

  private Guid ResolvePrimaryDependencyId(string stepId)
  {
    if (_stepObjectIds.TryGetValue(stepId, out var ids) && ids.Count > 0)
      return ids[0];

    return Guid.Empty;
  }

  private static Plane GetActivePlane(RhinoDoc doc)
  {
    var activeViewport = doc.Views.ActiveView?.ActiveViewport;
    return activeViewport is null ? Plane.WorldXY : activeViewport.ConstructionPlane();
  }

  private static double GetDouble(ExecutionStepPayload step, string key)
  {
    if (!step.Parameters.TryGetValue(key, out var value) || value is null)
      return 0;

    return value switch
    {
      double d => d,
      float f => f,
      int i => i,
      long l => l,
      decimal m => (double)m,
      JsonElement json when json.ValueKind == JsonValueKind.Number && json.TryGetDouble(out var parsedJson) => parsedJson,
      JsonElement json when json.ValueKind == JsonValueKind.String &&
                              double.TryParse(json.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedJsonString) => parsedJsonString,
      string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
      _ => Convert.ToDouble(value, CultureInfo.InvariantCulture)
    };
  }

  private static bool GetBool(ExecutionStepPayload step, string key, bool defaultValue = false)
  {
    if (!step.Parameters.TryGetValue(key, out var value) || value is null)
      return defaultValue;

    return value switch
    {
      bool b => b,
      JsonElement json when json.ValueKind is JsonValueKind.True or JsonValueKind.False => json.GetBoolean(),
      JsonElement json when json.ValueKind == JsonValueKind.String &&
                              bool.TryParse(json.GetString(), out var parsedJsonBool) => parsedJsonBool,
      string s when bool.TryParse(s, out var parsed) => parsed,
      _ => defaultValue
    };
  }

  private void ClearPlan()
  {
    _currentPlan = null;
    _nextStepIndex = 0;
    _isRunningStep = false;
    _isStepRunQueued = false;
    PublishState(UI.PlanExecutionState.Idle);
  }

  private UI.PlanExecutionState BuildAwaitingApprovalState()
  {
    var plan = _currentPlan!;
    return new UI.PlanExecutionState(
      Phase: UI.PlanExecutionPhase.AwaitingApproval,
      Title: "Plan Ready",
      Detail: $"{plan.Summary} Approve the plan to start Step 1 of {plan.Steps.Count}.",
      CompletedSteps: 0,
      TotalSteps: plan.Steps.Count,
      NextStepLabel: CurrentStep?.CommandName,
      CanApprove: true,
      CanReject: true,
      CanRunNextStep: false);
  }

  private UI.PlanExecutionState BuildReadyState(string? detailOverride = null, string? titleOverride = null, bool? canRunNextStep = null)
  {
    var plan = _currentPlan!;
    var nextStep = CurrentStep;
    var completed = _nextStepIndex;
    var detail = detailOverride ?? (nextStep is null
      ? "No steps remain."
      : $"Next up is Step {nextStep.Sequence} of {plan.Steps.Count}: {nextStep.CommandName}.");

    return new UI.PlanExecutionState(
      Phase: UI.PlanExecutionPhase.ReadyToRunStep,
      Title: titleOverride ?? "Plan Approved",
      Detail: detail,
      CompletedSteps: completed,
      TotalSteps: plan.Steps.Count,
      NextStepLabel: nextStep?.CommandName,
      CanApprove: false,
      CanReject: true,
      CanRunNextStep: canRunNextStep ?? false);
  }

  private UI.PlanExecutionState BuildRunningState(ExecutionStepPayload step)
  {
    var plan = _currentPlan!;
    return new UI.PlanExecutionState(
      Phase: UI.PlanExecutionPhase.RunningStep,
      Title: "Running Rhino Step",
      Detail: step.Strategy == StepStrategy.DeterministicGeometryWrite
        ? $"Step {step.Sequence} of {plan.Steps.Count} is executing automatically: {step.CommandName}."
        : $"Step {step.Sequence} of {plan.Steps.Count} is active: {step.CommandName}. Follow Rhino's command prompts.",
      CompletedSteps: _nextStepIndex,
      TotalSteps: plan.Steps.Count,
      NextStepLabel: step.CommandName,
      CanApprove: false,
      CanReject: false,
      CanRunNextStep: false);
  }

  private void PublishState(UI.PlanExecutionState state) => StateChanged?.Invoke(state);

  private static bool TryStartPlanRunner()
  {
    var attempts = new[]
    {
      "RhinoCopilotExecutePlanStep",
      "_RhinoCopilotExecutePlanStep",
      "! RhinoCopilotExecutePlanStep",
      "! _RhinoCopilotExecutePlanStep"
    };

    foreach (var macro in attempts)
    {
      var started = RhinoApp.RunScript(macro, false);
      RhinoApp.WriteLine($"Nexgen Copilot runner attempt '{macro}' => {(started ? "started" : "failed")}");
      if (started)
        return true;
    }

    return false;
  }
}
