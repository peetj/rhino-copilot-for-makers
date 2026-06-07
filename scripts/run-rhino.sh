#!/usr/bin/env bash
set -euo pipefail

# Launch Rhino 8.
# You typically still need to load/reload the plugin via PluginManager.

# Baked-in Rhino 8 path (per your install)
RHINO_EXE_WIN='C:\\Program Files\\Rhino 8\\System\\Rhino.exe'

if ! command -v cygpath >/dev/null 2>&1; then
  echo "cygpath not found. This script expects Git Bash." >&2
  exit 1
fi

RHINO_EXE_POSIX="$(cygpath -u "$RHINO_EXE_WIN")"

if [[ ! -f "$RHINO_EXE_POSIX" ]]; then
  echo "Rhino.exe not found at baked-in path: $RHINO_EXE_WIN" >&2
  echo "Edit scripts/run-rhino.sh if your install path differs." >&2
  exit 1
fi

# Use 'start' to detach from the shell.
cmd.exe /c start "" "$RHINO_EXE_WIN"

echo "Launched Rhino: $RHINO_EXE_WIN"
