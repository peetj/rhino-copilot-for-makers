using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.Commands;

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

      var l = (DynamicLayout)dlg.Content;
      l.AddRow(new Label { Text = "Endpoint (OpenAI-compatible):" });
      l.AddRow(endpoint);
      l.AddRow(new Label { Text = "Model:" });
      l.AddRow(model);
      l.AddRow(new Label { Text = "API Key (stored locally):" });
      l.AddRow(apiKey);

      dlg.DefaultButton = save;
      dlg.AbortButton = cancel;

      dlg.PositiveButtons.Add(save);
      dlg.NegativeButtons.Add(cancel);

      dlg.ShowModal(Rhino.UI.RhinoEtoApp.MainWindow);
      return Result.Success;
    }
    catch (System.Exception ex)
    {
      RhinoApp.WriteLine($"Rhino Copilot settings failed: {ex.Message}");
      return Result.Failure;
    }
  }
}
