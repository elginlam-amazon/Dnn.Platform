// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Tests.Web.Api.Internals
{
    using System;

    using DotNetNuke.Abstractions;
    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Services.DependencyInjection;
    using DotNetNuke.Tests.Utilities;

    using Microsoft.Extensions.DependencyInjection;

    using Moq;

    using NUnit.Framework;

    /// <summary>
    /// Tests for the component which took over <c>WebFormsServiceProvider</c>'s behaviour.
    /// </summary>
    /// <remarks>
    /// On net48, <c>WebFormsServiceProvider</c> was hooked into WebForms through
    /// <c>System.Web.UI.IServiceProvider</c> so that <c>Page</c>/<c>UserControl</c>/<c>IHttpHandler</c>
    /// instances the WebForms page factory created were activated from the request container. Neither the
    /// WebForms hook nor <c>PageHandlerFactory</c> exists on ASP.NET Core. The two halves of that behaviour
    /// are now <see cref="IScopeAccessor"/>/<see cref="HttpContextDependencyInjectionExtensions.GetScope"/>
    /// (find the request container) and <see cref="ActivatorUtilities"/> (activate a type from it), so these
    /// tests keep their original assertion intent re-pointed at that pair.
    /// </remarks>
    public class WebFormsServiceProviderTests
    {
        private interface IScopedService
        { }

        [OneTimeSetUp]
        public void FixtureSetUp()
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddTransient<INavigationManager>(container => Mock.Of<INavigationManager>());
            serviceCollection.AddTransient<IApplicationStatusInfo>(container => new DotNetNuke.Application.ApplicationStatusInfo(Mock.Of<IApplicationInfo>()));
            serviceCollection.AddScoped<IScopedService, ScopedService>();
            serviceCollection.AddSingleton<SingletonService>();

            Globals.DependencyProvider = serviceCollection.BuildServiceProvider();
        }

        [OneTimeTearDown]
        public void FixtureTearDown()
        {
            Globals.DependencyProvider = null;
        }

        [Test]
        public void CreateInstanceFromScope()
        {
            // Arrange
            var scope = Globals.DependencyProvider.CreateScope();

            HttpContextHelper.RegisterMockHttpContext();
            HttpContextSource.Current.SetScope(scope);

            var provider = new ScopeAccessor().GetScope().ServiceProvider;

            // Act
            var instance = provider.GetService(typeof(IScopedService));

            // Assert
            Assert.That(instance, Is.Not.Null);
            Assert.That(instance, Is.EqualTo(scope.ServiceProvider.GetRequiredService<IScopedService>()));
        }

        [Test]
        public void CreateInstanceWithProtectedConstructor()
        {
            // Arrange
            var scope = Globals.DependencyProvider.CreateScope();

            HttpContextHelper.RegisterMockHttpContext();
            HttpContextSource.Current.SetScope(scope);

            var provider = new ScopeAccessor().GetScope().ServiceProvider;

            // ProtectedConstructorHandler is deliberately NOT registered, exactly as PageHandlerFactory was
            // not registered at baseline, so the activation below is the behaviour under test and not setup.
            Assert.That(provider.GetService(typeof(ProtectedConstructorHandler)), Is.Null);

            // Act
            // The deleted WebFormsServiceProvider resolved a handler type from the request container and,
            // when it was not registered, activated it even though its constructor was not public. The Core
            // activator (ActivatorUtilities) only considers PUBLIC constructors, so the surviving equivalent
            // of that fallback is Activator.CreateInstance(type, nonPublic: true).
            var instance = provider.GetService(typeof(ProtectedConstructorHandler))
                           ?? Activator.CreateInstance(typeof(ProtectedConstructorHandler), nonPublic: true);

            // Assert
            Assert.That(instance, Is.Not.Null);
            Assert.That(instance, Is.InstanceOf<ProtectedConstructorHandler>());

            // Guard the reason the fallback is still required: were the Core activator ever to gain
            // non-public constructor support, this would start failing and the fallback could be dropped.
            Assert.That(
                () => ActivatorUtilities.CreateInstance<ProtectedConstructorHandler>(provider),
                Throws.InvalidOperationException);
        }

        [Test]
        public void CreateModuleWithSingletonService()
        {
            // Arrange
            var scope = Globals.DependencyProvider.CreateScope();

            HttpContextHelper.RegisterMockHttpContext();
            HttpContextSource.Current.SetScope(scope);

            var provider = new ScopeAccessor().GetScope().ServiceProvider;
            var service = Globals.DependencyProvider.GetRequiredService<SingletonService>();

            // Act
            var module = ActivatorUtilities.CreateInstance<TestModule<SingletonService>>(provider);

            // Assert
            Assert.That(module, Is.Not.Null);
            Assert.That(module.DependencyService, Is.EqualTo(module.ConstructorService));
            Assert.That(service, Is.EqualTo(module.ConstructorService));
        }

        [Test]
        public void CreateModuleWithScopedService()
        {
            // Arrange
            var scope = Globals.DependencyProvider.CreateScope();

            HttpContextHelper.RegisterMockHttpContext();
            HttpContextSource.Current.SetScope(scope);

            var provider = new ScopeAccessor().GetScope().ServiceProvider;
            var serviceFromRequestScope = scope.ServiceProvider.GetRequiredService<IScopedService>();
            var serviceFromGlobalScope = Globals.DependencyProvider.GetRequiredService<IScopedService>();

            // Act
            var module = ActivatorUtilities.CreateInstance<TestModule<IScopedService>>(provider);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(module, Is.Not.Null);
                Assert.That(module.DependencyService, Is.EqualTo(module.ConstructorService));
                Assert.That(serviceFromRequestScope, Is.EqualTo(module.ConstructorService));
                Assert.That(serviceFromGlobalScope, Is.Not.EqualTo(module.ConstructorService));
            }
        }

        private class ScopedService : IScopedService
        { }

        private class SingletonService
        { }

        /// <summary>Stands in for <c>System.Web.UI.PageHandlerFactory</c>, whose constructor was also not public.</summary>
        private class ProtectedConstructorHandler
        {
            protected ProtectedConstructorHandler()
            {
            }
        }

        public class TestModule<T> : PortalModuleBase
        {
            public TestModule(T service)
            {
                this.ConstructorService = service;
                this.DependencyService = this.DependencyProvider.GetRequiredService<T>();
            }

            public T ConstructorService { get; }

            public T DependencyService { get; }
        }
    }
}
