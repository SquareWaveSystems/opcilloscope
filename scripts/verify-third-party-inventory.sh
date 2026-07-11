#!/usr/bin/env bash
set -euo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
inventory="$repo_root/licenses/THIRD-PARTY-PACKAGES.tsv"
package_root="${NUGET_PACKAGES:-$HOME/.nuget/packages}"
supported_rids=(linux-x64 linux-arm64 win-x64 win-arm64 osx-x64 osx-arm64)

command -v jq >/dev/null 2>&1 || {
    echo "jq is required to validate the third-party inventory" >&2
    exit 1
}

tmp_dir=$(mktemp -d)
trap 'rm -rf "$tmp_dir"' EXIT

awk -F '\t' 'NR > 1 { print $1 "\t" $2 }' "$inventory" | sort \
    > "$tmp_dir/inventory.tsv"

for rid in "${supported_rids[@]}"; do
    lock_file="$repo_root/packages.$rid.lock.json"
    target="net10.0/$rid"

    if [ ! -f "$lock_file" ]; then
        echo "Missing dependency lock: $lock_file" >&2
        exit 1
    fi

    jq -e --arg target "$target" \
        '(.dependencies | keys | sort) == (["net10.0", $target] | sort)' \
        "$lock_file" >/dev/null

    jq -r '.dependencies["net10.0"] | to_entries[] | [.key, .value.resolved] | @tsv' \
        "$lock_file" | sort > "$tmp_dir/locked-$rid.tsv"
    if ! diff -u "$tmp_dir/inventory.tsv" "$tmp_dir/locked-$rid.tsv"; then
        echo "Third-party inventory does not match packages.$rid.lock.json" >&2
        exit 1
    fi

    jq -r --arg target "$target" \
        '.dependencies[$target] | to_entries[] | [.key, .value.resolved] | @tsv' \
        "$lock_file" | sort > "$tmp_dir/runtime-$rid.tsv"
    if ! diff -u <(printf 'Onigwrap\t1.0.11\n') "$tmp_dir/runtime-$rid.tsv"; then
        echo "Unexpected RID-specific dependency graph in packages.$rid.lock.json" >&2
        exit 1
    fi
done

cmp "$repo_root/licenses/ONIGWRAP-THIRD-PARTY-NOTICES.txt" \
    "$package_root/onigwrap/1.0.11/THIRD-PARTY-NOTICES.TXT"
tr -d '\r' < "$package_root/opcfoundation.netstandard.opc.ua.client/1.5.378.156/LICENSE.txt" \
    > "$tmp_dir/opc-foundation-license.txt"
cmp "$repo_root/licenses/OPC-FOUNDATION-LICENSE.txt" \
    "$tmp_dir/opc-foundation-license.txt"

reference_notice="$package_root/microsoft.extensions.dependencyinjection/10.0.8/THIRD-PARTY-NOTICES.TXT"
for package_notice in \
    "$package_root/microsoft.extensions.dependencyinjection.abstractions/10.0.8/THIRD-PARTY-NOTICES.TXT" \
    "$package_root/microsoft.extensions.logging/10.0.8/THIRD-PARTY-NOTICES.TXT" \
    "$package_root/microsoft.extensions.logging.abstractions/10.0.8/THIRD-PARTY-NOTICES.TXT" \
    "$package_root/microsoft.extensions.options/10.0.8/THIRD-PARTY-NOTICES.TXT" \
    "$package_root/microsoft.extensions.primitives/10.0.8/THIRD-PARTY-NOTICES.TXT"
do
    cmp "$reference_notice" "$package_notice"
done

echo "Third-party package inventory and exact packaged notices are current."
