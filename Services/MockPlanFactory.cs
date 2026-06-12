using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RhinoCopilotForMakers.Contracts;
using RhinoCopilotForMakers.Models;

namespace RhinoCopilotForMakers.Services;

internal static class MockPlanFactory
{
  public static TurnResponse? TryCreate(IntentInterpretationPayload? interpretation, RhinoContextSnapshot context)
  {
    if (interpretation is null)
      return null;

    if (interpretation.ExecutionReadiness == ExecutionReadiness.NeedsClarification)
      return BuildClarificationResponse(interpretation);

    var rectangleOperation = interpretation.Operations?.FirstOrDefault(x => x.Action == "create_rectangle_profile");
    if (rectangleOperation is null)
      return null;

    var width = GetDouble(rectangleOperation, "width");
    var height = GetDouble(rectangleOperation, "height");
    var centered = GetBool(rectangleOperation, "centered");
    var extrudeOperation = interpretation.Operations?.FirstOrDefault(x => x.Action == "extrude_profile");
    var filletOperation = interpretation.Operations?.FirstOrDefault(x => x.Action == "fillet_profile_corners");
    double? extrudeHeight = extrudeOperation is null ? null : GetDouble(extrudeOperation, "distance");
    double? filletRadius = filletOperation is null ? null : GetDouble(filletOperation, "radius");
    var hasFilletIntent = filletOperation is not null;

    var planId = "plan_" + Guid.NewGuid().ToString("N")[..8];
    var steps = new List<ExecutionStepPayload>();

    var rectangleStep = new ExecutionStepPayload(
        StepId: "step_create_rectangle",
        Sequence: NextSequence(steps),
        Type: StepType.DirectGeometryAction,
        CommandName: "CreateRectangle",
        Strategy: StepStrategy.DeterministicGeometryWrite,
        Macro: null,
        Interactive: false,
        DependsOn: Array.Empty<string>(),
        Preconditions: new[]
        {
          new StepConditionPayload("rhino_command_idle", Required: true)
        },
        Postconditions: new[]
        {
          new StepConditionPayload("objects_added_min", Value: 1)
        },
        Parameters: new Dictionary<string, object?>
        {
          ["width"] = width,
          ["height"] = height,
          ["centered"] = centered,
          ["document_units"] = context.DocumentUnits
        },
        HumanGuidance: new HumanGuidancePayload(
          BeforeRun: centered
            ? $"I will create a {width:0.###} x {height:0.###} rectangle centered on the active CPlane origin."
            : $"I will create a {width:0.###} x {height:0.###} rectangle from the active CPlane origin."),
        AllowedInCommandState: AllowedCommandState.IdleOnly);

    steps.Add(rectangleStep);

    if (filletOperation is not null)
    {
      var filletStep = new ExecutionStepPayload(
        StepId: "step_fillet_corners",
        Sequence: NextSequence(steps),
        Type: StepType.NativeCommand,
        CommandName: "FilletCorners",
        Strategy: StepStrategy.ScriptedNativeCommand,
        Macro: $"! _SelNone _SelId {SelectionToken(rectangleStep.StepId)} _FilletCorners Radius={filletRadius.GetValueOrDefault().ToString("0.###", CultureInfo.InvariantCulture)} _Enter",
        Interactive: false,
        DependsOn: new[] { rectangleStep.StepId },
        Preconditions: new[]
        {
          new StepConditionPayload("rhino_command_idle", Required: true)
        },
        Postconditions: Array.Empty<StepConditionPayload>(),
        Parameters: new Dictionary<string, object?>
        {
          ["radius"] = filletRadius.GetValueOrDefault()
        },
        HumanGuidance: new HumanGuidancePayload(
          BeforeRun: $"I will fillet the rectangle corners using radius {filletRadius.GetValueOrDefault():0.###}."),
        AllowedInCommandState: AllowedCommandState.IdleOnly);

      steps.Add(filletStep);
    }

    if (extrudeOperation is not null)
    {
      var sourceStep = steps.Last();
      var extrudeStep = new ExecutionStepPayload(
        StepId: "step_extrude_curve",
        Sequence: NextSequence(steps),
        Type: StepType.DirectGeometryAction,
        CommandName: "ExtrudeCurve",
        Strategy: StepStrategy.DeterministicGeometryWrite,
        Macro: null,
        Interactive: false,
        DependsOn: new[] { sourceStep.StepId },
        Preconditions: new[]
        {
          new StepConditionPayload("rhino_command_idle", Required: true)
        },
        Postconditions: new[]
        {
          new StepConditionPayload("objects_added_min", Value: 1)
        },
        Parameters: new Dictionary<string, object?>
        {
          ["distance"] = extrudeHeight.GetValueOrDefault(),
          ["cap"] = GetBool(extrudeOperation, "cap", defaultValue: true)
        },
        HumanGuidance: new HumanGuidancePayload(
          BeforeRun: $"I will extrude the resulting closed curve by {extrudeHeight.GetValueOrDefault():0.###} {context.DocumentUnits}."),
        AllowedInCommandState: AllowedCommandState.IdleOnly);

      steps.Add(extrudeStep);
    }

    var plan = new ExecutionPlanPayload(
      PlanId: planId,
      Intent: interpretation.PrimaryIntent,
      Summary: BuildSummary(width, height, extrudeHeight, hasFilletIntent, filletRadius, context.DocumentUnits),
      RequiresApproval: true,
      ApprovalMode: ApprovalMode.ApprovePlan,
      RiskLevel: RiskLevel.Low,
      ExecutionMode: ExecutionMode.Autonomous,
      Steps: steps);

    var automationSummary = string.Join(", ", steps.Select(step => $"{step.CommandName} ({DescribeStrategy(step.Strategy)})"));

    return new TurnResponse(
      SchemaVersion: CopilotSchema.Version,
      ResponseType: ResponseType.PlanResponse,
      RequestId: null,
      TurnId: "turn_local_mock",
      Routing: new RoutingPayload(
        Mode: RouteMode.MultiStepPlan,
        Confidence: interpretation.Confidence,
        Reason: "Local intent interpreter recognized a supported rectangle/extrude workflow."),
      Message: new AssistantMessagePayload(
        Role: "assistant",
        Text: $"I can turn that into a {steps.Count}-step Rhino plan. Known parameters will be executed automatically where possible: {automationSummary}."),
      Interpretation: interpretation,
      Plan: plan);
  }

  private static string DescribeStrategy(StepStrategy strategy) => strategy switch
  {
    StepStrategy.DeterministicGeometryWrite => "automatic",
    StepStrategy.ScriptedNativeCommand => "scripted",
    StepStrategy.InteractiveNativeCommand => "interactive",
    _ => "guided"
  };

  private static TurnResponse BuildClarificationResponse(IntentInterpretationPayload interpretation)
  {
    var missing = interpretation.MissingInputs ?? Array.Empty<MissingInputPayload>();
    var names = string.Join(", ", missing.Select(x => x.Name.Replace('_', ' ')));
    return new TurnResponse(
      SchemaVersion: CopilotSchema.Version,
      ResponseType: ResponseType.ClarificationRequest,
      RequestId: null,
      TurnId: "turn_local_mock",
      Routing: new RoutingPayload(
        Mode: RouteMode.Clarifying,
        Confidence: interpretation.Confidence,
        Reason: "Local intent interpreter understood the workflow but needs missing execution parameters."),
      Message: new AssistantMessagePayload(
        Role: "assistant",
        Text: $"I understand the workflow, but I still need: {names}. Once you provide those values, I can build an execution plan instead of falling back to copy/paste commands."),
      Interpretation: interpretation,
      MissingInputs: missing);
  }

  private static string BuildSummary(double width, double height, double? extrudeHeight, bool hasFilletIntent, double? filletRadius, string units)
  {
    var parts = new List<string>
    {
      $"Create a {width:0.###} x {height:0.###} rectangle"
    };

    if (hasFilletIntent)
      parts.Add($"fillet the corners with radius {filletRadius.GetValueOrDefault():0.###}");

    if (extrudeHeight.HasValue)
      parts.Add($"extrude it by {extrudeHeight.Value:0.###} {units}");

    return string.Join(", ", parts) + ".";
  }

  private static string SelectionToken(string stepId) => $"__STEP_RESULT__:{stepId}";

  private static int NextSequence(IReadOnlyCollection<ExecutionStepPayload> steps) => steps.Count + 1;

  private static double GetDouble(InterpretedOperationPayload operation, string name)
  {
    var parameter = operation.Parameters.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    if (parameter?.Value is null)
      return 0;

    return parameter.Value switch
    {
      double d => d,
      float f => f,
      int i => i,
      long l => l,
      decimal m => (double)m,
      string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
      _ => Convert.ToDouble(parameter.Value, CultureInfo.InvariantCulture)
    };
  }

  private static bool GetBool(InterpretedOperationPayload operation, string name, bool defaultValue = false)
  {
    var parameter = operation.Parameters.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    if (parameter?.Value is null)
      return defaultValue;

    return parameter.Value switch
    {
      bool b => b,
      string s when bool.TryParse(s, out var parsed) => parsed,
      _ => defaultValue
    };
  }
}
