// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

using System.Runtime.CompilerServices;

// Declared in source rather than as an MSBuild <InternalsVisibleTo> item, because this project sets
// GenerateAssemblyInfo=false (the shared SolutionInfo.cs supplies the assembly-level attributes) and
// the SDK only translates those items into attributes when assembly-info generation is enabled.
[assembly: InternalsVisibleTo("DotNetNuke.Tests.Web.Mvc")]
