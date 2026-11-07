// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Mvc.Framework.Attributes
{
    using System.Collections.Generic;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Security.Roles;
    using DotNetNuke.Tests.Utilities.Fakes;
    using DotNetNuke.Web.Mvc.Framework.ActionFilters;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Abstractions;
    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;

    using Moq;
    using Moq.Protected;
    using NUnit.Framework;

    [TestFixture]
    internal class DnnAuthorizeAttributeTests
    {
        private MockRepository _mockRepository;
        private Mock<IRoleController> _mockRoleController;
        private Mock<DnnAuthorizeAttribute> _mockDnnAuthorizeAttribute;
        private FakeServiceProvider _serviceProvider;

        [SetUp]
        public void Setup()
        {
            this._mockRepository = new MockRepository(MockBehavior.Default);

            this._mockRoleController = this._mockRepository.Create<IRoleController>();

            this._mockDnnAuthorizeAttribute = this._mockRepository.Create<DnnAuthorizeAttribute>();
            this._mockDnnAuthorizeAttribute.CallBase = true;

            // HandleUnauthorizedRequest resolves IHostSettings from Globals.GetCurrentServiceProvider().
            this._serviceProvider = FakeServiceProvider.Setup(
                services =>
                {
                    services.AddSingleton(Mock.Of<IHostSettings>());
                });
        }

        [TearDown]
        public void TearDown()
        {
            RoleController.ClearInstance();
            this._serviceProvider.Dispose();
        }

        [Test]
        public void AnonymousUser_IsNotAllowed_If_AllowAnonymousAtribute_IsNotPresent()
        {
            // Arrange
            this._mockDnnAuthorizeAttribute.Protected().Setup("HandleUnauthorizedRequest", ItExpr.IsAny<AuthorizationFilterContext>());
            var sut = this._mockDnnAuthorizeAttribute.Object;

            var context = CreateAuthorizationFilterContext(allowAnonymousAtController: false, allowAnonymousAtAction: false);

            // Act
            sut.OnAuthorization(context);

            // Assert
            this._mockRepository.VerifyAll();
        }

        [Test]
        public void AnonymousUser_IsAllowed_If_AllowAnonymousAtribute_IsAtControllerLevel()
        {
            // Arrange
            var sut = this._mockDnnAuthorizeAttribute.Object;

            var context = CreateAuthorizationFilterContext(allowAnonymousAtController: true, allowAnonymousAtAction: false);

            // Act
            sut.OnAuthorization(context);

            // Assert
            this._mockRepository.VerifyAll();
        }

        [Test]
        public void AnonymousUser_IsAllowed_If_AllowAnonymousAtribute_IsAtActionLevel()
        {
            // Arrange
            var sut = this._mockDnnAuthorizeAttribute.Object;

            var context = CreateAuthorizationFilterContext(allowAnonymousAtController: false, allowAnonymousAtAction: true);

            // Act
            sut.OnAuthorization(context);

            // Assert
            this._mockRepository.VerifyAll();
        }

        [Test]
        public void RegisteredUser_IsAllowed_ByDefault()
        {
            // Arrange
            this._mockDnnAuthorizeAttribute.Protected().Setup<bool>("IsAuthenticated", ItExpr.IsAny<HttpContext>()).Returns(true);
            this._mockDnnAuthorizeAttribute.Protected().Setup("HandleAuthorizedRequest", ItExpr.IsAny<AuthorizationFilterContext>());
            var sut = this._mockDnnAuthorizeAttribute.Object;

            var context = CreateAuthorizationFilterContext(allowAnonymousAtController: false, allowAnonymousAtAction: false);

            // Act
            sut.OnAuthorization(context);

            // Assert
            this._mockRepository.VerifyAll();
        }

        [Test]
        public void RegisteredUser_IsDenied_If_IncludedIn_DeniedRoles()
        {
            // Arrange
            const string roleName = "MyRole";

            var user = this.SetUpUserWithRole(roleName);

            this._mockDnnAuthorizeAttribute.Protected().Setup<bool>("IsAuthenticated", ItExpr.IsAny<HttpContext>()).Returns(true);
            this._mockDnnAuthorizeAttribute.Protected().Setup("HandleUnauthorizedRequest", ItExpr.IsAny<AuthorizationFilterContext>());
            this._mockDnnAuthorizeAttribute.Protected().Setup<UserInfo>("GetCurrentUser").Returns(user);
            var sut = this._mockDnnAuthorizeAttribute.Object;
            sut.DenyRoles = roleName;

            var context = CreateAuthorizationFilterContext(allowAnonymousAtController: false, allowAnonymousAtAction: false);

            // Act
            sut.OnAuthorization(context);

            // Assert
            this._mockRepository.VerifyAll();
        }

        [Test]
        public void RegisteredUser_IsAllowed_If_IncludedIn_DeniedRoles_But_IsSuperUser()
        {
            // Arrange
            const string roleName = "MyRole";

            var user = new UserInfo { IsSuperUser = true };

            this._mockDnnAuthorizeAttribute.Protected().Setup<bool>("IsAuthenticated", ItExpr.IsAny<HttpContext>()).Returns(true);
            this._mockDnnAuthorizeAttribute.Protected().Setup("HandleAuthorizedRequest", ItExpr.IsAny<AuthorizationFilterContext>());
            this._mockDnnAuthorizeAttribute.Protected().Setup<UserInfo>("GetCurrentUser").Returns(user);
            var sut = this._mockDnnAuthorizeAttribute.Object;
            sut.DenyRoles = roleName;

            var context = CreateAuthorizationFilterContext(allowAnonymousAtController: false, allowAnonymousAtAction: false);

            // Act
            sut.OnAuthorization(context);

            // Assert
            this._mockRepository.VerifyAll();
        }

        [Test]
        public void RegisteredUser_IsDenied_If_IncludedIn_StaticRoles()
        {
            // Arrange
            const string roleName = "MyRole";

            var user = this.SetUpUserWithRole(roleName);

            this._mockDnnAuthorizeAttribute.Protected().Setup<bool>("IsAuthenticated", ItExpr.IsAny<HttpContext>()).Returns(true);
            this._mockDnnAuthorizeAttribute.Protected().Setup("HandleAuthorizedRequest", ItExpr.IsAny<AuthorizationFilterContext>());
            this._mockDnnAuthorizeAttribute.Protected().Setup<UserInfo>("GetCurrentUser").Returns(user);
            var sut = this._mockDnnAuthorizeAttribute.Object;
            sut.StaticRoles = roleName;

            var context = CreateAuthorizationFilterContext(allowAnonymousAtController: false, allowAnonymousAtAction: false);

            // Act
            sut.OnAuthorization(context);

            // Assert
            this._mockRepository.VerifyAll();
        }

        /// <summary>
        /// Builds the ASP.NET Core replacement of the MVC 5
        /// descriptor mocks (action plus controller descriptor) +
        /// <c>IsDefined(typeof(AllowAnonymousAttribute), true)</c> arrangement.
        /// </summary>
        /// <param name="allowAnonymousAtController">
        /// Whether <c>[AllowAnonymous]</c> is applied at the controller level, which surfaces as
        /// <see cref="IAllowAnonymous"/> on the endpoint metadata of the request.
        /// </param>
        /// <param name="allowAnonymousAtAction">
        /// Whether <c>[AllowAnonymous]</c> is applied at the action level, which surfaces as
        /// <see cref="IAllowAnonymous"/> on <see cref="ActionDescriptor.EndpointMetadata"/>.
        /// </param>
        /// <returns>The authorization filter context to run the filter against.</returns>
        private static AuthorizationFilterContext CreateAuthorizationFilterContext(
            bool allowAnonymousAtController,
            bool allowAnonymousAtAction)
        {
            var httpContext = MockHelper.CreateMockHttpContext();

            if (allowAnonymousAtController)
            {
                httpContext.SetEndpoint(new Endpoint(
                    requestDelegate: null,
                    new EndpointMetadataCollection(new AllowAnonymousAttribute()),
                    "Controller level AllowAnonymous"));
            }

            var actionDescriptor = new ControllerActionDescriptor
            {
                ControllerName = "Fake",
                ActionName = "Action",
            };

            if (allowAnonymousAtAction)
            {
                actionDescriptor.EndpointMetadata = new List<object> { new AllowAnonymousAttribute() };
            }

            var actionContext = new ActionContext(httpContext, new RouteData(), actionDescriptor);
            return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
        }

        private UserInfo SetUpUserWithRole(string roleName)
        {
            var user = new UserInfo
            {
                UserID = 1,
                PortalID = 1,
            };

            var roles = new List<UserRoleInfo>
            {
                new UserRoleInfo
                {
                    RoleName = roleName,
                    Status = RoleStatus.Approved,
                },
            };

            this._mockRoleController.Setup(x => x.GetUserRoles(user, true)).Returns(roles);
            RoleController.SetTestableInstance(this._mockRoleController.Object);

            return user;
        }
    }
}
