// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
using System.Reflection;
using System.Runtime.InteropServices;

// Only the attributes that the linked SolutionInfo.cs does NOT provide are kept here.
// SolutionInfo.cs carries Company/Product/Copyright/Trademark/Version/FileVersion/
// InformationalVersion; the title, description and typelib GUID below are specific to this
// assembly, and with GenerateAssemblyInfo=false nothing would regenerate them. The GUID in
// particular has no MSBuild property equivalent, so it must stay as an attribute.
[assembly: AssemblyTitle("DotNetNuke.Modules.RazorHostView")]
[assembly: AssemblyDescription("Razor Host Module")]
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("dba19364-20c8-49ec-9c3a-69008fe699f6")]
