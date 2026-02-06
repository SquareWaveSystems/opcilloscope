#!/bin/bash
set -e

# Opcilloscope uninstaller for Linux and macOS
# Usage: curl -fsSL https://raw.githubusercontent.com/SquareWaveSystems/opcilloscope/main/uninstall.sh | bash

INSTALL_DIR="${OPCILLOSCOPE_INSTALL_DIR:-$HOME/.local/bin}"
CONFIG_DIR="${XDG_CONFIG_HOME:-$HOME/.config}/opcilloscope"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

info() { echo -e "${GREEN}[INFO]${NC} $1"; }
warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
error() { echo -e "${RED}[ERROR]${NC} $1"; exit 1; }

uninstall() {
    echo ""
    echo "  ╔═══════════════════════════════════╗"
    echo "  ║    Opcilloscope Uninstaller       ║"
    echo "  ║   Terminal OPC UA Client          ║"
    echo "  ╚═══════════════════════════════════╝"
    echo ""

    local binary="${INSTALL_DIR}/opcilloscope"
    local removed_something=false

    # Remove binary
    if [ -f "$binary" ]; then
        rm "$binary"
        info "Removed binary: ${binary}"
        removed_something=true
    else
        warn "Binary not found at ${binary}"
    fi

    # Remove config directory
    if [ -d "$CONFIG_DIR" ]; then
        echo ""
        echo -n "Remove configuration directory ${CONFIG_DIR}? [y/N] "
        # When piped from curl, stdin is the script itself, so default to no
        if [ -t 0 ]; then
            read -r answer
        else
            answer="n"
            echo "(skipped — run interactively to remove config files)"
        fi

        if [ "$answer" = "y" ] || [ "$answer" = "Y" ]; then
            rm -rf "$CONFIG_DIR"
            info "Removed config directory: ${CONFIG_DIR}"
        else
            info "Kept config directory: ${CONFIG_DIR}"
        fi
    fi

    echo ""
    if [ "$removed_something" = true ]; then
        info "Opcilloscope has been uninstalled."
    else
        warn "Opcilloscope does not appear to be installed at ${INSTALL_DIR}."
        echo ""
        echo "If you installed to a custom directory, run:"
        echo "  OPCILLOSCOPE_INSTALL_DIR=/your/path bash uninstall.sh"
    fi
}

uninstall
