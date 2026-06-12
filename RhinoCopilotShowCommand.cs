using System;
using Rhino;
using Rhino.Commands;
using Rhino.UI;

namespace RhinoCopilotForMakers;

/// <summary>
/// Command: RhinoCopilotShow
/// Opens the dockable chat panel without toggling it closed if already visible.
/// </summary>
public sealed class RhinoCopilotShowCommand : Command
{
  public override string EnglishName => "RhinoCopilotShow";

  protected override Result RunCommand(RhinoDoc doc, RunMode mode)
  {
    try
    {
      Panels.OpenPanel(UI.CopilotPanelHost.PanelId);
      return Result.Success;
    }
    catch (Exception ex)
    {
      RhinoApp.WriteLine($"RhinoCopilotShow failed: {ex.Message}");
      return Result.Failure;
    }
  }
}
