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
Write-Host "In Rhino: run PluginManager and (re)load, or install from $destDir."
