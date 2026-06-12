using Rhino;
using Rhino.Commands;
using RhinoCopilotForMakers.UI;

namespace RhinoCopilotForMakers;

/// <summary>
/// Opens the settings dialog.
/// </summary>
public sealed class RhinoCopilotSettingsCommand : Rhino.Commands.Command
{
  public override string EnglishName => "RhinoCopilotSettings";

  protected override Result RunCommand(RhinoDoc doc, RunMode mode)
  {
    try
    {
      var settings = RhinoCopilotPlugin.Instance!.CopilotSettings;
      CopilotSettingsDialog.Show(Rhino.UI.RhinoEtoApp.MainWindow, settings);
      return Result.Success;
    }
    catch (System.Exception ex)
    {
      RhinoApp.WriteLine($"Nexgen Copilot settings failed: {ex.Message}");
      return Result.Failure;
    }
  }
}
