using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;

namespace RhinoCopilotForMakers.UI;

internal sealed class MessageRenderer
{
  private const int ContentViewportInset = 14;
  private const int RowHorizontalPadding = 12;
  private const int BubbleSideGutter = 18;
  private const int MinimumBubbleWidth = 120;

  private readonly StackLayout _messagesStack;
  private readonly Scrollable _scroll;
  private readonly List<Control> _resizableBubbles = new();

  private int _currentContentWidth;

  public MessageRenderer(StackLayout messagesStack, Scrollable scroll)
  {
    _messagesStack = messagesStack;
    _scroll = scroll;
  }

  public void UpdateViewportWidth(int viewportWidth)
  {
    var contentWidth = Math.Max(0, viewportWidth - ContentViewportInset);
    if (contentWidth <= 0)
      return;

    _currentContentWidth = contentWidth;
    _messagesStack.Width = contentWidth;

    var bubbleWidth = Math.Max(
      MinimumBubbleWidth,
      contentWidth - RowHorizontalPadding - BubbleSideGutter);

    foreach (var bubble in _resizableBubbles)
    {
      bubble.Width = bubbleWidth;
    }

    _messagesStack.Invalidate();
    _scroll.Invalidate();
  }

  public void AddMessageBubble(string content, bool isAssistant)
  {
    AddMessageBubbleCustom(() => content, BuildMessageBody(content), isAssistant);
  }

  private Control AddMessageBubbleCustom(Func<string> getCopyText, Control body, bool isAssistant)
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
      HorizontalContentAlignment = HorizontalAlignment.Stretch,
      Items = { new StackLayoutItem(null, expand: true), copyIcon }
    };

    var bubble = new Panel
    {
      BackgroundColor = Colors.White,
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
      row.Items.Add(bubbleControl);
      row.Items.Add(new StackLayoutItem(null, expand: true));
    }
    else
    {
      row.Items.Add(new StackLayoutItem(null, expand: true));
      row.Items.Add(bubbleControl);
    }

    _resizableBubbles.Add(bubbleControl);
    _messagesStack.Items.Add(new StackLayoutItem(row) { HorizontalAlignment = HorizontalAlignment.Stretch });

    if (_currentContentWidth > 0)
      UpdateViewportWidth(_currentContentWidth + ContentViewportInset);

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
  private Control BuildExpandableCodeBlock(string code)
  {
    var trimmed = (code ?? string.Empty).TrimEnd();
    var firstLine = trimmed.Replace("\r\n", "\n").Split('\n')[0].Trim();
    if (firstLine.Length > 80) firstLine = firstLine.Substring(0, 80) + "…";

    var chevron = new Label { Text = "▸", TextColor = Colors.Gray };
    var summary = new Label
    {
      Text = string.IsNullOrWhiteSpace(firstLine) ? "Commands" : firstLine,
      Font = new Font(FontFamilies.Monospace, 9),
      TextColor = Colors.Gray,
      Wrap = WrapMode.Word
    };

    var header = new StackLayout
    {
      Orientation = Orientation.Horizontal,
      Spacing = 6,
      HorizontalContentAlignment = HorizontalAlignment.Stretch,
      Items = { chevron, new StackLayoutItem(summary, expand: true) }
    };

    var codeLabel = new Label
    {
      Text = trimmed,
      Font = new Font(FontFamilies.Monospace, 9),
      Wrap = WrapMode.Word,
      TextColor = Colors.Black
    };

    // White background + subtle Nexgen orange border.
    var border = Color.FromArgb(64, 0xFF, 0x5E, 0x19);
    var body = new Panel { Padding = 8, BackgroundColor = Colors.White, Content = codeLabel };
    var framed = new Panel
    {
      Padding = 1,
      BackgroundColor = border,
      Content = body,
      Visible = false
    };

    void Toggle()
    {
      framed.Visible = !framed.Visible;
      chevron.Text = framed.Visible ? "▾" : "▸";
      _messagesStack.Invalidate();
      _scroll.Invalidate();
    }

    header.MouseDown += (_, _) => Toggle();

    return new StackLayout
    {
      Orientation = Orientation.Vertical,
      Spacing = 4,
      Items = { header, framed }
    };
  }

  private Control BuildMessageBody(string content)
  {
    var parts = MessageFormatter.SplitFencedBlocks(content);

    var stack = new StackLayout
    {
      Spacing = 6,
      Orientation = Orientation.Vertical,
      HorizontalContentAlignment = HorizontalAlignment.Stretch
    };

    foreach (var part in parts)
    {
      if (part.IsCode)
      {
        stack.Items.Add(BuildExpandableCodeBlock(part.Text));
      }
      else
      {
        stack.Items.Add(RenderMarkdownLite(part.Text));
      }
    }

    return stack;
  }

  private static Control RenderMarkdownLite(string text)
  {
    var s = (text ?? string.Empty).Replace("\r\n", "\n").Trim();

    if (!s.Contains('\n'))
    {
      return new Label { Text = s, Wrap = WrapMode.Word };
    }

    var stack = new StackLayout
    {
      Orientation = Orientation.Vertical,
      Spacing = 4,
      HorizontalContentAlignment = HorizontalAlignment.Stretch
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
        stack.Items.Add(new Panel { Height = 6 });
        continue;
      }

      if (line.StartsWith("#"))
      {
        var hashes = line.TakeWhile(c => c == '#').Count();
        if (hashes is >= 1 and <= 4 && line.Length > hashes && line[hashes] == ' ')
        {
          FlushParagraph();
          var title = line.Substring(hashes + 1).Trim();
          var size = hashes switch { 1 => 12f, 2 => 11.5f, _ => 11f };
          stack.Items.Add(new Label { Text = title, Font = new Font(SystemFont.Bold, size), Wrap = WrapMode.Word });
          continue;
        }
      }

      var trimmed = line.TrimStart();
      if (trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || trimmed.StartsWith("• "))
      {
        FlushParagraph();
        var item = trimmed.Substring(2).Trim();
        stack.Items.Add(new Label { Text = "• " + item, Wrap = WrapMode.Word });
        continue;
      }

      var dot = trimmed.IndexOf('.', 0);
      if (dot > 0 && dot < 4 && int.TryParse(trimmed.Substring(0, dot), out var n) && trimmed.Length > dot + 1 && trimmed[dot + 1] == ' ')
      {
        FlushParagraph();
        var item = trimmed.Substring(dot + 2).Trim();
        stack.Items.Add(new Label { Text = $"{n}. {item}", Wrap = WrapMode.Word });
        continue;
      }

      if (trimmed.Contains('`'))
      {
        FlushParagraph();
        stack.Items.Add(new Panel
        {
          Padding = 1,
          BackgroundColor = Color.FromArgb(64, 0xFF, 0x5E, 0x19),
          Content = new Panel
          {
            Padding = 6,
            BackgroundColor = Colors.White,
            Content = new Label
            {
              Text = trimmed.Replace('`', ' '),
              Font = new Font(FontFamilies.Monospace, 9),
              Wrap = WrapMode.Word
            }
          }
        });
        continue;
      }

      para.Add(trimmed);
    }

    FlushParagraph();
    return stack;
  }
}
