#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if ! command -v powershell >/dev/null 2>&1; then
  echo "powershell not found in PATH." >&2
  exit 1
fi

powershell -ExecutionPolicy Bypass -File "$SCRIPT_DIR/DevReloadRhinoCopilot.ps1" "$@"
