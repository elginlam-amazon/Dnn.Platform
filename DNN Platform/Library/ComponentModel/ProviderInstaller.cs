// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.ComponentModel
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Loader;

    using DotNetNuke.Framework.Providers;
    using DotNetNuke.Instrumentation;

    using Microsoft.Extensions.Logging;

    public class ProviderInstaller : IComponentInstaller
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<ProviderInstaller>();
        private readonly ComponentLifeStyleType componentLifeStyle;
        private readonly Type providerInterface;
        private readonly string providerType;
        private Type defaultProvider;

        /// <summary>Initializes a new instance of the <see cref="ProviderInstaller"/> class.</summary>
        /// <param name="providerType">The type name of the provider implementation.</param>
        /// <param name="providerInterface">The <see cref="Type"/> of the provider interface.</param>
        public ProviderInstaller(string providerType, Type providerInterface)
        {
            this.componentLifeStyle = ComponentLifeStyleType.Singleton;
            this.providerType = providerType;
            this.providerInterface = providerInterface;
        }

        /// <summary>Initializes a new instance of the <see cref="ProviderInstaller"/> class.</summary>
        /// <param name="providerType">The type name of the provider implementation.</param>
        /// <param name="providerInterface">The <see cref="Type"/> of the provider interface.</param>
        /// <param name="defaultProvider">The default <see cref="Type"/> for the implementation.</param>
        public ProviderInstaller(string providerType, Type providerInterface, Type defaultProvider)
        {
            this.componentLifeStyle = ComponentLifeStyleType.Singleton;
            this.providerType = providerType;
            this.providerInterface = providerInterface;
            this.defaultProvider = defaultProvider;
        }

        /// <summary>Initializes a new instance of the <see cref="ProviderInstaller"/> class.</summary>
        /// <param name="providerType">The type name of the provider implementation.</param>
        /// <param name="providerInterface">The <see cref="Type"/> of the provider interface.</param>
        /// <param name="lifeStyle">The <see cref="ComponentLifeStyleType"/> of the instance.</param>
        public ProviderInstaller(string providerType, Type providerInterface, ComponentLifeStyleType lifeStyle)
        {
            this.componentLifeStyle = lifeStyle;
            this.providerType = providerType;
            this.providerInterface = providerInterface;
        }

        /// <inheritdoc />
        public void InstallComponents(IContainer container)
        {
            ProviderConfiguration config = ProviderConfiguration.GetProviderConfiguration(this.providerType);

            // Register the default provider first (so it is the first component registered for its service interface
            if (config != null)
            {
                this.InstallProvider(container, (Provider)config.Providers[config.DefaultProvider]);

                // Register the others
                foreach (Provider provider in config.Providers.Values)
                {
                    // Skip the default because it was registered above
                    if (!config.DefaultProvider.Equals(provider.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        this.InstallProvider(container, provider);
                    }
                }
            }
        }

        private void InstallProvider(IContainer container, Provider provider)
        {
            if (provider != null)
            {
                Type type = null;

                // Get the provider type
                try
                {
                    type = GetTypeFromLoadedAssemblies(provider.Type);
                }
                catch (TypeLoadException)
                {
                    if (this.defaultProvider != null)
                    {
                        type = this.defaultProvider;
                    }
                }

                if (type == null)
                {
                    Logger.ProviderInstallerCouldNotLoadProvider(new InvalidOperationException($"Could not load provider {provider.Type}"));
                }
                else
                {
                    // Register the component
                    container.RegisterComponent(provider.Name, this.providerInterface, type, this.componentLifeStyle);

                    // Load the settings into a dictionary
                    var settingsDict = new Dictionary<string, string> { { "providerName", provider.Name } };
                    foreach (string key in provider.Attributes.Keys)
                    {
                        settingsDict.Add(key, provider.Attributes.Get(key));
                    }

                    // Register the settings as dependencies
                    container.RegisterComponentSettings(type.FullName, settingsDict);
                }
            }
        }

        /// <summary>Resolves a type by (optionally assembly qualified) name across the loaded assemblies.</summary>
        /// <param name="typeName">The name of the type.</param>
        /// <returns>The resolved <see cref="Type"/>, or <see langword="null"/> when it is in no loaded assembly.</returns>
        /// <remarks>
        /// Replaces <c>System.Web.Compilation.BuildManager.GetType(typeName, throwOnError: false,
        /// ignoreCase: true)</c>, which does not exist on modern .NET. The equivalent is
        /// <see cref="Type.GetType(string,bool,bool)"/> followed by a scan over
        /// <see cref="AssemblyLoadContext.Default"/>, which is where DNN's provider assemblies load.
        /// Like the original overload, failures return <see langword="null"/> rather than throwing.
        /// </remarks>
        private static Type GetTypeFromLoadedAssemblies(string typeName)
        {
            var type = Type.GetType(typeName, false, true);
            if (type is not null)
            {
                return type;
            }

            foreach (var assembly in AssemblyLoadContext.Default.Assemblies)
            {
                if (assembly.IsDynamic)
                {
                    continue;
                }

                try
                {
                    type = assembly.GetType(typeName, false, true);
                }
                catch (Exception ex)
                {
                    // A single unloadable assembly must not stop the scan.
                    Logger.LogDebug(ex, "Could not search assembly {Assembly} for provider type {TypeName}", assembly.FullName, typeName);
                    continue;
                }

                if (type is not null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
