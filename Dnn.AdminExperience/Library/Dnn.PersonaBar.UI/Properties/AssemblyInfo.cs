// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

using System;
using System.Reflection;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.

// Review the values of the assembly attributes
[assembly: AssemblyTitle("Dnn.PersonaBar.UI")]
[assembly: AssemblyDescription("Dnn.PersonaBar.UI")]

// ASP.NET Core's public surface (HttpRequest, IActionResult, Blazor's ComponentBase via
// DnnComponentBase, ...) is now part of this assembly's public API, and the DotNetNuke
// assemblies it builds on (DotNetNuke.Library, DotNetNuke.Web, Dnn.PersonaBar.Library) already
// declare CLSCompliant(false) for the same reason, so this assembly can no longer claim CLS
// compliance. It was CLSCompliant(true) on net48, where the public surface was System.Web only.
[assembly: CLSCompliant(false)]

[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("4D33BE6B-CF0F-49D3-956D-9955E42C4524")]
[assembly: AssemblyDelaySign(false)]
