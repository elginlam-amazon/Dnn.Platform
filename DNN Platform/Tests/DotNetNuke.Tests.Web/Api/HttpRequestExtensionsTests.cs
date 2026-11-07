// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Api
{
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Web.Api;

    using Moq;

    using NUnit.Framework;

    [TestFixture]
    public class HttpRequestExtensionsTests
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
        public void FindTabIdTriesAllProviders()
        {
            // Arrange
            var provider = new Mock<ITabAndModuleInfoProvider>();
            var request = TabAndModuleInfoProviderTestUtilities.CreateRequestWithProviders(provider.Object, provider.Object);
            int i;
            provider.Setup(x => x.TryFindTabId(request, out i)).Returns(false);

            // Act
            request.FindTabId();

            // Assert
            provider.Verify(x => x.TryFindTabId(request, out i), Times.Exactly(2));
        }

        [Test]
        public void FindTabIdStopCallingProvidersAfterOneSuccess()
        {
            // Arrange
            var provider = new Mock<ITabAndModuleInfoProvider>();
            var request = TabAndModuleInfoProviderTestUtilities.CreateRequestWithProviders(provider.Object, provider.Object);
            int i;
            provider.Setup(x => x.TryFindTabId(request, out i)).Returns(true);

            // Act
            request.FindTabId();

            // Assert
            provider.Verify(x => x.TryFindTabId(request, out i), Times.Once());
        }

        [Test]
        public void FindModuleIdTriesAllProviders()
        {
            // Arrange
            var provider = new Mock<ITabAndModuleInfoProvider>();
            var request = TabAndModuleInfoProviderTestUtilities.CreateRequestWithProviders(provider.Object, provider.Object);
            int i;
            provider.Setup(x => x.TryFindModuleId(request, out i)).Returns(false);

            // Act
            request.FindModuleId();

            // Assert
            provider.Verify(x => x.TryFindModuleId(request, out i), Times.Exactly(2));
        }

        [Test]
        public void FindModuleIdStopCallingProvidersAfterOneSuccess()
        {
            // Arrange
            var provider = new Mock<ITabAndModuleInfoProvider>();
            var request = TabAndModuleInfoProviderTestUtilities.CreateRequestWithProviders(provider.Object, provider.Object);
            int i;
            provider.Setup(x => x.TryFindModuleId(request, out i)).Returns(true);

            // Act
            request.FindModuleId();

            // Assert
            provider.Verify(x => x.TryFindModuleId(request, out i), Times.Once());
        }

        [Test]
        public void FindModuleInfoTriesAllProviders()
        {
            // Arrange
            var provider = new Mock<ITabAndModuleInfoProvider>();
            var request = TabAndModuleInfoProviderTestUtilities.CreateRequestWithProviders(provider.Object, provider.Object);
            ModuleInfo moduleInfo;
            provider.Setup(x => x.TryFindModuleInfo(request, out moduleInfo)).Returns(false);

            // Act
            request.FindModuleInfo();

            // Assert
            provider.Verify(x => x.TryFindModuleInfo(request, out moduleInfo), Times.Exactly(2));
        }

        [Test]
        public void FindModuleInfoStopCallingProvidersAfterOneSuccess()
        {
            // Arrange
            var provider = new Mock<ITabAndModuleInfoProvider>();
            var request = TabAndModuleInfoProviderTestUtilities.CreateRequestWithProviders(provider.Object, provider.Object);
            ModuleInfo moduleInfo;
            provider.Setup(x => x.TryFindModuleInfo(request, out moduleInfo)).Returns(true);

            // Act
            request.FindModuleInfo();

            // Assert
            provider.Verify(x => x.TryFindModuleInfo(request, out moduleInfo), Times.Once());
        }
    }
}
