#!/bin/bash
set -euo pipefail

# Opcilloscope installer for Linux and macOS
# Usage: curl -fsSL https://raw.githubusercontent.com/SquareWaveSystems/opcilloscope/main/install.sh | bash

REPO="SquareWaveSystems/opcilloscope"
INSTALL_DIR="${OPCILLOSCOPE_INSTALL_DIR:-$HOME/.local/bin}"

case "$(uname -s)" in
    Darwin*) APP_DATA_DIR="$HOME/Library/Application Support/opcilloscope" ;;
    *)       APP_DATA_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/opcilloscope" ;;
esac
LICENSE_DIR="${APP_DATA_DIR}/licenses"
INSTALL_TMP_DIR=""
trap '[ -z "$INSTALL_TMP_DIR" ] || rm -rf "$INSTALL_TMP_DIR"' EXIT

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

info() { echo -e "${GREEN}[INFO]${NC} $1"; }
warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
error() { echo -e "${RED}[ERROR]${NC} $1"; exit 1; }

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

get_latest_version() {
    curl -fsSL "https://api.github.com/repos/${REPO}/releases/latest" |
        grep '"tag_name":' |
        sed -E 's/.*"([^"]+)".*/\1/'
}

# Verify the archive against SHA256SUMS published with the same release.
# The installer always targets the latest release, and current releases are
# required to publish a checksum for every archive.
verify_checksum() {
    local version="$1" archive_name="$2" archive_path="$3" tmp_dir="$4"
    local sums_url="https://github.com/${REPO}/releases/download/${version}/SHA256SUMS"
    local expected actual

    if ! curl -fsSL "$sums_url" -o "${tmp_dir}/SHA256SUMS"; then
        error "Could not download SHA256SUMS for ${version}. Refusing an unverified install."
    fi

    expected=$(awk -v archive="$archive_name" '$2 == archive { print $1; exit }' "${tmp_dir}/SHA256SUMS")
    if [ "${#expected}" -ne 64 ] || [[ "$expected" == *[!0-9A-Fa-f]* ]]; then
        error "SHA256SUMS has no valid checksum entry for ${archive_name}. Refusing an unverified install."
    fi

    if command -v sha256sum >/dev/null 2>&1; then
        actual=$(sha256sum "$archive_path" | awk '{print $1}')
    elif command -v shasum >/dev/null 2>&1; then
        actual=$(shasum -a 256 "$archive_path" | awk '{print $1}')
    else
        error "Neither sha256sum nor shasum is available. Cannot verify the release archive."
    fi

    if [ "$actual" != "$expected" ]; then
        echo ""
        echo "  Expected: ${expected}"
        echo "  Actual:   ${actual}"
        error "Checksum mismatch for ${archive_name}. The download may be corrupted or tampered with. Aborting."
    fi

    info "Checksum verified (SHA-256)."
}

install_license_material() {
    local tmp_dir="$1"
    local copied=false

    # This directory is installer-owned. Replace it so notices removed by a
    # package upgrade cannot linger and misdescribe the installed release.
    rm -rf "$LICENSE_DIR"
    mkdir -p "$LICENSE_DIR"

    if [ -f "${tmp_dir}/LICENSE" ]; then
        cp "${tmp_dir}/LICENSE" "${LICENSE_DIR}/LICENSE.txt"
        copied=true
    fi
    if [ -f "${tmp_dir}/THIRD-PARTY-NOTICES.md" ]; then
        cp "${tmp_dir}/THIRD-PARTY-NOTICES.md" "$LICENSE_DIR/"
        copied=true
    fi
    if [ -d "${tmp_dir}/licenses" ]; then
        cp -R "${tmp_dir}/licenses/." "$LICENSE_DIR/"
        copied=true
    fi

    if [ "$copied" = true ]; then
        info "Installed license notices to ${LICENSE_DIR}"
    else
        warn "This older release archive did not contain license notice files."
        rmdir "$LICENSE_DIR" 2>/dev/null || true
    fi
}

install_opcilloscope() {
    local platform version download_url archive_name tmp_dir binary

    info "Detecting platform..."
    platform=$(detect_platform)
    info "Platform: ${platform}"

    info "Fetching latest version..."
    version=$(get_latest_version)
    if [ -z "$version" ]; then
        error "Could not determine latest version. Check your internet connection."
    fi
    info "Version: ${version}"

    archive_name="opcilloscope-${platform}.tar.gz"
    download_url="https://github.com/${REPO}/releases/download/${version}/${archive_name}"
    info "Downloading from: ${download_url}"

    tmp_dir=$(mktemp -d)
    INSTALL_TMP_DIR="$tmp_dir"

    if ! curl -fsSL "$download_url" -o "${tmp_dir}/${archive_name}"; then
        error "Download failed. Check if the release exists for platform: ${platform}"
    fi

    info "Verifying checksum..."
    verify_checksum "$version" "$archive_name" "${tmp_dir}/${archive_name}" "$tmp_dir"

    info "Extracting..."
    tar -xzf "${tmp_dir}/${archive_name}" -C "$tmp_dir"

    binary=$(find "$tmp_dir" -maxdepth 1 -type f -iname 'opcilloscope' | head -n 1)
    if [ -z "$binary" ]; then
        error "Could not find opcilloscope binary in archive"
    fi

    info "Installing to ${INSTALL_DIR}..."
    mkdir -p "$INSTALL_DIR"
    mv "$binary" "${INSTALL_DIR}/opcilloscope"
    chmod +x "${INSTALL_DIR}/opcilloscope"
    install_license_material "$tmp_dir"

    if ! "${INSTALL_DIR}/opcilloscope" --help >/dev/null; then
        error "Installed executable failed its command-line smoke test"
    fi

    info "Opcilloscope ${version} installed successfully!"
    echo ""

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
}

main() {
    echo ""
    echo "  ╔═══════════════════════════════════╗"
    echo "  ║     Opcilloscope Installer        ║"
    echo "  ║   Terminal OPC UA Client          ║"
    echo "  ╚═══════════════════════════════════╝"
    echo ""

    command -v curl >/dev/null 2>&1 || error "curl is required but not installed"
    command -v tar >/dev/null 2>&1 || error "tar is required but not installed"

    install_opcilloscope
}

main "$@"
