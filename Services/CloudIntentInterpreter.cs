using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Rhino;
using RhinoCopilotForMakers.Contracts;
using RhinoCopilotForMakers.Models;
using RhinoCopilotForMakers.Settings;

namespace RhinoCopilotForMakers.Services;

internal sealed class CloudIntentInterpreter : IIntentInterpreter
{
  private const string SystemPrompt =
    "You are the intent interpreter for Nexgen Copilot for Rhino. " +
    "Your job is to convert the current user request plus recent conversation context into a strict JSON object matching the IntentInterpretationPayload contract. " +
    "Return JSON only. Do not use markdown fences. Do not include commentary. " +
    "Classify the request into execution_readiness: informational_only, ready_to_plan, needs_clarification, or unsafe. " +
    "When the request implies executable Rhino work, normalize it into semantic operations instead of copying user phrasing literally. " +
    "Prefer intent understanding, typo tolerance, and contextual interpretation over keyword matching. " +
    "Supported canonical action families include: create_rectangle_profile, create_circle_profile, create_line_curve, create_box_solid, create_cylinder_solid, create_sphere_solid, fillet_profile_corners, extrude_profile, move_objects, rotate_objects, scale_objects, boolean_union, boolean_difference, boolean_intersection, offset_curve, offset_surface, create_loft_surface, create_sweep_surface, create_patch_surface. " +
    "If the user asks for something executable but leaves out required parameters, set execution_readiness to needs_clarification and populate missing_inputs with only the truly missing values. " +
    "If the user gives enough context to make a standard Rhino assumption safely, do so and record that in assumptions instead of over-asking. " +
    "If the user is just asking for advice or explanation, set execution_readiness to informational_only with empty operations. " +
    "If the request would be destructive or risky, set execution_readiness to unsafe. " +
    "Use prior turns to resolve short clarification replies like '2', 'yes', 'centered', or follow-up edits to the previous modeling request.";

  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true
  };

  private readonly LlmClient _llmClient;
  private readonly Func<CopilotSettings> _settingsProvider;

  public CloudIntentInterpreter(LlmClient llmClient, Func<CopilotSettings> settingsProvider)
  {
    _llmClient = llmClient;
    _settingsProvider = settingsProvider;
  }

  public async Task<IntentInterpretationPayload?> TryInterpretAsync(
    string userText,
    RhinoContextSnapshot context,
    IReadOnlyList<ChatMessage> history,
    CancellationToken cancellationToken)
  {
    var settings = _settingsProvider();
    if (!settings.HasApiKey || string.IsNullOrWhiteSpace(settings.Endpoint) || string.IsNullOrWhiteSpace(settings.Model))
      return null;

    try
    {
      var response = await _llmClient.GetChatCompletionAsync(
        endpoint: settings.Endpoint,
        apiKey: settings.ApiKey,
        model: settings.Model,
        systemPrompt: SystemPrompt,
        context: context,
        history: BuildInterpreterHistory(userText, history),
        cancellationToken: cancellationToken);

      var json = ExtractJsonObject(response);
      if (string.IsNullOrWhiteSpace(json))
        return null;

      var interpretation = JsonSerializer.Deserialize<IntentInterpretationPayload>(json, JsonOptions);
      return IsUsable(interpretation) ? interpretation : null;
    }
    catch (OperationCanceledException)
    {
      throw;
    }
    catch (Exception ex)
    {
      RhinoApp.WriteLine($"Nexgen Copilot cloud interpreter fallback: {ex.Message}");
      return null;
    }
  }

  private static bool IsUsable(IntentInterpretationPayload? interpretation) =>
    interpretation is not null &&
    !string.IsNullOrWhiteSpace(interpretation.PrimaryIntent);

  private static IReadOnlyList<ChatMessage> BuildInterpreterHistory(string userText, IReadOnlyList<ChatMessage> history)
  {
    if (history.Count == 0)
      return new[] { new ChatMessage(ChatRole.User, userText) };

    var relevant = history
      .Where(m => m.Role is ChatRole.User or ChatRole.Assistant)
      .TakeLast(8)
      .ToList();

    var last = relevant.LastOrDefault();
    if (last is null || last.Role != ChatRole.User || !string.Equals(last.Content, userText, StringComparison.Ordinal))
      relevant.Add(new ChatMessage(ChatRole.User, userText));

    return relevant;
  }

  private static string? ExtractJsonObject(string content)
  {
    if (string.IsNullOrWhiteSpace(content))
      return null;

    var trimmed = content.Trim();
    if (trimmed.StartsWith("```", StringComparison.Ordinal))
    {
      var firstNewline = trimmed.IndexOf('\n');
      if (firstNewline >= 0)
        trimmed = trimmed[(firstNewline + 1)..];

      var closingFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
      if (closingFence >= 0)
        trimmed = trimmed[..closingFence].Trim();
    }

    var start = trimmed.IndexOf('{');
    var end = trimmed.LastIndexOf('}');
    if (start < 0 || end <= start)
      return null;

    return trimmed.Substring(start, end - start + 1);
  }
}
