// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("DotNetNuke.HttpModules")]
[assembly: AssemblyDescription("Open Source Web Application Framework")]

// The public surface of this assembly is now built from ASP.NET Core types
// (IApplicationBuilder, HttpContext) and from the migrated DotNetNuke (Library) API, neither of
// which is CLS-compliant — the Library itself is marked [assembly: CLSCompliant(false)] after its
// own net10.0 migration. Claiming compliance here only produced CS3001/CS3002/CS3009 on members
// that are deliberately unchanged, so the claim is dropped rather than the API altered.
[assembly: CLSCompliant(false)]
[assembly: Guid("7FF35751-D6A3-4DA0-A5E7-2F1AB026832B")]

[assembly: InternalsVisibleTo("DotNetNuke.Tests.Core")]
[assembly: InternalsVisibleTo("DotNetNuke.Tests.Urls")]
