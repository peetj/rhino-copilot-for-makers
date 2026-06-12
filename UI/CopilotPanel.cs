using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Eto.Drawing;
using Eto.Forms;
using RhinoCopilotForMakers.Models;
using RhinoCopilotForMakers.Services;

namespace RhinoCopilotForMakers.UI;

/// <summary>
/// Dockable Eto panel implementing the chat UI and approved plan controls.
/// </summary>
[Guid("2D640F2D-0E52-4DD0-8B9C-6D1C6A4E6B35")]
public sealed class CopilotPanel : Panel
{
  private const string WelcomeMessage =
    "Hi — ask me anything about Rhino 8 product-design workflows. " +
    "I can suggest safe step-by-step actions and copyable Rhino commands.";

  private readonly ChatSessionController _chatSession;
  private readonly MessageRenderer _messageRenderer;
  private readonly StackLayout _messagesStack;
  private readonly Scrollable _scroll;
  private readonly TextArea _input;
  private readonly Button _send;
  private readonly Label _status;
  private readonly Panel _planPanel;
  private readonly Label _planTitle;
  private readonly Label _planDetail;
  private readonly Button _approvePlan;
  private readonly Button _rejectPlan;
  private readonly Button _runNextStep;

  // System prompt required by spec.
  private const string SystemPrompt =
    "You are Rhino Copilot for Makers, a professional assistant embedded inside Rhino 8. " +
    "You help product designers, makers, and 3D printing users with Rhino 8 workflows. " +
    "You understand the current Rhino document context provided by the plugin, including units, selected object types, layers, and bounding box dimensions. " +
    "Give concise, practical guidance. When useful, provide copyable Rhino command sequences. " +
    "When you output Rhino commands, ALWAYS put them in a fenced code block using triple backticks (```), so they are easy to copy. " +
    "Do not claim to edit the model. Do not ask unnecessary clarifying questions; make the most likely Rhino 8 assumption and proceed. " +
    "Warn before destructive operations. Prefer safe, reversible workflows.";

  public CopilotPanel()
  {
    Padding = 10;

    _chatSession = new ChatSessionController(
      new RhinoContextCollector(),
      RhinoCopilotPlugin.Instance!.LlmClient,
      () => RhinoCopilotPlugin.Instance!.CopilotSettings,
      RhinoCopilotPlugin.Instance!.PlanExecutionCoordinator,
      RhinoCopilotPlugin.Instance!.IntentInterpreter,
      MessageFormatter.NormalizeCommandBlocks,
      SystemPrompt);

    _messagesStack = new StackLayout
    {
      Spacing = 12,
      Orientation = Orientation.Vertical,
      HorizontalContentAlignment = HorizontalAlignment.Stretch
    };

    _scroll = new Scrollable
    {
      Content = _messagesStack,
      Border = BorderType.None,
      BackgroundColor = Colors.White,
      ExpandContentWidth = true
    };

    _messageRenderer = new MessageRenderer(_messagesStack, _scroll);

    _scroll.SizeChanged += (_, _) => ClampContentWidth();
    SizeChanged += (_, _) => ClampContentWidth();
    Application.Instance.AsyncInvoke(ClampContentWidth);

    _status = new Label
    {
      Text = "",
      Font = new Font(SystemFont.Default, 9),
      TextColor = Colors.Gray,
      Visible = false
    };

    _planTitle = new Label
    {
      Font = new Font(SystemFont.Bold, 9.5f),
      Wrap = WrapMode.Word
    };

    _planDetail = new Label
    {
      Font = new Font(SystemFont.Default, 9),
      Wrap = WrapMode.Word,
      TextColor = Colors.Gray
    };

    _approvePlan = new Button { Text = "Approve Plan" };
    _approvePlan.Click += (_, _) => _chatSession.ApprovePendingPlan();

    _rejectPlan = new Button { Text = "Reject" };
    _rejectPlan.Click += (_, _) => _chatSession.RejectPendingPlan();

    _runNextStep = new Button { Text = "Run Next Step" };
    _runNextStep.Click += (_, _) => _chatSession.RunNextPlanStep();

    _planPanel = new Panel
    {
      Visible = false,
      Padding = new Padding(10),
      BackgroundColor = Color.FromArgb(247, 247, 247),
      Content = new StackLayout
      {
        Orientation = Orientation.Vertical,
        Spacing = 6,
        Items =
        {
          _planTitle,
          _planDetail,
          new StackLayout
          {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Items =
            {
              _approvePlan,
              _rejectPlan,
              _runNextStep,
              null
            }
          }
        }
      }
    };

    _input = new TextArea
    {
      Text = "",
      Height = 90
    };

    _send = new Button { Text = "➤", ToolTip = "Send (Enter)", Size = new Size(32, 32), Visible = false };
    _send.Click += async (_, _) => await SendCurrentInputAsync();

    _input.KeyDown += async (_, e) =>
    {
      if (e.KeyData == Keys.Enter && !e.Shift)
      {
        e.Handled = true;
        await SendCurrentInputAsync();
      }
    };

    var settingsBtn = new LinkButton { Text = "⚙", ToolTip = "Settings" };
    settingsBtn.Click += (_, _) => ShowSettingsDialog();
    var resetBtn = new LinkButton { Text = "Reset", ToolTip = "Clear conversation" };
    resetBtn.Click += (_, _) => ResetConversation();

    Content = new DynamicLayout
    {
      Spacing = new Size(6, 6)
    };

    var layout = (DynamicLayout)Content;
    layout.AddRow(new StackLayout
    {
      Orientation = Orientation.Horizontal,
      Spacing = 6,
      Items =
      {
        new Label { Text = "Rhino Copilot for Makers", Font = new Font(SystemFont.Bold, 11.5f), Wrap = WrapMode.Word },
        null,
        resetBtn,
        settingsBtn
      }
    });
    layout.Add(_scroll, yscale: true);
    layout.AddRow(_status);
    layout.AddRow(_planPanel);
    layout.AddRow(_input);

    _chatSession.MessageAdded += OnSessionMessageAdded;
    _chatSession.StateChanged += OnSessionStateChanged;
    _chatSession.PlanStateChanged += OnPlanStateChanged;

    _chatSession.AddLocalAssistantMessage(WelcomeMessage);
  }

  private void ClampContentWidth()
  {
    var w = _scroll.VisibleRect.Width > 0 ? _scroll.VisibleRect.Width : _scroll.Size.Width;
    if (w > 0)
      _messageRenderer.UpdateViewportWidth(w);
  }

  private async Task SendCurrentInputAsync()
  {
    var text = (_input.Text ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(text))
      return;

    _input.Text = string.Empty;
    await _chatSession.SendAsync(text);
  }

  private void OnSessionStateChanged(ChatSessionState state)
  {
    _send.Enabled = !state.IsBusy;
    _input.Enabled = !state.IsBusy;
    _status.Text = state.StatusText;
    _status.Visible = state.StatusText.Length > 0;
  }

  private void OnSessionMessageAdded(ChatMessage message)
  {
    var isAssistant = message.Role == ChatRole.Assistant;
    _messageRenderer.AddMessageBubble(message.Content, isAssistant);
  }

  private void OnPlanStateChanged(PlanExecutionState state)
  {
    Application.Instance.AsyncInvoke(() =>
    {
      var isVisible = state.Phase != PlanExecutionPhase.Idle;
      _planPanel.Visible = isVisible;
      if (!isVisible)
      {
        _planTitle.Text = "";
        _planDetail.Text = "";
        return;
      }

      _planTitle.Text = state.Title;
      var progressText = state.TotalSteps > 0
        ? $"Progress: {state.CompletedSteps}/{state.TotalSteps}"
        : "";
      _planDetail.Text = string.IsNullOrWhiteSpace(progressText)
        ? state.Detail
        : $"{state.Detail}\n{progressText}";

      _approvePlan.Visible = state.CanApprove;
      _rejectPlan.Visible = state.CanReject;
      _runNextStep.Visible = state.CanRunNextStep;
      _runNextStep.Text = state.NextStepLabel is null ? "Run Next Step" : $"Run {state.NextStepLabel}";

      _planPanel.Invalidate();
    });
  }

  private void ShowSettingsDialog() =>
    CopilotSettingsDialog.Show(this, RhinoCopilotPlugin.Instance!.CopilotSettings);

  private void ResetConversation()
  {
    _chatSession.ClearConversation();
    _messageRenderer.ClearMessages();
    _chatSession.AddLocalAssistantMessage(WelcomeMessage);
  }
}
