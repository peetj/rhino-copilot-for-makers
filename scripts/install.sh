#!/usr/bin/env bash
set -euo pipefail

# Copies the built .rhp into the Rhino 8 per-user Plug-ins folder.
# This is a convenience for local dev. You still load/reload via PluginManager.

CONFIGURATION="${1:-Debug}"

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RHP_PATH="$ROOT_DIR/bin/$CONFIGURATION/RhinoCopilotForMakers.rhp"

if [[ ! -f "$RHP_PATH" ]]; then
  echo "Missing: $RHP_PATH" >&2
  echo "Run: ./scripts/build.sh $CONFIGURATION" >&2
  exit 1
fi

# Convert Windows %APPDATA% to a Git Bash path via cygpath.
if ! command -v cygpath >/dev/null 2>&1; then
  echo "cygpath not found. This script expects Git Bash (MSYS) with cygpath available." >&2
  exit 1
fi

APPDATA_WIN="${APPDATA:-}"
if [[ -z "$APPDATA_WIN" ]]; then
  echo "APPDATA is not set. Are you running inside Git Bash on Windows?" >&2
  exit 1
fi

APPDATA_POSIX="$(cygpath -u "$APPDATA_WIN")"
PLUGINS_DIR="$APPDATA_POSIX/McNeel/Rhinoceros/8.0/Plug-ins/RhinoCopilotForMakers"

mkdir -p "$PLUGINS_DIR"
cp -f "$RHP_PATH" "$PLUGINS_DIR/RhinoCopilotForMakers.rhp"

echo "Installed to: $PLUGINS_DIR/RhinoCopilotForMakers.rhp"
echo "In Rhino: PluginManager → find plugin → Reload (or Install from that path)."
