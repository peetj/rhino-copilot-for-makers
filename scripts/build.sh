#!/usr/bin/env bash
set -euo pipefail

# Build the plugin using dotnet.
# Note: This requires the .NET 7 SDK on Windows.
# In Visual Studio, you can also just Build the solution.

CONFIGURATION="${1:-Debug}"

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

# Prefer dotnet if available.
if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet not found. Install .NET 7 SDK or build via Visual Studio." >&2
  exit 1
fi

dotnet build "$ROOT_DIR/RhinoCopilotForMakers.csproj" -c "$CONFIGURATION"

# The csproj copies the built DLL to a .rhp after build.
RHP_PATH="$ROOT_DIR/bin/$CONFIGURATION/RhinoCopilotForMakers.rhp"
if [[ -f "$RHP_PATH" ]]; then
  echo "Built: $RHP_PATH"
else
  echo "Build succeeded but .rhp not found at: $RHP_PATH" >&2
  echo "Check the CreateRhp target in RhinoCopilotForMakers.csproj" >&2
  exit 2
fi
