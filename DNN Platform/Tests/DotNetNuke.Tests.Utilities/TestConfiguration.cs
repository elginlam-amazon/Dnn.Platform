// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Instance.Utilities
{
    using System;
    using System.IO;

    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// Single, shared resolution point for the test-host settings that used to live in the
    /// net48 <c>app.config</c>/<c>web.config</c> <c>&lt;appSettings&gt;</c> section and were read
    /// through <c>System.Configuration.ConfigurationManager.AppSettings</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On modern .NET there is no ambient <c>ConfigurationManager</c> backed by an XML config
    /// file, so the values are resolved from an <see cref="IConfiguration"/> built (lazily, once
    /// per process) from — in increasing order of precedence — <c>appsettings.json</c>,
    /// <c>appsettings.&lt;Environment&gt;.json</c> and environment variables, both at the test
    /// host's base directory and at the current working directory.
    /// </para>
    /// <para>
    /// Both the legacy flat key (<c>DefaultPhysicalAppPath</c>) and the conventional nested key
    /// (<c>AppSettings:DefaultPhysicalAppPath</c>) are honoured, so an existing
    /// <c>appSettings</c> block can be transliterated either way.
    /// </para>
    /// </remarks>
    public static class TestConfiguration
    {
        private const string DefaultPhysicalAppPathKey = "DefaultPhysicalAppPath";

        private static readonly object SyncRoot = new object();

        private static IConfiguration configuration;

        /// <summary>
        /// Gets the <see cref="IConfiguration"/> used to resolve test-host settings. Built on
        /// first access and cached for the lifetime of the process.
        /// </summary>
        public static IConfiguration Configuration
        {
            get
            {
                if (configuration != null)
                {
                    return configuration;
                }

                lock (SyncRoot)
                {
                    return configuration ??= Build();
                }
            }
        }

        /// <summary>
        /// Gets the configured physical path of the website under test — the replacement for
        /// <c>ConfigurationManager.AppSettings["DefaultPhysicalAppPath"]</c>.
        /// </summary>
        /// <value>
        /// The configured path, or <see langword="null"/> when the setting is absent. Callers
        /// keep their own fallback behaviour, exactly as they did when
        /// <c>AppSettings</c> returned <see langword="null"/> for a missing key.
        /// </value>
        public static string DefaultPhysicalAppPath => GetSetting(DefaultPhysicalAppPathKey);

        /// <summary>
        /// Replaces the registered configuration, for tests that need to control a setting
        /// without touching the file system or the process environment.
        /// </summary>
        /// <param name="instance">
        /// The configuration to use, or <see langword="null"/> to revert to the
        /// file/environment-backed configuration built on next access.
        /// </param>
        public static void RegisterInstance(IConfiguration instance)
        {
            lock (SyncRoot)
            {
                configuration = instance;
            }
        }

        /// <summary>Reads a single test-host setting by its legacy <c>appSettings</c> key.</summary>
        /// <param name="key">The legacy <c>appSettings</c> key, for example <c>DefaultPhysicalAppPath</c>.</param>
        /// <returns>
        /// The configured value, or <see langword="null"/> when neither the flat key nor the
        /// <c>AppSettings:</c>-prefixed key is present or when the value is empty.
        /// </returns>
        public static string GetSetting(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            var value = Configuration[key];
            if (string.IsNullOrEmpty(value))
            {
                value = Configuration["AppSettings:" + key];
            }

            return string.IsNullOrEmpty(value) ? null : value;
        }

        private static IConfiguration Build()
        {
            var environmentName =
                Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            var builder = new ConfigurationBuilder();

            // The base directory is where the test host copies appsettings.json next to the
            // assembly; the working directory is what the legacy fixtures assumed.
            foreach (var root in GetConfigurationRoots())
            {
                builder.AddJsonFile(Path.Combine(root, "appsettings.json"), optional: true, reloadOnChange: true);
                if (!string.IsNullOrEmpty(environmentName))
                {
                    builder.AddJsonFile(
                        Path.Combine(root, $"appsettings.{environmentName}.json"),
                        optional: true,
                        reloadOnChange: true);
                }
            }

            builder.AddEnvironmentVariables();

            return builder.Build();
        }

        private static string[] GetConfigurationRoots()
        {
            var baseDirectory = AppContext.BaseDirectory;
            var currentDirectory = Directory.GetCurrentDirectory();

            return string.Equals(
                Path.GetFullPath(baseDirectory).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(currentDirectory).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase)
                ? new[] { baseDirectory }
                : new[] { baseDirectory, currentDirectory };
        }
    }
}
