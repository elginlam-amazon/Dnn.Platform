#region Apache License
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
#endregion

namespace log4net {

    /// <summary>
    /// Provides information about the environment the assembly has
    /// been built for.
    /// </summary>
    public sealed class AssemblyInfo {
        /// <summary>Version of the assembly</summary>
        public const string Version = "2.0.9";

        /// <summary>Version of the framework targeted</summary>
        public const decimal TargetFrameworkVersion = 10.0M;

        /// <summary>Type of framework targeted</summary>
        public const string TargetFramework = ".NET";

        /// <summary>Does it target a client profile?</summary>
        /// <remarks>
        /// The .NET Framework Client Profile does not exist on modern .NET, so this is always false.
        /// </remarks>
        public const bool ClientProfile = false;

        /// <summary>
        /// Identifies the version and target for this assembly.
        /// </summary>
        public static string Info {
            get {
                return string.Format("Apache log4net version {0} compiled for {1}{2} {3}",
                                     Version, TargetFramework,
                                     string.Empty,
                                     TargetFrameworkVersion);
            }
        }
    }

}
