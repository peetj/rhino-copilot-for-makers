using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Rhino;
using Rhino.Commands;
using RhinoCopilotForMakers.Contracts;
using RhinoCopilotForMakers.Models;
using RhinoCopilotForMakers.Settings;

namespace RhinoCopilotForMakers.Services;

internal sealed class CopilotCloudClient
{
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNameCaseInsensitive = true,
    WriteIndented = false
  };

  private readonly HttpClient _httpClient;
  private readonly Func<CopilotSettings> _settingsProvider;
  private readonly string _sessionId = "session_" + Guid.NewGuid().ToString("N");

  public CopilotCloudClient(HttpClient httpClient, Func<CopilotSettings> settingsProvider)
  {
    _httpClient = httpClient;
    _settingsProvider = settingsProvider;
  }

  public bool IsConfigured => _settingsProvider().HasWorkerUrl;

  public async Task<TurnResponse?> TrySendTurnAsync(
    string userText,
    RhinoContextSnapshot context,
    IReadOnlyList<ChatMessage> history,
    CancellationToken cancellationToken)
  {
    var settings = _settingsProvider();
    if (!settings.HasWorkerUrl)
      return null;

    var requestPayload = BuildTurnRequest(userText, context, history);
    var requestUri = BuildTurnUri(settings.WorkerUrl);

    using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
    request.Content = new StringContent(JsonSerializer.Serialize(requestPayload, JsonOptions), Encoding.UTF8, "application/json");

    if (settings.HasPluginSharedSecret)
      request.Headers.Add("x-plugin-secret", settings.PluginSharedSecret);

    using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
    if (!response.IsSuccessStatusCode)
      throw new InvalidOperationException($"Cloud worker request failed ({(int)response.StatusCode}): {responseText}");

    var turnResponse = JsonSerializer.Deserialize<TurnResponse>(responseText, JsonOptions);
    if (turnResponse is null)
      throw new InvalidOperationException("Cloud worker returned an empty response.");

    return turnResponse;
  }

  private TurnRequest BuildTurnRequest(
    string userText,
    RhinoContextSnapshot context,
    IReadOnlyList<ChatMessage> history)
  {
    var doc = RhinoDoc.ActiveDoc;
    var docPath = doc?.Path ?? string.Empty;
    var docName = !string.IsNullOrWhiteSpace(docPath)
      ? Path.GetFileName(docPath)
      : doc?.Name;
    var docId = doc is null
      ? "no_document"
      : $"rhino_doc_{doc.RuntimeSerialNumber}";

    return new TurnRequest(
      SchemaVersion: CopilotSchema.Version,
      RequestId: "req_" + Guid.NewGuid().ToString("N"),
      SentAt: DateTimeOffset.UtcNow.ToString("O"),
      Tenant: new TenantRef("nexgen-local"),
      User: new UserRef(Environment.UserName, Environment.UserName),
      Session: new SessionRef(_sessionId, "copilot-panel"),
      Document: new DocumentRef(
        DocumentId: docId,
        DocumentName: string.IsNullOrWhiteSpace(docName) ? null : docName,
        DocumentFingerprint: string.IsNullOrWhiteSpace(docPath) ? docId : docPath),
      Turn: new TurnPayload(
        TurnId: "turn_" + Guid.NewGuid().ToString("N"),
        ParentTurnId: null,
        MessageText: userText,
        ClientCapabilities: new ClientCapabilities(
          SupportsStreaming: false,
          SupportsApprovals: true,
          SupportsLocalExecution: true,
          SupportsStepwiseExecution: true,
          SupportsExecutionEvents: false)),
      Conversation: history
        .Where(message => message.Role is ChatRole.User or ChatRole.Assistant)
        .TakeLast(12)
        .Select(message => new ConversationMessagePayload(
          TurnId: null,
          Role: message.Role switch
          {
            ChatRole.System => "system",
            ChatRole.User => "user",
            _ => "assistant"
          },
          Text: message.Content,
          CreatedAt: null))
        .ToList(),
      RhinoContext: new RhinoContextPayload(
        RhinoVersion: context.RhinoVersion,
        DocumentUnits: context.DocumentUnits,
        ActiveViewport: context.ActiveViewport,
        AbsoluteTolerance: context.AbsoluteTolerance,
        AngleToleranceDegrees: context.AngleToleranceDegrees,
        SelectedObjectCount: context.SelectedObjectCount,
        SelectedObjectTypes: context.SelectedObjectTypes,
        SelectedBoundingBox: context.SelectedBoundingBox,
        SelectedLayerNames: context.SelectedLayerNames,
        DocumentLayerNames: context.DocumentLayerNames,
        CommandState: new CommandStatePayload(
          IsCommandRunning: Command.InCommand(),
          ActiveCommandName: null)));
  }

  private static Uri BuildTurnUri(string workerUrl)
  {
    if (!Uri.TryCreate(workerUrl.Trim(), UriKind.Absolute, out var parsed))
      throw new InvalidOperationException("Worker URL is not a valid absolute URL.");

    if (parsed.AbsolutePath.EndsWith("/turn", StringComparison.OrdinalIgnoreCase))
      return parsed;

    var builder = new UriBuilder(parsed)
    {
      Path = "/turn"
    };

    return builder.Uri;
  }
}
