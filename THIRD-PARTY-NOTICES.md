# Third-Party Notices

opcilloscope's own source code is licensed under the MIT License; see
[LICENSE](LICENSE).

Official releases are self-contained .NET applications. The executable
bundles the managed dependencies below and embeds the native Oniguruma runtime
asset for extraction when needed. Release archives include this inventory,
the exact notices under [`licenses/`](licenses/), and the exact .NET runtime
and Microsoft.Extensions notices copied from the packages used for that RID.

The authoritative resolved graphs are the checked-in `packages.<rid>.lock.json`
files for all six supported release targets. CI verifies that their package
versions match [`licenses/THIRD-PARTY-PACKAGES.tsv`](licenses/THIRD-PARTY-PACKAGES.tsv).

## Bundled package inventory

| Package | Version | License |
|---------|---------|---------|
| OPCFoundation.NetStandard.Opc.Ua.Client | 1.5.378.156 | OPC Foundation MIT 1.00 |
| OPCFoundation.NetStandard.Opc.Ua.Configuration | 1.5.378.156 | OPC Foundation MIT 1.00 |
| OPCFoundation.NetStandard.Opc.Ua.Core | 1.5.378.156 | OPC Foundation MIT 1.00 |
| OPCFoundation.NetStandard.Opc.Ua.Security.Certificates | 1.5.378.156 | OPC Foundation MIT 1.00 |
| OPCFoundation.NetStandard.Opc.Ua.Types | 1.5.378.156 | OPC Foundation MIT 1.00 |
| Terminal.Gui | 2.4.5 | MIT |
| BitFaster.Caching | 2.6.0 | MIT |
| ColorHelper | 1.8.1 | MIT |
| JetBrains.Annotations | 2025.2.4 | MIT |
| Markdig | 1.1.3 | BSD-2-Clause |
| Microsoft.Extensions.DependencyInjection | 10.0.8 | MIT |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.8 | MIT |
| Microsoft.Extensions.Logging | 10.0.8 | MIT |
| Microsoft.Extensions.Logging.Abstractions | 10.0.8 | MIT |
| Microsoft.Extensions.Options | 10.0.8 | MIT |
| Microsoft.Extensions.Primitives | 10.0.8 | MIT |
| Newtonsoft.Json | 13.0.4 | MIT |
| Onigwrap | 1.0.11 | MIT; bundled Oniguruma uses a BSD-style license |
| System.IO.Abstractions | 22.1.1 | MIT |
| TestableIO.System.IO.Abstractions | 22.1.1 | MIT |
| TestableIO.System.IO.Abstractions.Wrappers | 22.1.1 | MIT |
| Testably.Abstractions.FileSystem.Interface | 10.1.0 | MIT |
| TextMateSharp | 2.0.4 | MIT |
| TextMateSharp.Grammars | 2.0.4 | MIT |
| Wcwidth | 4.0.1 | MIT |
| Microsoft .NET Runtime | resolved from the .NET 10 SDK for each RID | MIT plus packaged third-party notices |

`MinVer` 7.0.0 (Apache-2.0) and `Microsoft.NET.ILLink.Tasks` 10.0.9
(MIT) are build-only tools and contribute no code to published binaries.

## Exact notices carried with releases

- [OPC Foundation MIT License 1.00](licenses/OPC-FOUNDATION-LICENSE.txt)
- [Markdig BSD-2-Clause License](licenses/MARKDIG-LICENSE.txt)
- [Onigwrap third-party notices](licenses/ONIGWRAP-THIRD-PARTY-NOTICES.txt),
  including the required native Oniguruma notice
- `DOTNET-RUNTIME-<rid>-LICENSE.txt` and
  `DOTNET-RUNTIME-<rid>-THIRD-PARTY-NOTICES.txt`, copied from the exact
  self-contained runtime pack during release publishing
- `MICROSOFT-EXTENSIONS-THIRD-PARTY-NOTICES.txt`, copied from the exact
  resolved Microsoft.Extensions package after verifying that the six bundled
  packages carry identical notice content

See [notice sources and maintenance](licenses/NOTICE-SOURCES.md) for the
validation and regeneration procedure.

## Other MIT-licensed components

The following copyright notices apply to the remaining MIT components:

- Terminal.Gui — Copyright 2007-2011 Novell Inc; Copyright 2017 Microsoft Corp
- BitFaster.Caching — Copyright (c) 2020 Alex Peck
- ColorHelper — Copyright (c) 2020 Artyom Gritsuk
- JetBrains.Annotations — Copyright (c) 2016-2024 JetBrains s.r.o.
- Microsoft.Extensions.* and the Microsoft .NET Runtime:
  - Copyright (c) .NET Foundation and Contributors
  - All rights reserved.
- Newtonsoft.Json — Copyright (c) 2007 James Newton-King
- Onigwrap — Copyright (c) 2024 Aikawa Yataro; its packaged historical and
  third-party notices are reproduced separately
- System.IO.Abstractions / TestableIO.System.IO.Abstractions*:
  - Copyright (c) Tatham Oddie and Contributors
  - All rights reserved.
- Testably.Abstractions.FileSystem.Interface — Copyright (c) 2022 Valentin Breuß
- TextMateSharp / TextMateSharp.Grammars — Copyright (c) 2021 Daniel Peñalba
- Wcwidth — Copyright Patrik Svensson. Phil Scott

MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
