# Third-party notice sources

`THIRD-PARTY-PACKAGES.tsv` is the reviewed legal inventory for the dependency
graph in the six `../packages.<rid>.lock.json` files. Run
`scripts/verify-third-party-inventory.sh` after changing any package.

To deliberately regenerate all supported RID locks, run:

```bash
for rid in linux-x64 linux-arm64 win-x64 win-arm64 osx-x64 osx-arm64; do
  dotnet restore Opcilloscope.csproj -p:RuntimeIdentifier="$rid" \
    --use-lock-file --force-evaluate
done
```

Then review the resolved versions and licenses, update the TSV and notice
files, and rerun the validator. Ordinary restore selects the current SDK host
RID's lock. Release publishing passes its target RID explicitly and uses the
matching checked-in lock without restoring every platform pack at once.

Exact committed notices are sourced as follows:

- `ONIGWRAP-THIRD-PARTY-NOTICES.txt` is copied byte-for-byte from
  `Onigwrap` 1.0.11's `THIRD-PARTY-NOTICES.TXT`. It includes the required
  native Oniguruma notice.
- `OPC-FOUNDATION-LICENSE.txt` is copied verbatim from the
  `OPCFoundation.NetStandard.Opc.Ua.Client` 1.5.378.156 package, with CRLF
  normalized to repository-standard LF.
- `MARKDIG-LICENSE.txt` reproduces the Markdig upstream license with formatting
  whitespace normalized; the Markdig 1.1.3 NuGet metadata identifies it as
  BSD-2-Clause.

Release archives also receive two files directly from the exact packages
resolved on the build runner:

- `DOTNET-RUNTIME-<rid>-LICENSE.txt` and
  `DOTNET-RUNTIME-<rid>-THIRD-PARTY-NOTICES.txt` from the self-contained
  `Microsoft.NETCore.App.Runtime.<rid>` pack.
- `MICROSOFT-EXTENSIONS-THIRD-PARTY-NOTICES.txt` from
  `Microsoft.Extensions.DependencyInjection` 10.0.8. The release validation
  first confirms that all six resolved `Microsoft.Extensions.*` packages
  carry the same notice; if they diverge, the build fails so each distinct
  notice can be added deliberately.

Do not paraphrase packaged notice text. Preserve package-provided files
verbatim in release archives and installed license directories; a
source-derived license may normalize formatting whitespace when documented
above without changing its wording.
