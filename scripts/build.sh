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

# The csproj copies the built DLL to a timestamped deploy folder.
DEPLOY_ROOT="$ROOT_DIR/bin/$CONFIGURATION/_deploy"
shopt -s nullglob
RHP_CANDIDATES=( "$DEPLOY_ROOT"/*/RhinoCopilotForMakers.rhp )
shopt -u nullglob

if (( ${#RHP_CANDIDATES[@]} == 0 )); then
  echo "Build succeeded but no deployed .rhp was found under: $DEPLOY_ROOT" >&2
  echo "Check the CreateRhp target in RhinoCopilotForMakers.csproj" >&2
  exit 2
fi

IFS=$'\n' SORTED_CANDIDATES=($(printf '%s\n' "${RHP_CANDIDATES[@]}" | sort))
unset IFS
LAST_INDEX=$((${#SORTED_CANDIDATES[@]} - 1))
RHP_PATH="${SORTED_CANDIDATES[$LAST_INDEX]}"

echo "Built: $RHP_PATH"
