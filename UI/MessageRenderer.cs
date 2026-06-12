using System;
using System.Collections.Generic;
using System.Linq;
using Eto.Drawing;
using Eto.Forms;

namespace RhinoCopilotForMakers.UI;

internal sealed class MessageRenderer
{
  private const int ContentViewportInset = 8;
  private const int RowLeftPadding = 8;
  private const int RowRightPadding = 18;
  private const int BubbleSideGutter = 18;
  private const int MinimumBubbleWidth = 120;
  private const float UserBubbleMaxWidthRatio = 0.74f;
  private const float UserBubbleCornerRadius = 12f;
  private static readonly Color NexgenOrange = Rgba(0xFF, 0x5E, 0x19);
  private static readonly Color NexgenOrangeBorder = Rgba(0xFF, 0x5E, 0x19, 120);
  private static readonly Color UserBubbleStroke = Rgba(0xFF, 0x5E, 0x19, 118);
  private static readonly Color UserBubbleShadow = Rgba(0, 0, 0, 52);
  private static readonly Color MutedText = Color.FromArgb(78, 78, 78);
  private static readonly Color CopyIconIdle = Color.FromArgb(180, 180, 180);
  private static readonly Color CopyIconHover = Rgba(0xFF, 0x5E, 0x19, 210);

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
      contentWidth - RowLeftPadding - RowRightPadding - BubbleSideGutter);

    foreach (var bubble in _resizableBubbles)
    {
      if (bubble is SpeechBubbleDrawable speechBubble)
        speechBubble.SetMaxBubbleWidth(Math.Max(MinimumBubbleWidth, (int)Math.Round(bubbleWidth * UserBubbleMaxWidthRatio)));
      else
        bubble.Width = bubbleWidth;
    }

    _messagesStack.Invalidate();
    _scroll.Invalidate();
  }

  public void AddMessageBubble(string content, bool isAssistant)
  {
    AddMessageBubbleCustom(() => content, BuildMessageBody(content, isAssistant), isAssistant);
  }

  public void ClearMessages()
  {
    _resizableBubbles.Clear();
    _messagesStack.Items.Clear();
    _messagesStack.Invalidate();
    _scroll.Invalidate();
  }

  private Control AddMessageBubbleCustom(Func<string> getCopyText, Control body, bool isAssistant)
  {
    // ChatGPT-ish light UI:
    // - assistant messages are left-aligned with minimal chrome
    // - user messages are right-aligned with a subtle shaded bubble

    if (isAssistant)
    {
      var copyIcon = new Label
      {
        Text = "⧉",
        ToolTip = "Copy response",
        TextColor = CopyIconIdle,
        Cursor = Cursors.Pointer
      };
      WireCopyHover(copyIcon);
      copyIcon.MouseDown += (_, _) => Clipboard.Instance.Text = getCopyText();
      body = new StackLayout
      {
        Orientation = Orientation.Horizontal,
        Spacing = 8,
        VerticalContentAlignment = VerticalAlignment.Top,
        Items =
        {
          new StackLayoutItem(body, expand: true),
          copyIcon
        }
      };
    }

    Control bubbleControl;
    if (isAssistant)
    {
      bubbleControl = new Panel
      {
        BackgroundColor = Colors.White,
        Padding = new Padding(12, 10),
        Content = body
      };
    }
    else
    {
      bubbleControl = new SpeechBubbleDrawable(
        SanitizeInlineMarkdown(getCopyText()),
        SystemFonts.Default(),
        MutedText,
        UserBubbleStroke,
        UserBubbleCornerRadius);

      if (_currentContentWidth > 0 && bubbleControl is SpeechBubbleDrawable sizedBubble)
      {
        var bubbleWidth = Math.Max(
          MinimumBubbleWidth,
          _currentContentWidth - RowLeftPadding - RowRightPadding - BubbleSideGutter);
        sizedBubble.SetMaxBubbleWidth(Math.Max(MinimumBubbleWidth, (int)Math.Round(bubbleWidth * UserBubbleMaxWidthRatio)));
      }
    }

    // IMPORTANT: the container MUST expand full width, otherwise "right aligned" rows
    // will still appear left-aligned because the row shrinks to content width.
    // Using a horizontal StackLayout with an expanding spacer reliably anchors bubbles.
    var row = new StackLayout
    {
      Orientation = Orientation.Horizontal,
      Spacing = 0,
      Padding = new Padding(RowLeftPadding, 0, RowRightPadding, 0)
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
    _messagesStack.Items.Add(new StackLayoutItem(row, expand: false) { HorizontalAlignment = HorizontalAlignment.Stretch });

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
    var copyIcon = new Label
    {
      Text = "⧉",
      ToolTip = "Copy commands",
      TextColor = CopyIconIdle,
      Cursor = Cursors.Pointer
    };
    WireCopyHover(copyIcon);

    var header = new StackLayout
    {
      Orientation = Orientation.Horizontal,
      Spacing = 6,
      HorizontalContentAlignment = HorizontalAlignment.Stretch,
      Items = { chevron, new StackLayoutItem(summary, expand: true), copyIcon }
    };

    var codeLabel = new Label
    {
      Text = trimmed,
      Font = new Font(FontFamilies.Monospace, 9),
      Wrap = WrapMode.Word,
      TextColor = Colors.Black
    };

    // White background + subtle Nexgen orange border.
    var border = Rgba(0xFF, 0x5E, 0x19, 64);
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
    copyIcon.MouseDown += (_, _) => Clipboard.Instance.Text = trimmed;

    return new StackLayout
    {
      Orientation = Orientation.Vertical,
      Spacing = 4,
      Items = { header, framed }
    };
  }

  private Control BuildMessageBody(string content, bool isAssistant)
  {
    var parts = MessageFormatter.SplitFencedBlocks(content);

    var stack = new StackLayout
    {
      Spacing = 6,
      Orientation = Orientation.Vertical,
      HorizontalContentAlignment = isAssistant ? HorizontalAlignment.Stretch : HorizontalAlignment.Right
    };

    foreach (var part in parts)
    {
      if (part.IsCode)
      {
        stack.Items.Add(BuildExpandableCodeBlock(part.Text));
      }
      else
      {
        stack.Items.Add(RenderMarkdownLite(part.Text, isAssistant));
      }
    }

    return stack;
  }

  private static Control RenderMarkdownLite(string text, bool isAssistant)
  {
    var s = (text ?? string.Empty).Replace("\r\n", "\n").Trim();
    var alignment = isAssistant ? TextAlignment.Left : TextAlignment.Right;

    if (!s.Contains('\n'))
    {
      return BuildTextLabel(SanitizeInlineMarkdown(s), alignment, isAssistant ? Colors.Black : MutedText);
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
      var t = SanitizeInlineMarkdown(string.Join(" ", para.Select(l => l.Trim())).Trim());
      para.Clear();
      if (t.Length == 0) return;
      stack.Items.Add(BuildTextLabel(t, alignment, isAssistant ? Colors.Black : MutedText));
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
          var title = SanitizeInlineMarkdown(line.Substring(hashes + 1).Trim());
          var size = hashes switch { 1 => 12f, 2 => 11.5f, _ => 11f };
          stack.Items.Add(BuildTextLabel(title, alignment, NexgenOrange, new Font(SystemFont.Bold, size)));
          continue;
        }
      }

      var trimmed = line.TrimStart();
      string heading;
      if (TryParseSectionHeading(trimmed, out heading))
      {
        FlushParagraph();
        stack.Items.Add(BuildTextLabel(heading, alignment, NexgenOrange, new Font(SystemFont.Bold, 10.5f)));
        continue;
      }

      if (trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || trimmed.StartsWith("• "))
      {
        FlushParagraph();
        var item = SanitizeInlineMarkdown(trimmed.Substring(2).Trim());
        stack.Items.Add(BuildTextLabel("• " + item, alignment));
        continue;
      }

      var dot = trimmed.IndexOf('.', 0);
      if (dot > 0 && dot < 4 && int.TryParse(trimmed.Substring(0, dot), out var n) && trimmed.Length > dot + 1 && trimmed[dot + 1] == ' ')
      {
        FlushParagraph();
        var item = SanitizeInlineMarkdown(trimmed.Substring(dot + 2).Trim());
        stack.Items.Add(BuildTextLabel($"{n}. {item}", alignment));
        continue;
      }

      if (trimmed.Contains('`'))
      {
        FlushParagraph();
        stack.Items.Add(new Panel
        {
          Padding = 1,
          BackgroundColor = NexgenOrangeBorder,
          Content = new Panel
          {
            Padding = 6,
            BackgroundColor = Colors.White,
            Content = new Label
            {
              Text = SanitizeInlineMarkdown(trimmed.Replace('`', ' ').Trim()),
              Font = new Font(FontFamilies.Monospace, 9),
              Wrap = WrapMode.Word,
              TextAlignment = alignment
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

  private static Label BuildTextLabel(string text, TextAlignment alignment, Color? textColor = null, Font? font = null)
  {
    return new Label
    {
      Text = text,
      Wrap = WrapMode.Word,
      TextAlignment = alignment,
      TextColor = textColor ?? Colors.Black,
      Font = font ?? SystemFonts.Default()
    };
  }

  private static Color Rgba(int red, int green, int blue, int alpha = 255) =>
    Color.FromArgb(red, green, blue, alpha);

  private static void WireCopyHover(Label copyIcon)
  {
    copyIcon.MouseEnter += (_, _) => copyIcon.TextColor = CopyIconHover;
    copyIcon.MouseLeave += (_, _) => copyIcon.TextColor = CopyIconIdle;
  }

  private static string SanitizeInlineMarkdown(string text) =>
    (text ?? string.Empty).Replace("**", string.Empty).Trim();

  private static bool TryParseSectionHeading(string line, out string heading)
  {
    heading = string.Empty;
    var trimmed = (line ?? string.Empty).Trim();
    if (trimmed.Length == 0 || !trimmed.Contains("**"))
      return false;

    var open = trimmed.IndexOf("**", StringComparison.Ordinal);
    var close = trimmed.LastIndexOf("**", StringComparison.Ordinal);
    if (open < 0 || close <= open)
      return false;

    var prefix = trimmed.Substring(0, open).Trim();
    var core = trimmed.Substring(open + 2, close - open - 2).Trim();
    var suffix = trimmed.Substring(close + 2).Trim();
    if (core.Length == 0 || suffix.Length > 0)
      return false;

    if (prefix.Length > 0)
    {
      if (prefix.EndsWith(")"))
      {
        var numberPart = prefix.Substring(0, prefix.Length - 1).Trim();
        if (!int.TryParse(numberPart, out _))
          return false;

        heading = $"{numberPart}. {SanitizeInlineMarkdown(core)}";
        return true;
      }

      if (prefix.EndsWith("."))
      {
        var numberPart = prefix.Substring(0, prefix.Length - 1).Trim();
        if (!int.TryParse(numberPart, out _))
          return false;

        heading = $"{numberPart}. {SanitizeInlineMarkdown(core)}";
        return true;
      }

      return false;
    }

    heading = SanitizeInlineMarkdown(core);
    return true;
  }

  private sealed class SpeechBubbleDrawable : Drawable
  {
    private const int TailWidth = 16;
    private const int TailHeight = 10;
    private const int TailInset = 30;
    private const int BubbleRightInset = 4;
    private const float BorderThickness = 1.5f;

    private readonly string _text;
    private readonly Font _font;
    private readonly Color _textColor;
    private readonly Color _borderColor;
    private readonly float _cornerRadius;
    private readonly Padding _padding = new(12, 8, 14, 9);
    private int _maxBubbleWidth = MinimumBubbleWidth;

    public SpeechBubbleDrawable(string text, Font font, Color textColor, Color borderColor, float cornerRadius)
    {
      _text = text ?? string.Empty;
      _font = font;
      _textColor = textColor;
      _borderColor = borderColor;
      _cornerRadius = cornerRadius;
      SetMaxBubbleWidth(320);
    }

    public void SetMaxBubbleWidth(int width)
    {
      _maxBubbleWidth = Math.Max(MinimumBubbleWidth, width);
      var desiredWidth = MeasureDesiredWidth();
      Width = Math.Min(_maxBubbleWidth, desiredWidth);
      Height = MeasureHeight(Width);
      Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
      base.OnPaint(e);

      var rect = new RectangleF(
        0.5f,
        0.5f,
        Math.Max(0, Width - BubbleRightInset - 1),
        Math.Max(0, Height - TailHeight - 1));
      var shadowRect = new RectangleF(rect.X, rect.Y + 1, rect.Width, rect.Height);
      using var path = GraphicsPath.GetRoundRect(rect, _cornerRadius);
      using var shadowPath = GraphicsPath.GetRoundRect(shadowRect, _cornerRadius);
      using var pen = new Pen(_borderColor, BorderThickness);
      using var shadowPen = new Pen(UserBubbleShadow, 1);
      using var textBrush = new SolidBrush(_textColor);
      using var fillBrush = new SolidBrush(Colors.White);

      e.Graphics.DrawPath(shadowPen, shadowPath);
      e.Graphics.FillPath(fillBrush, path);
      e.Graphics.DrawPath(pen, path);

      var tailBaseRight = Math.Max(rect.Left + _cornerRadius + TailWidth + 4, rect.Right - TailInset);
      var tailBaseLeft = tailBaseRight - TailWidth;
      var tip = new PointF(rect.Right - 8, rect.Bottom + TailHeight - 1);
      var shadowTip = new PointF(tip.X, tip.Y + 1);

      using var tailPath = new GraphicsPath();
      tailPath.AddLine(new PointF(tailBaseLeft, rect.Bottom), tip);
      tailPath.AddLine(tip, new PointF(tailBaseRight, rect.Bottom));
      tailPath.CloseFigure();

      using var shadowTailPath = new GraphicsPath();
      shadowTailPath.AddLine(new PointF(tailBaseLeft, rect.Bottom + 1), shadowTip);
      shadowTailPath.AddLine(shadowTip, new PointF(tailBaseRight, rect.Bottom + 1));
      shadowTailPath.CloseFigure();

      e.Graphics.DrawPath(shadowPen, shadowTailPath);
      e.Graphics.FillPath(fillBrush, tailPath);
      e.Graphics.DrawPath(pen, tailPath);

      var textRect = new RectangleF(
        _padding.Left,
        _padding.Top,
        Math.Max(0, Width - _padding.Left - _padding.Right),
        Math.Max(0, Height - TailHeight - _padding.Top - _padding.Bottom));

      e.Graphics.DrawText(
        _font,
        textBrush,
        textRect,
        _text,
        FormattedTextWrapMode.Word,
        FormattedTextAlignment.Right,
        FormattedTextTrimming.None);
    }

    private int MeasureDesiredWidth()
    {
      var measured = _font.MeasureString(_text);
      return Math.Max(
        MinimumBubbleWidth,
        (int)Math.Ceiling(measured.Width + _padding.Left + _padding.Right));
    }

    private int MeasureHeight(int width)
    {
      var innerWidth = Math.Max(1, width - _padding.Left - _padding.Right);
      using var text = new FormattedText
      {
        Font = _font,
        Text = _text,
        MaximumWidth = innerWidth,
        Wrap = FormattedTextWrapMode.Word,
        Alignment = FormattedTextAlignment.Right
      };

      var size = text.Measure();
      return (int)Math.Ceiling(size.Height + _padding.Top + _padding.Bottom + TailHeight);
    }
  }
}
