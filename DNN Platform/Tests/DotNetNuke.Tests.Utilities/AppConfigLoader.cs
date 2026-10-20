// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Utilities
{
    using System;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Helper that makes <see cref="ConfigurationManager"/> read a test assembly's
    /// <c>*.dll.config</c> when running under <c>dotnet test</c>.
    /// </summary>
    /// <remarks>
    /// On .NET Framework the test runner applied the test assembly's configuration file
    /// (e.g. <c>DotNetNuke.Tests.Data.dll.config</c>) to the <see cref="AppDomain"/>
    /// automatically, so <see cref="ConfigurationManager.ConnectionStrings"/> and
    /// <see cref="ConfigurationManager.AppSettings"/> contained the DNN test entries.
    /// Under <c>dotnet test</c> the host process is <c>testhost</c>, whose own configuration
    /// is used instead, so those entries are missing. This helper points
    /// <see cref="ConfigurationManager"/> at the test assembly's configuration file and
    /// resets its cached state so the entries become available again.
    /// </remarks>
    public static class AppConfigLoader
    {
        /// <summary>Applies the given configuration file to <see cref="ConfigurationManager"/>.</summary>
        /// <param name="configFilePath">The full path to the configuration file to apply.</param>
        public static void Apply(string configFilePath)
        {
            if (string.IsNullOrEmpty(configFilePath) || !File.Exists(configFilePath))
            {
                return;
            }

            AppDomain.CurrentDomain.SetData("APP_CONFIG_FILE", configFilePath);
            ResetConfigMechanism();
        }

        /// <summary>
        /// Applies the configuration file that belongs to the assembly that contains
        /// <paramref name="markerType"/> (its <c>&lt;assembly&gt;.dll.config</c>).
        /// </summary>
        /// <param name="markerType">A type from the test assembly whose configuration should be loaded.</param>
        public static void ApplyForAssembly(Type markerType)
        {
            var assembly = markerType.Assembly;
            var configPath = assembly.Location + ".config";
            if (!File.Exists(configPath))
            {
                // Fall back to <assembly name>.dll.config next to the running test.
                configPath = Path.Combine(AppContext.BaseDirectory, Path.GetFileName(assembly.Location) + ".config");
            }

            Apply(configPath);
        }

        private static void ResetConfigMechanism()
        {
            var configManagerType = typeof(ConfigurationManager);

            configManagerType
                .GetField("s_initState", BindingFlags.NonPublic | BindingFlags.Static)?
                .SetValue(null, 0);

            configManagerType
                .GetField("s_configSystem", BindingFlags.NonPublic | BindingFlags.Static)?
                .SetValue(null, null);

            configManagerType.Assembly
                .GetTypes()
                .FirstOrDefault(t => t.FullName == "System.Configuration.ClientConfigPaths")?
                .GetField("s_current", BindingFlags.NonPublic | BindingFlags.Static)?
                .SetValue(null, null);
        }
    }
}
