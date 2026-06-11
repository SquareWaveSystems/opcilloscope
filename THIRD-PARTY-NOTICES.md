# Third-Party Notices

opcilloscope's own source code is licensed under the MIT License (see
[LICENSE](LICENSE)).

Official binary releases of opcilloscope are published as self-contained,
single-file builds. These binaries statically bundle the third-party
components listed below, together with the Microsoft .NET runtime. This file
lists those components, their versions, upstream projects, and licenses.

Packages that are used only at build time (MinVer, Microsoft.SourceLink.*,
Microsoft.Build.Tasks.Git, Microsoft.NET.ILLink.Tasks,
Microsoft.CodeAnalysis.Analyzers) contribute no code to the published
binaries and are not listed individually.

## Summary of bundled components

| Package | Version | Upstream | License (SPDX) |
|---------|---------|----------|----------------|
| OPCFoundation.NetStandard.Opc.Ua.Client | 1.5.378.65 | https://github.com/OPCFoundation/UA-.NETStandard | MIT (see notes below) |
| OPCFoundation.NetStandard.Opc.Ua.Configuration | 1.5.378.65 | https://github.com/OPCFoundation/UA-.NETStandard | MIT (see notes below) |
| OPCFoundation.NetStandard.Opc.Ua.Core | 1.5.378.65 | https://github.com/OPCFoundation/UA-.NETStandard | MIT (see notes below) |
| OPCFoundation.NetStandard.Opc.Ua.Security.Certificates | 1.5.378.65 | https://github.com/OPCFoundation/UA-.NETStandard | MIT (see notes below) |
| OPCFoundation.NetStandard.Opc.Ua.Types | 1.5.378.65 | https://github.com/OPCFoundation/UA-.NETStandard | MIT (see notes below) |
| Terminal.Gui | 2.0.0 | https://github.com/gui-cs/Terminal.Gui | MIT |
| BitFaster.Caching | 2.5.4 | https://github.com/bitfaster/BitFaster.Caching | MIT |
| ColorHelper | 1.8.1 | https://github.com/iamartyom/ColorHelper | MIT |
| Humanizer.Core | 2.14.1 | https://github.com/Humanizr/Humanizer | MIT |
| JetBrains.Annotations | 2024.2.0 | https://github.com/JetBrains/JetBrains.Annotations | MIT |
| Microsoft.Bcl.AsyncInterfaces | 8.0.0 | https://github.com/dotnet/runtime | MIT |
| Microsoft.CodeAnalysis.Common | 4.10.0 | https://github.com/dotnet/roslyn | MIT |
| Microsoft.CodeAnalysis.CSharp | 4.10.0 | https://github.com/dotnet/roslyn | MIT |
| Microsoft.CodeAnalysis.CSharp.Workspaces | 4.10.0 | https://github.com/dotnet/roslyn | MIT |
| Microsoft.CodeAnalysis.VisualBasic | 4.10.0 | https://github.com/dotnet/roslyn | MIT |
| Microsoft.CodeAnalysis.VisualBasic.Workspaces | 4.10.0 | https://github.com/dotnet/roslyn | MIT |
| Microsoft.CodeAnalysis.Workspaces.Common | 4.10.0 | https://github.com/dotnet/roslyn | MIT |
| Microsoft.Extensions.DependencyInjection | 10.0.1 | https://github.com/dotnet/runtime | MIT |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.1 | https://github.com/dotnet/runtime | MIT |
| Microsoft.Extensions.Logging | 10.0.1 | https://github.com/dotnet/runtime | MIT |
| Microsoft.Extensions.Logging.Abstractions | 10.0.1 | https://github.com/dotnet/runtime | MIT |
| Microsoft.Extensions.Options | 10.0.1 | https://github.com/dotnet/runtime | MIT |
| Microsoft.Extensions.Primitives | 10.0.1 | https://github.com/dotnet/runtime | MIT |
| Newtonsoft.Json | 13.0.4 | https://www.newtonsoft.com/json | MIT |
| System.Composition.AttributedModel | 8.0.0 | https://github.com/dotnet/runtime | MIT |
| System.Composition.Convention | 8.0.0 | https://github.com/dotnet/runtime | MIT |
| System.Composition.Hosting | 8.0.0 | https://github.com/dotnet/runtime | MIT |
| System.Composition.Runtime | 8.0.0 | https://github.com/dotnet/runtime | MIT |
| System.Composition.TypedParts | 8.0.0 | https://github.com/dotnet/runtime | MIT |
| System.IO.Abstractions | 21.0.22 | https://github.com/TestableIO/System.IO.Abstractions | MIT |
| System.Text.Json | 8.0.5 | https://github.com/dotnet/runtime | MIT |
| TestableIO.System.IO.Abstractions | 21.0.22 | https://github.com/TestableIO/System.IO.Abstractions | MIT |
| TestableIO.System.IO.Abstractions.Wrappers | 21.0.22 | https://github.com/TestableIO/System.IO.Abstractions | MIT |
| Wcwidth | 2.0.0 | https://github.com/spectreconsole/wcwidth | MIT |
| Microsoft .NET Runtime (self-contained) | 10.0.x | https://github.com/dotnet/runtime | MIT |

Direct NuGet dependencies of opcilloscope are `Terminal.Gui` and
`OPCFoundation.NetStandard.Opc.Ua.Client`; the remaining packages are
transitive dependencies resolved at restore time.

---

## OPC Foundation UA .NET Standard stack

Applies to: `OPCFoundation.NetStandard.Opc.Ua.Client`,
`OPCFoundation.NetStandard.Opc.Ua.Configuration`,
`OPCFoundation.NetStandard.Opc.Ua.Core`,
`OPCFoundation.NetStandard.Opc.Ua.Security.Certificates`,
`OPCFoundation.NetStandard.Opc.Ua.Types` (all version 1.5.378.65).

Copyright (c) 2004-2025 OPC Foundation, Inc.

Upstream project: https://github.com/OPCFoundation/UA-.NETStandard

### Licensing of the bundled version (1.5.378.65)

Version 1.5.378.65 of the UA .NET Standard stack — the version bundled in
opcilloscope binary releases — is distributed by the OPC Foundation under the
OPC Foundation MIT License 1.00 (SPDX: MIT). This is declared in each NuGet
package's license metadata (`<license type="expression">MIT</license>`) and
in the `LICENSE.txt` file shipped inside each package. Release 1.5.378.65 is
the release in which the OPC Foundation changed the project's licensing to
MIT. The full license text is also published at
https://opcfoundation.org/license/mit.html.

> MIT License
>
> OPC Foundation MIT License 1.00
>
> Copyright (c) 2005-2025 OPC Foundation, Inc. Permission is hereby granted,
> free of charge, to any person obtaining a copy of this software and
> associated documentation files (the "Software"), to deal in the Software
> without restriction, including without limitation the rights to use, copy,
> modify, merge, publish, distribute, sublicense, and/or sell copies of the
> Software, and to permit persons to whom the Software is furnished to do so,
> subject to the following conditions:
>
> The above copyright notice and this permission notice shall be included in
> all copies or substantial portions of the Software.
>
> THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
> IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
> FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
> AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
> LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
> FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
> DEALINGS IN THE SOFTWARE.

### Dual GPL-2.0/RCL licensing of earlier versions

Releases of the UA .NET Standard stack prior to 1.5.378.65 were dual-licensed
by the OPC Foundation:

- For OPC Foundation Corporate Members: the Reciprocal Community License
  ("RCL", no SPDX identifier; published by the OPC Foundation), available at
  https://opcfoundation.org/license/rcl.html.
- For everyone else: the GNU General Public License, version 2.0 (SPDX:
  GPL-2.0-only), available at https://opcfoundation.org/license/gpl.html.

For distributions that include one of those earlier, GPL-2.0-licensed
versions, the standard GPLv2 notice applies:

> This program is free software; you can redistribute it and/or modify it
> under the terms of the GNU General Public License as published by the Free
> Software Foundation; version 2 of the License.
>
> This program is distributed in the hope that it will be useful, but WITHOUT
> ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or
> FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
> more details.
>
> You should have received a copy of the GNU General Public License along
> with this program; if not, write to the Free Software Foundation, Inc.,
> 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.

Full license texts: https://opcfoundation.org/license/gpl.html (GPL-2.0),
https://opcfoundation.org/license/rcl.html (RCL), and the `LICENSE.txt` file
included in each `OPCFoundation.NetStandard.Opc.Ua.*` NuGet package for the
version actually in use.

---

## Terminal.Gui

Applies to: `Terminal.Gui` 2.0.0.

Upstream project: https://github.com/gui-cs/Terminal.Gui

License: MIT.

> Copyright 2007-2011 Novell Inc
> Copyright 2017 Microsoft Corp
>
> Permission is hereby granted, free of charge, to any person obtaining a
> copy of this software and associated documentation files (the "Software"),
> to deal in the Software without restriction, including without limitation
> the rights to use, copy, modify, merge, publish, distribute, sublicense,
> and/or sell copies of the Software, and to permit persons to whom the
> Software is furnished to do so, subject to the following conditions:
>
> The above copyright notice and this permission notice shall be included in
> all copies or substantial portions of the Software.
>
> THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
> IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
> FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
> AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
> LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
> FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
> DEALINGS IN THE SOFTWARE.

---

## Other MIT-licensed components

The following bundled components are licensed under the MIT License. The
standard MIT License text is reproduced once below the copyright notices.

- BitFaster.Caching 2.5.4 — Copyright (c) 2020 Alex Peck —
  https://github.com/bitfaster/BitFaster.Caching
- ColorHelper 1.8.1 — Copyright (c) Artyom Tonoyan —
  https://github.com/iamartyom/ColorHelper
- Humanizer.Core 2.14.1 — Copyright (c) .NET Foundation and Contributors —
  https://github.com/Humanizr/Humanizer
- JetBrains.Annotations 2024.2.0 — Copyright (c) 2016-2024 JetBrains s.r.o. —
  https://github.com/JetBrains/JetBrains.Annotations
- Microsoft.Bcl.AsyncInterfaces 8.0.0, Microsoft.Extensions.* 10.0.1,
  System.Composition.* 8.0.0, System.Text.Json 8.0.5, and the Microsoft .NET
  Runtime — Copyright (c) .NET Foundation and Contributors / Microsoft
  Corporation — https://github.com/dotnet/runtime
- Microsoft.CodeAnalysis.* (Roslyn) 4.10.0 — Copyright (c) .NET Foundation
  and Contributors / Microsoft Corporation —
  https://github.com/dotnet/roslyn
- Newtonsoft.Json 13.0.4 — Copyright (c) 2007 James Newton-King —
  https://www.newtonsoft.com/json
- System.IO.Abstractions / TestableIO.System.IO.Abstractions /
  TestableIO.System.IO.Abstractions.Wrappers 21.0.22 — Copyright (c) Tatham
  Oddie & friends 2010-2024 —
  https://github.com/TestableIO/System.IO.Abstractions
- Wcwidth 2.0.0 — Copyright (c) Patrik Svensson and contributors —
  https://github.com/spectreconsole/wcwidth

> MIT License
>
> Permission is hereby granted, free of charge, to any person obtaining a
> copy of this software and associated documentation files (the "Software"),
> to deal in the Software without restriction, including without limitation
> the rights to use, copy, modify, merge, publish, distribute, sublicense,
> and/or sell copies of the Software, and to permit persons to whom the
> Software is furnished to do so, subject to the following conditions:
>
> The above copyright notice and this permission notice shall be included in
> all copies or substantial portions of the Software.
>
> THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
> IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
> FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
> AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
> LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
> FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
> DEALINGS IN THE SOFTWARE.
