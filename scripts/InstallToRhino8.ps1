param(
  [ValidateSet('Debug','Release')][string]$Configuration = 'Debug',
  [string]$RhinoPluginsDir = "$env:APPDATA\McNeel\Rhinoceros\8.0\Plug-ins"
)

$ErrorActionPreference = 'Stop'
$pluginId = 'A6D1A2E4-7F2C-4D7E-9E1D-3B4B4C7C2C1D'

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

$destFile = Join-Path $destDir $rhp.Name
Copy-Item -Force $rhp.FullName $destFile

function Update-PluginRegistryPath {
  param(
    [Parameter(Mandatory = $true)][string]$RegistryRoot
  )

  $pluginKey = Join-Path $RegistryRoot $pluginId
  $pluginSubKey = Join-Path $pluginKey 'PlugIn'

  if (-not (Test-Path -LiteralPath $pluginSubKey)) {
    return $false
  }

  Set-ItemProperty -LiteralPath $pluginSubKey -Name 'FileName' -Value $destFile
  return $true
}

$updatedHkcu = Update-PluginRegistryPath -RegistryRoot 'HKCU:\Software\McNeel\Rhinoceros\8.0\Plug-Ins'
$updatedHklm = $false
try {
  $updatedHklm = Update-PluginRegistryPath -RegistryRoot 'HKLM:\SOFTWARE\McNeel\Rhinoceros\8.0\Plug-Ins'
}
catch {
  Write-Host "Skipped HKLM registry update: $($_.Exception.Message)"
}

Write-Host "Installed: $($rhp.FullName) -> $destDir"
Write-Host "Stable plug-in path: $destFile"
if ($updatedHkcu) {
  Write-Host "Updated HKCU Rhino plug-in registration to the stable path."
}
if ($updatedHklm) {
  Write-Host "Updated HKLM Rhino plug-in registration to the stable path."
}
Write-Host "If Rhino has already been told to load this plug-in from $destDir, you can skip PluginManager."
Write-Host "For a faster dev loop, run scripts\\DevReloadRhinoCopilot.ps1 -RestartRhino"
