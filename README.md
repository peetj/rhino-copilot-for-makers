# Rhino Copilot for Makers (Rhino 8)

First iteration: a guidance-only, dockable in-app chatbot panel for Rhino 8 (Windows) built with **C# + RhinoCommon + Eto.Forms**.

## MVP features
- Command `RhinoCopilot` toggles a **dockable panel**.
- Chat UI: history, multiline input, Send/Cancel, Copy buttons, loading state.
- Streaming responses (tokens appear as they arrive).
- Markdown-lite rendering for headings/bullets/numbered steps + fenced command blocks.
- OpenAI-compatible **Chat Completions** HTTP call.
- Compact **Rhino document context snapshot** sent with every request.
- **Safety**: guidance-only. No automatic command execution, no geometry modification, no RunScript.

## File structure
- `RhinoCopilotPlugin.cs` – plugin entrypoint + panel registration
- `RhinoCopilotCommand.cs` – `RhinoCopilot` command toggles panel
- `UI/CopilotPanelHost.cs` – registers the panel with Rhino
- `UI/CopilotPanel.cs` – Eto UI + chat logic
- `Services/LlmClient.cs` – OpenAI-compatible chat completions client
- `Services/RhinoContextCollector.cs` – collects Rhino model context
- `Models/*` – message + context snapshot models
- `Settings/CopilotSettings.cs` – endpoint/model/key stored in Rhino plugin settings

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
- `run-rhino.sh` uses a baked-in Rhino path: `C:\\Program Files\\Rhino 8\\System\\Rhino.exe`
  - If yours differs, edit `scripts/run-rhino.sh`.

## Configure the LLM endpoint/model/key
In Rhino:
1. Run `RhinoCopilot` to open the panel.
2. Click **Settings**.
3. Fill in:
   - **Endpoint**: e.g. `https://api.openai.com/v1/chat/completions` (or your compatible server)
   - **Model**: e.g. `gpt-4.1-mini` (placeholder)
   - **API Key**: your key

The API key is stored locally via Rhino plugin settings (no secrets are hardcoded).

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

## Safety / non-goals (v1)
- No automatic model edits
- No `RhinoApp.RunScript` execution
- No direct geometry modifications

## Next steps (after MVP)
- Better message rendering (markdown-lite, inline command chips)
- Streaming responses
- Local prompt presets (3D printing / surfacing / booleans)
- Better selection summaries (polycount, naked edges, manifold checks)
