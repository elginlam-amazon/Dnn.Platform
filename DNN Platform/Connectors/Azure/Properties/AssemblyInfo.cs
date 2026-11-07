// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

using System;
using System.Reflection;

[assembly: AssemblyTitle("DotNetNuke")]

// The public surface of this assembly exposes types from DotNetNuke.Library (which declares
// [assembly: CLSCompliant(false)]), DotNetNuke.Web and Microsoft.AspNetCore.Mvc (unmarked, and
// therefore not CLS-compliant), so this assembly can no longer claim CLS compliance. This matches
// the DotNetNuke.Providers.FolderProviders assembly, which exposes the same DNN types.
[assembly: CLSCompliant(false)]
