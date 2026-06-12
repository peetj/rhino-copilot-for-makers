using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using RhinoCopilotForMakers.Contracts;
using RhinoCopilotForMakers.Models;

namespace RhinoCopilotForMakers.Services;

internal sealed class HeuristicIntentInterpreter : IIntentInterpreter
{
  private static readonly Regex RectangleSizeRegex = new(@"(?<w>\d+(?:\.\d+)?)\s*(?:x|×|by)\s*(?<h>\d+(?:\.\d+)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
  private static readonly Regex ExtrudeIntentRegex = new(@"\bextrud\w*\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
  private static readonly Regex RectangleIntentRegex = new(@"\brect(?:angle)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
  private static readonly Regex CircleIntentRegex = new(@"\bcircl[e]?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
  private static readonly string[] FuzzyKeywords =
  {
    "rectangle", "rect", "circle", "sphere", "extrude", "fillet", "radius", "centered", "centred",
    "create", "make", "draw", "put", "place", "solid", "diameter", "origin"
  };

  public Task<IntentInterpretationPayload?> TryInterpretAsync(
    string userText,
    RhinoContextSnapshot context,
    IReadOnlyList<ChatMessage> history,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(userText))
      return Task.FromResult<IntentInterpretationPayload?>(null);

    var normalized = NormalizeIntentText(userText);
    var hasRectangleIntent = HasRectangleIntent(normalized);
    var hasCircleIntent = HasCircleIntent(normalized);
    if (!hasRectangleIntent && !hasCircleIntent)
      return Task.FromResult<IntentInterpretationPayload?>(null);

    var widthHeight = TryParseRectangleSize(userText);
    var extrudeHeight = HasExtrudeIntent(normalized) ? TryParseExtrudeHeight(userText) : null;
    var centered = normalized.Contains("centered") || normalized.Contains("centred") || normalized.Contains("center ");
    var hasFilletIntent = hasRectangleIntent && normalized.Contains("fillet");
    var filletRadius = hasFilletIntent ? TryParseFilletRadius(userText) : null;
    var circleRadius = hasCircleIntent ? TryParseCircleRadius(userText) : null;
    var circleCenter = hasCircleIntent ? TryParsePoint2(userText) : null;

    var operations = new List<InterpretedOperationPayload>();
    var missingInputs = new List<MissingInputPayload>();
    var assumptions = new List<string>();

    if (hasRectangleIntent)
    {
      if (!centered)
        assumptions.Add("If not otherwise specified, create the rectangle from the active CPlane origin.");

      if (widthHeight is null)
      {
        missingInputs.Add(new MissingInputPayload("rectangle_size", "2d_size", context.DocumentUnits, Required: true));
      }
      else
      {
        operations.Add(new InterpretedOperationPayload(
          OperationId: "op_create_rectangle_profile",
          Action: "create_rectangle_profile",
          Target: "active_cplane",
          DependsOn: Array.Empty<string>(),
          Parameters: new[]
          {
            Parameter("width", widthHeight.Value.Width, context.DocumentUnits, widthHeight.Value.Width.ToString("0.###", CultureInfo.InvariantCulture)),
            Parameter("height", widthHeight.Value.Height, context.DocumentUnits, widthHeight.Value.Height.ToString("0.###", CultureInfo.InvariantCulture)),
            Parameter("centered", centered, null, centered ? "centered" : "origin")
          },
          Confidence: 0.98,
          CanExecuteDeterministically: true));
      }
    }

    if (hasCircleIntent)
    {
      assumptions.Add(circleCenter.HasValue
        ? "Create the circle on the active CPlane using the supplied center point."
        : "If not otherwise specified, create the circle at the active CPlane origin.");

      if (!circleRadius.HasValue)
      {
        missingInputs.Add(new MissingInputPayload("circle_radius", "distance", context.DocumentUnits, Required: true));
      }
      else
      {
        operations.Add(new InterpretedOperationPayload(
          OperationId: "op_create_circle_profile",
          Action: "create_circle_profile",
          Target: "active_cplane",
          DependsOn: Array.Empty<string>(),
          Parameters: new[]
          {
            Parameter("radius", circleRadius.Value, context.DocumentUnits, circleRadius.Value.ToString("0.###", CultureInfo.InvariantCulture)),
            Parameter("center_x", circleCenter?.X ?? 0, context.DocumentUnits, (circleCenter?.X ?? 0).ToString("0.###", CultureInfo.InvariantCulture)),
            Parameter("center_y", circleCenter?.Y ?? 0, context.DocumentUnits, (circleCenter?.Y ?? 0).ToString("0.###", CultureInfo.InvariantCulture))
          },
          Confidence: 0.96,
          CanExecuteDeterministically: true));
      }
    }

    if (hasFilletIntent)
    {
      if (!filletRadius.HasValue)
      {
        missingInputs.Add(new MissingInputPayload("fillet_radius", "distance", context.DocumentUnits, Required: true));
      }
      else
      {
        var profileDependency = operations.Any(x => x.OperationId == "op_create_rectangle_profile")
          ? "op_create_rectangle_profile"
          : "op_create_circle_profile";

        operations.Add(new InterpretedOperationPayload(
          OperationId: "op_fillet_profile_corners",
          Action: "fillet_profile_corners",
          Target: "latest_profile",
          DependsOn: profileDependency is null ? Array.Empty<string>() : new[] { profileDependency },
          Parameters: new[]
          {
            Parameter("radius", filletRadius.Value, context.DocumentUnits, filletRadius.Value.ToString("0.###", CultureInfo.InvariantCulture))
          },
          Confidence: 0.94,
          CanExecuteDeterministically: true));
      }
    }

    if (HasExtrudeIntent(normalized))
    {
      if (!extrudeHeight.HasValue)
      {
        missingInputs.Add(new MissingInputPayload("extrude_height", "distance", context.DocumentUnits, Required: true));
      }
      else
      {
        var dependency = hasFilletIntent && filletRadius.HasValue
          ? "op_fillet_profile_corners"
          : operations.Any(x => x.OperationId == "op_create_rectangle_profile")
            ? "op_create_rectangle_profile"
            : "op_create_circle_profile";

        operations.Add(new InterpretedOperationPayload(
          OperationId: "op_extrude_profile",
          Action: "extrude_profile",
          Target: "latest_profile",
          DependsOn: new[] { dependency },
          Parameters: new[]
          {
            Parameter("distance", extrudeHeight.Value, context.DocumentUnits, extrudeHeight.Value.ToString("0.###", CultureInfo.InvariantCulture)),
            Parameter("cap", true, null, "solid")
          },
          Confidence: 0.96,
          CanExecuteDeterministically: true));
      }
    }

    var readiness = missingInputs.Count > 0
      ? ExecutionReadiness.NeedsClarification
      : ExecutionReadiness.ReadyToPlan;

    return Task.FromResult<IntentInterpretationPayload?>(new IntentInterpretationPayload(
      PrimaryIntent: BuildPrimaryIntent(operations),
      ExecutionReadiness: readiness,
      Confidence: ComputeConfidence(widthHeight, extrudeHeight, hasFilletIntent, filletRadius),
      Operations: operations,
      MissingInputs: missingInputs.Count == 0 ? null : missingInputs,
      Assumptions: assumptions.Count == 0 ? null : assumptions));
  }

  private static InterpretedParameterPayload Parameter(string name, object? value, string? unit, string? sourceText) =>
    new(name, value, unit, sourceText, Confidence: 0.98);

  private static string BuildPrimaryIntent(IReadOnlyCollection<InterpretedOperationPayload> operations)
  {
    if (operations.Any(x => x.Action == "extrude_profile"))
      return "create_profile_then_extrude";

    if (operations.Any(x => x.Action == "fillet_profile_corners"))
      return "create_and_finish_profile";

    if (operations.Any(x => x.Action == "create_circle_profile"))
      return "create_circle_profile";

    return "create_rectangle_profile";
  }

  private static double ComputeConfidence((double Width, double Height)? widthHeight, double? extrudeHeight, bool hasFilletIntent, double? filletRadius)
  {
    var confidence = 0.82;
    if (widthHeight.HasValue)
      confidence += 0.08;
    if (extrudeHeight.HasValue)
      confidence += 0.05;
    if (!hasFilletIntent || filletRadius.HasValue)
      confidence += 0.03;

    return Math.Min(0.98, confidence);
  }

  private static bool HasRectangleIntent(string normalizedText) =>
    RectangleIntentRegex.IsMatch(normalizedText);

  private static bool HasCircleIntent(string normalizedText) =>
    CircleIntentRegex.IsMatch(normalizedText);

  private static bool HasExtrudeIntent(string normalizedText) =>
    ExtrudeIntentRegex.IsMatch(normalizedText);

  internal static (double Width, double Height)? TryParseRectangleSize(string text)
  {
    var match = RectangleSizeRegex.Match(text);
    if (!match.Success)
      return null;

    if (!double.TryParse(match.Groups["w"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var width))
      return null;
    if (!double.TryParse(match.Groups["h"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var height))
      return null;

    return width > 0 && height > 0 ? (width, height) : null;
  }

  internal static double? TryParseExtrudeHeight(string text)
  {
    var patterns = new[]
    {
      @"extrud\w*\s+(?:it\s+)?(?:to|by)?\s*(?<d>\d+(?:\.\d+)?)",
      @"height\s+(?:of|to|=)?\s*(?<d>\d+(?:\.\d+)?)",
      @"thick(?:ness)?\s+(?:of|to|=)?\s*(?<d>\d+(?:\.\d+)?)"
    };

    foreach (var pattern in patterns)
    {
      var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
      if (match.Success &&
          double.TryParse(match.Groups["d"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var distance) &&
          distance > 0)
        return distance;
    }

    return null;
  }

  internal static double? TryParseFilletRadius(string text)
  {
    var patterns = new[]
    {
      @"fillet(?:\s+radius)?(?:\s+of|\s+to|\s*=)?\s*(?<r>\d+(?:\.\d+)?)",
      @"fillet(?:\s+the)?(?:\s+\w+){0,4}\s+to\s+(?<r>\d+(?:\.\d+)?)",
      @"radius\s+(?:of|to|=)?\s*(?<r>\d+(?:\.\d+)?)"
    };

    foreach (var pattern in patterns)
    {
      var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
      if (match.Success &&
          double.TryParse(match.Groups["r"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var radius) &&
          radius > 0)
      {
        return radius;
      }
    }

    return null;
  }

  internal static double? TryParseCircleRadius(string text)
  {
    var patterns = new[]
    {
      @"radius\s+(?:of|to|=)?\s*(?<r>\d+(?:\.\d+)?)",
      @"r(?:adius)?\s*(?<r>\d+(?:\.\d+)?)",
      @"diameter\s+(?:of|to|=)?\s*(?<d>\d+(?:\.\d+)?)"
    };

    foreach (var pattern in patterns)
    {
      var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
      if (!match.Success)
        continue;

      if (match.Groups["r"].Success &&
          double.TryParse(match.Groups["r"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var radius) &&
          radius > 0)
      {
        return radius;
      }

      if (match.Groups["d"].Success &&
          double.TryParse(match.Groups["d"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var diameter) &&
          diameter > 0)
      {
        return diameter / 2.0;
      }
    }

    return null;
  }

  internal static (double X, double Y)? TryParsePoint2(string text)
  {
    var match = Regex.Match(text, @"\bat\s*(?<x>-?\d+(?:\.\d+)?)\s*,\s*(?<y>-?\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
    if (!match.Success)
      return null;

    if (!double.TryParse(match.Groups["x"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x))
      return null;
    if (!double.TryParse(match.Groups["y"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
      return null;

    return (x, y);
  }

  private static string NormalizeIntentText(string text)
  {
    var normalized = text.ToLowerInvariant();
    foreach (Match match in Regex.Matches(normalized, @"[a-z]+"))
    {
      var word = match.Value;
      var replacement = CanonicalizeWord(word);
      if (!string.Equals(word, replacement, StringComparison.Ordinal))
        normalized = Regex.Replace(normalized, $@"\b{Regex.Escape(word)}\b", replacement);
    }

    return normalized;
  }

  private static string CanonicalizeWord(string word)
  {
    if (word.Length < 3)
      return word;

    foreach (var keyword in FuzzyKeywords)
    {
      if (word == keyword)
        return word;

      var maxDistance = keyword.Length >= 7 ? 2 : 1;
      if (char.ToLowerInvariant(word[0]) == keyword[0] &&
          Math.Abs(word.Length - keyword.Length) <= maxDistance &&
          LevenshteinDistance(word, keyword) <= maxDistance)
      {
        return keyword;
      }
    }

    return word;
  }

  private static int LevenshteinDistance(string a, string b)
  {
    var costs = new int[b.Length + 1];
    for (var j = 0; j <= b.Length; j++)
      costs[j] = j;

    for (var i = 1; i <= a.Length; i++)
    {
      var previous = costs[0];
      costs[0] = i;
      for (var j = 1; j <= b.Length; j++)
      {
        var temp = costs[j];
        var substitution = a[i - 1] == b[j - 1] ? 0 : 1;
        costs[j] = Math.Min(
          Math.Min(costs[j] + 1, costs[j - 1] + 1),
          previous + substitution);
        previous = temp;
      }
    }

    return costs[b.Length];
  }
}
