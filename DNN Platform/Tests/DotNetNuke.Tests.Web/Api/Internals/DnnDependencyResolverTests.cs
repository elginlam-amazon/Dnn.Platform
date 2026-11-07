// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Tests.Web.Api.Internals
{
    using System;
    using System.Linq;

    using DotNetNuke.Services.DependencyInjection;

    using Microsoft.Extensions.DependencyInjection;

    using NUnit.Framework;

    /// <summary>
    /// Tests for the component which took over <c>DnnDependencyResolver</c>'s behaviour.
    /// </summary>
    /// <remarks>
    /// On Web API 2, <c>DnnDependencyResolver</c> implemented
    /// <c>System.Web.Http.Dependencies.IDependencyResolver</c> and was the single bridge between Web API
    /// and the DNN container: <c>GetService</c>/<c>GetServices</c> resolved from the ambient scope and
    /// <c>BeginScope</c> produced a child resolver. None of those types exist on ASP.NET Core, where the
    /// framework resolves from <c>HttpContext.RequestServices</c> itself. The same responsibilities are now
    /// split between <see cref="IScopeAccessor"/> (which scope am I in) and
    /// <see cref="IServiceProvider"/>/<see cref="IServiceScope"/> (resolution and child scopes), so these
    /// tests keep their original assertion intent re-pointed at that pair.
    /// </remarks>
    [TestFixture]
    public class DnnDependencyResolverTests
    {
        private ServiceProvider serviceProvider;
        private IScopeAccessor scopeAccessor;

        private interface ITestService
        {
        }

        [OneTimeSetUp]
        public void FixtureSetUp()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IScopeAccessor>(sp => new FakeScopeAccessor(sp.CreateScope()));
            services.AddScoped(typeof(ITestService), typeof(TestService));

            this.serviceProvider = services.BuildServiceProvider();
            this.scopeAccessor = this.serviceProvider.GetRequiredService<IScopeAccessor>();
        }

        [OneTimeTearDown]
        public void FixtureTearDown()
        {
            (this.scopeAccessor as IDisposable)?.Dispose();
            this.scopeAccessor = null;

            this.serviceProvider?.Dispose();
            this.serviceProvider = null;
        }

        [Test]
        public void NotNull()
        {
            Assert.That(this.scopeAccessor, Is.Not.Null);
            Assert.That(this.scopeAccessor.GetScope(), Is.Not.Null);
        }

        [Test]
        public void IsOfTypeDnnDependencyResolver()
        {
            // The baseline asserted that the container handed back the PRODUCTION resolver type, so this
            // resolves IScopeAccessor from a container using the production registration (not the fixture's
            // FakeScopeAccessor, which would only assert the test's own setup).
            var services = new ServiceCollection();
            services.AddSingleton<IScopeAccessor, ScopeAccessor>();

            using var provider = services.BuildServiceProvider();

            var actual = provider.GetRequiredService<IScopeAccessor>();

            Assert.That(actual, Is.InstanceOf<ScopeAccessor>());
        }

        [Test]
        public void GetTestService()
        {
            var expected = new TestService();
            var actual = this.scopeAccessor.GetScope().ServiceProvider.GetService(typeof(ITestService));

            Assert.That(actual, Is.Not.Null);
            Assert.That(actual.GetType(), Is.EqualTo(expected.GetType()));
        }

        [Test]
        public void GetTestServices()
        {
            var expected = new TestService();
            var actual = this.scopeAccessor.GetScope().ServiceProvider.GetServices(typeof(ITestService)).ToArray();

            Assert.That(actual, Is.Not.Null);
            Assert.That(actual, Has.Length.EqualTo(1));
            Assert.That(actual[0].GetType(), Is.EqualTo(expected.GetType()));
        }

        [Test]
        public void BeginScope()
        {
            var actual = this.serviceProvider.CreateScope();

            Assert.That(actual, Is.Not.Null);
            Assert.That(actual, Is.InstanceOf<IServiceScope>());
            Assert.That(actual.ServiceProvider, Is.Not.Null);

            // Disposing a child scope disposes the services it created (the old resolver's Dispose contract).
            var scopedService = (TestService)actual.ServiceProvider.GetRequiredService<ITestService>();
            Assert.That(scopedService.IsDisposed, Is.False);

            actual.Dispose();

            Assert.That(scopedService.IsDisposed, Is.True);
        }

        [Test]
        public void BeginScope_GetService()
        {
            using var scope = this.serviceProvider.CreateScope();

            var expected = new TestService();
            var actual = scope.ServiceProvider.GetService(typeof(ITestService));

            Assert.That(actual, Is.Not.Null);
            Assert.That(actual.GetType(), Is.EqualTo(expected.GetType()));
        }

        [Test]
        public void BeginScope_GetServices()
        {
            using var scope = this.serviceProvider.CreateScope();

            var expected = new TestService();
            var actual = scope.ServiceProvider.GetServices(typeof(ITestService)).ToArray();

            Assert.That(actual, Is.Not.Null);
            Assert.That(actual, Has.Length.EqualTo(1));
            Assert.That(actual[0].GetType(), Is.EqualTo(expected.GetType()));
        }

        private class TestService : ITestService, IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                this.IsDisposed = true;
            }
        }

        private class FakeScopeAccessor : IScopeAccessor, IDisposable
        {
            private IServiceScope fakeScope;

            public FakeScopeAccessor(IServiceScope fakeScope)
            {
                this.fakeScope = fakeScope;
            }

            public IServiceScope GetScope()
            {
                return this.fakeScope;
            }

            public void Dispose()
            {
                this.fakeScope?.Dispose();
                this.fakeScope = null;
            }
        }
    }
}
