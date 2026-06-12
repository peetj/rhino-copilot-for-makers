using System.Collections.Generic;
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

  public IntentInterpretationPayload? TryInterpret(string userText, RhinoContextSnapshot context)
  {
    foreach (var interpreter in _interpreters)
    {
      var interpretation = interpreter.TryInterpret(userText, context);
      if (interpretation is not null)
        return interpretation;
    }

    return null;
  }
}
