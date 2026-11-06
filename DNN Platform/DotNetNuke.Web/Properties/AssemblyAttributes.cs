// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// Declared in source rather than as MSBuild properties/items, because this project sets
// GenerateAssemblyInfo=false (the shared SolutionInfo.cs supplies the versioning attributes)
// and the SDK only translates those into attributes when assembly-info generation is enabled.
// These are the attributes the deleted net48 Properties/AssemblyInfo.cs carried.
[assembly: AssemblyTitle("DotNetNuke.Web")]
[assembly: AssemblyDescription("Open Source Web Application Framework")]

// Was CLSCompliant(true) on net48. The ASP.NET Core shared-framework types this assembly now
// exposes (ControllerBase, HttpRequest, ...) are not marked CLS-compliant, which matches the
// already-migrated sibling DotNetNuke.Web.Client declaring CLSCompliant(false).
[assembly: CLSCompliant(false)]
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("99d0e26d-a924-41d2-a195-33496a3f48ad")]

[assembly: InternalsVisibleTo("DotNetNuke.Tests.Content")]
[assembly: InternalsVisibleTo("DotNetNuke.Tests.Messaging")]
[assembly: InternalsVisibleTo("DotNetNuke.Tests.Web")]
