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

# Verify the downloaded archive against the SHA256SUMS published with the release.
# Degrades gracefully (warning only) when SHA256SUMS is unavailable (older releases).
verify_checksum() {
    local version="$1" archive_name="$2" archive_path="$3" tmp_dir="$4"
    local sums_url="https://github.com/${REPO}/releases/download/${version}/SHA256SUMS"
    local expected actual

    if ! curl -fsSL "$sums_url" -o "${tmp_dir}/SHA256SUMS" 2>/dev/null; then
        warn "SHA256SUMS not found for ${version} (older release?). Skipping checksum verification."
        return 0
    fi

    expected=$(grep " ${archive_name}\$" "${tmp_dir}/SHA256SUMS" | awk '{print $1}')
    if [ -z "$expected" ]; then
        warn "No checksum entry for ${archive_name} in SHA256SUMS. Skipping checksum verification."
        return 0
    fi

    if command -v sha256sum >/dev/null 2>&1; then
        actual=$(sha256sum "$archive_path" | awk '{print $1}')
    elif command -v shasum >/dev/null 2>&1; then
        actual=$(shasum -a 256 "$archive_path" | awk '{print $1}')
    else
        warn "Neither sha256sum nor shasum is available. Skipping checksum verification."
        return 0
    fi

    if [ "$actual" != "$expected" ]; then
        echo ""
        echo "  Expected: ${expected}"
        echo "  Actual:   ${actual}"
        error "Checksum mismatch for ${archive_name}. The download may be corrupted or tampered with. Aborting."
    fi

    info "Checksum verified (SHA-256)."
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

    info "Verifying checksum..."
    verify_checksum "$version" "opcilloscope-${platform}.tar.gz" "${tmp_dir}/opcilloscope.tar.gz" "$tmp_dir"

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

    install
}

main "$@"
