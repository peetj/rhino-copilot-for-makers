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
      "Rhino Copilot",
      null);
  }
}
