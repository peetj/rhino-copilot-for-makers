using RhinoCopilotForMakers.Contracts;
using RhinoCopilotForMakers.Models;

namespace RhinoCopilotForMakers.Services;

internal sealed class FixedIntentInterpreter : IIntentInterpreter
{
  private readonly IntentInterpretationPayload? _interpretation;

  public FixedIntentInterpreter(IntentInterpretationPayload? interpretation)
  {
    _interpretation = interpretation;
  }

  public IntentInterpretationPayload? TryInterpret(string userText, RhinoContextSnapshot context) => _interpretation;
}
