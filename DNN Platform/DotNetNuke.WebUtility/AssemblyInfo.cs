// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// The assembly level attributes of this project are declared here because the project sets
// GenerateAssemblyInfo to false. The ten System.Web.UI.WebResource attributes the original
// AssemblyInfo.vb carried for the embedded js/*.js files are replaced by the manifest embedded file
// provider and the MapDnnWebUtilityScripts endpoints (see ScriptEndpointRouteBuilderExtensions).
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("DotNetNuke")]
[assembly: AssemblyDescription("Open Source Web Application Framework")]
[assembly: AssemblyCompany("DotNetNuke Corporation")]
[assembly: AssemblyProduct("DotNetNuke.WebUtility")]
[assembly: AssemblyCopyright("DotNetNuke is copyright 2002-2018 by DotNetNuke Corporation. All Rights Reserved.")]
[assembly: AssemblyTrademark("DotNetNuke")]

// The original assembly declared CLSCompliant(true); it cannot be kept because the ASP.NET Core
// types this library now exposes (HttpContext, IApplicationBuilder, IEndpointRouteBuilder) are not
// marked as CLS compliant themselves.

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("6673DCA3-048B-455F-883F-C75AF6D4B15F")]
[assembly: AssemblyVersion("04.02.01.783")]
