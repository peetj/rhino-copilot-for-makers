using RhinoCopilotForMakers.Contracts;
using RhinoCopilotForMakers.Models;
using System.Threading;
using System.Threading.Tasks;

namespace RhinoCopilotForMakers.Services;

internal interface IIntentInterpreter
{
  Task<IntentInterpretationPayload?> TryInterpretAsync(
    string userText,
    RhinoContextSnapshot context,
    IReadOnlyList<ChatMessage> history,
    CancellationToken cancellationToken);
}
