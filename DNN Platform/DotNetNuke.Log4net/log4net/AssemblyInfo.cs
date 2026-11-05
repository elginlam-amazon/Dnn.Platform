// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
// 
// Licensed to the Apache Software Foundation (ASF) under one or more
// contributor license agreements. See the NOTICE file distributed with
// this work for additional information regarding copyright ownership.
// The ASF licenses this file to you under the Apache License, Version 2.0
// (the "License"); you may not use this file except in compliance with
// the License. You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// 

using System.Reflection;
using System.Runtime.CompilerServices;

//
// log4net makes use of static methods which cannot be made com visible
//
[assembly: System.Runtime.InteropServices.ComVisible(false)]

//
// log4net is CLS compliant
//
[assembly: System.CLSCompliant(true)]

//
// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
//
[assembly: AssemblyInformationalVersionAttribute("2.0.14.0-.NET")]
[assembly: AssemblyTitle("DotNetNuke.Log4Net")]

#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Retail")]
#endif

[assembly: AssemblyProduct("log4net")]
[assembly: AssemblyDefaultAlias("log4net")]
[assembly: AssemblyCulture("")]
[assembly: AssemblyDescription("DotNetNuke branded version of log4net")]

// Assembly signing is configured through the project file (SignAssembly/AssemblyOriginatorKeyFile);
// the legacy AssemblyKeyFile/AssemblyDelaySign attributes are not used on modern .NET.

[assembly: InternalsVisibleTo("log4net.Tests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100297dcac908e286"
    + "89360399027b0ea4cd852fbb74e1ed95e695a5ba55cbd1d075ec20cdb5fa6fc593d3d571527b20"
    + "558d6f39e1f4d5cfe0798428c589c311965244b209c38a02aaa8c9da3b72405b6fedeeb4292c34"
    + "57e9769b74e645c19cb06c2be75fb2d12281a585fbeabf7bd195d6961ba113286fc3e286d7bbd6"
    + "9024ceda")]
