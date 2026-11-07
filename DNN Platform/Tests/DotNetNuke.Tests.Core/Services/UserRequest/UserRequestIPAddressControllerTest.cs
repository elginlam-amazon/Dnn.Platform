// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Tests.Core.Services.UserRequest
{
    using System.Net;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Common;
    using DotNetNuke.Entities.Controllers;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Services.UserRequest;
    using DotNetNuke.Tests.Utilities;
    using DotNetNuke.Tests.Utilities.Fakes;
    using DotNetNuke.Tests.Utilities.Mocks;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    using Moq;

    using NUnit.Framework;

    [TestFixture]
    internal class UserRequestIPAddressControllerTest
    {
        private Mock<IPortalController> mockPortalController;
        private Mock<IHostSettingsService> hostSettingsService;
        private Mock<HttpContext> mockHttpContext;
        private Mock<HttpRequest> mockRequest;
        private Mock<ConnectionInfo> mockConnection;

        private UserRequestIPAddressController userRequestIPAddressController;
        private FakeServiceProvider serviceProvider;

        [SetUp]
        public void Setup()
        {
            // Setup Mock
            this.hostSettingsService = new Mock<IHostSettingsService>();
            this.mockPortalController = MockComponentProvider.CreateNew<IPortalController>();
            PortalController.SetTestableInstance(this.mockPortalController.Object);

            this.serviceProvider = FakeServiceProvider.Setup(
                services =>
                {
                    services.AddSingleton(this.mockPortalController.Object);
                    services.AddSingleton(this.hostSettingsService.Object);
                    services.AddSingleton((IHostSettingsService)this.hostSettingsService.Object);
                });
            this.mockHttpContext = Mock.Get(HttpContextSource.Current);
            this.mockRequest = Mock.Get(this.mockHttpContext.Object.Request);
            this.mockConnection = Mock.Get(this.mockHttpContext.Object.Connection);

            // The net48 fixture started from an empty ServerVariables collection, i.e. no
            // REMOTE_ADDR. The Core equivalent of REMOTE_ADDR is Connection.RemoteIpAddress,
            // so start with no remote address at all.
            this.mockConnection.SetupGet(x => x.RemoteIpAddress).Returns((IPAddress)null);

            // System under test
            this.userRequestIPAddressController = new UserRequestIPAddressController(this.hostSettingsService.Object);
        }

        [TearDown]
        public void TearDown()
        {
            this.serviceProvider.Dispose();
            MockComponentProvider.ResetContainer();
        }

        [TestCase("111.111.111.111", "X-Forwarded-For")]
        [TestCase("111.111.111.111,123.112.11.33", "X-Forwarded-For")]
        [TestCase("111.111.111.111", "X-ProxyUser-Ip")]
        [TestCase("111.111.111.111,23.112.11.33", "X-ProxyUser-Ip")]
        public void UserRequestIPAddress_ShouldReturnIP_IfAnyHeaderIsPresent(string requestIp, string headerName)
        {
            // Arrange
            var expectedIp = "111.111.111.111";

            var headersWithXForwardedHeaders = new HeaderDictionary
            {
                { headerName, requestIp },
            };
            this.hostSettingsService.As<IHostSettingsService>().Setup(hc => hc.GetString(It.IsAny<string>())).Returns(headerName);
            this.mockRequest.Setup(x => x.Headers).Returns(headersWithXForwardedHeaders);

            // Act
            string userRequestIPAddress = this.userRequestIPAddressController.GetUserRequestIPAddress(this.mockRequest.Object);

            // Assert
            Assert.That(userRequestIPAddress, Is.EqualTo(expectedIp));
        }

        [Test]
        public void UserRequestIPAddress_ShouldReturnIP_IfRemoteAddrServerVariablePresent()
        {
            // Arrange
            var expectedIp = "111.111.111.111";
            var remoteVariable = "REMOTE_ADDR";
            var requestIp = "111.111.111.111";

            // REMOTE_ADDR has no Core counterpart; the server-variable shim writes it onto
            // the member that replaced it (Connection.RemoteIpAddress).
            HttpContextHelper.SetServerVariable(this.mockHttpContext, remoteVariable, requestIp);

            // Act
            var userRequestIPAddress = this.userRequestIPAddressController.GetUserRequestIPAddress(this.mockRequest.Object);

            // Assert
            Assert.That(userRequestIPAddress, Is.EqualTo(expectedIp));
            this.mockConnection.VerifyGet(c => c.RemoteIpAddress);
        }

        [Test]
        public void UserRequestIPAddress_ShouldReturnIP_IfUserHostAddress()
        {
            // Arrange
            var expectedIp = "111.111.111.111";

            // Request.UserHostAddress became HttpContext.Connection.RemoteIpAddress.
            this.mockConnection.SetupGet(x => x.RemoteIpAddress).Returns(IPAddress.Parse(expectedIp));

            // Act
            var userRequestIPAddress = this.userRequestIPAddressController.GetUserRequestIPAddress(this.mockRequest.Object);

            // Assert
            Assert.That(userRequestIPAddress, Is.EqualTo(expectedIp));
            this.mockConnection.VerifyGet(c => c.RemoteIpAddress);
        }

        [TestCase("abc.111.eer")]
        [TestCase("somedomain.com")]
        [TestCase("244.275.111.111")]
        public void UserRequestIPAddress_ShouldReturnEmptyString_IfIPAddressIsNotValid(string requestIp)
        {
            // Arrange
            var headerName = "X-Forwarded-For";

            var headersWithXForwardedHeaders = new HeaderDictionary
            {
                { headerName, requestIp },
            };
            this.mockRequest.Setup(x => x.Headers).Returns(headersWithXForwardedHeaders);
            this.hostSettingsService.Setup(hc => hc.GetString(It.IsAny<string>(), It.IsAny<string>())).Returns(headerName);
            this.hostSettingsService.Setup(hc => hc.GetString(It.IsAny<string>())).Returns(headerName);

            // Act
            var userRequestIPAddress = this.userRequestIPAddressController.GetUserRequestIPAddress(this.mockRequest.Object);

            // Assert
            Assert.That(userRequestIPAddress, Is.SameAs(string.Empty));
        }
    }
}
