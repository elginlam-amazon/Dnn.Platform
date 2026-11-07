// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Common
{
    using System;
    using System.Diagnostics;
    using System.Linq;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// The single seam through which the platform's many <see langword="static"/> call sites reach
    /// the application's <see cref="IConfiguration"/>. Replaces the legacy
    /// <c>System.Configuration.ConfigurationManager</c> / <c>WebConfigurationManager</c> statics,
    /// which do not read <c>appsettings.json</c> and therefore return <see langword="null"/> for
    /// every key on modern .NET.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a class library: the overwhelming majority of configuration reads happen from
    /// <see langword="static"/> members (<see cref="DotNetNuke.Common.Utilities.Config"/>,
    /// <see cref="DotNetNuke.Framework.Providers.ProviderConfiguration"/>, …) that cannot take
    /// constructor injection. The host application MUST therefore call
    /// <see cref="RegisterConfiguration"/> exactly once during start-up, mirroring
    /// <see cref="HttpContextSource.RegisterAccessor"/>:
    /// </para>
    /// <code>
    /// ConfigurationSource.RegisterConfiguration(app.Services.GetRequiredService&lt;IConfiguration&gt;());
    /// </code>
    /// <para>
    /// Applying the same no-silent-null rule as <see cref="HttpContextSource"/>: when nothing has
    /// been registered and no request-scoped provider can supply an <see cref="IConfiguration"/>,
    /// <see cref="Current"/> traces an error once and the value-returning helpers throw, rather
    /// than quietly reporting every setting as unset.
    /// </para>
    /// </remarks>
    public static class ConfigurationSource
    {
        /// <summary>
        /// The <c>appsettings.json</c> section that holds what used to live in the web.config
        /// <c>&lt;appSettings&gt;</c> element.
        /// </summary>
        public const string AppSettingsSectionName = "AppSettings";

        /// <summary>
        /// The <c>appsettings.json</c> section that holds what used to live in the web.config
        /// <c>&lt;dotnetnuke&gt;</c> element (the provider configuration).
        /// </summary>
        public const string DotNetNukeSectionName = "DotNetNuke";

        private static IConfiguration configuration;
        private static int warningLogged;

        /// <summary>
        /// Gets the application's configuration, or <see langword="null"/> when none has been
        /// registered and none can be resolved from the current request's services. Reading
        /// <see langword="null"/> traces an error once, because it always indicates a host
        /// mis-configuration rather than a legitimate state.
        /// </summary>
        public static IConfiguration Current
        {
            get
            {
                var registered = configuration;
                if (registered != null)
                {
                    return registered;
                }

                // Fallback resolution path: when a request is in flight its service provider can
                // hand us the very same IConfiguration singleton.
                var requestServices = HttpContextSource.Current?.RequestServices;
                var resolved = requestServices?.GetService<IConfiguration>();
                if (resolved != null)
                {
                    return resolved;
                }

                WarnNotRegistered();
                return null;
            }
        }

        /// <summary>Gets the application's configuration and throws when none is available.</summary>
        /// <exception cref="InvalidOperationException">No <see cref="IConfiguration"/> has been registered.</exception>
        public static IConfiguration CurrentOrThrow =>
            Current ?? throw new InvalidOperationException(
                $"No {nameof(IConfiguration)} has been registered with {nameof(ConfigurationSource)}. " +
                "Call ConfigurationSource.RegisterConfiguration(app.Services.GetRequiredService<IConfiguration>()) " +
                "during application start-up, otherwise every setting in the platform would read as unset.");

        /// <summary>Gets a value indicating whether an <see cref="IConfiguration"/> is available.</summary>
        public static bool IsRegistered => configuration != null;

        /// <summary>
        /// Registers the DI-resolved <see cref="IConfiguration"/> that backs every static
        /// configuration read in the platform. The host application must call this once during
        /// start-up.
        /// </summary>
        /// <param name="instance">The configuration resolved from the application's service provider.</param>
        /// <exception cref="ArgumentNullException"><paramref name="instance"/> is <see langword="null"/>.</exception>
        public static void RegisterConfiguration(IConfiguration instance)
        {
            configuration = instance ?? throw new ArgumentNullException(nameof(instance));
            warningLogged = 0;
        }

        /// <summary>
        /// Gets a single application setting — the replacement for
        /// <c>ConfigurationManager.AppSettings[key]</c>.
        /// </summary>
        /// <param name="key">The setting key, as it was written in <c>&lt;appSettings&gt;</c>.</param>
        /// <returns>The setting value, or <see langword="null"/> when the key is not present.</returns>
        /// <remarks>
        /// Looks in the <c>AppSettings</c> section first (where <c>&lt;appSettings&gt;</c> was
        /// migrated to) and falls back to the configuration root, so keys supplied as environment
        /// variables or command-line arguments are honoured too.
        /// </remarks>
        public static string GetAppSetting(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            var config = CurrentOrThrow;
            return config.GetSection(AppSettingsSectionName)[key] ?? config[key];
        }

        /// <summary>
        /// Gets a named connection string — the replacement for
        /// <c>ConfigurationManager.ConnectionStrings[name].ConnectionString</c>.
        /// </summary>
        /// <param name="name">The connection string name.</param>
        /// <returns>The connection string, or <see langword="null"/> when it is not configured.</returns>
        public static string GetConnectionString(string name)
        {
            return string.IsNullOrEmpty(name) ? null : CurrentOrThrow.GetConnectionString(name);
        }

        /// <summary>
        /// Gets the name of the first entry in the <c>ConnectionStrings</c> section — the
        /// replacement for <c>ConfigurationManager.ConnectionStrings[0].Name</c>.
        /// </summary>
        /// <returns>The default connection string name.</returns>
        /// <exception cref="InvalidOperationException">The <c>ConnectionStrings</c> section is empty.</exception>
        public static string GetDefaultConnectionStringName()
        {
            var name = CurrentOrThrow.GetSection("ConnectionStrings")
                                     .GetChildren()
                                     .Select(child => child.Key)
                                     .FirstOrDefault(key => !key.StartsWith("_", StringComparison.Ordinal));
            if (string.IsNullOrEmpty(name))
            {
                throw new InvalidOperationException(
                    "The \"ConnectionStrings\" section of the application configuration is empty, so there is no " +
                    "default connection string to use. Add a connection string (for example \"SiteSqlServer\") to " +
                    "appsettings.json, an environment variable or user secrets.");
            }

            return name;
        }

        /// <summary>Gets a configuration section by its colon-delimited path.</summary>
        /// <param name="path">The section path, e.g. <c>DotNetNuke:data</c>.</param>
        /// <returns>The section. Use <c>Exists()</c> to find out whether it was configured.</returns>
        public static IConfigurationSection GetSection(string path)
        {
            return CurrentOrThrow.GetSection(path);
        }

        /// <summary>
        /// Gets a strongly-typed options instance for a configuration section, preferring the
        /// DI-registered <see cref="IOptions{TOptions}"/> (so that <c>Configure</c> callbacks and
        /// post-configuration in the host are honoured) and falling back to binding the section
        /// directly.
        /// </summary>
        /// <typeparam name="T">The options type.</typeparam>
        /// <param name="sectionName">The configuration section name to bind when DI has no options registered.</param>
        /// <returns>The options instance, never <see langword="null"/>.</returns>
        public static T GetOptions<T>(string sectionName)
            where T : class, new()
        {
            var fromDi = HttpContextSource.Current?.RequestServices?.GetService<IOptions<T>>();
            if (fromDi?.Value != null)
            {
                return fromDi.Value;
            }

            var options = new T();
            GetSection(sectionName).Bind(options);
            return options;
        }

        private static void WarnNotRegistered()
        {
            if (System.Threading.Interlocked.Exchange(ref warningLogged, 1) == 0)
            {
                Trace.TraceError(
                    $"{nameof(ConfigurationSource)}.{nameof(Current)} was read before any {nameof(IConfiguration)} was registered. " +
                    "Call ConfigurationSource.RegisterConfiguration(app.Services.GetRequiredService<IConfiguration>()) during " +
                    "application start-up, otherwise every setting, connection string and provider configuration in the platform " +
                    "would behave as if it had never been configured.");
            }
        }
    }
}
