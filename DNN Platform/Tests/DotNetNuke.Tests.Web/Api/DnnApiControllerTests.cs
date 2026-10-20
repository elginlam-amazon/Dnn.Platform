// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Api
{
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Web.Api;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Abstractions;
    using Microsoft.AspNetCore.Mvc.Controllers;

    using Moq;

    using NUnit.Framework;

    [TestFixture]
    public class DnnApiControllerTests
    {
        // DnnApiController was ported from a Web API ApiController to an ASP.NET Core ControllerBase.
        // The active module is resolved from Request.FindModuleInfo() (the request being an
        // Microsoft.AspNetCore.Http.HttpRequest), and the tab/module info providers are now registered
        // in TabAndModuleInfoProviderRegistry (replacing HttpConfiguration.AddTabAndModuleInfoProvider).
        [SetUp]
        public void SetUp()
        {
            ClearRegistry();
        }

        [TearDown]
        public void TearDown()
        {
            PortalController.ClearInstance();
            ClearRegistry();
        }

        [Test]
        public void GetsModuleInfoViaTheTabModuleInfoProviders()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            var request = httpContext.Request;
            var provider = new Mock<ITabAndModuleInfoProvider>();
            var expectedModule = new ModuleInfo();
            provider.Setup(x => x.TryFindModuleInfo(request, out expectedModule)).Returns(true);
            TabAndModuleInfoProviderRegistry.AddTabAndModuleInfoProvider(provider.Object);

            var controller = new DnnApiControllerHelper
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = httpContext,
                    ActionDescriptor = new ControllerActionDescriptor(),
                },
            };

            // Act
            var result = controller.ActiveModule;

            // Assert
            Assert.That(result, Is.EqualTo(expectedModule));
            provider.Verify(x => x.TryFindModuleInfo(request, out expectedModule), Times.Once());
        }

        [Test]
        public void GetsPortalSettingsViaTestablePortalController()
        {
            // Arrange
            var controller = new DnnApiControllerHelper();
            var mockPortalController = new Mock<IPortalController>();
            var expectedPortalSettings = new PortalSettings();
            mockPortalController.Setup(x => x.GetCurrentPortalSettings()).Returns(expectedPortalSettings);
            PortalController.SetTestableInstance(mockPortalController.Object);

            // Act
            var result = controller.PortalSettings;

            // Assert
            mockPortalController.Verify(x => x.GetCurrentPortalSettings(), Times.Once());
            Assert.That(result, Is.EqualTo(expectedPortalSettings));
        }

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

        internal class DnnApiControllerHelper : DnnApiController
        {
        }
    }
}
