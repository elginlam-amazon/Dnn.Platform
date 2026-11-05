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

using System;
using System.Collections.Generic;

using Microsoft.Extensions.Configuration;

namespace log4net.Util
{
    /// <summary>
    /// Abstraction over the host application's settings used by log4net.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On .NET Framework log4net read the <c>&lt;appSettings&gt;</c> and
    /// <c>&lt;connectionStrings&gt;</c> sections of the application configuration file through
    /// <c>System.Configuration.ConfigurationManager</c>. That type does not exist on modern .NET,
    /// so every settings read in this assembly goes through this interface instead.
    /// </para>
    /// <para>
    /// Implementations must never throw: when a setting is unknown they return <c>null</c> (or an
    /// empty sequence for <see cref="EnumerateAppSettings"/>).
    /// </para>
    /// </remarks>
    public interface ILog4NetSettings
    {
        /// <summary>Looks up a single application setting.</summary>
        /// <param name="key">The name of the setting to read.</param>
        /// <returns>The value of the setting, or <c>null</c> when it is not present.</returns>
        string GetAppSetting(string key);

        /// <summary>Enumerates every application setting that is visible to log4net.</summary>
        /// <returns>
        /// The known settings as name/value pairs. An empty sequence is returned when no settings
        /// are available; this method never returns <c>null</c>.
        /// </returns>
        IEnumerable<KeyValuePair<string, string>> EnumerateAppSettings();

        /// <summary>Looks up a named connection string.</summary>
        /// <param name="name">The name of the connection string to read.</param>
        /// <returns>The connection string, or <c>null</c> when it is not present.</returns>
        string GetConnectionString(string name);
    }

    /// <summary>
    /// Holds the <see cref="ILog4NetSettings"/> instance that this assembly reads its settings from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The hosting application (for DotNetNuke: the DNN host startup code) assigns
    /// <see cref="Configuration"/> — or a completely custom <see cref="Settings"/> implementation —
    /// once during start-up. Until that happens a safe fallback implementation backed by
    /// environment variables is used, so nothing in log4net throws when the host has not wired
    /// anything up.
    /// </para>
    /// <para>
    /// Setting names are resolved both verbatim and with the historic <c>log4net.</c> prefix, which
    /// keeps configuration keys such as <c>log4net.Config</c>, <c>log4net.Config.Watch</c>,
    /// <c>log4net.NullText</c> and <c>log4net.Internal.Debug</c> working exactly as they did when
    /// they lived in <c>&lt;appSettings&gt;</c>.
    /// </para>
    /// </remarks>
    public static class Log4NetSettings
    {
        /// <summary>
        /// Prefix applied to application setting names so that log4net settings stay namespaced
        /// away from unrelated configuration or environment values.
        /// </summary>
        public const string AppSettingPrefix = "log4net.";

        /// <summary>
        /// Name of the configuration section that holds the legacy <c>&lt;appSettings&gt;</c>
        /// values when they are provided through an <see cref="IConfiguration"/>.
        /// </summary>
        public const string AppSettingsSectionName = "appSettings";

        private static readonly ILog4NetSettings fallbackSettings = new EnvironmentLog4NetSettings();

        private static readonly object syncRoot = new object();

        private static ILog4NetSettings currentSettings;

        private static IConfiguration currentConfiguration;

        /// <summary>
        /// Gets or sets the settings implementation used by log4net.
        /// </summary>
        /// <value>
        /// The current settings implementation. Never <c>null</c>: assigning <c>null</c> restores
        /// the environment-variable backed fallback.
        /// </value>
        public static ILog4NetSettings Settings
        {
            get
            {
                lock (syncRoot)
                {
                    return currentSettings ?? fallbackSettings;
                }
            }

            set
            {
                lock (syncRoot)
                {
                    currentSettings = value;
                    if (value == null)
                    {
                        currentConfiguration = null;
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="IConfiguration"/> that log4net reads its settings from.
        /// </summary>
        /// <value>
        /// The configuration supplied by the host, or <c>null</c> when the host has not supplied
        /// one (in which case the environment-variable fallback is in effect).
        /// </value>
        /// <remarks>
        /// <para>
        /// Assigning a configuration replaces <see cref="Settings"/> with an
        /// <see cref="IConfiguration"/>-backed implementation. Assigning <c>null</c> restores the
        /// fallback implementation.
        /// </para>
        /// </remarks>
        public static IConfiguration Configuration
        {
            get
            {
                lock (syncRoot)
                {
                    return currentConfiguration;
                }
            }

            set
            {
                lock (syncRoot)
                {
                    currentConfiguration = value;
                    currentSettings = value == null ? null : new ConfigurationLog4NetSettings(value);
                }
            }
        }

        /// <summary>Looks up a single application setting.</summary>
        /// <param name="key">The name of the setting to read.</param>
        /// <returns>The value of the setting, or <c>null</c> when it is not present.</returns>
        /// <remarks>
        /// <para>
        /// This method never throws; failures are reported through <see cref="LogLog"/> and
        /// <c>null</c> is returned.
        /// </para>
        /// </remarks>
        public static string GetAppSetting(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            try
            {
                return Settings.GetAppSetting(key);
            }
            catch (Exception ex)
            {
                LogLog.Error(declaringType, "Exception while reading application setting [" + key + "].", ex);
                return null;
            }
        }

        /// <summary>Enumerates every application setting that is visible to log4net.</summary>
        /// <returns>The known settings as name/value pairs; never <c>null</c>.</returns>
        /// <remarks>
        /// <para>
        /// This method never throws; failures are reported through <see cref="LogLog"/> and an
        /// empty sequence is returned.
        /// </para>
        /// </remarks>
        public static IEnumerable<KeyValuePair<string, string>> EnumerateAppSettings()
        {
            try
            {
                return Settings.EnumerateAppSettings() ?? EmptySettings;
            }
            catch (Exception ex)
            {
                LogLog.Error(declaringType, "Exception while enumerating application settings.", ex);
                return EmptySettings;
            }
        }

        /// <summary>Looks up a named connection string.</summary>
        /// <param name="name">The name of the connection string to read.</param>
        /// <returns>The connection string, or <c>null</c> when it is not present.</returns>
        /// <remarks>
        /// <para>
        /// This method never throws; failures are reported through <see cref="LogLog"/> and
        /// <c>null</c> is returned.
        /// </para>
        /// </remarks>
        public static string GetConnectionString(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            try
            {
                return Settings.GetConnectionString(name);
            }
            catch (Exception ex)
            {
                LogLog.Error(declaringType, "Exception while reading connection string [" + name + "].", ex);
                return null;
            }
        }

        private static IEnumerable<KeyValuePair<string, string>> EmptySettings
        {
            get { return new KeyValuePair<string, string>[0]; }
        }

        /// <summary>The fully qualified type used by the internal logger.</summary>
        private static readonly Type declaringType = typeof(Log4NetSettings);

        /// <summary>
        /// The <see cref="ILog4NetSettings"/> implementation used when the host has supplied an
        /// <see cref="IConfiguration"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Settings are looked up in the configuration root, in the <c>appSettings</c> section and
        /// with the <c>log4net.</c> prefix applied, in that order; this covers both
        /// <c>appsettings.json</c> layouts and configurations that were migrated verbatim from a
        /// legacy <c>&lt;appSettings&gt;</c> section. Anything that is not found in the
        /// configuration falls back to the environment, matching the behaviour of hosts that only
        /// set log4net settings as process environment variables.
        /// </para>
        /// </remarks>
        private sealed class ConfigurationLog4NetSettings : ILog4NetSettings
        {
            private readonly IConfiguration configuration;

            private readonly ILog4NetSettings fallback = new EnvironmentLog4NetSettings();

            internal ConfigurationLog4NetSettings(IConfiguration configuration)
            {
                this.configuration = configuration;
            }

            public string GetAppSetting(string key)
            {
                if (string.IsNullOrEmpty(key))
                {
                    return null;
                }

                string value = this.configuration[key];
                if (value != null)
                {
                    return value;
                }

                value = this.configuration.GetSection(AppSettingsSectionName)[key];
                if (value != null)
                {
                    return value;
                }

                if (!key.StartsWith(AppSettingPrefix, StringComparison.Ordinal))
                {
                    value = this.configuration[AppSettingPrefix + key];
                    if (value != null)
                    {
                        return value;
                    }

                    value = this.configuration.GetSection(AppSettingsSectionName)[AppSettingPrefix + key];
                    if (value != null)
                    {
                        return value;
                    }
                }
                else
                {
                    string unprefixed = key.Substring(AppSettingPrefix.Length);
                    if (unprefixed.Length > 0)
                    {
                        value = this.configuration.GetSection(AppSettingsSectionName)[unprefixed];
                        if (value != null)
                        {
                            return value;
                        }
                    }
                }

                return this.fallback.GetAppSetting(key);
            }

            public IEnumerable<KeyValuePair<string, string>> EnumerateAppSettings()
            {
                Dictionary<string, string> settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                // The migrated <appSettings> section first, so its names keep their original shape.
                foreach (IConfigurationSection section in this.configuration.GetSection(AppSettingsSectionName).GetChildren())
                {
                    if (section.Value != null && !settings.ContainsKey(section.Key))
                    {
                        settings[section.Key] = section.Value;
                    }
                }

                // Then any log4net.* key defined at the configuration root.
                foreach (IConfigurationSection section in this.configuration.GetChildren())
                {
                    if (section.Value == null)
                    {
                        continue;
                    }

                    if (!section.Key.StartsWith(AppSettingPrefix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string name = section.Key.Substring(AppSettingPrefix.Length);
                    if (name.Length > 0 && !settings.ContainsKey(name))
                    {
                        settings[name] = section.Value;
                    }
                }

                foreach (KeyValuePair<string, string> entry in this.fallback.EnumerateAppSettings())
                {
                    if (!settings.ContainsKey(entry.Key))
                    {
                        settings[entry.Key] = entry.Value;
                    }
                }

                return settings;
            }

            public string GetConnectionString(string name)
            {
                if (string.IsNullOrEmpty(name))
                {
                    return null;
                }

                string value = this.configuration.GetConnectionString(name);
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }

                return this.fallback.GetConnectionString(name);
            }
        }

        /// <summary>
        /// The safe no-configuration fallback: application settings and connection strings are
        /// resolved from process environment variables.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Setting names are probed verbatim first and then with the <c>log4net.</c> prefix
        /// applied. Connection strings use the <c>ConnectionStrings:&lt;name&gt;</c> naming
        /// convention that <c>Microsoft.Extensions.Configuration</c> uses for environment
        /// variables, and also accept a plain variable named after the connection string.
        /// </para>
        /// </remarks>
        private sealed class EnvironmentLog4NetSettings : ILog4NetSettings
        {
            private const string ConnectionStringEnvironmentPrefix = "ConnectionStrings:";

            public string GetAppSetting(string key)
            {
                if (string.IsNullOrEmpty(key))
                {
                    return null;
                }

                string value = GetEnvironmentVariable(key);
                if (value != null)
                {
                    return value;
                }

                if (!key.StartsWith(AppSettingPrefix, StringComparison.Ordinal))
                {
                    value = GetEnvironmentVariable(AppSettingPrefix + key);
                    if (value != null)
                    {
                        return value;
                    }
                }

                return null;
            }

            public IEnumerable<KeyValuePair<string, string>> EnumerateAppSettings()
            {
                Dictionary<string, string> settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                try
                {
                    // Only the log4net namespaced variables are listed, so the output stays
                    // equivalent to the old <appSettings> listing instead of spilling the whole
                    // process environment.
                    foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
                    {
                        string key = entry.Key as string;
                        if (key == null || !key.StartsWith(AppSettingPrefix, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        string name = key.Substring(AppSettingPrefix.Length);
                        if (name.Length > 0 && !settings.ContainsKey(name))
                        {
                            settings[name] = entry.Value as string;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogLog.Error(declaringType, "Exception while reading application settings from the environment.", ex);
                }

                return settings;
            }

            public string GetConnectionString(string name)
            {
                if (string.IsNullOrEmpty(name))
                {
                    return null;
                }

                string value = GetEnvironmentVariable(ConnectionStringEnvironmentPrefix + name);
                if (value != null)
                {
                    return value;
                }

                return GetEnvironmentVariable(name);
            }

            private static string GetEnvironmentVariable(string name)
            {
                try
                {
                    return Environment.GetEnvironmentVariable(name);
                }
                catch (Exception ex)
                {
                    // Reading the environment can fail if the process is not permitted to do so.
                    LogLog.Error(declaringType, "Exception while reading the environment variable [" + name + "].", ex);
                    return null;
                }
            }
        }
    }
}
