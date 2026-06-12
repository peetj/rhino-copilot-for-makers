using RhinoCopilotForMakers.Contracts;
using RhinoCopilotForMakers.Models;
using System.Threading;
using System.Threading.Tasks;

namespace RhinoCopilotForMakers.Services;

internal sealed class FixedIntentInterpreter : IIntentInterpreter
{
  private readonly IntentInterpretationPayload? _interpretation;

  public FixedIntentInterpreter(IntentInterpretationPayload? interpretation)
  {
    _interpretation = interpretation;
  }

  public Task<IntentInterpretationPayload?> TryInterpretAsync(string userText, RhinoContextSnapshot context, CancellationToken cancellationToken) =>
    Task.FromResult(_interpretation);
}
