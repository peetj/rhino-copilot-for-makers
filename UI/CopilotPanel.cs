using System;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Eto.Drawing;
using Eto.Forms;
using RhinoCopilotForMakers.Models;
using RhinoCopilotForMakers.Services;

namespace RhinoCopilotForMakers.UI;

/// <summary>
/// Dockable Eto panel implementing the chat UI.
/// Guidance-only: does not execute commands or modify geometry.
/// </summary>
[Guid("2D640F2D-0E52-4DD0-8B9C-6D1C6A4E6B35")]
public sealed class CopilotPanel : Panel
{
  private readonly ChatSessionController _chatSession;
  private readonly MessageRenderer _messageRenderer;
  private readonly StackLayout _messagesStack;
  private readonly Scrollable _scroll;
  private readonly TextArea _input;
  private readonly Button _send;
  private readonly Label _status;

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
      new LlmClient(new HttpClient()),
      () => RhinoCopilotPlugin.Instance!.CopilotSettings,
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
      BackgroundColor = Colors.White
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
        settingsBtn
      }
    });
    layout.Add(_scroll, yscale: true);
    layout.AddRow(_status);
    layout.AddRow(_input);

    _chatSession.MessageAdded += OnSessionMessageAdded;
    _chatSession.StateChanged += OnSessionStateChanged;

    _chatSession.AddLocalAssistantMessage(
      "Hi — ask me anything about Rhino 8 product-design workflows. " +
      "I can suggest safe step-by-step actions and copyable Rhino commands."
    );
  }

  private void ClampContentWidth()
  {
    var w = _scroll.Size.Width;
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

  private void ShowSettingsDialog() =>
    CopilotSettingsDialog.Show(this, RhinoCopilotPlugin.Instance!.CopilotSettings);
}
