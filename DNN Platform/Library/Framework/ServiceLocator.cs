// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Framework
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    using DotNetNuke.Common;

    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Provides a readily testable way to manage a Singleton.</summary>
    /// <typeparam name="TContract">The interface that the controller provides.</typeparam>
    /// <typeparam name="TSelf">The type of the controller itself, used to call the GetFactory override.</typeparam>
    public abstract class ServiceLocator<TContract, TSelf>
        where TSelf : ServiceLocator<TContract, TSelf>, new()
    {
        private static Lazy<TContract> instance = new Lazy<TContract>(InitInstance, true);
        private static TContract testableInstance;
        private static bool useTestable;

        /// <summary>Gets a singleton of T.</summary>
        [SuppressMessage("Microsoft.Design", "CA1000:DoNotDeclareStaticMembersOnGenericTypes", Justification = "Breaking change")]
        public static TContract Instance
        {
            get
            {
                if (useTestable)
                {
                    return testableInstance;
                }

                // Prefer the host's DI container (the SAME container the application runs on).
                // Scoped registrations resolve from the current request scope; outside a request
                // they resolve from the root provider.
                var provider = ServiceLocator.ServiceProvider;
                if (provider is not null)
                {
                    var resolved = provider.GetService<TContract>();
                    if (resolved is not null)
                    {
                        return resolved;
                    }
                }

                // Not registered in DI (or DI not initialized yet): fall back to the
                // type's own factory, preserving the historical behavior.
                return instance.Value;
            }
        }

        /// <summary>Gets or sets the service locator factory.</summary>
        protected static Func<TContract> Factory { get; set; }

        /// <summary>Registers an instance to use for the Singleton.</summary>
        /// <remarks>Intended for unit testing purposes, not thread safe.</remarks>
        /// <param name="instance">The instance to set.</param>
        [SuppressMessage("Microsoft.Design", "CA1000:DoNotDeclareStaticMembersOnGenericTypes", Justification = "Breaking change")]
        public static void SetTestableInstance(TContract instance)
        {
            testableInstance = instance;
            useTestable = true;
        }

        /// <summary>Clears the current instance, a new instance will be initialized when next requested.</summary>
        /// <remarks>Intended for unit testing purposes, not thread safe.</remarks>
        [SuppressMessage("Microsoft.Design", "CA1000:DoNotDeclareStaticMembersOnGenericTypes", Justification = "Breaking change")]
        public static void ClearInstance()
        {
            useTestable = false;
            testableInstance = default(TContract);
            instance = new Lazy<TContract>(InitInstance, true);
        }

        /// <summary>Gets the service locator factory.</summary>
        /// <returns>A factory function.</returns>
        protected abstract Func<TContract> GetFactory();

        private static TContract InitInstance()
        {
            if (Factory == null)
            {
                var controllerInstance = new TSelf();
                Factory = controllerInstance.GetFactory();
            }

            return Factory();
        }
    }

    /// <summary>
    /// Bridges the <see cref="ServiceLocator{TContract,TSelf}"/> static accessors to the
    /// application's dependency-injection container.
    /// </summary>
    /// <remarks>
    /// There is exactly ONE container: the host calls
    /// <see cref="SetServiceProvider(IServiceProvider)"/> once with <c>app.Services</c>.
    /// Scoped services are resolved from the current request's <c>RequestServices</c>, falling
    /// back to the root provider when there is no request (scheduler, startup, console).
    /// </remarks>
    public static class ServiceLocator
    {
        private static IServiceProvider rootServiceProvider;

        /// <summary>Gets the service provider to resolve platform services from, or <see langword="null"/> when the host has not initialized DI yet.</summary>
        public static IServiceProvider ServiceProvider
        {
            get
            {
                var requestServices = HttpContextSource.Current?.RequestServices;
                return requestServices ?? rootServiceProvider;
            }
        }

        /// <summary>Gets a value indicating whether the host has wired the container.</summary>
        public static bool IsInitialized => rootServiceProvider is not null;

        /// <summary>Wires the static service locator to the application's root service provider.</summary>
        /// <param name="serviceProvider">The application's root <see cref="IServiceProvider"/> (<c>app.Services</c>).</param>
        public static void SetServiceProvider(IServiceProvider serviceProvider)
        {
            rootServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>Resolves a required service from the current request scope, or the root provider.</summary>
        /// <typeparam name="TService">The service to resolve.</typeparam>
        /// <returns>The resolved service.</returns>
        public static TService GetRequiredService<TService>()
            where TService : class
        {
            var provider = ServiceProvider
                ?? throw new InvalidOperationException(
                    "The DotNetNuke service locator has not been initialized. Call DotNetNuke.Framework.ServiceLocator.SetServiceProvider(app.Services) during application startup.");
            return provider.GetRequiredService<TService>();
        }

        /// <summary>Resolves an optional service from the current request scope, or the root provider.</summary>
        /// <typeparam name="TService">The service to resolve.</typeparam>
        /// <returns>The resolved service, or <see langword="null"/> when it is not registered.</returns>
        public static TService GetService<TService>()
            where TService : class
        {
            return ServiceProvider?.GetService<TService>();
        }
    }
}
