using System;
using Rhino.UI;

namespace RhinoCopilotForMakers.UI;

/// <summary>
/// Registers the dockable panel with Rhino.
/// </summary>
internal static class CopilotPanelHost
{
  public static Guid PanelId => typeof(CopilotPanel).GUID;

  public static void Register()
  {
    // The icon is optional in MVP; you can add an embedded resource later.
    Panels.RegisterPanel(
      RhinoCopilotPlugin.Instance!,
      typeof(CopilotPanel),
      "Nexgen Copilot",
      null);
  }

  public static void OpenInPreferredDock()
  {
    try
    {
      Panels.OpenPanel(PanelIds.Layers);
      var layersDockBarId = Panels.PanelDockBar(PanelIds.Layers);
      if (layersDockBarId != Guid.Empty)
      {
        Panels.OpenPanel(layersDockBarId, PanelId, true);
        return;
      }

      var openedAsSibling = Panels.OpenPanelAsSibling(PanelId, PanelIds.Layers, true);
      if (openedAsSibling)
        return;
    }
    catch
    {
    }

    Panels.OpenPanel(PanelId, true);
  }
}
