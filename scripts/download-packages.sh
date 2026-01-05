#!/bin/bash
# Download all NuGet packages needed for OpcScope
# Usage: ./scripts/download-packages.sh

set -e
PKGDIR="$(dirname "$0")/../packages"
mkdir -p "$PKGDIR"
cd "$PKGDIR"

download_pkg() {
    local name="$1"
    local version="$2"
    local url="https://api.nuget.org/v3-flatcontainer/${name}/${version}/${name}.${version}.nupkg"
    local file="${name}.${version}.nupkg"

    if [ -f "$file" ]; then
        echo "Already exists: $file"
        return
    fi

    echo "Downloading: $name $version"
    curl -sL -o "$file" "$url"

    # Verify it's a valid zip
    if ! unzip -tq "$file" >/dev/null 2>&1; then
        echo "ERROR: Invalid package $file"
        rm -f "$file"
        return 1
    fi
}

# Main packages
download_pkg "terminal.gui" "2.0.0"
download_pkg "nauful-libua-core" "1.0.2"

# Terminal.Gui dependencies
download_pkg "colorhelper" "1.8.1"
download_pkg "jetbrains.annotations" "2024.2.0"
download_pkg "wcwidth" "2.0.0"
download_pkg "microsoft.codeanalysis" "4.10.0"
download_pkg "microsoft.codeanalysis.common" "4.10.0"
download_pkg "microsoft.codeanalysis.csharp" "4.10.0"
download_pkg "microsoft.codeanalysis.csharp.workspaces" "4.10.0"
download_pkg "microsoft.codeanalysis.visualbasic.workspaces" "4.10.0"
download_pkg "microsoft.codeanalysis.workspaces.common" "4.10.0"
download_pkg "microsoft.extensions.logging" "8.0.0"
download_pkg "microsoft.extensions.logging.abstractions" "8.0.0"
download_pkg "microsoft.sourcelink.github" "8.0.0"
download_pkg "microsoft.sourcelink.common" "8.0.0"
download_pkg "microsoft.build.tasks.git" "8.0.0"
download_pkg "system.io.abstractions" "21.0.29"
download_pkg "testableio.system.io.abstractions" "21.0.29"
download_pkg "testableio.system.io.abstractions.wrappers" "21.0.29"

# Transitive dependencies
download_pkg "humanizer.core" "2.14.1"
download_pkg "microsoft.bcl.asyncinterfaces" "8.0.0"
download_pkg "microsoft.codeanalysis.analyzers" "3.3.4"
download_pkg "system.collections.immutable" "8.0.0"
download_pkg "system.composition" "8.0.0"
download_pkg "system.composition.attributedmodel" "8.0.0"
download_pkg "system.composition.convention" "8.0.0"
download_pkg "system.composition.hosting" "8.0.0"
download_pkg "system.composition.runtime" "8.0.0"
download_pkg "system.composition.typedparts" "8.0.0"
download_pkg "system.io.pipelines" "8.0.0"
download_pkg "system.reflection.metadata" "8.0.0"
download_pkg "system.text.encoding.codepages" "8.0.0"
download_pkg "system.text.json" "8.0.5"
download_pkg "system.text.encodings.web" "8.0.0"
download_pkg "system.threading.channels" "8.0.0"
download_pkg "system.threading.tasks.extensions" "4.5.4"
download_pkg "microsoft.extensions.dependencyinjection" "8.0.0"
download_pkg "microsoft.extensions.dependencyinjection.abstractions" "8.0.0"
download_pkg "microsoft.extensions.options" "8.0.0"
download_pkg "microsoft.extensions.primitives" "8.0.0"

echo ""
echo "All packages downloaded to $PKGDIR"
echo "Run 'dotnet restore' to complete setup"
