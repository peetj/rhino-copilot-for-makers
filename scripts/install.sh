#!/usr/bin/env bash
set -euo pipefail

# Copies the built .rhp into the Rhino 8 per-user Plug-ins folder.
# This is a convenience for local dev. You still load/reload via PluginManager.

CONFIGURATION="${1:-Debug}"

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DEPLOY_ROOT="$ROOT_DIR/bin/$CONFIGURATION/_deploy"
shopt -s nullglob
RHP_CANDIDATES=( "$DEPLOY_ROOT"/*/RhinoCopilotForMakers.rhp )
shopt -u nullglob

if (( ${#RHP_CANDIDATES[@]} == 0 )); then
  echo "Missing deployed .rhp under: $DEPLOY_ROOT" >&2
  echo "Run: ./scripts/build.sh $CONFIGURATION" >&2
  exit 1
fi

IFS=$'\n' SORTED_CANDIDATES=($(printf '%s\n' "${RHP_CANDIDATES[@]}" | sort))
unset IFS
LAST_INDEX=$((${#SORTED_CANDIDATES[@]} - 1))
RHP_PATH="${SORTED_CANDIDATES[$LAST_INDEX]}"

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

echo "Installed: $RHP_PATH -> $PLUGINS_DIR/RhinoCopilotForMakers.rhp"
echo "In Rhino: PluginManager → find plugin → Reload (or Install from that path)."
