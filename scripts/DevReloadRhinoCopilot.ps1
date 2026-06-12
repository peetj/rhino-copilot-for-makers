param(
  [ValidateSet('Debug','Release')][string]$Configuration = 'Debug',
  [string]$RhinoExe = 'C:\Program Files\Rhino 8\System\Rhino.exe',
  [switch]$RestartRhino
)

$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $PSScriptRoot

Push-Location $scriptRoot
try {
  Write-Host "Building $Configuration..."
  dotnet build .\RhinoCopilotForMakers.csproj -c $Configuration
  if ($LASTEXITCODE -ne 0) {
    throw "Build failed."
  }

  Write-Host "Installing latest .rhp into Rhino plug-ins folder..."
  & "$PSScriptRoot\InstallToRhino8.ps1" -Configuration $Configuration

  if (-not (Test-Path -LiteralPath $RhinoExe)) {
    throw "Rhino.exe not found at '$RhinoExe'."
  }

  $running = Get-Process -Name Rhino -ErrorAction SilentlyContinue
  if ($running -and $RestartRhino) {
    Write-Host "Stopping Rhino..."
    $running | Stop-Process -Force
    Start-Sleep -Seconds 2
  }
  elseif ($running) {
    Write-Host "Rhino is already running. Re-run with -RestartRhino to relaunch into the updated plug-in automatically."
    Write-Host "Build/install completed."
    return
  }

  $startupMacro = '/nosplash /runscript="_RhinoCopilotShow"'
  Write-Host "Launching Rhino and opening the Copilot panel..."
  Start-Process -FilePath $RhinoExe -ArgumentList $startupMacro
}
finally {
  Pop-Location
}
