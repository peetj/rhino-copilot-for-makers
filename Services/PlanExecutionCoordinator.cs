using System;
using Eto.Forms;
using Rhino;
using Rhino.Commands;
using RhinoCopilotForMakers.Contracts;

namespace RhinoCopilotForMakers.Services;

internal sealed class PlanExecutionCoordinator
{
  private ExecutionPlanPayload? _currentPlan;
  private int _nextStepIndex;
  private bool _isRunningStep;

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

    PublishState(BuildAwaitingApprovalState());
  }

  public void ApprovePlan(bool autoStartFirstStep = true)
  {
    if (_currentPlan is null)
      return;

    AssistantMessageGenerated?.Invoke($"Plan approved. Ready to run Step 1 of {_currentPlan.Steps.Count}: {CurrentStep?.CommandName}.");
    PublishState(BuildReadyState());

    if (autoStartFirstStep)
      RequestRunNextStep();
  }

  public void RejectPlan()
  {
    if (_currentPlan is null)
      return;

    AssistantMessageGenerated?.Invoke("Plan rejected. I have not run any Rhino commands.");
    ClearPlan();
  }

  public void RequestRunNextStep()
  {
    if (_currentPlan is null || _isRunningStep || CurrentStep is null)
      return;

    Application.Instance.AsyncInvoke(() =>
    {
      var started = RhinoApp.RunScript("_RhinoCopilotExecutePlanStep", false);
      if (!started)
      {
        AssistantMessageGenerated?.Invoke("I could not start the Rhino plan runner command.");
        PublishState(BuildReadyState(detailOverride: "Step runner failed to start. Try again."));
      }
    });
  }

  public Result ExecuteCurrentStep(RhinoDoc doc)
  {
    if (_currentPlan is null || CurrentStep is null)
      return Result.Failure;

    var step = CurrentStep;
    _isRunningStep = true;
    PublishState(BuildRunningState(step));
    AssistantMessageGenerated?.Invoke($"Running Step {step.Sequence} of {_currentPlan.Steps.Count}: {step.CommandName}.");

    var macro = string.IsNullOrWhiteSpace(step.Macro) ? "_" + step.CommandName : step.Macro!;
    var ok = RhinoApp.RunScript(macro, false);

    _isRunningStep = false;

    if (!ok)
    {
      AssistantMessageGenerated?.Invoke($"Step {step.Sequence} did not complete. You can retry {step.CommandName} when ready.");
      PublishState(BuildReadyState(detailOverride: $"Step {step.Sequence} was cancelled or failed. You can run it again."));
      return Result.Cancel;
    }

    AssistantMessageGenerated?.Invoke($"Step {step.Sequence} completed: {step.CommandName}.");
    _nextStepIndex++;

    if (_currentPlan is not null && _nextStepIndex >= _currentPlan.Steps.Count)
    {
      AssistantMessageGenerated?.Invoke("Plan complete. Rhino has finished all mocked steps.");
      ClearPlan();
      return Result.Success;
    }

    PublishState(BuildReadyState());
    return Result.Success;
  }

  private void ClearPlan()
  {
    _currentPlan = null;
    _nextStepIndex = 0;
    _isRunningStep = false;
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

  private UI.PlanExecutionState BuildReadyState(string? detailOverride = null)
  {
    var plan = _currentPlan!;
    var nextStep = CurrentStep;
    var completed = _nextStepIndex;
    var detail = detailOverride ?? (nextStep is null
      ? "No steps remain."
      : $"Next up is Step {nextStep.Sequence} of {plan.Steps.Count}: {nextStep.CommandName}.");

    return new UI.PlanExecutionState(
      Phase: UI.PlanExecutionPhase.ReadyToRunStep,
      Title: "Plan Approved",
      Detail: detail,
      CompletedSteps: completed,
      TotalSteps: plan.Steps.Count,
      NextStepLabel: nextStep?.CommandName,
      CanApprove: false,
      CanReject: true,
      CanRunNextStep: nextStep is not null);
  }

  private UI.PlanExecutionState BuildRunningState(ExecutionStepPayload step)
  {
    var plan = _currentPlan!;
    return new UI.PlanExecutionState(
      Phase: UI.PlanExecutionPhase.RunningStep,
      Title: "Running Rhino Step",
      Detail: $"Step {step.Sequence} of {plan.Steps.Count} is active: {step.CommandName}. Follow Rhino's command prompts.",
      CompletedSteps: _nextStepIndex,
      TotalSteps: plan.Steps.Count,
      NextStepLabel: step.CommandName,
      CanApprove: false,
      CanReject: false,
      CanRunNextStep: false);
  }

  private void PublishState(UI.PlanExecutionState state) => StateChanged?.Invoke(state);
}
