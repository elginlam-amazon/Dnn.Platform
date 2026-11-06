// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
using System.Reflection;
using System.Runtime.InteropServices;

// These four attributes were carried by the legacy AssemblyInfo.cs, which the SDK-style
// conversion removed. The project sets GenerateAssemblyInfo=false (the shared SolutionInfo.cs
// owns the company/version attributes), so the SDK does not regenerate them and they have to
// stay in source. The COM type-library GUID in particular is stable published surface and must
// not change across the retarget.
[assembly: AssemblyTitle("DotNetNuke.Modules.Html")]
[assembly: AssemblyDescription("HTML Module for Open Source Web Application Framework")]
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("334e9cee-0d47-4d70-924b-b5098a3432cb")]
