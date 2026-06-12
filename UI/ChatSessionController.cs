using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RhinoCopilotForMakers.Contracts;
using RhinoCopilotForMakers.Models;
using RhinoCopilotForMakers.Services;
using RhinoCopilotForMakers.Settings;

namespace RhinoCopilotForMakers.UI;

/// <summary>
/// Owns chat history and request orchestration so the panel can stay focused on UI concerns.
/// </summary>
internal sealed class ChatSessionController : IDisposable
{
  private readonly List<ChatMessage> _history = new();
  private readonly RhinoContextCollector _contextCollector;
  private readonly LlmClient _llmClient;
  private readonly Func<CopilotSettings> _settingsProvider;
  private readonly Func<string, string> _normalizeAssistantContent;
  private readonly PlanExecutionCoordinator _planExecutionCoordinator;
  private readonly IIntentInterpreter _intentInterpreter;
  private readonly string _systemPrompt;

  private CancellationTokenSource? _cts;
  private ChatSessionState _state = ChatSessionState.Idle;

  public ChatSessionController(
    RhinoContextCollector contextCollector,
    LlmClient llmClient,
    Func<CopilotSettings> settingsProvider,
    PlanExecutionCoordinator planExecutionCoordinator,
    IIntentInterpreter intentInterpreter,
    Func<string, string> normalizeAssistantContent,
    string systemPrompt)
  {
    _contextCollector = contextCollector;
    _llmClient = llmClient;
    _settingsProvider = settingsProvider;
    _planExecutionCoordinator = planExecutionCoordinator;
    _intentInterpreter = intentInterpreter;
    _normalizeAssistantContent = normalizeAssistantContent;
    _systemPrompt = systemPrompt;

    _planExecutionCoordinator.StateChanged += state => PlanStateChanged?.Invoke(state);
    _planExecutionCoordinator.AssistantMessageGenerated += AddLocalAssistantMessage;
  }

  public event Action<ChatMessage>? MessageAdded;
  public event Action<ChatSessionState>? StateChanged;
  public event Action<PlanExecutionState>? PlanStateChanged;

  public IReadOnlyList<ChatMessage> History => _history;
  public ChatSessionState State => _state;

  public void AddLocalAssistantMessage(string content) =>
    AddMessage(ChatRole.Assistant, content);

  public void ApprovePendingPlan() => _planExecutionCoordinator.ApprovePlan();

  public void RejectPendingPlan() => _planExecutionCoordinator.RejectPlan();

  public void RunNextPlanStep() => _planExecutionCoordinator.RequestRunNextStep();

  public void ClearConversation()
  {
    _cts?.Cancel();
    _cts?.Dispose();
    _cts = null;
    _history.Clear();
    _planExecutionCoordinator.Reset();
    UpdateState(isBusy: false, statusText: "");
  }

  public async Task SendAsync(string text)
  {
    if (string.IsNullOrWhiteSpace(text))
      return;

    AddMessage(ChatRole.User, text);

    var context = _contextCollector.Collect();
    _cts?.Cancel();
    _cts?.Dispose();
    _cts = new CancellationTokenSource();
    UpdateState(isBusy: true, statusText: "Interpreting...");

    try
    {
      var interpretation = await _intentInterpreter.TryInterpretAsync(text, context, _cts.Token);
      var mockResponse = MockPlanFactory.TryCreate(interpretation, context);
      if (mockResponse is not null)
      {
        if (mockResponse.Message is not null)
          AddMessage(ChatRole.Assistant, mockResponse.Message.Text);

        if (mockResponse.Plan is not null)
          _planExecutionCoordinator.LoadPlan(mockResponse);

        return;
      }

      var settings = _settingsProvider();
      if (!settings.HasApiKey)
      {
        AddMessage(ChatRole.Assistant, "Set your API key first: click Settings -> paste key -> Save. (It is stored locally in Rhino plugin settings.)");
        return;
      }

      UpdateState(isBusy: true, statusText: "Thinking...");

      var historyForApi = _history
        .Where(m => m.Role is ChatRole.User or ChatRole.Assistant)
        .ToList();

      var sb = new StringBuilder();

      await foreach (var chunk in _llmClient.StreamChatCompletionAsync(
        endpoint: settings.Endpoint,
        apiKey: settings.ApiKey,
        model: settings.Model,
        systemPrompt: _systemPrompt,
        context: context,
        history: historyForApi,
        cancellationToken: _cts.Token))
      {
        sb.Append(chunk);
      }

      AddMessage(ChatRole.Assistant, sb.ToString().Trim());
    }
    catch (OperationCanceledException)
    {
      AddMessage(ChatRole.Assistant, "Cancelled.");
    }
    catch (Exception ex)
    {
      AddMessage(ChatRole.Assistant, "Network/API error: " + ex.Message);
    }
    finally
    {
      UpdateState(isBusy: false, statusText: "");
    }
  }

  public void Cancel() => _cts?.Cancel();

  public void Dispose()
  {
    _cts?.Cancel();
    _cts?.Dispose();
  }

  private void AddMessage(ChatRole role, string content)
  {
    if (role == ChatRole.Assistant)
      content = _normalizeAssistantContent(content);

    var message = new ChatMessage(role, content);
    _history.Add(message);
    MessageAdded?.Invoke(message);
  }

  private void UpdateState(bool isBusy, string statusText)
  {
    var next = new ChatSessionState(isBusy, statusText?.Trim() ?? "");
    if (next == _state)
      return;

    _state = next;
    StateChanged?.Invoke(_state);
  }
}
