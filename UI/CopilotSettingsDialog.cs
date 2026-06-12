using Eto.Drawing;
using Eto.Forms;
using RhinoCopilotForMakers.Settings;

namespace RhinoCopilotForMakers.UI;

/// <summary>
/// Shared settings dialog for editing the copilot cloud worker connection.
/// </summary>
internal static class CopilotSettingsDialog
{
  public static bool Show(Control parent, CopilotSettings settings)
  {
    using var dlg = CreateDialog(settings);
    return dlg.ShowModal(parent);
  }

  public static bool Show(Window parent, CopilotSettings settings)
  {
    using var dlg = CreateDialog(settings);
    return dlg.ShowModal(parent);
  }

  private static Dialog<bool> CreateDialog(CopilotSettings settings)
  {
    var workerUrl = new TextBox { Text = settings.WorkerUrl };
    var pluginSharedSecret = new PasswordBox { Text = settings.PluginSharedSecret };

    var save = new Button { Text = "Save" };
    var cancel = new Button { Text = "Close" };

    var dlg = new Dialog<bool>
    {
      Title = "Nexgen Copilot Settings",
      Resizable = true,
      Padding = 10,
      MinimumSize = new Size(520, 220)
    };

    save.Click += (_, _) =>
    {
      settings.WorkerUrl = workerUrl.Text ?? settings.WorkerUrl;
      settings.PluginSharedSecret = pluginSharedSecret.Text ?? "";
      dlg.Close(true);
    };

    cancel.Click += (_, _) => dlg.Close(false);

    dlg.Content = new DynamicLayout
    {
      Spacing = new Size(6, 6)
    };

    var layout = (DynamicLayout)dlg.Content;
    layout.AddRow(new Label { Text = "Worker URL:" });
    layout.AddRow(workerUrl);
    layout.AddRow(new Label { Text = "Plugin Shared Secret (optional for local dev):" });
    layout.AddRow(pluginSharedSecret);
    layout.AddRow(new Label
    {
      Text = "Examples: http://127.0.0.1:8787 or https://your-worker.your-subdomain.workers.dev",
      TextColor = Colors.Gray,
      Wrap = WrapMode.Word
    });

    dlg.DefaultButton = save;
    dlg.AbortButton = cancel;

    dlg.PositiveButtons.Add(save);
    dlg.NegativeButtons.Add(cancel);

    return dlg;
  }
}
