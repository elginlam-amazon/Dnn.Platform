// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Api
{
    using System.Linq;

    using DotNetNuke.Web.Api;

    using Microsoft.Extensions.DependencyInjection;

    using NUnit.Framework;

    [TestFixture]
    public class HttpConfigurationExtensionTests
    {
        [SetUp]
        public void SetUp()
        {
            TabAndModuleInfoProviderTestUtilities.ClearRuntimeProviders();
        }

        [TearDown]
        public void TearDown()
        {
            TabAndModuleInfoProviderTestUtilities.ClearRuntimeProviders();
        }

        [Test]
        public void GetTabAndModuleInfoProvidersReturnsEmptyWhenNoProvidersAdded()
        {
            // Arrange
            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            // Act
            var providers = serviceProvider.GetTabAndModuleInfoProviders();

            // Assert
            Assert.That(providers, Is.Empty);
        }

        [Test]
        public void AddTabAndModuleInfoProviderWorksForFirstProvider()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddTabAndModuleInfoProvider(new StandardTabAndModuleInfoProvider());

            // Assert
            Assert.That(services.BuildServiceProvider().GetTabAndModuleInfoProviders().Count(), Is.EqualTo(1));
        }

        [Test]
        public void AddTabAndModuleInfoProviderWorksForManyProviders()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddTabAndModuleInfoProvider(new StandardTabAndModuleInfoProvider());
            services.AddTabAndModuleInfoProvider(new StandardTabAndModuleInfoProvider());
            services.AddTabAndModuleInfoProvider(new StandardTabAndModuleInfoProvider());

            // Assert
            Assert.That(services.BuildServiceProvider().GetTabAndModuleInfoProviders().Count(), Is.EqualTo(3));
        }
    }
}
