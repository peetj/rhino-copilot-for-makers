using Rhino;

namespace RhinoCopilotForMakers.Settings;

/// <summary>
/// Simple wrapper around Rhino's PersistentSettings.
/// Stores API endpoint/model/key without hardcoding secrets.
/// </summary>
public sealed class CopilotSettings
{
  private readonly PersistentSettings _settings;

  public CopilotSettings(PersistentSettings settings)
  {
    _settings = settings;
  }

  public string Endpoint
  {
    get => _settings.GetString(nameof(Endpoint), "https://api.openai.com/v1/chat/completions");
    set => _settings.SetString(nameof(Endpoint), value);
  }

  public string Model
  {
    get => _settings.GetString(nameof(Model), "gpt-4.1-mini");
    set => _settings.SetString(nameof(Model), value);
  }

  /// <summary>
  /// API key stored locally in Rhino plugin settings.
  /// (This is local to the machine/user profile.)
  /// </summary>
  public string ApiKey
  {
    get => _settings.GetString(nameof(ApiKey), "");
    set => _settings.SetString(nameof(ApiKey), value);
  }

  public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);
}
