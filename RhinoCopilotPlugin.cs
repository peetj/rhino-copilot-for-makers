using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using Rhino;
using Rhino.PlugIns;
using Rhino.UI;
using RhinoCopilotForMakers.Services;

namespace RhinoCopilotForMakers;

/// <summary>
/// Main Rhino plugin entrypoint.
/// </summary>
[Guid("A6D1A2E4-7F2C-4D7E-9E1D-3B4B4C7C2C1D")]
public sealed class RhinoCopilotPlugin : PlugIn
{
  private const string AutoOpenFlagFileName = "rhino-copilot-auto-open.flag";
  private const string PluginDisplayName = "Nexgen Copilot for Rhino";

  public static RhinoCopilotPlugin? Instance { get; private set; }
  public override PlugInLoadTime LoadTime => PlugInLoadTime.AtStartup;
  protected override string LocalPlugInName => PluginDisplayName;

  public RhinoCopilotPlugin()
  {
    Instance = this;
    IntentInterpreter = new HeuristicIntentInterpreter();
  }

  internal PlanExecutionCoordinator PlanExecutionCoordinator { get; } = new();
  internal HttpClient HttpClient { get; } = new();
  internal IIntentInterpreter IntentInterpreter { get; }
  internal CopilotCloudClient CloudClient => _cloudClient ??= new CopilotCloudClient(HttpClient, () => CopilotSettings);
  private CopilotCloudClient? _cloudClient;

  /// <summary>
  /// Use Rhino's persistent plugin settings store.
  /// </summary>
  public Settings.CopilotSettings CopilotSettings => new(Settings);

  internal static string AutoOpenFlagPath =>
    Path.Combine(Path.GetTempPath(), AutoOpenFlagFileName);

  protected override LoadReturnCode OnLoad(ref string errorMessage)
  {
    // Registering panels during early load can fail if Rhino hasn't assigned the plug-in ID yet
    // (Guid.Empty). Defer registration until the first Idle.
    EventHandler? handler = null;
    handler = (_, _) =>
    {
      RhinoApp.Idle -= handler;
      try
      {
        UI.CopilotPanelHost.Register();
        TryAutoOpenPanelForDevLoop();
      }
      catch (Exception ex)
      {
        RhinoApp.WriteLine($"Nexgen Copilot: panel registration failed: {ex.Message}");
      }
    };
    RhinoApp.Idle += handler;

    var assembly = typeof(RhinoCopilotPlugin).Assembly;
    var numericVersion = assembly.GetName().Version?.ToString() ?? "unknown";
    RhinoApp.WriteLine(
      $"{PluginDisplayName} loaded. Version: {numericVersion} Build: {GetBuildVersion(assembly)} Path: {assembly.Location}");
    return LoadReturnCode.Success;
  }

  private static void TryAutoOpenPanelForDevLoop()
  {
    if (!File.Exists(AutoOpenFlagPath))
      return;

    try
    {
      File.Delete(AutoOpenFlagPath);
    }
    catch
    {
      // Best-effort cleanup only. If deletion fails, still try to open the panel.
    }

    UI.CopilotPanelHost.OpenInPreferredDock();
  }

  private static string GetBuildVersion(Assembly assembly)
  {
    var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    if (!string.IsNullOrWhiteSpace(informational))
      return informational!;

    var metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>();

    foreach (var entry in metadata)
    {
      if (string.Equals(entry.Key, "RhinoCopilotBuildVersion", StringComparison.Ordinal))
        return entry.Value ?? "unknown";
    }

    return assembly.GetName().Version?.ToString() ?? "unknown";
  }
}
