#!/bin/bash
# Script to download NuGet packages for offline use
# Packages are downloaded from nuget.org using curl

set -e

PACKAGES_DIR="$(dirname "$0")/../packages"
mkdir -p "$PACKAGES_DIR"

download_package() {
    local id="${1,,}"  # lowercase
    local version="$2"
    local filename="${id}.${version}.nupkg"
    local url="https://api.nuget.org/v3-flatcontainer/${id}/${version}/${filename}"

    if [ -f "$PACKAGES_DIR/$filename" ]; then
        echo "Already exists: $filename"
    else
        echo "Downloading: $filename"
        curl -sSL -o "$PACKAGES_DIR/$filename" "$url"
    fi
}

# Main project packages
download_package "Terminal.Gui" "2.0.0"
download_package "OPCFoundation.NetStandard.Opc.Ua.Client" "1.5.374.126"

# OPC Foundation dependencies
download_package "OPCFoundation.NetStandard.Opc.Ua.Core" "1.5.374.126"
download_package "OPCFoundation.NetStandard.Opc.Ua.Configuration" "1.5.374.126"
download_package "OPCFoundation.NetStandard.Opc.Ua.Security.Certificates" "1.5.374.126"
download_package "OPCFoundation.NetStandard.Opc.Ua.Bindings.Https" "1.5.374.126"

# Test project packages
download_package "coverlet.collector" "6.0.2"
download_package "Microsoft.NET.Test.Sdk" "17.11.0"
download_package "Moq" "4.20.72"
download_package "OPCFoundation.NetStandard.Opc.Ua.Server" "1.5.374.126"
download_package "xunit" "2.9.0"
download_package "xunit.runner.visualstudio" "2.8.0"

# Common transitive dependencies
download_package "xunit.abstractions" "2.0.3"
download_package "xunit.core" "2.9.0"
download_package "xunit.extensibility.core" "2.9.0"
download_package "xunit.extensibility.execution" "2.9.0"
download_package "xunit.assert" "2.9.0"
download_package "xunit.analyzers" "1.15.0"
download_package "Castle.Core" "5.1.1"
download_package "System.IO.Pipelines" "8.0.0"
download_package "Microsoft.Extensions.Logging.Abstractions" "8.0.2"
download_package "Microsoft.Extensions.Logging" "8.0.1"
download_package "Microsoft.Extensions.Logging" "8.0.0"
download_package "Portable.BouncyCastle" "1.9.0"
download_package "Newtonsoft.Json" "13.0.3"
download_package "Microsoft.CodeCoverage" "17.11.0"
download_package "Microsoft.TestPlatform.TestHost" "17.11.0"
download_package "Microsoft.TestPlatform.ObjectModel" "17.11.0"
download_package "NuGet.Frameworks" "6.11.0"
download_package "System.Reflection.Metadata" "8.0.0"
download_package "System.Collections.Immutable" "8.0.0"

# Additional transitive dependencies (Terminal.Gui and more)
download_package "ColorHelper" "1.8.1"
download_package "JetBrains.Annotations" "2024.2.0"
download_package "Microsoft.CodeAnalysis" "4.11.0"
download_package "Microsoft.CodeAnalysis.CSharp" "4.11.0"
download_package "Microsoft.CodeAnalysis.Common" "4.11.0"
download_package "Microsoft.CodeAnalysis.CSharp.Workspaces" "4.11.0"
download_package "Microsoft.CodeAnalysis.VisualBasic.Workspaces" "4.11.0"
download_package "Microsoft.CodeAnalysis.Workspaces.Common" "4.11.0"
download_package "Microsoft.CodeAnalysis.VisualBasic" "4.11.0"
download_package "Microsoft.SourceLink.GitHub" "8.0.0"
download_package "Microsoft.SourceLink.Common" "8.0.0"
download_package "System.IO.Abstractions" "21.0.29"
download_package "System.Text.Json" "8.0.5"
download_package "Wcwidth" "2.0.0"
download_package "Microsoft.Extensions.DependencyInjection" "8.0.1"
download_package "Microsoft.Extensions.DependencyInjection.Abstractions" "8.0.2"
download_package "Microsoft.Extensions.Options" "8.0.2"
download_package "Microsoft.Extensions.Primitives" "8.0.0"
download_package "System.Diagnostics.EventLog" "8.0.0"
download_package "Microsoft.CodeAnalysis.Analyzers" "3.3.4"
download_package "Microsoft.Build.Tasks.Git" "8.0.0"
download_package "System.Text.Encoding.CodePages" "8.0.0"
download_package "Humanizer.Core" "2.14.1"
download_package "Microsoft.Bcl.AsyncInterfaces" "8.0.0"
download_package "System.Composition" "8.0.0"
download_package "System.Composition.AttributedModel" "8.0.0"
download_package "System.Composition.Convention" "8.0.0"
download_package "System.Composition.Hosting" "8.0.0"
download_package "System.Composition.Runtime" "8.0.0"
download_package "System.Composition.TypedParts" "8.0.0"
download_package "TestableIO.System.IO.Abstractions" "21.0.29"
download_package "TestableIO.System.IO.Abstractions.Wrappers" "21.0.29"

echo "Done downloading packages to $PACKAGES_DIR"
