// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Api
{
    using System.Linq;

    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Web.Api;

    using Microsoft.AspNetCore.Http;

    using Moq;

    using NUnit.Framework;

    [TestFixture]
    public class HttpRequestMessageExtensionsTests
    {
        // On .NET Framework the tab/module info providers were stored on the Web API HttpConfiguration
        // and resolved from the HttpRequestMessage. DotNetNuke.Web/Api now runs on ASP.NET Core:
        // providers are registered in TabAndModuleInfoProviderRegistry (replacing
        // HttpConfiguration.AddTabAndModuleInfoProvider) and the FindXyz extensions operate on
        // Microsoft.AspNetCore.Http.HttpRequest.
        [SetUp]
        public void SetUp()
        {
            ClearRegistry();
        }

        [TearDown]
        public void TearDown()
        {
            ClearRegistry();
        }

        [Test]
        public void FindTabIdTriesAllProviders()
        {
            // Arrange
            var request = new DefaultHttpContext().Request;
            var provider = new Mock<ITabAndModuleInfoProvider>();
            int i;
            provider.Setup(x => x.TryFindTabId(request, out i)).Returns(false);
            TabAndModuleInfoProviderRegistry.AddTabAndModuleInfoProvider(provider.Object);
            TabAndModuleInfoProviderRegistry.AddTabAndModuleInfoProvider(provider.Object);

            // Act
            request.FindTabId();

            // Assert
            provider.Verify(x => x.TryFindTabId(request, out i), Times.Exactly(2));
        }

        [Test]
        public void FindTabIdStopCallingProvidersAfterOneSuccess()
        {
            // Arrange
            var request = new DefaultHttpContext().Request;
            var provider = new Mock<ITabAndModuleInfoProvider>();
            int i;
            provider.Setup(x => x.TryFindTabId(request, out i)).Returns(true);
            TabAndModuleInfoProviderRegistry.AddTabAndModuleInfoProvider(provider.Object);
            TabAndModuleInfoProviderRegistry.AddTabAndModuleInfoProvider(provider.Object);

            // Act
            request.FindTabId();

            // Assert
            provider.Verify(x => x.TryFindTabId(request, out i), Times.Once());
        }

        [Test]
        public void FindModuleIdTriesAllProviders()
        {
            // Arrange
            var request = new DefaultHttpContext().Request;
            var provider = new Mock<ITabAndModuleInfoProvider>();
            int i;
            provider.Setup(x => x.TryFindModuleId(request, out i)).Returns(false);
            TabAndModuleInfoProviderRegistry.AddTabAndModuleInfoProvider(provider.Object);
            TabAndModuleInfoProviderRegistry.AddTabAndModuleInfoProvider(provider.Object);

            // Act
            request.FindModuleId();

            // Assert
            provider.Verify(x => x.TryFindModuleId(request, out i), Times.Exactly(2));
        }

        [Test]
        public void FindModuleIdStopCallingProvidersAfterOneSuccess()
        {
            // Arrange
            var request = new DefaultHttpContext().Request;
            var provider = new Mock<ITabAndModuleInfoProvider>();
            int i;
            provider.Setup(x => x.TryFindModuleId(request, out i)).Returns(true);
            TabAndModuleInfoProviderRegistry.AddTabAndModuleInfoProvider(provider.Object);
            TabAndModuleInfoProviderRegistry.AddTabAndModuleInfoProvider(provider.Object);

            // Act
            request.FindModuleId();

            // Assert
            provider.Verify(x => x.TryFindModuleId(request, out i), Times.Once());
        }

        [Test]
        public void FindModuleInfoTriesAllProviders()
        {
            // Arrange
            var request = new DefaultHttpContext().Request;
            var provider = new Mock<ITabAndModuleInfoProvider>();
            ModuleInfo moduleInfo;
            provider.Setup(x => x.TryFindModuleInfo(request, out moduleInfo)).Returns(false);
            TabAndModuleInfoProviderRegistry.AddTabAndModuleInfoProvider(provider.Object);
            TabAndModuleInfoProviderRegistry.AddTabAndModuleInfoProvider(provider.Object);

            // Act
            request.FindModuleInfo();

            // Assert
            provider.Verify(x => x.TryFindModuleInfo(request, out moduleInfo), Times.Exactly(2));
        }

        [Test]
        public void FindModuleInfoStopCallingProvidersAfterOneSuccess()
        {
            // Arrange
            var request = new DefaultHttpContext().Request;
            var provider = new Mock<ITabAndModuleInfoProvider>();
            ModuleInfo moduleInfo;
            provider.Setup(x => x.TryFindModuleInfo(request, out moduleInfo)).Returns(true);
            TabAndModuleInfoProviderRegistry.AddTabAndModuleInfoProvider(provider.Object);
            TabAndModuleInfoProviderRegistry.AddTabAndModuleInfoProvider(provider.Object);

            // Act
            request.FindModuleInfo();

            // Assert
            provider.Verify(x => x.TryFindModuleInfo(request, out moduleInfo), Times.Once());
        }

        // The registry exposes no public reset, so drain its internal queue between tests to keep
        // the fixtures isolated.
        private static void ClearRegistry()
        {
            var field = typeof(TabAndModuleInfoProviderRegistry)
                .GetField("Providers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (field?.GetValue(null) is System.Collections.Concurrent.ConcurrentQueue<ITabAndModuleInfoProvider> queue)
            {
                while (queue.TryDequeue(out _))
                {
                }
            }
        }
    }
}
