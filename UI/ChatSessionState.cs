namespace RhinoCopilotForMakers.UI;

internal sealed record ChatSessionState(bool IsBusy, string StatusText)
{
  public static ChatSessionState Idle { get; } = new(false, "");
}
