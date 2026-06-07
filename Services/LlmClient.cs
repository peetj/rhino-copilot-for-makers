using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using RhinoCopilotForMakers.Models;

namespace RhinoCopilotForMakers.Services;

/// <summary>
/// Minimal OpenAI-compatible Chat Completions client.
/// Uses /v1/chat/completions-style payload.
/// </summary>
public sealed class LlmClient
{
  private readonly HttpClient _http;

  public LlmClient(HttpClient? httpClient = null)
  {
    _http = httpClient ?? new HttpClient();
  }

  public async Task<string> GetChatCompletionAsync(
    string endpoint,
    string apiKey,
    string model,
    string systemPrompt,
    RhinoContextSnapshot context,
    IReadOnlyList<ChatMessage> history,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(endpoint))
      throw new ArgumentException("Endpoint is required.", nameof(endpoint));
    if (string.IsNullOrWhiteSpace(model))
      throw new ArgumentException("Model is required.", nameof(model));
    if (string.IsNullOrWhiteSpace(apiKey))
      throw new ArgumentException("API key is required.", nameof(apiKey));

    var messages = new List<ApiMessage>
    {
      new("system", systemPrompt + "\n\nCurrent Rhino context (JSON):\n" + JsonSerializer.Serialize(context, JsonOptions))
    };

    foreach (var m in history)
    {
      var role = m.Role switch
      {
        ChatRole.User => "user",
        ChatRole.Assistant => "assistant",
        _ => "user"
      };
      messages.Add(new ApiMessage(role, m.Content));
    }

    var payload = new ChatCompletionsRequest
    {
      Model = model,
      Messages = messages,
      Temperature = 0.2,
      Stream = false
    };

    using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    req.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

    using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    var respText = await resp.Content.ReadAsStringAsync(cancellationToken);

    if (!resp.IsSuccessStatusCode)
      throw new InvalidOperationException($"LLM request failed ({(int)resp.StatusCode}): {respText}");

    var parsed = JsonSerializer.Deserialize<ChatCompletionsResponse>(respText, JsonOptions);
    var content = parsed?.Choices is { Count: > 0 }
      ? parsed!.Choices[0].Message?.Content
      : null;

    return string.IsNullOrWhiteSpace(content) ? "(No response content)" : content.Trim();
  }

  /// <summary>
  /// Streaming variant using OpenAI-compatible Server-Sent Events (SSE).
  /// Yields incremental content deltas (choices[0].delta.content).
  /// </summary>
  public async IAsyncEnumerable<string> StreamChatCompletionAsync(
    string endpoint,
    string apiKey,
    string model,
    string systemPrompt,
    RhinoContextSnapshot context,
    IReadOnlyList<ChatMessage> history,
    [EnumeratorCancellation] CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(endpoint))
      throw new ArgumentException("Endpoint is required.", nameof(endpoint));
    if (string.IsNullOrWhiteSpace(model))
      throw new ArgumentException("Model is required.", nameof(model));
    if (string.IsNullOrWhiteSpace(apiKey))
      throw new ArgumentException("API key is required.", nameof(apiKey));

    var messages = new List<ApiMessage>
    {
      new("system", systemPrompt + "\n\nCurrent Rhino context (JSON):\n" + JsonSerializer.Serialize(context, JsonOptions))
    };

    foreach (var m in history)
    {
      var role = m.Role switch
      {
        ChatRole.User => "user",
        ChatRole.Assistant => "assistant",
        _ => "user"
      };
      messages.Add(new ApiMessage(role, m.Content));
    }

    var payload = new ChatCompletionsRequest
    {
      Model = model,
      Messages = messages,
      Temperature = 0.2,
      Stream = true
    };

    using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
    req.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

    using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    if (!resp.IsSuccessStatusCode)
    {
      var err = await resp.Content.ReadAsStringAsync(cancellationToken);
      throw new InvalidOperationException($"LLM request failed ({(int)resp.StatusCode}): {err}");
    }

    await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken);
    using var reader = new StreamReader(stream);

    while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
    {
      var line = await reader.ReadLineAsync();
      if (line is null)
        break;

      if (string.IsNullOrWhiteSpace(line))
        continue;

      if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        continue;

      var data = line.Substring(5).Trim();
      if (data == "[DONE]")
        yield break;

      // Parse JSON chunk.
      string? chunk = null;
      try
      {
        using var doc = JsonDocument.Parse(data);
        var root = doc.RootElement;

        // OpenAI chat.completions stream format:
        // { choices: [ { delta: { content: "..." } } ] }
        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
          continue;

        var delta = choices[0].GetProperty("delta");
        if (delta.ValueKind == JsonValueKind.Object && delta.TryGetProperty("content", out var contentEl))
          chunk = contentEl.GetString();
      }
      catch (JsonException)
      {
        // Ignore malformed chunks
      }

      if (!string.IsNullOrEmpty(chunk))
        yield return chunk;
    }
  }

  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true
  };

  private sealed class ChatCompletionsRequest
  {
    [JsonPropertyName("model")] public string Model { get; set; } = "";
    [JsonPropertyName("messages")] public List<ApiMessage> Messages { get; set; } = new();
    [JsonPropertyName("temperature")] public double? Temperature { get; set; }
    [JsonPropertyName("stream")] public bool? Stream { get; set; }
  }

  private sealed record ApiMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

  private sealed class ChatCompletionsResponse
  {
    [JsonPropertyName("choices")] public List<Choice> Choices { get; set; } = new();

    internal sealed class Choice
    {
      [JsonPropertyName("message")] public Message? Message { get; set; }
    }

    internal sealed class Message
    {
      [JsonPropertyName("content")] public string? Content { get; set; }
    }
  }
}
