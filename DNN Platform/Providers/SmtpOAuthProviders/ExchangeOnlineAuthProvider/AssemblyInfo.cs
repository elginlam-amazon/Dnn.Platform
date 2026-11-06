// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// These are the assembly level attributes the net48 Properties/AssemblyInfo.cs declared. They are
// kept in source rather than as MSBuild properties because this project sets
// GenerateAssemblyInfo=false (the linked SolutionInfo.cs owns the
// company/product/copyright/trademark/version attributes), and with that disabled the SDK emits
// no attributes from properties such as <AssemblyTitle>.
using System;
using System.Reflection;

[assembly: AssemblyTitle("DotNetNuke")]

// The original assembly declared CLSCompliant(true); it cannot be kept because this provider's
// public surface exposes the DotNetNuke.Library and DotNetNuke.Web abstractions
// (ISmtpOAuthProvider, IPortalController, IOAuth2SmtpClient, IMapRoute, DnnApiController), and
// those assemblies had to drop their own CLSCompliant(true) during their migration - DotNetNuke
// .Library's Properties/AssemblyInfo.cs now declares CLSCompliant(false) - for the ASP.NET Core
// types they expose. Same decision, and the same reason, as the already-migrated sibling
// Dnn.GoogleMailAuthProvider. No member was renamed or removed to reach this: only the
// declaration of the claim changed.
[assembly: CLSCompliant(false)]
