using Rhino;
using System;

namespace RhinoCopilotForMakers.Settings;

/// <summary>
/// Simple wrapper around Rhino's PersistentSettings.
/// Stores cloud worker connection settings without hardcoding secrets.
/// </summary>
public sealed class CopilotSettings
{
  private const string LegacyEndpointKey = "Endpoint";
  private const string LegacyApiKeyKey = "ApiKey";

  private readonly PersistentSettings _settings;

  public CopilotSettings(PersistentSettings settings)
  {
    _settings = settings;
  }

  public string WorkerUrl
  {
    get
    {
      var configured = _settings.GetString(nameof(WorkerUrl), "");
      if (!string.IsNullOrWhiteSpace(configured))
        return configured;

      var legacy = _settings.GetString(LegacyEndpointKey, "");
      if (!string.IsNullOrWhiteSpace(legacy) &&
          !legacy.Contains("openai.com", StringComparison.OrdinalIgnoreCase) &&
          !legacy.Contains("/chat/completions", StringComparison.OrdinalIgnoreCase))
      {
        return legacy;
      }

      return "";
    }

    set => _settings.SetString(nameof(WorkerUrl), value);
  }

  public string PluginSharedSecret
  {
    get => _settings.GetString(nameof(PluginSharedSecret), "");
    set => _settings.SetString(nameof(PluginSharedSecret), value);
  }

  public bool HasWorkerUrl => !string.IsNullOrWhiteSpace(WorkerUrl);
  public bool HasPluginSharedSecret => !string.IsNullOrWhiteSpace(PluginSharedSecret);

  public string Endpoint
  {
    get => _settings.GetString(LegacyEndpointKey, "");
    set => _settings.SetString(LegacyEndpointKey, value);
  }

  public string Model
  {
    get => _settings.GetString(nameof(Model), "");
    set => _settings.SetString(nameof(Model), value);
  }

  public string ApiKey
  {
    get => _settings.GetString(LegacyApiKeyKey, "");
    set => _settings.SetString(LegacyApiKeyKey, value);
  }

  public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);
}
