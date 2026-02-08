#!/bin/bash
set -e

# Opcilloscope installer for Linux and macOS
# Usage: curl -fsSL https://raw.githubusercontent.com/SquareWaveSystems/opcilloscope/main/install.sh | bash

REPO="SquareWaveSystems/opcilloscope"
INSTALL_DIR="${OPCILLOSCOPE_INSTALL_DIR:-$HOME/.local/bin}"

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

    download_url="https://github.com/${REPO}/releases/download/${version}/opcilloscope-${platform}.tar.gz"
    info "Downloading from: ${download_url}"

    tmp_dir=$(mktemp -d)
    trap "rm -rf ${tmp_dir}" EXIT

    if ! curl -fsSL "$download_url" -o "${tmp_dir}/opcilloscope.tar.gz"; then
        error "Download failed. Check if the release exists for platform: ${platform}"
    fi

    info "Extracting..."
    tar -xzf "${tmp_dir}/opcilloscope.tar.gz" -C "${tmp_dir}"

    info "Installing to ${INSTALL_DIR}..."
    mkdir -p "${INSTALL_DIR}"

    # Find the binary (handles both 'opcilloscope' and 'Opcilloscope' from different releases)
    local binary
    binary=$(find "${tmp_dir}" -maxdepth 1 -type f -iname 'opcilloscope' | head -n 1)
    if [ -z "$binary" ]; then
        error "Could not find opcilloscope binary in archive"
    fi

    mv "$binary" "${INSTALL_DIR}/opcilloscope"
    chmod +x "${INSTALL_DIR}/opcilloscope"

    # Verify installation
    if [ -x "${INSTALL_DIR}/opcilloscope" ]; then
        info "Opcilloscope ${version} installed successfully!"
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

        echo "Run 'opcilloscope' to start the application."
        echo ""
        echo "To uninstall later:"
        echo "  curl -fsSL https://raw.githubusercontent.com/SquareWaveSystems/opcilloscope/main/uninstall.sh | bash"
    else
        error "Installation failed"
    fi
}

# Main
main() {
    echo ""
    echo "  ╔═══════════════════════════════════╗"
    echo "  ║     Opcilloscope Installer        ║"
    echo "  ║   Terminal OPC UA Client          ║"
    echo "  ╚═══════════════════════════════════╝"
    echo ""

    # Check for required commands
    command -v curl >/dev/null 2>&1 || error "curl is required but not installed"
    command -v tar >/dev/null 2>&1 || error "tar is required but not installed"

    # Check for ICU libraries (required at runtime by .NET for globalization)
    if [ "$(uname -s)" = "Linux" ]; then
        local icu_found=false
        if command -v ldconfig >/dev/null 2>&1; then
            ldconfig -p 2>/dev/null | grep -q libicu && icu_found=true
        elif compgen -G "/usr/lib/*/libicu*.so*" >/dev/null 2>&1 || compgen -G "/usr/lib/libicu*.so*" >/dev/null 2>&1; then
            icu_found=true
        fi
        if [ "$icu_found" = false ]; then
            warn "ICU libraries not found. opcilloscope requires libicu at runtime."
            echo "  Install with:"
            echo "    Debian/Ubuntu: sudo apt install libicu72  (or libicu-dev)"
            echo "    Fedora/RHEL:   sudo dnf install libicu"
            echo ""
        fi
    fi

    install
}

main "$@"
