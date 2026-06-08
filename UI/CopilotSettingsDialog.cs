using Eto.Drawing;
using Eto.Forms;
using RhinoCopilotForMakers.Settings;

namespace RhinoCopilotForMakers.UI;

/// <summary>
/// Shared settings dialog for editing the copilot endpoint, model, and API key.
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
    var endpoint = new TextBox { Text = settings.Endpoint };
    var model = new TextBox { Text = settings.Model };
    var apiKey = new PasswordBox { Text = settings.ApiKey };

    var save = new Button { Text = "Save" };
    var cancel = new Button { Text = "Close" };

    var dlg = new Dialog<bool>
    {
      Title = "Rhino Copilot Settings",
      Resizable = true,
      Padding = 10,
      MinimumSize = new Size(520, 220)
    };

    save.Click += (_, _) =>
    {
      settings.Endpoint = endpoint.Text ?? settings.Endpoint;
      settings.Model = model.Text ?? settings.Model;
      settings.ApiKey = apiKey.Text ?? "";
      dlg.Close(true);
    };

    cancel.Click += (_, _) => dlg.Close(false);

    dlg.Content = new DynamicLayout
    {
      Spacing = new Size(6, 6)
    };

    var layout = (DynamicLayout)dlg.Content;
    layout.AddRow(new Label { Text = "Endpoint (OpenAI-compatible):" });
    layout.AddRow(endpoint);
    layout.AddRow(new Label { Text = "Model:" });
    layout.AddRow(model);
    layout.AddRow(new Label { Text = "API Key (stored locally):" });
    layout.AddRow(apiKey);

    dlg.DefaultButton = save;
    dlg.AbortButton = cancel;

    dlg.PositiveButtons.Add(save);
    dlg.NegativeButtons.Add(cancel);

    return dlg;
  }
}
