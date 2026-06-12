using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RhinoCopilotForMakers.Contracts;
using RhinoCopilotForMakers.Models;

namespace RhinoCopilotForMakers.Services;

internal sealed class CompositeIntentInterpreter : IIntentInterpreter
{
  private readonly IReadOnlyList<IIntentInterpreter> _interpreters;

  public CompositeIntentInterpreter(params IIntentInterpreter[] interpreters)
  {
    _interpreters = interpreters;
  }

  public async Task<IntentInterpretationPayload?> TryInterpretAsync(
    string userText,
    RhinoContextSnapshot context,
    IReadOnlyList<ChatMessage> history,
    CancellationToken cancellationToken)
  {
    foreach (var interpreter in _interpreters)
    {
      var interpretation = await interpreter.TryInterpretAsync(userText, context, history, cancellationToken);
      if (interpretation is not null)
        return interpretation;
    }

    return null;
  }
}
