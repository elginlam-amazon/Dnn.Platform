// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Framework.Providers
{
    using System.Collections;

    using DotNetNuke.Common;

    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// The configuration of one of the platform's provider groups (data, caching, scheduling, …),
    /// i.e. one child of the web.config <c>&lt;dotnetnuke&gt;</c> element, now the
    /// <c>DotNetNuke</c> section of <c>appsettings.json</c>.
    /// </summary>
    public class ProviderConfiguration
    {
        private readonly Hashtable providers = new Hashtable();
        private string defaultProvider;

        /// <summary>Gets the name of the provider to use when no specific one is requested.</summary>
        public string DefaultProvider
        {
            get
            {
                return this.defaultProvider;
            }
        }

        /// <summary>Gets the configured providers, keyed by name, with <see cref="Provider"/> values.</summary>
        public Hashtable Providers
        {
            get
            {
                return this.providers;
            }
        }

        /// <summary>Gets the configuration of a provider group.</summary>
        /// <param name="strProvider">The provider group name, e.g. <c>data</c> or <c>folder</c>.</param>
        /// <returns>
        /// The <see cref="ProviderConfiguration"/>, or <see langword="null"/> when the group is not
        /// configured (matching the behavior of the legacy section handler, which returned
        /// <see langword="null"/> for a missing section).
        /// </returns>
        /// <remarks>
        /// Reads <c>DotNetNuke:&lt;strProvider&gt;</c> from <see cref="ConfigurationSource"/>,
        /// replacing the <c>IConfigurationSectionHandler</c>-based XML section handler that does
        /// not exist on modern .NET. Configuration keys are matched case-insensitively, so the
        /// legacy camel-cased group names (<c>htmlEditor</c>, <c>friendlyUrl</c>, …) keep working.
        /// </remarks>
        public static ProviderConfiguration GetProviderConfiguration(string strProvider)
        {
            if (string.IsNullOrEmpty(strProvider))
            {
                return null;
            }

            var section = ConfigurationSource.GetSection(ConfigurationSource.DotNetNukeSectionName)
                                             .GetSection(strProvider);
            if (!section.Exists())
            {
                return null;
            }

            var configuration = new ProviderConfiguration();
            configuration.LoadValuesFromConfiguration(section);
            return configuration;
        }

        /// <summary>Gets the provider named by <see cref="DefaultProvider"/>.</summary>
        /// <returns>The default <see cref="Provider"/>.</returns>
        public Provider GetDefaultProvider()
        {
            return (Provider)this.providers[this.defaultProvider];
        }

        /// <summary>Populates this instance from a configuration section.</summary>
        /// <param name="section">The <c>DotNetNuke:&lt;group&gt;</c> configuration section.</param>
        internal void LoadValuesFromConfiguration(IConfigurationSection section)
        {
            // Get the default provider.
            this.defaultProvider = section["defaultProvider"];

            // Read the providers. In JSON these are an array (or an object keyed by name) under
            // "providers"; the legacy add/remove/clear elements no longer apply because the
            // configuration system itself layers and overrides the values.
            this.GetProviders(section.GetSection("providers"));

            if (string.IsNullOrEmpty(this.defaultProvider) && this.providers.Count == 1)
            {
                foreach (DictionaryEntry entry in this.providers)
                {
                    this.defaultProvider = (string)entry.Key;
                }
            }
        }

        /// <summary>Populates <see cref="Providers"/> from the <c>providers</c> configuration section.</summary>
        /// <param name="providersSection">The <c>providers</c> configuration section.</param>
        internal void GetProviders(IConfigurationSection providersSection)
        {
            if (providersSection == null)
            {
                return;
            }

            foreach (var child in providersSection.GetChildren())
            {
                // Tolerate both shapes: an array of objects with a "name" key, and an object whose
                // keys are the provider names.
                var provider = new Provider(child);
                var name = string.IsNullOrEmpty(provider.Name) ? child.Key : provider.Name;
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(provider.Type))
                {
                    continue;
                }

                this.providers[name] = string.IsNullOrEmpty(provider.Name)
                    ? new Provider(name, provider.Type, provider.Attributes)
                    : provider;
            }
        }
    }
}
