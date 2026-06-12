using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using RhinoCopilotForMakers.Contracts;
using RhinoCopilotForMakers.Models;
using RhinoCopilotForMakers.Services;

namespace RhinoCopilotForMakers.UI;

/// <summary>
/// Owns chat history and request orchestration so the panel can stay focused on UI concerns.
/// </summary>
internal sealed class ChatSessionController : IDisposable
{
  private readonly List<ChatMessage> _history = new();
  private readonly RhinoContextCollector _contextCollector;
  private readonly CopilotCloudClient _cloudClient;
  private readonly Func<string, string> _normalizeAssistantContent;
  private readonly PlanExecutionCoordinator _planExecutionCoordinator;
  private readonly IIntentInterpreter _intentInterpreter;

  private CancellationTokenSource? _cts;
  private ChatSessionState _state = ChatSessionState.Idle;
  private string? _pendingClarificationPrompt;

  public ChatSessionController(
    RhinoContextCollector contextCollector,
    CopilotCloudClient cloudClient,
    PlanExecutionCoordinator planExecutionCoordinator,
    IIntentInterpreter intentInterpreter,
    Func<string, string> normalizeAssistantContent)
  {
    _contextCollector = contextCollector;
    _cloudClient = cloudClient;
    _planExecutionCoordinator = planExecutionCoordinator;
    _intentInterpreter = intentInterpreter;
    _normalizeAssistantContent = normalizeAssistantContent;

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
    _pendingClarificationPrompt = null;
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
      if (_cloudClient.IsConfigured)
      {
        var cloudResponse = await _cloudClient.TrySendTurnAsync(text, context, _history, _cts.Token);
        if (cloudResponse is not null)
        {
          HandleCloudTurnResponse(cloudResponse);
          return;
        }
      }

      var isClarificationReply = _pendingClarificationPrompt is not null && LooksLikeClarificationReply(text);
      var interpretationText = isClarificationReply
        ? $"{_pendingClarificationPrompt}\nClarification: {text}"
        : text;
      var interpretationHistory = _history
        .Where(m => m.Role is ChatRole.User or ChatRole.Assistant)
        .ToList();
      var interpretation = await _intentInterpreter.TryInterpretAsync(interpretationText, context, interpretationHistory, _cts.Token);
      var mockResponse = MockPlanFactory.TryCreate(interpretation, context);
      if (mockResponse is not null)
      {
        _pendingClarificationPrompt = mockResponse.ResponseType == ResponseType.ClarificationRequest
          ? interpretationText
          : null;

        if (mockResponse.Message is not null)
          AddMessage(ChatRole.Assistant, mockResponse.Message.Text);

        if (mockResponse.Plan is not null)
          _planExecutionCoordinator.LoadPlan(mockResponse);

        return;
      }

      if (isClarificationReply)
      {
        AddMessage(ChatRole.Assistant, "I couldn't apply that clarification to the pending Rhino action. Try a specific value like `10mm` or restate the full request.");
        return;
      }

      _pendingClarificationPrompt = null;
      AddMessage(
        ChatRole.Assistant,
        LooksLikeRhinoExecutionRequest(text)
          ? "The cloud worker is not configured, so I can't interpret that broadly yet. Set the Worker URL in Settings to use the cloud planner/orchestrator path."
          : "The cloud worker is not configured. Set the Worker URL in Settings so this panel can route normal Rhino questions and execution planning through the cloud agents.");
    }
    catch (OperationCanceledException)
    {
      AddMessage(ChatRole.Assistant, "Cancelled.");
    }
    catch (Exception ex)
    {
      AddMessage(ChatRole.Assistant, "Cloud worker error: " + ex.Message);
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

  private void HandleCloudTurnResponse(TurnResponse response)
  {
    _pendingClarificationPrompt = response.ResponseType == ResponseType.ClarificationRequest
      ? _history.LastOrDefault(m => m.Role == ChatRole.User)?.Content
      : null;

    if (response.Message is not null && !string.IsNullOrWhiteSpace(response.Message.Text))
      AddMessage(ChatRole.Assistant, response.Message.Text);

    switch (response.ResponseType)
    {
      case ResponseType.PlanResponse:
        if (response.Plan is not null)
          _planExecutionCoordinator.LoadPlan(response);
        break;

      case ResponseType.ErrorResponse:
        if (response.Error is not null)
          AddMessage(ChatRole.Assistant, $"{response.Error.Code}: {response.Error.Message}");
        break;

      case ResponseType.ChatResponse:
      case ResponseType.ClarificationRequest:
        break;

      default:
        AddMessage(ChatRole.Assistant, $"Received unsupported cloud response type: {response.ResponseType}.");
        break;
    }
  }

  private static bool LooksLikeClarificationReply(string text)
  {
    var trimmed = (text ?? string.Empty).Trim();
    if (trimmed.Length == 0 || trimmed.Length > 80)
      return false;

    if (Regex.IsMatch(trimmed, @"^\d+(?:\.\d+)?\s*(?:mm|cm|m|in|inch|inches)?$", RegexOptions.IgnoreCase))
      return true;

    if (Regex.IsMatch(trimmed, @"^(?:yes|no|centered|centred|solid|open|cap|uncapped)$", RegexOptions.IgnoreCase))
      return true;

    return !Regex.IsMatch(trimmed, @"\b(rect(?:angle)?|extrud\w*|fillet|circle|cylinder|create|make|draw|what|how|why)\b", RegexOptions.IgnoreCase);
  }

  private static bool LooksLikeRhinoExecutionRequest(string text)
  {
    var trimmed = (text ?? string.Empty).Trim();
    if (trimmed.Length == 0)
      return false;

    return Regex.IsMatch(
      trimmed,
      @"\b(create|make|draw|put|place|add|move|rotate|scale|extrud\w*|fillet|boolean|offset|loft|sweep|patch|sphere|box|cylinder|rectangle|circle|line|surface|solid|hole|mount|clip)\b",
      RegexOptions.IgnoreCase);
  }
}
