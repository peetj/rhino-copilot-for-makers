param(
  [ValidateSet('Debug','Release')][string]$Configuration = 'Debug',
  [string]$RhinoPluginsDir = "$env:APPDATA\McNeel\Rhinoceros\8.0\Plug-ins"
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$bin = Join-Path $root "bin\$Configuration"
$deployRoot = Join-Path $bin "_deploy"

$rhp = Get-ChildItem -Path $deployRoot -Filter "RhinoCopilotForMakers.rhp" -Recurse -ErrorAction SilentlyContinue |
  Sort-Object LastWriteTimeUtc -Descending |
  Select-Object -First 1
if (-not $rhp) {
  throw "Could not find RhinoCopilotForMakers.rhp under $deployRoot. Build first."
}

$destDir = Join-Path $RhinoPluginsDir "RhinoCopilotForMakers"
New-Item -ItemType Directory -Force -Path $destDir | Out-Null

Copy-Item -Force $rhp.FullName (Join-Path $destDir $rhp.Name)

Write-Host "Installed: $($rhp.FullName) -> $destDir"
Write-Host "If Rhino has already been told to load this plug-in from $destDir, you can skip PluginManager."
Write-Host "For a faster dev loop, run scripts\\DevReloadRhinoCopilot.ps1 -RestartRhino"
