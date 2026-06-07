using System;
using Rhino;
using Rhino.Commands;
using Rhino.UI;

namespace RhinoCopilotForMakers;

/// <summary>
/// Command: RhinoCopilot
/// Opens/toggles the dockable chat panel.
/// </summary>
public sealed class RhinoCopilotCommand : Command
{
  public override string EnglishName => "RhinoCopilot";

  protected override Result RunCommand(RhinoDoc doc, RunMode mode)
  {
    try
    {
      var panelId = UI.CopilotPanelHost.PanelId;

      // Toggle: if visible then close, else open.
      var isVisible = Panels.IsPanelVisible(panelId);
      if (isVisible)
        Panels.ClosePanel(panelId);
      else
        Panels.OpenPanel(panelId);

      return Result.Success;
    }
    catch (Exception ex)
    {
      RhinoApp.WriteLine($"RhinoCopilot failed: {ex.Message}");
      return Result.Failure;
    }
  }
}
