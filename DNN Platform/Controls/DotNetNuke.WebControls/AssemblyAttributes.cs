// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

using System;
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: Guid("1CDFE09D-9111-4ABE-8CF4-02D1FDC157A7")]

// The original assembly declared CLSCompliant(true); it cannot be kept because the controls now
// derive from, and expose, the ClientApiControlBase / IClientApiControl / IClientApiPage
// abstractions of DotNetNuke.WebUtility, which are not marked as CLS compliant themselves (that
// assembly had to drop its own CLSCompliant(true) for the ASP.NET Core types it exposes).
[assembly: CLSCompliant(false)]
[assembly: AssemblyTrademark("DNN")]
[assembly: AssemblyCopyright("DNN Platform is copyright 2002-2026 by .NET Foundation. All Rights Reserved.")]
[assembly: AssemblyProduct("https://dnncommunity.org")]
[assembly: AssemblyCompany(".NET Foundation")]
[assembly: AssemblyDescription("Web Controls for DNN Platform")]
[assembly: AssemblyTitle("DotNetNuke WebControls")]
[assembly: AssemblyVersion("10.02.03")]

// NOTE: The original AssemblyInfo.vb also carried <Assembly: WebResource(...)> entries for the
// 11 embedded client scripts and <Assembly: TagPrefix("DotNetNuke.UI.WebControls", "DNN")>.
// Both attributes live in System.Web.UI, which has no .NET 10 equivalent: WebResource is replaced
// by Razor Class Library static web assets (_content/DotNetNuke.WebControls/js/...) and TagPrefix
// is a WebForms-only registration concept that Blazor components do not use.
