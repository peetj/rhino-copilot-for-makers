using System;
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
    "You are the intent interpreter for Rhino Copilot. " +
    "Your job is to convert the latest user request into a strict JSON object matching the IntentInterpretationPayload contract. " +
    "Return JSON only. Do not use markdown fences. Do not include commentary. " +
    "Classify the request into execution_readiness: informational_only, ready_to_plan, needs_clarification, or unsafe. " +
    "When the request implies executable Rhino work, normalize it into operations using canonical actions such as create_rectangle_profile, fillet_profile_corners, and extrude_profile. " +
    "If required dimensions or parameters are missing, set execution_readiness to needs_clarification and populate missing_inputs. " +
    "If the user is just asking for advice or explanation, set execution_readiness to informational_only with empty operations. " +
    "Prefer semantic interpretation over literal keyword matching.";

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

  public async Task<IntentInterpretationPayload?> TryInterpretAsync(string userText, RhinoContextSnapshot context, CancellationToken cancellationToken)
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
        history: new[] { new ChatMessage(ChatRole.User, userText) },
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
      RhinoApp.WriteLine($"Rhino Copilot cloud interpreter fallback: {ex.Message}");
      return null;
    }
  }

  private static bool IsUsable(IntentInterpretationPayload? interpretation) =>
    interpretation is not null &&
    !string.IsNullOrWhiteSpace(interpretation.PrimaryIntent);

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
