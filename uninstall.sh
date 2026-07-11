#!/bin/bash
set -e

# Opcilloscope uninstaller for Linux and macOS
# Usage: curl -fsSL https://raw.githubusercontent.com/SquareWaveSystems/opcilloscope/main/uninstall.sh | bash

INSTALL_DIR="${OPCILLOSCOPE_INSTALL_DIR:-$HOME/.local/bin}"

if [ "$(uname -s)" = "Darwin" ]; then
    # .NET 8+ maps ApplicationData and LocalApplicationData to this directory.
    CONFIG_DIR="$HOME/Library/Application Support/opcilloscope"
    DATA_DIR="$CONFIG_DIR"
    MACOS=true
else
    CONFIG_DIR="${XDG_CONFIG_HOME:-$HOME/.config}/opcilloscope"
    DATA_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/opcilloscope"
    MACOS=false
fi

CERT_DIR="${DATA_DIR}/pki"
LICENSE_DIR="${DATA_DIR}/licenses"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

info() { echo -e "${GREEN}[INFO]${NC} $1"; }
warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }

confirm_removal() {
    local prompt="$1" answer

    echo -n "$prompt [y/N] "
    if [ -t 0 ]; then
        read -r answer
    else
        answer="n"
        echo "(skipped — run interactively to remove retained user data)"
    fi

    [ "$answer" = "y" ] || [ "$answer" = "Y" ]
}

uninstall_opcilloscope() {
    echo ""
    echo "  ╔═══════════════════════════════════╗"
    echo "  ║    Opcilloscope Uninstaller       ║"
    echo "  ║   Terminal OPC UA Client          ║"
    echo "  ╚═══════════════════════════════════╝"
    echo ""

    local binary="${INSTALL_DIR}/opcilloscope"
    local removed_something=false
    local has_config=false

    # INSTALL_DIR may be a shared custom bin directory. Remove only our file.
    if [ -f "$binary" ]; then
        rm "$binary"
        info "Removed binary: ${binary}"
        removed_something=true
    else
        warn "Binary not found at ${binary}"
    fi

    # License material is installer-owned and safe to remove automatically.
    if [ -d "$LICENSE_DIR" ]; then
        rm -rf "$LICENSE_DIR"
        info "Removed license notices: ${LICENSE_DIR}"
        removed_something=true
    fi

    if [ "$MACOS" = true ]; then
        # Config and certificates share one macOS Application Support parent.
        # Treat only the known config entries as configuration data so a user
        # can retain the PKI store independently.
        if [ -d "${CONFIG_DIR}/configs" ] || [ -f "${CONFIG_DIR}/recent-files.json" ]; then
            has_config=true
        fi
    elif [ -d "$CONFIG_DIR" ]; then
        has_config=true
    fi

    if [ "$has_config" = true ]; then
        echo ""
        if confirm_removal "Remove opcilloscope configuration data at ${CONFIG_DIR}?"; then
            if [ "$MACOS" = true ]; then
                rm -rf "${CONFIG_DIR}/configs"
                rm -f "${CONFIG_DIR}/recent-files.json"
            else
                rm -rf "$CONFIG_DIR"
            fi
            info "Removed configuration data: ${CONFIG_DIR}"
            removed_something=true
        else
            info "Kept configuration data: ${CONFIG_DIR}"
        fi
    fi

    if [ -d "$CERT_DIR" ]; then
        echo ""
        if confirm_removal "Remove OPC UA certificate store ${CERT_DIR}?"; then
            rm -rf "$CERT_DIR"
            info "Removed certificate store: ${CERT_DIR}"
            removed_something=true
        else
            info "Kept certificate store: ${CERT_DIR}"
        fi
    fi

    # Remove app-owned parents only when they are empty. Never remove a shared
    # custom install directory.
    rmdir "$DATA_DIR" 2>/dev/null || true
    if [ "$CONFIG_DIR" != "$DATA_DIR" ]; then
        rmdir "$CONFIG_DIR" 2>/dev/null || true
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

uninstall_opcilloscope
