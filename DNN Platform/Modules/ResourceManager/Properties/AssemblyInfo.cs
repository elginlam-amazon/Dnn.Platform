// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
using System.Reflection;
using System.Runtime.InteropServices;

// Only the attributes that nothing else supplies are kept here. The version, company, product,
// copyright and trademark attributes come from the linked SolutionInfo.cs, and the project sets
// GenerateAssemblyInfo=false so the SDK does not generate a competing set - which is why these
// three have to stay in a file rather than becoming MSBuild properties.
// Precedent: DNN Platform/Providers/FolderProviders/Properties/AssemblyInfo.cs, which the
// already-migrated sibling kept for the same reason.
[assembly: AssemblyTitle("Dnn.Modules.ResourceManager")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("5ef01dd5-84a1-49f3-9232-067440288455")]
