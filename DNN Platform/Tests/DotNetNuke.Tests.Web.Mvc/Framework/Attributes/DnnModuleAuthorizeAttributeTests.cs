// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Mvc.Framework.Attributes
{
    using System.Collections.Generic;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Tests.Utilities.Fakes;
    using DotNetNuke.UI.Modules;
    using DotNetNuke.Web.Mvc.Framework.ActionFilters;
    using DotNetNuke.Web.Mvc.Framework.Modules;

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
    internal class DnnModuleAuthorizeAttributeTests
    {
        private MockRepository _mockRepository;
        private Mock<DnnModuleAuthorizeAttribute> _mockDnnModuleAuthorizeAttribute;
        private FakeServiceProvider _serviceProvider;

        [SetUp]
        public void Setup()
        {
            this._mockRepository = new MockRepository(MockBehavior.Default);

            this._mockDnnModuleAuthorizeAttribute = this._mockRepository.Create<DnnModuleAuthorizeAttribute>();
            this._mockDnnModuleAuthorizeAttribute.CallBase = true;

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
            this._serviceProvider.Dispose();
        }

        [Test]
        public void AnonymousUser_IsNotAllowed_If_AllowAnonymousAttribute_IsNotPresent()
        {
            // Arrange
            this._mockDnnModuleAuthorizeAttribute.Protected().Setup("HandleUnauthorizedRequest", ItExpr.IsAny<AuthorizationFilterContext>());
            var sut = this._mockDnnModuleAuthorizeAttribute.Object;

            var fakeController = new Fakes.FakeDnnController { ModuleContext = new ModuleInstanceContext() };
            var context = CreateAuthorizationFilterContext(fakeController, allowAnonymousAtController: false, allowAnonymousAtAction: false);

            // Act
            sut.OnAuthorization(context);

            // Assert
            this._mockRepository.VerifyAll();
        }

        [Test]
        public void AnonymousUser_IsAllowed_If_AllowAnonymousAttribute_IsAtControllerLevel()
        {
            // Arrange
            var sut = this._mockDnnModuleAuthorizeAttribute.Object;

            var fakeController = new Fakes.FakeDnnController { ModuleContext = new ModuleInstanceContext() };
            var context = CreateAuthorizationFilterContext(fakeController, allowAnonymousAtController: true, allowAnonymousAtAction: false);

            // Act
            sut.OnAuthorization(context);

            // Assert
            this._mockRepository.VerifyAll();
        }

        [Test]
        public void AnonymousUser_IsAllowed_If_AllowAnonymousAttribute_IsAtActionLevel()
        {
            // Arrange
            var sut = this._mockDnnModuleAuthorizeAttribute.Object;

            var fakeController = new Fakes.FakeDnnController { ModuleContext = new ModuleInstanceContext() };
            var context = CreateAuthorizationFilterContext(fakeController, allowAnonymousAtController: false, allowAnonymousAtAction: true);

            // Act
            sut.OnAuthorization(context);

            // Assert
            // Assert.IsTrue(a.IsAuthorized);
            this._mockRepository.VerifyAll();
        }

        [Test]
        public void RegisteredUser_IsAllowed_IfHasModuleAccess()
        {
            // Arrange
            var sut = this._mockDnnModuleAuthorizeAttribute.Object;
            this._mockDnnModuleAuthorizeAttribute.Protected().Setup<bool>("HasModuleAccess").Returns(true);
            this._mockDnnModuleAuthorizeAttribute.Protected().Setup("HandleAuthorizedRequest", ItExpr.IsAny<AuthorizationFilterContext>());

            var moduleContext = new ModuleInstanceContext();
            moduleContext.Configuration = new ModuleInfo();
            var fakeController = new Fakes.FakeDnnController { ModuleContext = moduleContext };
            var context = CreateAuthorizationFilterContext(fakeController, allowAnonymousAtController: false, allowAnonymousAtAction: false);

            // Act
            sut.OnAuthorization(context);

            // Assert
            this._mockRepository.VerifyAll();
        }

        [Test]
        public void RegisteredUser_IsDenied_IfHasNoModuleAccess()
        {
            // Arrange
            var sut = this._mockDnnModuleAuthorizeAttribute.Object;
            this._mockDnnModuleAuthorizeAttribute.Protected().Setup<bool>("HasModuleAccess").Returns(false);
            this._mockDnnModuleAuthorizeAttribute.Protected().Setup("HandleUnauthorizedRequest", ItExpr.IsAny<AuthorizationFilterContext>());

            var moduleContext = new ModuleInstanceContext();
            moduleContext.Configuration = new ModuleInfo();
            var fakeController = new Fakes.FakeDnnController { ModuleContext = moduleContext };
            var context = CreateAuthorizationFilterContext(fakeController, allowAnonymousAtController: false, allowAnonymousAtAction: false);

            // Act
            sut.OnAuthorization(context);

            // Assert
            this._mockRepository.VerifyAll();
        }

        /// <summary>
        /// Builds the ASP.NET Core replacement of the MVC 5
        /// MVC 5 controller-context/request-context pairing +
        /// <c>Mock&lt;ActionDescriptor&gt;</c>.<c>IsDefined(typeof(AllowAnonymousAttribute), true)</c> arrangement:
        /// the executing DNN controller is published on the request (where
        /// <see cref="ModuleApplication.GetCurrentDnnController(HttpContext)"/> reads it) and
        /// <c>[AllowAnonymous]</c> surfaces as <see cref="IAllowAnonymous"/> metadata.
        /// </summary>
        /// <param name="controller">The executing DNN module controller.</param>
        /// <param name="allowAnonymousAtController">Whether <c>[AllowAnonymous]</c> is applied at the controller level (endpoint metadata of the request).</param>
        /// <param name="allowAnonymousAtAction">Whether <c>[AllowAnonymous]</c> is applied at the action level (<see cref="ActionDescriptor.EndpointMetadata"/>).</param>
        /// <returns>The authorization filter context to run the filter against.</returns>
        private static AuthorizationFilterContext CreateAuthorizationFilterContext(
            object controller,
            bool allowAnonymousAtController,
            bool allowAnonymousAtAction)
        {
            var httpContext = MockHelper.CreateMockHttpContext();
            httpContext.Items[ModuleApplication.CurrentControllerItemKey] = controller;

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
    }
}
