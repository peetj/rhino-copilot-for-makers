using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Eto.Forms;
using Eto.Drawing;
using Rhino;
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
  private readonly List<ChatMessage> _history = new();
  private readonly RhinoContextCollector _contextCollector = new();
  private readonly LlmClient _llmClient = new(new HttpClient());

  private readonly StackLayout _messagesStack;
  private readonly Scrollable _scroll;
  private readonly TextArea _input;
  private readonly Button _send;
  private readonly Label _status;

  private CancellationTokenSource? _cts;

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

    // Prevent horizontal scrolling: keep the inner stack exactly as wide as the viewport.
    _scroll.SizeChanged += (_, _) =>
    {
      // Width is enough; height is driven by content.
      _messagesStack.Width = Math.Max(0, _scroll.Size.Width - (Padding.Left + Padding.Right));
    };

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

    // ChatGPT-style: Enter sends, Shift+Enter inserts newline.
    // Keep a small send button as a fallback (e.g. mouse-only users).
    _send = new Button { Text = "➤", ToolTip = "Send (Enter)" , Size = new Size(32, 32) };
    _send.Click += async (_, _) => await SendAsync();

    _input.KeyDown += async (_, e) =>
    {
      if (e.KeyData == Keys.Enter && !e.Shift)
      {
        e.Handled = true;
        await SendAsync();
      }
    };

    // No explicit Cancel button in the ChatGPT-style composer.
    // If we need cancel later, we can add it as a small inline icon.

    // ChatGPT-ish: settings as an unobtrusive cog icon.
    var settingsBtn = new LinkButton { Text = "⚙", ToolTip = "Settings" };
    settingsBtn.Click += (_, _) => ShowSettingsDialog();

    Content = new DynamicLayout
    {
      Spacing = new Size(6, 6)
    };

    var layout = (DynamicLayout)Content;

    // Header row (title + right-aligned cog)
    var header = new StackLayout
    {
      Orientation = Orientation.Horizontal,
      Spacing = 6,
      Items =
      {
        new Label { Text = "Rhino Copilot for Makers", Font = new Font(SystemFont.Bold, 12) },
        null,
        settingsBtn
      }
    };
    layout.AddRow(header);

    // IMPORTANT: the chat area must take the remaining space; otherwise message controls can
    // expand vertically and push the composer off-screen.
    layout.Add(_scroll, yscale: true);

    // Status + composer
    layout.AddRow(_status);
    layout.AddRow(_input);

    // ChatGPT-style: no visible Send/Cancel buttons.
    // Enter sends, Shift+Enter inserts a newline.
    _send.Visible = false;

    // Starter greeting.
    AddAssistantMessage(
      "Hi — ask me anything about Rhino 8 product-design workflows. " +
      "I can suggest safe step-by-step actions and copyable Rhino commands."
    );
  }

  private async Task SendAsync()
  {
    var text = (_input.Text ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(text))
      return;

    _input.Text = string.Empty;
    AddUserMessage(text);

    var settings = RhinoCopilotPlugin.Instance!.CopilotSettings;
    if (!settings.HasApiKey)
    {
      AddAssistantMessage("Set your API key first: click Settings → paste key → Save. (It is stored locally in Rhino plugin settings.)");
      return;
    }

    _cts?.Cancel();
    _cts = new CancellationTokenSource();

    SetBusy(true, "Thinking…");

    try
    {
      var context = _contextCollector.Collect();

      // Send only user/assistant turns; system prompt is injected in client.
      var historyForApi = _history
        .Where(m => m.Role is ChatRole.User or ChatRole.Assistant)
        .ToList();

        // Streaming response: update status only (no placeholder box).
      var sb = new System.Text.StringBuilder();

      await foreach (var chunk in _llmClient.StreamChatCompletionAsync(
        endpoint: settings.Endpoint,
        apiKey: settings.ApiKey,
        model: settings.Model,
        systemPrompt: SystemPrompt,
        context: context,
        history: historyForApi,
        cancellationToken: _cts.Token))
      {
        sb.Append(chunk);
      }

      AddAssistantMessage(sb.ToString().Trim());
    }
    catch (OperationCanceledException)
    {
      AddAssistantMessage("Cancelled.");
    }
    catch (Exception ex)
    {
      AddAssistantMessage("Network/API error: " + ex.Message);
    }
    finally
    {
      SetBusy(false, "");
    }
  }

  private void SetBusy(bool busy, string status)
  {
    _send.Enabled = !busy;
    _input.Enabled = !busy;

    var s = status?.Trim() ?? string.Empty;
    _status.Text = s;
    _status.Visible = s.Length > 0;
  }

  private void AddUserMessage(string content)
  {
    _history.Add(new ChatMessage(ChatRole.User, content));
    AddMessageBubble("You", content, isAssistant: false);
  }

  private void AddAssistantMessage(string content)
  {
    // Normalize common “command block” patterns into fenced blocks for nicer UI rendering.
    content = NormalizeCommandBlocks(content);

    _history.Add(new ChatMessage(ChatRole.Assistant, content));
    AddMessageBubble("Copilot", content, isAssistant: true);
  }

  // NOTE: We intentionally do not render a "placeholder" assistant bubble while waiting.
  // ChatGPT doesn't insert an empty box; it just shows the assistant response when it arrives.

  private static string NormalizeCommandBlocks(string content)
  {
    // If the model returns something like:
    // Commands:
    // _Select _Enter
    // we prefer fenced blocks so they become copyable.
    // This is deliberately conservative: if fenced blocks already exist, do nothing.
    if (content.Contains("```"))
      return content;

    var lines = content.Replace("\r\n", "\n").Split('\n');
    var idx = Array.FindIndex(lines, l => l.Trim().Equals("commands:", StringComparison.OrdinalIgnoreCase)
                                      || l.Trim().Equals("rhino commands:", StringComparison.OrdinalIgnoreCase));
    if (idx < 0)
      return content;

    // Take subsequent non-empty lines as a block until a blank line.
    var block = new List<string>();
    for (var i = idx + 1; i < lines.Length; i++)
    {
      var t = lines[i];
      if (string.IsNullOrWhiteSpace(t)) break;
      block.Add(t);
    }

    if (block.Count == 0)
      return content;

    var before = string.Join("\n", lines.Take(idx)).TrimEnd();
    var afterStart = idx + 1 + block.Count;
    var after = string.Join("\n", lines.Skip(afterStart)).TrimStart();

    var fenced = "```\n" + string.Join("\n", block).Trim() + "\n```";

    if (string.IsNullOrWhiteSpace(before))
      return fenced + (string.IsNullOrWhiteSpace(after) ? "" : "\n\n" + after);

    return before + "\n\n" + fenced + (string.IsNullOrWhiteSpace(after) ? "" : "\n\n" + after);
  }

  private void AddMessageBubble(string header, string content, bool isAssistant)
  {
    AddMessageBubbleCustom(header, () => content, BuildMessageBody(content), isAssistant);
  }

  private Control AddMessageBubbleCustom(string header, Func<string> getCopyText, Control body, bool isAssistant)
  {
    // ChatGPT-ish light UI:
    // - assistant messages are left-aligned with minimal chrome
    // - user messages are right-aligned with a subtle shaded bubble

    // Header actions (ChatGPT-ish): no repeated "Copilot" label per message.
    // Keep only an unobtrusive copy icon on the right for assistant messages.
    var copyIcon = new Label
    {
      Text = "⧉",
      ToolTip = "Copy",
      TextColor = Color.FromArgb(180, 180, 180)
    };
    copyIcon.MouseDown += (_, _) => Clipboard.Instance.Text = getCopyText();

    var headerRow = new StackLayout
    {
      Orientation = Orientation.Horizontal,
      Spacing = 6,
      Items = { null, copyIcon }
    };

    // Bubble look
    var bubbleBg = Colors.White;

    var bubble = new Panel
    {
      BackgroundColor = bubbleBg,
      Padding = isAssistant ? new Padding(12, 10) : new Padding(10, 6),
      Content = isAssistant
        ? new StackLayout { Spacing = 6, Items = { headerRow, body } }
        : body
    };

    // User bubble border: Nexgen orange (FF5E19) with transparency.
    // Eto Panels don't support border color directly, so we fake it with a 1px wrapper panel.
    var nexgenOrangeBorder = Color.FromArgb(64, 0xFF, 0x5E, 0x19);
    Control bubbleControl = bubble;
    if (!isAssistant)
    {
      bubbleControl = new Panel
      {
        BackgroundColor = nexgenOrangeBorder,
        Padding = 1,
        Content = bubble
      };
    }

    // IMPORTANT: the container MUST expand full width, otherwise "right aligned" rows
    // will still appear left-aligned because the row shrinks to content width.
    // Using a horizontal StackLayout with an expanding spacer reliably anchors bubbles.
    var row = new StackLayout
    {
      Orientation = Orientation.Horizontal,
      Spacing = 0,
      Padding = new Padding(6, 0, 6, 0)
    };

    if (isAssistant)
    {
      // Left aligned: bubble then expanding spacer
      row.Items.Add(bubbleControl);
      row.Items.Add(new StackLayoutItem(null, expand: true));
    }
    else
    {
      // Right aligned: expanding spacer then bubble
      row.Items.Add(new StackLayoutItem(null, expand: true));
      row.Items.Add(bubbleControl);
    }

    _messagesStack.Items.Add(new StackLayoutItem(row) { HorizontalAlignment = HorizontalAlignment.Stretch });

    // Scroll to bottom.
    Application.Instance.AsyncInvoke(() => _scroll.ScrollPosition = new Point(0, int.MaxValue));

    return row;
  }

  /// <summary>
  /// Formats assistant output supporting:
  /// - plain paragraphs
  /// - fenced code blocks (``` ... ```), treated as copyable "command blocks" in the UI
  ///
  /// Tip: Encourage the model to use fenced blocks for Rhino commands.
  /// </summary>
  private Control BuildMessageBody(string content)
  {
    var parts = SplitFencedBlocks(content);

    var stack = new StackLayout
    {
      Spacing = 6,
      Orientation = Orientation.Vertical
    };

    foreach (var p in parts)
    {
      if (p.IsCode)
      {
        var codeArea = new TextArea
        {
          Text = p.Text,
          ReadOnly = true,
          Font = new Font(FontFamilies.Monospace, 10),
          BackgroundColor = Colors.White,
          Border = BorderType.None,
          Wrap = true,
          // Fit full content height so the main chat scroll handles scrolling (no inner scrollbars).
          Height = 24 + (p.Text.Count(c => c == '\n') * 16)
        };

        // ChatGPT-like: no "Copy block" buttons everywhere.
        // We'll rely on the per-message copy icon for now.
        stack.Items.Add(new StackLayout
        {
          Orientation = Orientation.Vertical,
          Spacing = 4,
          Items =
          {
            new Panel { Padding = 8, BackgroundColor = Colors.White, Content = codeArea }
          }
        });
      }
      else
      {
        // Markdown-lite renderer for better readability (headings, bullets, numbered steps).
        stack.Items.Add(RenderMarkdownLite(p.Text));
      }
    }

    return stack;
  }

  private Control RenderMarkdownLite(string text)
  {
    var s = (text ?? string.Empty).Replace("\r\n", "\n").Trim();

    // If it's a single line, still wrap — Rhino panels can be narrow.
    if (!s.Contains('\n'))
    {
      return new Label { Text = s, Wrap = WrapMode.Word };
    }

    var stack = new StackLayout
    {
      Orientation = Orientation.Vertical,
      Spacing = 4
    };

    var lines = s.Split('\n');
    var para = new List<string>();

    void FlushParagraph()
    {
      if (para.Count == 0) return;
      var t = string.Join(" ", para.Select(l => l.Trim())).Trim();
      para.Clear();
      if (t.Length == 0) return;
      stack.Items.Add(new Label { Text = t, Wrap = WrapMode.Word });
    }

    foreach (var raw in lines)
    {
      var line = raw.TrimEnd();

      if (string.IsNullOrWhiteSpace(line))
      {
        FlushParagraph();
        // Visual gap between blocks
        stack.Items.Add(new Panel { Height = 6 });
        continue;
      }

      // Headings: # / ## / ###
      if (line.StartsWith("#"))
      {
        var hashes = line.TakeWhile(c => c == '#').Count();
        if (hashes is >= 1 and <= 4 && line.Length > hashes && line[hashes] == ' ')
        {
          FlushParagraph();
          var title = line.Substring(hashes + 1).Trim();
          var size = hashes switch { 1 => 14f, 2 => 13f, _ => 12f };
          stack.Items.Add(new Label { Text = title, Font = new Font(SystemFont.Bold, size) });
          continue;
        }
      }

      // Bullets: -, *, •
      var trimmed = line.TrimStart();
      if (trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || trimmed.StartsWith("• "))
      {
        FlushParagraph();
        var item = trimmed.Substring(2).Trim();
        stack.Items.Add(new Label { Text = "• " + item, Wrap = WrapMode.Word });
        continue;
      }

      // Numbered steps: 1. 2. ...
      var dot = trimmed.IndexOf('.', 0);
      if (dot > 0 && dot < 4 && int.TryParse(trimmed.Substring(0, dot), out var n) && trimmed.Length > dot + 1 && trimmed[dot + 1] == ' ')
      {
        FlushParagraph();
        var item = trimmed.Substring(dot + 2).Trim();
        stack.Items.Add(new Label { Text = $"{n}. {item}", Wrap = WrapMode.Word });
        continue;
      }

      // Inline code: we can't do rich styling easily with plain Labels;
      // render any line containing backticks as monospaced read-only TextArea.
      if (trimmed.Contains('`'))
      {
        FlushParagraph();
        stack.Items.Add(new TextArea
        {
          Text = trimmed.Replace('`', ' '),
          ReadOnly = true,
          Font = new Font(FontFamilies.Monospace, 10),
          BackgroundColor = Colors.White,
          Border = BorderType.None,
          Wrap = false,
          Height = 28
        });
        continue;
      }

      para.Add(trimmed);
    }

    FlushParagraph();
    return stack;
  }

  private static List<(bool IsCode, string Text)> SplitFencedBlocks(string content)
  {
    // Small parser for triple-backtick fences.
    // Supports optional language after opening fence: ```rhino
    // We ignore the language for now, but the fence makes blocks visually distinct.
    var result = new List<(bool, string)>();
    var s = content.Replace("\r\n", "\n");

    var i = 0;
    while (i < s.Length)
    {
      var open = s.IndexOf("```", i, StringComparison.Ordinal);
      if (open < 0)
      {
        var tail = s.Substring(i).Trim();
        if (tail.Length > 0) result.Add((false, tail));
        break;
      }

      var before = s.Substring(i, open - i).Trim();
      if (before.Length > 0) result.Add((false, before));

      // Skip optional language tag on the opening fence line.
      var langEnd = s.IndexOf('\n', open + 3);
      if (langEnd < 0) langEnd = open + 3;

      var close = s.IndexOf("```", langEnd, StringComparison.Ordinal);
      if (close < 0)
      {
        // No closing fence; treat rest as text.
        var rest = s.Substring(open).Trim();
        if (rest.Length > 0) result.Add((false, rest));
        break;
      }

      var code = s.Substring(langEnd + 1, close - (langEnd + 1)).Trim();
      if (code.Length > 0) result.Add((true, code));

      i = close + 3;
    }

    return result;
  }

  private void ShowSettingsDialog()
  {
    var settings = RhinoCopilotPlugin.Instance!.CopilotSettings;

    var endpoint = new TextBox { Text = settings.Endpoint };
    var model = new TextBox { Text = settings.Model };
    var apiKey = new PasswordBox { Text = settings.ApiKey };

    var save = new Button { Text = "Save" };
    var cancel = new Button { Text = "Close" };

    var dlg = new Dialog<bool>
    {
      Title = "Rhino Copilot Settings",
      Resizable = true,
      Padding = 10,
      MinimumSize = new Size(520, 220)
    };

    save.Click += (_, _) =>
    {
      settings.Endpoint = endpoint.Text ?? settings.Endpoint;
      settings.Model = model.Text ?? settings.Model;
      settings.ApiKey = apiKey.Text ?? "";
      dlg.Close(true);
    };

    cancel.Click += (_, _) => dlg.Close(false);

    dlg.Content = new DynamicLayout
    {
      Spacing = new Size(6, 6)
    };

    var l = (DynamicLayout)dlg.Content;
    l.AddRow(new Label { Text = "Endpoint (OpenAI-compatible):" });
    l.AddRow(endpoint);
    l.AddRow(new Label { Text = "Model:" });
    l.AddRow(model);
    l.AddRow(new Label { Text = "API Key (stored locally):" });
    l.AddRow(apiKey);

    dlg.DefaultButton = save;
    dlg.AbortButton = cancel;

    dlg.PositiveButtons.Add(save);
    dlg.NegativeButtons.Add(cancel);

    dlg.ShowModal(this);
  }
}
