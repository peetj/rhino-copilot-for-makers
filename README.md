# Nexgen Copilot for Rhino (Rhino 8)

Current iteration: a dockable Rhino 8 copilot panel for Windows built with **C# + RhinoCommon + Eto.Forms**, backed by a Cloudflare worker orchestration layer.

## MVP features
- Command `RhinoCopilot` toggles a **dockable panel**.
- Chat UI: history, multiline input, Send/Cancel, Copy buttons, loading state.
- Markdown-lite rendering for headings/bullets/numbered steps + fenced command blocks.
- Structured turn requests sent to a **Cloudflare worker**.
- Compact **Rhino document context snapshot** sent with every request.
- Plan approval flow for supported local execution steps.
- Safe local execution boundary inside the Rhino plugin.

## File structure
- `RhinoCopilotPlugin.cs` – plugin entrypoint + panel registration
- `RhinoCopilotCommand.cs` – `RhinoCopilot` command toggles panel
- `UI/CopilotPanelHost.cs` – registers the panel with Rhino
- `UI/CopilotPanel.cs` – Eto UI + chat logic
- `Services/CopilotCloudClient.cs` – Cloud worker transport client
- `Services/RhinoContextCollector.cs` – collects Rhino model context
- `Models/*` – message + context snapshot models
- `Settings/CopilotSettings.cs` – worker URL + shared-secret settings
- `cloud/` – Cloudflare worker, orchestrator, planner, critic, compiler

## Build (Windows)
Prereqs:
- Rhino 8 for Windows
- Visual Studio 2022
- .NET 7 SDK

Steps:
1. Open `RhinoCopilotForMakers.sln` in Visual Studio.
2. Restore NuGet packages.
3. Build.

### .rhp build output
This project includes an MSBuild step that copies the built assembly to an installable **.rhp** file in a deploy folder (so Rhino locking the installed plug-in won’t break rebuilds):
- `bin/Debug/_deploy/<timestamp>/RhinoCopilotForMakers.rhp`
- `bin/Release/_deploy/<timestamp>/RhinoCopilotForMakers.rhp`

### Loading into Rhino
- In Rhino, run `PluginManager` → **Install** and select the `.rhp`.

### Optional: local install scripts
PowerShell:
```powershell
./scripts/InstallToRhino8.ps1 -Configuration Debug
./scripts/DevReloadRhinoCopilot.ps1 -Configuration Debug -RestartRhino
```

Git Bash:
```bash
./scripts/dev-reload.sh -Configuration Debug -RestartRhino
```

Git Bash:
```bash
./scripts/build.sh Debug
./scripts/install.sh Debug
./scripts/run-rhino.sh
# or all-in-one:
./scripts/dev.sh Debug
```

Both install scripts resolve the newest timestamped `.rhp` from `bin/<Configuration>/_deploy/`.

Notes:
- You typically still **Reload** the plugin via `PluginManager` if Rhino is already running.
- Faster Windows dev loop:
  - `./scripts/DevReloadRhinoCopilot.ps1 -Configuration Debug -RestartRhino`
  - This builds, installs the latest `.rhp`, restarts Rhino, and drops a one-shot flag so the plug-in opens the Copilot panel after it has loaded.
  - If Rhino is already running, use `-RestartRhino`; otherwise the script now skips install rather than trying to overwrite a locked plug-in file.
- `run-rhino.sh` uses a baked-in Rhino path: `C:\\Program Files\\Rhino 8\\System\\Rhino.exe`
  - If yours differs, edit `scripts/run-rhino.sh`.

## Configure the Cloud Worker
In Rhino:
1. Run `RhinoCopilot` to open the panel.
2. Click **Settings**.
3. Fill in:
   - **Worker URL**: e.g. `http://127.0.0.1:8787` for local `wrangler dev`, or your deployed Worker URL
   - **Plugin Shared Secret**: optional for local dev, recommended once worker auth is enabled in deployed environments

The plugin stores these values locally via Rhino plugin settings.

## Notes on context snapshot
The plugin collects a compact snapshot before each request:
- Rhino version
- document units
- active viewport name
- absolute + angle tolerances
- selected object count
- selected object types (mesh/brep/curve/surface/extrusion/SubD/etc)
- combined bounding box dimensions of selection
- selected object layer names
- first 30 document layer names

See: `Services/RhinoContextCollector.cs`.

## Response formatting
Assistant responses support fenced code blocks:

```text
Use these steps...

``` 
_Some Rhino commands_
```
```

Those blocks are displayed in a monospaced, visually distinct box with a **Copy block** button.

## Safety boundary
- Cloud interprets and plans, but does not mutate Rhino directly.
- The Rhino plugin is the only executor.
- Unsupported or unsafe actions are rejected or clarified before execution.
- Local execution remains allowlisted and bounded by the plugin.

## Next steps (after MVP)
- Better message rendering (markdown-lite, inline command chips)
- Streaming responses
- Local prompt presets (3D printing / surfacing / booleans)
- Better selection summaries (polycount, naked edges, manifold checks)
