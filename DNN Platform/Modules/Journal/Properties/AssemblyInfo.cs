// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

using System.Reflection;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
//
// These attributes are NOT emitted by the SDK and are NOT declared by the linked
// SolutionInfo.cs (which carries Company, Product, Copyright, Trademark and the versions), so
// they are kept here rather than lost to GenerateAssemblyInfo=false. ComVisible in particular
// must stay explicit: an assembly with no [ComVisible] attribute defaults to COM-visible, so
// dropping it would invert the original setting.
//
// The original [assembly: CLSCompliant(true)] is deliberately NOT carried over. The migrated DNN
// Platform assemblies this module builds against (PortalModuleBase, DnnApiController,
// DnnComponentBase, PortalSettings, UserInfo, ModuleSearchBase, ...) are no longer marked
// CLS-compliant, so asserting compliance here produces CS3001/CS3002/CS3003/CS3009 on every
// public member that touches them — the claim is no longer satisfiable rather than merely
// unchecked. The already-migrated sibling modules (for example DDRMenu) likewise do not assert
// it. No public surface changed; only the unenforceable assertion was dropped.
[assembly: AssemblyTitle("DotNetNuke CorporationJournal")]
[assembly: AssemblyDescription("A DotNetNuke Module from DotNetNuke Corporation Software")]
[assembly: ComVisible(false)]
