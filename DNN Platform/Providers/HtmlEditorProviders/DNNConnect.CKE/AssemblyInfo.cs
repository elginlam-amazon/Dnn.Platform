// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
using System.Reflection;

// These are the two attributes the net48 Properties/AssemblyInfo.cs declared. They are kept in
// source rather than as MSBuild properties because this project sets GenerateAssemblyInfo=false
// (the linked SolutionInfo.cs owns the company/product/copyright/trademark/version attributes),
// and with that disabled the SDK emits no attributes from properties such as <AssemblyTitle>.
[assembly: AssemblyDescription("CKEditor HTML Editor Provider for DNN")]
[assembly: AssemblyTitle("CKEditor HTML Editor Provider for DNN")]
