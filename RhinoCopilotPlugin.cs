using System;
using System.IO;
using System.Net.Http;
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

  public static RhinoCopilotPlugin? Instance { get; private set; }
  public override PlugInLoadTime LoadTime => PlugInLoadTime.AtStartup;

  public RhinoCopilotPlugin()
  {
    Instance = this;
    IntentInterpreter = new CompositeIntentInterpreter(
      new CloudIntentInterpreter(LlmClient, () => CopilotSettings),
      new HeuristicIntentInterpreter());
  }

  internal PlanExecutionCoordinator PlanExecutionCoordinator { get; } = new();
  internal LlmClient LlmClient { get; } = new(new HttpClient());
  internal IIntentInterpreter IntentInterpreter { get; }

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
        RhinoApp.WriteLine($"Rhino Copilot: panel registration failed: {ex.Message}");
      }
    };
    RhinoApp.Idle += handler;

    RhinoApp.WriteLine("Rhino Copilot for Makers loaded.");
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
}
