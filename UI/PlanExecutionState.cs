namespace RhinoCopilotForMakers.UI;

internal enum PlanExecutionPhase
{
  Idle,
  AwaitingApproval,
  ReadyToRunStep,
  RunningStep
}

internal sealed record PlanExecutionState(
  PlanExecutionPhase Phase,
  string Title,
  string Detail,
  int CompletedSteps,
  int TotalSteps,
  string? NextStepLabel,
  bool CanApprove,
  bool CanReject,
  bool CanRunNextStep)
{
  public static PlanExecutionState Idle { get; } = new(
    Phase: PlanExecutionPhase.Idle,
    Title: "",
    Detail: "",
    CompletedSteps: 0,
    TotalSteps: 0,
    NextStepLabel: null,
    CanApprove: false,
    CanReject: false,
    CanRunNextStep: false);
}
