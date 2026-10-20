// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Mvc.Framework.Modules
{
    using System;
    using System.Collections.Generic;

    using DotNetNuke.Entities.Modules;
    using DotNetNuke.UI.Modules;
    using DotNetNuke.Web.Mvc.Framework.Modules;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc.Abstractions;
    using Microsoft.AspNetCore.Mvc.Infrastructure;
    using Microsoft.Extensions.DependencyInjection;

    using Moq;
    using Moq.Protected;

    using NUnit.Framework;

    [TestFixture]
    public class ModuleApplicationTests
    {
        private const string ActionName = "Action";
        private const string ControllerName = "Controller";

        private ServiceProvider requestServices;

        [SetUp]
        public void Setup()
        {
            // The new ModuleApplication resolves the action-descriptor provider and the action
            // invoker factory from the request's service provider. Provide empty collections so
            // the application reaches its "action not found" guard deterministically.
            var descriptorProvider = new Mock<IActionDescriptorCollectionProvider>();
            descriptorProvider
                .SetupGet(p => p.ActionDescriptors)
                .Returns(new ActionDescriptorCollection(new List<ActionDescriptor>(), 0));

            var services = new ServiceCollection();
            services.AddSingleton(descriptorProvider.Object);
            services.AddSingleton(Mock.Of<IActionInvokerFactory>());
            this.requestServices = services.BuildServiceProvider();
        }

        [TearDown]
        public void TearDown()
        {
            this.requestServices?.Dispose();
        }

        [Test]
        public void Init_Is_Called_In_First_ExecuteRequest_And_Not_In_Subsequent_Requests()
        {
            // Arrange
            var app = new Mock<ModuleApplication> { CallBase = true };

            int initCounter = 0;
            app.Protected().Setup("Init")
                .Callback(() => initCounter++);

            // Act (no action descriptor is registered, so each request throws after initialization)
            TryExecute(() => app.Object.ExecuteRequest(this.CreateModuleContext(ControllerName, ActionName)));
            TryExecute(() => app.Object.ExecuteRequest(this.CreateModuleContext(ControllerName, ActionName)));

            // Assert
            Assert.That(initCounter, Is.EqualTo(1));
        }

        [Test]
        public void ExecuteRequest_Throws_InvalidOperationException_If_Action_Cannot_Be_Found()
        {
            // Arrange
            var app = new ModuleApplication();
            var moduleRequestContext = this.CreateModuleContext(ControllerName, ActionName);

            // Act and Assert
            Assert.Throws<InvalidOperationException>(() => app.ExecuteRequest(moduleRequestContext));
        }

        [Test]
        public void ExecuteRequest_Uses_Default_Controller_And_Action_When_RouteData_Is_Empty()
        {
            // Arrange
            var app = new ModuleApplication
            {
                DefaultControllerName = ControllerName,
                DefaultActionName = ActionName,
            };

            var moduleRequestContext = this.CreateModuleContext(null, null);

            // Act and Assert (no matching descriptor exists, so the defaults are resolved and the guard fires)
            var ex = Assert.Throws<InvalidOperationException>(() => app.ExecuteRequest(moduleRequestContext));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(ex.Message, Does.Contain(ActionName));
                Assert.That(ex.Message, Does.Contain(ControllerName));
            }
        }

        private static void TryExecute(Action action)
        {
            try
            {
                action();
            }
            catch (InvalidOperationException)
            {
                // Expected: no action descriptor is registered for these tests.
            }
        }

        private ModuleRequestContext CreateModuleContext(string controllerName, string actionName)
        {
            var routeData = new Microsoft.AspNetCore.Routing.RouteData();
            if (controllerName != null)
            {
                routeData.Values["controller"] = controllerName;
            }

            if (actionName != null)
            {
                routeData.Values["action"] = actionName;
            }

            var httpContext = MockHelper.CreateMockHttpContext("http://localhost/Portal/Page/ModuleRoute");
            httpContext.RequestServices = this.requestServices;

            var moduleContext = new ModuleInstanceContext { Configuration = new ModuleInfo { ModuleID = 42 } };
            return new ModuleRequestContext
            {
                HttpContext = httpContext,
                RouteData = routeData,
                ModuleContext = moduleContext,
            };
        }
    }
}
