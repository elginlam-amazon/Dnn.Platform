// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Mvc.Framework.Attributes
{
    using System.Collections.Generic;

    using DotNetNuke.Entities.Users;
    using DotNetNuke.Security.Roles;
    using DotNetNuke.Tests.Web.Mvc.Fakes;
    using DotNetNuke.Web.Mvc.Framework.ActionFilters;

    using Microsoft.AspNetCore.Mvc.Filters;

    using Moq;
    using Moq.Protected;
    using NUnit.Framework;

    [TestFixture]
    internal class DnnAuthorizeAttributeTests
    {
        private MockRepository mockRepository;
        private Mock<IRoleController> mockRoleController;
        private Mock<DnnAuthorizeAttribute> mockDnnAuthorizeAttribute;

        [SetUp]
        public void Setup()
        {
            this.mockRepository = new MockRepository(MockBehavior.Default);

            this.mockRoleController = this.mockRepository.Create<IRoleController>();

            this.mockDnnAuthorizeAttribute = this.mockRepository.Create<DnnAuthorizeAttribute>();
            this.mockDnnAuthorizeAttribute.CallBase = true;
        }

        [TearDown]
        public void TearDown()
        {
            RoleController.ClearInstance();
        }

        [Test]
        public void AnonymousUser_IsNotAllowed_If_AllowAnonymousAtribute_IsNotPresent()
        {
            // Arrange
            this.mockDnnAuthorizeAttribute.Protected().Setup("HandleUnauthorizedRequest", ItExpr.IsAny<AuthorizationFilterContext>());
            this.mockDnnAuthorizeAttribute.Protected().Setup<bool>("IsAuthenticated").Returns(false);
            var sut = this.mockDnnAuthorizeAttribute.Object;

            var context = MockHelper.CreateAuthorizationFilterContext(typeof(SecuredFakeController), nameof(SecuredFakeController.Secured));

            // Act
            sut.OnAuthorization(context);

            // Assert
            this.mockRepository.VerifyAll();
        }

        [Test]
        public void AnonymousUser_IsAllowed_If_AllowAnonymousAtribute_IsAtControllerLevel()
        {
            // Arrange
            var sut = this.mockDnnAuthorizeAttribute.Object;

            var context = MockHelper.CreateAuthorizationFilterContext(typeof(AnonymousControllerFakeController), nameof(AnonymousControllerFakeController.Anonymous));

            // Act
            sut.OnAuthorization(context);

            // Assert
            this.mockRepository.VerifyAll();
        }

        [Test]
        public void AnonymousUser_IsAllowed_If_AllowAnonymousAtribute_IsAtActionLevel()
        {
            // Arrange
            var sut = this.mockDnnAuthorizeAttribute.Object;

            var context = MockHelper.CreateAuthorizationFilterContext(typeof(AnonymousActionFakeController), nameof(AnonymousActionFakeController.Anonymous));

            // Act
            sut.OnAuthorization(context);

            // Assert
            this.mockRepository.VerifyAll();
        }

        [Test]
        public void RegisteredUser_IsAllowed_ByDefault()
        {
            // Arrange
            this.mockDnnAuthorizeAttribute.Protected().Setup<bool>("IsAuthenticated").Returns(true);
            this.mockDnnAuthorizeAttribute.Protected().Setup("HandleAuthorizedRequest", ItExpr.IsAny<AuthorizationFilterContext>());
            var sut = this.mockDnnAuthorizeAttribute.Object;

            var context = MockHelper.CreateAuthorizationFilterContext(typeof(SecuredFakeController), nameof(SecuredFakeController.Secured));

            // Act
            sut.OnAuthorization(context);

            // Assert
            this.mockRepository.VerifyAll();
        }

        [Test]
        public void RegisteredUser_IsDenied_If_IncludedIn_DeniedRoles()
        {
            // Arrange
            const string roleName = "MyRole";

            var user = this.SetUpUserWithRole(roleName);

            this.mockDnnAuthorizeAttribute.Protected().Setup<bool>("IsAuthenticated").Returns(true);
            this.mockDnnAuthorizeAttribute.Protected().Setup("HandleUnauthorizedRequest", ItExpr.IsAny<AuthorizationFilterContext>());
            this.mockDnnAuthorizeAttribute.Protected().Setup<UserInfo>("GetCurrentUser").Returns(user);
            var sut = this.mockDnnAuthorizeAttribute.Object;
            sut.DenyRoles = roleName;

            var context = MockHelper.CreateAuthorizationFilterContext(typeof(SecuredFakeController), nameof(SecuredFakeController.Secured));

            // Act
            sut.OnAuthorization(context);

            // Assert
            this.mockRepository.VerifyAll();
        }

        [Test]
        public void RegisteredUser_IsAllowed_If_IncludedIn_DeniedRoles_But_IsSuperUser()
        {
            // Arrange
            const string roleName = "MyRole";

            var user = new UserInfo { IsSuperUser = true };

            this.mockDnnAuthorizeAttribute.Protected().Setup<bool>("IsAuthenticated").Returns(true);
            this.mockDnnAuthorizeAttribute.Protected().Setup("HandleAuthorizedRequest", ItExpr.IsAny<AuthorizationFilterContext>());
            this.mockDnnAuthorizeAttribute.Protected().Setup<UserInfo>("GetCurrentUser").Returns(user);
            var sut = this.mockDnnAuthorizeAttribute.Object;
            sut.DenyRoles = roleName;

            var context = MockHelper.CreateAuthorizationFilterContext(typeof(SecuredFakeController), nameof(SecuredFakeController.Secured));

            // Act
            sut.OnAuthorization(context);

            // Assert
            this.mockRepository.VerifyAll();
        }

        [Test]
        public void RegisteredUser_IsDenied_If_IncludedIn_StaticRoles()
        {
            // Arrange
            const string roleName = "MyRole";

            var user = this.SetUpUserWithRole(roleName);

            this.mockDnnAuthorizeAttribute.Protected().Setup<bool>("IsAuthenticated").Returns(true);
            this.mockDnnAuthorizeAttribute.Protected().Setup("HandleAuthorizedRequest", ItExpr.IsAny<AuthorizationFilterContext>());
            this.mockDnnAuthorizeAttribute.Protected().Setup<UserInfo>("GetCurrentUser").Returns(user);
            var sut = this.mockDnnAuthorizeAttribute.Object;
            sut.StaticRoles = roleName;

            var context = MockHelper.CreateAuthorizationFilterContext(typeof(SecuredFakeController), nameof(SecuredFakeController.Secured));

            // Act
            sut.OnAuthorization(context);

            // Assert
            this.mockRepository.VerifyAll();
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

            this.mockRoleController.Setup(x => x.GetUserRoles(user, true)).Returns(roles);
            RoleController.SetTestableInstance(this.mockRoleController.Object);

            return user;
        }
    }
}
