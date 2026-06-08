using System;
using System.Collections.Generic;
using System.Linq;

namespace RhinoCopilotForMakers.UI;

internal static class MessageFormatter
{
  public static string NormalizeCommandBlocks(string content)
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

  public static IReadOnlyList<MessageContentPart> SplitFencedBlocks(string content)
  {
    // Small parser for triple-backtick fences.
    // Supports optional language after opening fence: ```rhino
    // We ignore the language for now, but the fence makes blocks visually distinct.
    var result = new List<MessageContentPart>();
    var s = content.Replace("\r\n", "\n");

    var i = 0;
    while (i < s.Length)
    {
      var open = s.IndexOf("```", i, StringComparison.Ordinal);
      if (open < 0)
      {
        var tail = s.Substring(i).Trim();
        if (tail.Length > 0) result.Add(new MessageContentPart(false, tail));
        break;
      }

      var before = s.Substring(i, open - i).Trim();
      if (before.Length > 0) result.Add(new MessageContentPart(false, before));

      // Skip optional language tag on the opening fence line.
      var langEnd = s.IndexOf('\n', open + 3);
      if (langEnd < 0) langEnd = open + 3;

      var close = s.IndexOf("```", langEnd, StringComparison.Ordinal);
      if (close < 0)
      {
        // No closing fence; treat rest as text.
        var rest = s.Substring(open).Trim();
        if (rest.Length > 0) result.Add(new MessageContentPart(false, rest));
        break;
      }

      var code = s.Substring(langEnd + 1, close - (langEnd + 1)).Trim();
      if (code.Length > 0) result.Add(new MessageContentPart(true, code));

      i = close + 3;
    }

    return result;
  }
}
