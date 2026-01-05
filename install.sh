#!/bin/bash
set -e

# OpcScope installer for Linux and macOS
# Usage: curl -fsSL https://raw.githubusercontent.com/BrettKinny/OpcScope/main/install.sh | bash

REPO="BrettKinny/OpcScope"
INSTALL_DIR="${OPCSCOPE_INSTALL_DIR:-$HOME/.local/bin}"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

info() { echo -e "${GREEN}[INFO]${NC} $1"; }
warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
error() { echo -e "${RED}[ERROR]${NC} $1"; exit 1; }

# Detect OS and architecture
detect_platform() {
    local os arch

    case "$(uname -s)" in
        Linux*)  os="linux" ;;
        Darwin*) os="osx" ;;
        *)       error "Unsupported OS: $(uname -s)" ;;
    esac

    case "$(uname -m)" in
        x86_64|amd64)  arch="x64" ;;
        arm64|aarch64) arch="arm64" ;;
        *)             error "Unsupported architecture: $(uname -m)" ;;
    esac

    echo "${os}-${arch}"
}

# Get latest release version
get_latest_version() {
    curl -fsSL "https://api.github.com/repos/${REPO}/releases/latest" |
        grep '"tag_name":' |
        sed -E 's/.*"([^"]+)".*/\1/'
}

# Download and install
install() {
    local platform version download_url tmp_dir

    info "Detecting platform..."
    platform=$(detect_platform)
    info "Platform: ${platform}"

    info "Fetching latest version..."
    version=$(get_latest_version)
    if [ -z "$version" ]; then
        error "Could not determine latest version. Check your internet connection."
    fi
    info "Version: ${version}"

    download_url="https://github.com/${REPO}/releases/download/${version}/opcscope-${platform}.tar.gz"
    info "Downloading from: ${download_url}"

    tmp_dir=$(mktemp -d)
    trap "rm -rf ${tmp_dir}" EXIT

    if ! curl -fsSL "$download_url" -o "${tmp_dir}/opcscope.tar.gz"; then
        error "Download failed. Check if the release exists for platform: ${platform}"
    fi

    info "Extracting..."
    tar -xzf "${tmp_dir}/opcscope.tar.gz" -C "${tmp_dir}"

    info "Installing to ${INSTALL_DIR}..."
    mkdir -p "${INSTALL_DIR}"
    mv "${tmp_dir}/opcscope" "${INSTALL_DIR}/opcscope"
    chmod +x "${INSTALL_DIR}/opcscope"

    # Verify installation
    if [ -x "${INSTALL_DIR}/opcscope" ]; then
        info "OpcScope ${version} installed successfully!"
        echo ""

        # Check if install dir is in PATH
        if [[ ":$PATH:" != *":${INSTALL_DIR}:"* ]]; then
            warn "${INSTALL_DIR} is not in your PATH"
            echo ""
            echo "Add it to your shell profile:"
            echo "  echo 'export PATH=\"\$PATH:${INSTALL_DIR}\"' >> ~/.bashrc"
            echo "  # or for zsh:"
            echo "  echo 'export PATH=\"\$PATH:${INSTALL_DIR}\"' >> ~/.zshrc"
            echo ""
        fi

        echo "Run 'opcscope' to start the application."
    else
        error "Installation failed"
    fi
}

# Main
main() {
    echo ""
    echo "  ╔═══════════════════════════════════╗"
    echo "  ║       OpcScope Installer          ║"
    echo "  ║   Terminal OPC UA Client          ║"
    echo "  ╚═══════════════════════════════════╝"
    echo ""

    # Check for required commands
    command -v curl >/dev/null 2>&1 || error "curl is required but not installed"
    command -v tar >/dev/null 2>&1 || error "tar is required but not installed"

    install
}

main "$@"
