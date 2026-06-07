#!/usr/bin/env bash
set -euo pipefail

# Convenience dev loop:
# 1) build
# 2) install into Rhino's per-user plug-ins folder
# 3) launch Rhino
#
# Note: if Rhino is already running, you usually just Reload the plugin via PluginManager.

CONFIGURATION="${1:-Debug}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

"$SCRIPT_DIR/build.sh" "$CONFIGURATION"
"$SCRIPT_DIR/install.sh" "$CONFIGURATION"
"$SCRIPT_DIR/run-rhino.sh"
