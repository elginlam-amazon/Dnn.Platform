// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

using System.Reflection;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.

// Review the values of the assembly attributes
[assembly: AssemblyTitle("Dnn.EditBar.UI")]
[assembly: AssemblyDescription("Dnn.EditBar.UI")]

// The original [assembly: CLSCompliant(true)] is deliberately NOT carried over. The migrated DNN
// Platform assemblies this project builds against (DnnApiController, IMapRoute, IActionResult-returning
// actions, DnnComponentBase, PortalSettings, PersonalizationController, ...) are no longer marked
// CLS-compliant, so asserting compliance here produces CS3001/CS3002/CS3003/CS3009 on every public
// member that touches them - the claim is no longer satisfiable rather than merely unchecked. The
// already-migrated sibling modules (for example DNN Platform/Modules/Journal) likewise do not assert
// it. No public surface changed; only the unenforceable assertion was dropped.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("4D33BE6B-CF0F-49D3-956D-9955E42C4524")]
[assembly: AssemblyDelaySign(false)]
