// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Mvc.Framework.Attributes
{
    using System;

    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Tests.Web.Mvc.Fakes;
    using DotNetNuke.UI.Modules;
    using DotNetNuke.Web.Mvc.Framework.ActionFilters;
    using DotNetNuke.Web.Mvc.Framework.Modules;

    using Microsoft.AspNetCore.Mvc.Filters;

    using Moq;
    using Moq.Protected;
    using NUnit.Framework;

    [TestFixture]
    internal class DnnModuleAuthorizeAttributeTests
    {
        private MockRepository mockRepository;
        private Mock<DnnModuleAuthorizeAttribute> mockDnnModuleAuthorizeAttribute;

        [SetUp]
        public void Setup()
        {
            this.mockRepository = new MockRepository(MockBehavior.Default);

            this.mockDnnModuleAuthorizeAttribute = this.mockRepository.Create<DnnModuleAuthorizeAttribute>();
            this.mockDnnModuleAuthorizeAttribute.CallBase = true;
        }

        [Test]
        public void OnAuthorization_Throws_If_No_ModuleState_In_Context()
        {
            // Arrange
            var sut = this.mockDnnModuleAuthorizeAttribute.Object;
            var context = MockHelper.CreateAuthorizationFilterContext(typeof(SecuredFakeController), nameof(SecuredFakeController.Secured));

            // Act, Assert
            Assert.Throws<InvalidOperationException>(() => sut.OnAuthorization(context));
        }

        [Test]
        public void AnonymousUser_IsNotAllowed_If_AllowAnonymousAttribute_IsNotPresent()
        {
            // Arrange
            this.mockDnnModuleAuthorizeAttribute.Protected().Setup("HandleUnauthorizedRequest", ItExpr.IsAny<AuthorizationFilterContext>());
            var sut = this.mockDnnModuleAuthorizeAttribute.Object;

            var context = SetupContext(typeof(SecuredFakeController), nameof(SecuredFakeController.Secured), new ModuleInstanceContext());

            // Act
            sut.OnAuthorization(context);

            // Assert
            this.mockRepository.VerifyAll();
        }

        [Test]
        public void AnonymousUser_IsAllowed_If_AllowAnonymousAttribute_IsAtControllerLevel()
        {
            // Arrange
            var sut = this.mockDnnModuleAuthorizeAttribute.Object;

            var context = SetupContext(typeof(AnonymousControllerFakeController), nameof(AnonymousControllerFakeController.Anonymous), new ModuleInstanceContext());

            // Act
            sut.OnAuthorization(context);

            // Assert
            this.mockRepository.VerifyAll();
        }

        [Test]
        public void AnonymousUser_IsAllowed_If_AllowAnonymousAttribute_IsAtActionLevel()
        {
            // Arrange
            var sut = this.mockDnnModuleAuthorizeAttribute.Object;

            var context = SetupContext(typeof(AnonymousActionFakeController), nameof(AnonymousActionFakeController.Anonymous), new ModuleInstanceContext());

            // Act
            sut.OnAuthorization(context);

            // Assert
            this.mockRepository.VerifyAll();
        }

        [Test]
        public void RegisteredUser_IsAllowed_IfHasModuleAccess()
        {
            // Arrange
            var sut = this.mockDnnModuleAuthorizeAttribute.Object;
            this.mockDnnModuleAuthorizeAttribute.Protected().Setup<bool>("HasModuleAccess").Returns(true);
            this.mockDnnModuleAuthorizeAttribute.Protected().Setup("HandleAuthorizedRequest", ItExpr.IsAny<AuthorizationFilterContext>());

            var moduleContext = new ModuleInstanceContext { Configuration = new ModuleInfo() };
            var context = SetupContext(typeof(SecuredFakeController), nameof(SecuredFakeController.Secured), moduleContext);

            // Act
            sut.OnAuthorization(context);

            // Assert
            this.mockRepository.VerifyAll();
        }

        [Test]
        public void RegisteredUser_IsDenied_IfHasNoModuleAccess()
        {
            // Arrange
            var sut = this.mockDnnModuleAuthorizeAttribute.Object;
            this.mockDnnModuleAuthorizeAttribute.Protected().Setup<bool>("HasModuleAccess").Returns(false);
            this.mockDnnModuleAuthorizeAttribute.Protected().Setup("HandleUnauthorizedRequest", ItExpr.IsAny<AuthorizationFilterContext>());

            var moduleContext = new ModuleInstanceContext { Configuration = new ModuleInfo() };
            var context = SetupContext(typeof(SecuredFakeController), nameof(SecuredFakeController.Secured), moduleContext);

            // Act
            sut.OnAuthorization(context);

            // Assert
            this.mockRepository.VerifyAll();
        }

        private static AuthorizationFilterContext SetupContext(Type controllerType, string actionMethodName, ModuleInstanceContext moduleContext)
        {
            var context = MockHelper.CreateAuthorizationFilterContext(controllerType, actionMethodName);
            context.HttpContext.Items[typeof(DnnModuleState)] = new DnnModuleState { ModuleContext = moduleContext };
            return context;
        }
    }
}
