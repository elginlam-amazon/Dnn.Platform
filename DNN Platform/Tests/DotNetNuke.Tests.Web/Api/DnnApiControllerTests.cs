// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Api
{
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Web.Api;

    using Microsoft.AspNetCore.Mvc;

    using Moq;
    using NUnit.Framework;

#pragma warning disable CS0618 // IPortalController.GetCurrentPortalSettings is deprecated, but it is the member under test here.

    [TestFixture]
    public class DnnApiControllerTests
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
            PortalController.ClearInstance();
        }

        [Test]
        public void GetsModuleInfoViaTheTabModuleInfoProviders()
        {
            // Arrange
            var provider = new Mock<ITabAndModuleInfoProvider>();
            var expectedModule = new ModuleInfo();
            var request = TabAndModuleInfoProviderTestUtilities.CreateRequestWithProviders(provider.Object);
            provider.Setup(x => x.TryFindModuleInfo(request, out expectedModule)).Returns(true);

            var controller = new DnnApiControllerHelper
            {
                ControllerContext = new ControllerContext { HttpContext = request.HttpContext, },
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

        internal class DnnApiControllerHelper : DnnApiController
        {
        }

        // A test that would be nice to run, but I see not good way to test the source of the
        // userinfo
        //        [Test]
        //        public void UserInfoComesFromPortalSettings()
        //        {
        //            //Arrange
        //            var controller = new DnnApiControllerHelper();
        //            var mockPortalController = new Mock<IPortalController>();
        //            var expectedPortalSettings = new PortalSettings();
        // expectedPortalSettings.UserInfo = ??????
        //            mockPortalController.Setup(x => x.GetCurrentPortalSettings()).Returns(expectedPortalSettings);
        //            TestablePortalController.SetTestableInstance(mockPortalController.Object);
        //
        //            //Act
        //            var result = controller.PortalSettings;
        //
        //            //Assert
        //            mockPortalController.Verify(x => x.GetCurrentPortalSettings(), Times.Once());
        //            Assert.AreEqual(expectedPortalSettings, result);
        //        }
    }
#pragma warning restore CS0618
}
