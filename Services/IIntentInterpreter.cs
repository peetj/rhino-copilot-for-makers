using RhinoCopilotForMakers.Contracts;
using RhinoCopilotForMakers.Models;

namespace RhinoCopilotForMakers.Services;

internal interface IIntentInterpreter
{
  IntentInterpretationPayload? TryInterpret(string userText, RhinoContextSnapshot context);
}
