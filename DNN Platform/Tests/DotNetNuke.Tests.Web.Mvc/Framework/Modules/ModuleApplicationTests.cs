// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Tests.Web.Mvc.Framework.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Tests.Utilities.Fakes;
    using DotNetNuke.UI.Modules;
    using DotNetNuke.Web.Mvc.Framework.Controllers;
    using DotNetNuke.Web.Mvc.Framework.Modules;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Abstractions;
    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.AspNetCore.Mvc.Infrastructure;
    using Microsoft.AspNetCore.Routing;

    using Microsoft.Extensions.DependencyInjection;

    using Moq;

    using NUnit.Framework;

    [TestFixture]
    public class ModuleApplicationTests
    {
        private const string ActionName = "Action";
        private const string ControllerName = "Controller";

        private FakeServiceProvider serviceProvider;

        /// <summary>The controller instance the fake action invoker runs through the capturing filter.</summary>
        private object executingController;

        /// <summary>The exception the fake action invoker throws, when any.</summary>
        private Exception invokeException;

        /// <summary>The action contexts the fake action invoker was created for.</summary>
        private List<ActionContext> invokedActionContexts;

        [SetUp]
        public void Setup()
        {
            this.executingController = null;
            this.invokeException = null;
            this.invokedActionContexts = new List<ActionContext>();

            // ModuleApplication.ExecuteRequestAsync resolves the controller factory, the action descriptor
            // collection and the action invoker factory from the request services (falling back to the DNN
            // service provider), so all three have to be registered to keep these tests unit tests.
            this.serviceProvider = FakeServiceProvider.Setup(
                services =>
                {
                    services.AddSingleton(Mock.Of<IControllerFactory>());
                    services.AddSingleton<IActionDescriptorCollectionProvider>(new FakeActionDescriptorCollectionProvider());
                    services.AddSingleton<IActionInvokerFactory>(new FakeActionInvokerFactory(this));
                });
        }

        [TearDown]
        public void TearDown()
        {
            this.serviceProvider.Dispose();
        }

        [Test]
        public async Task Init_Is_Called_In_First_ExecuteRequest_And_Not_In_Subsequent_Requests()
        {
            // Arrange
            int initCounter = 0;
            var app = new InitCountingModuleApplication(() => initCounter++);

            var controller = new Mock<IDnnController>();

            var controllerFactory = this.SetupControllerFactory(controller.Object);
            app.ControllerFactory = controllerFactory.Object;

            // Act
            await app.ExecuteRequest(CreateModuleContext(ControllerName, ActionName));
            await app.ExecuteRequest(CreateModuleContext(ControllerName, ActionName));

            // Assert
            Assert.That(initCounter, Is.EqualTo(1));
        }

        [Test]
        public async Task ExecuteRequest_Calls_ControllerFactory_To_Construct_Controller()
        {
            // Arrange
            var app = new ModuleApplication();

            var controller = new Mock<IDnnController>();
            this.executingController = controller.Object;

            var controllerFactory = new Mock<IControllerFactory>();
            ControllerContext actualControllerContext = null;
            controllerFactory.Setup(f => f.CreateController(It.IsAny<ControllerContext>()))
                                 .Callback<ControllerContext>(c => actualControllerContext = c)
                                 .Returns(controller.Object);

            app.ControllerFactory = controllerFactory.Object;

            ModuleRequestContext moduleRequestContext = CreateModuleContext(ControllerName, ActionName);

            // Act
            await app.ExecuteRequest(moduleRequestContext);

            // Assert
            controllerFactory.Verify(f => f.CreateController(It.IsAny<ControllerContext>()));
            Assert.That(actualControllerContext.HttpContext, Is.SameAs(moduleRequestContext.HttpContext));
        }

        [Test]
        public void ExecuteRequest_Throws_InvalidOperationException_If_Controller_Only_Implements_IController()
        {
            // Arrange
            var app = new ModuleApplication();

            // ASP.NET Core has no IController: a controller which does not implement IDnnController is the
            // equivalent of the legacy "only implements IController" case.
            var controller = new Mock<Controller>();
            var controllerFactory = this.SetupControllerFactory(controller.Object);
            app.ControllerFactory = controllerFactory.Object;

            ModuleRequestContext moduleRequestContext = CreateModuleContext(ControllerName, ActionName);

            // Act and Assert
            Assert.ThrowsAsync<InvalidOperationException>(() => app.ExecuteRequest(moduleRequestContext));
        }

        [Test]
        public void ExecuteRequest_Throws_InvalidOperationException_If_Controller_Has_NonStandard_Action_Invoker()
        {
            // Arrange
            var app = new ModuleApplication();

            // The legacy guard rejected a controller whose ActionInvoker was not the DNN result capturing
            // invoker. In ASP.NET Core the capturing invoker is a filter the module host always attaches,
            // and the equivalent guard rejects a controller which cannot surrender its result, i.e. one
            // which does not implement IDnnController ("Could Not Construct Controller").
            var controller = new Mock<ControllerBase>();
            var controllerFactory = this.SetupControllerFactory(controller.Object);
            app.ControllerFactory = controllerFactory.Object;

            ModuleRequestContext moduleRequestContext = CreateModuleContext(ControllerName, ActionName);

            // Act and Assert
            Assert.ThrowsAsync<InvalidOperationException>(() => app.ExecuteRequest(moduleRequestContext));
        }

        [Test]
        public void ExecuteRequest_Does_Not_Throw_If_Controller_Implements_IDnnController()
        {
            // Arrange
            var app = new ModuleApplication();

            var controller = new Mock<IDnnController>();

            var controllerFactory = this.SetupControllerFactory(controller.Object);
            app.ControllerFactory = controllerFactory.Object;

            ModuleRequestContext moduleRequestContext = CreateModuleContext(ControllerName, ActionName);

            // Act and Assert
            Assert.DoesNotThrowAsync(() => app.ExecuteRequest(moduleRequestContext));
        }

        [Test]
        public async Task ExecuteRequest_Returns_Result_And_ControllerContext_From_Controller()
        {
            // Arrange
            var app = new ModuleApplication();

            ControllerContext controllerContext = MockHelper.CreateMockControllerContext();
            IActionResult actionResult = new Mock<IActionResult>().Object;

            var controller = SetupMockController(actionResult, controllerContext);

            var controllerFactory = this.SetupControllerFactory(controller.Object);
            app.ControllerFactory = controllerFactory.Object;

            ModuleRequestContext moduleRequestContext = CreateModuleContext(ControllerName, ActionName);

            // Act
            ModuleRequestResult result = await app.ExecuteRequest(moduleRequestContext);

            using (Assert.EnterMultipleScope())
            {
                // Assert
                Assert.That(result.ActionResult, Is.SameAs(actionResult));
                Assert.That(result.ActionContext, Is.SameAs(controllerContext));
            }
        }

        [Test]
        public async Task ExecuteRequest_Executes_Constructed_Controller_And_Provides_RequestContext()
        {
            // Arrange
            var app = new ModuleApplication();

            var controller = new Mock<IDnnController>();

            var controllerFactory = this.SetupControllerFactory(controller.Object);
            app.ControllerFactory = controllerFactory.Object;

            ModuleRequestContext moduleRequestContext = CreateModuleContext(ControllerName, ActionName);

            // Act
            await app.ExecuteRequest(moduleRequestContext);

            // Assert
            // ASP.NET Core executes the controller through IActionInvokerFactory/IActionInvoker rather than
            // through the MVC 5 IController.Execute entry point; ActionContext replaces the old request context.
            Assert.That(
                this.invokedActionContexts.Any(ac =>
                    ac.HttpContext == moduleRequestContext.HttpContext &&
                    (string)ac.RouteData.Values["controller"] == ControllerName),
                Is.True);
        }

        [Test]
        public async Task ExecuteRequest_ReleasesController_After_Executing()
        {
            // Arrange
            var app = new ModuleApplication();

            var controller = new Mock<IDnnController>();

            var controllerFactory = this.SetupControllerFactory(controller.Object);
            app.ControllerFactory = controllerFactory.Object;

            ModuleRequestContext moduleRequestContext = CreateModuleContext(ControllerName, ActionName);

            // Act
            await app.ExecuteRequest(moduleRequestContext);

            // Assert
            controllerFactory.Verify(cf => cf.ReleaseController(It.IsAny<ControllerContext>(), controller.Object));
        }

        [Test]
        public void ExecuteRequest_ReleasesController_Even_If_It_Throws_An_Exception()
        {
            // Arrange
            var app = new ModuleApplication();

            var controller = new Mock<IDnnController>();

            var controllerFactory = this.SetupControllerFactory(controller.Object);
            app.ControllerFactory = controllerFactory.Object;

            this.invokeException = new Exception("Uh Oh!");

            ModuleRequestContext moduleRequestContext = CreateModuleContext(ControllerName, ActionName);

            // Act (and verify the exception is thrown; also supresses the exception so it doesn't fail the test)
            Assert.ThrowsAsync<Exception>(() => app.ExecuteRequest(moduleRequestContext));

            // Assert
            controllerFactory.Verify(f => f.ReleaseController(It.IsAny<ControllerContext>(), controller.Object));
        }

        private static Mock<IDnnController> SetupMockController(IActionResult actionResult, ControllerContext controllerContext)
        {
            var mockController = new Mock<IDnnController>();
            mockController.Setup(c => c.ResultOfLastExecute)
                .Returns(actionResult);
            mockController.Setup(c => c.ControllerContext)
                .Returns(controllerContext);
            return mockController;
        }

        private static ModuleRequestContext CreateModuleContext(string controllerName, string actionName)
        {
            var routeData = new RouteData();
            routeData.Values.Add("controller", controllerName);
            routeData.Values.Add("action", actionName);

            var moduleContext = new ModuleInstanceContext { Configuration = new ModuleInfo { ModuleID = 42 } };
            return new ModuleRequestContext
            {
                HttpContext = MockHelper.CreateMockHttpContext("http://localhost/Portal/Page/ModuleRoute"),
                RouteData = routeData,
                ModuleContext = moduleContext,
            };
        }

        private Mock<IControllerFactory> SetupControllerFactory(object controller)
        {
            this.executingController = controller;
            var controllerFactory = new Mock<IControllerFactory>();
            controllerFactory.Setup(cf => cf.CreateController(It.IsAny<ControllerContext>()))
                            .Returns(controller);
            return controllerFactory;
        }

        /// <summary>A module application which counts (and replaces) its initialization.</summary>
        /// <remarks>
        /// The net48 test mocked the virtual <c>Init</c> method. <c>Init</c> is
        /// <c>protected internal virtual</c> on the ported application, so a test double subclass
        /// records the calls instead.
        /// </remarks>
        private sealed class InitCountingModuleApplication : ModuleApplication
        {
            private readonly Action onInit;

            public InitCountingModuleApplication(Action onInit)
            {
                this.onInit = onInit;
            }

            protected internal override void Init()
            {
                this.onInit();
            }
        }

        /// <summary>Provides the single action descriptor these tests execute against.</summary>
        private sealed class FakeActionDescriptorCollectionProvider : IActionDescriptorCollectionProvider
        {
            private readonly ActionDescriptorCollection actions = new ActionDescriptorCollection(
                new List<ActionDescriptor>
                {
                    new ControllerActionDescriptor { ControllerName = ControllerName, ActionName = ActionName },
                },
                0);

            public ActionDescriptorCollection ActionDescriptors => this.actions;
        }

        /// <summary>Creates the <see cref="FakeActionInvoker"/> which stands in for the MVC pipeline.</summary>
        private sealed class FakeActionInvokerFactory : IActionInvokerFactory
        {
            private readonly ModuleApplicationTests owner;

            public FakeActionInvokerFactory(ModuleApplicationTests owner)
            {
                this.owner = owner;
            }

            public IActionInvoker CreateInvoker(ActionContext actionContext)
            {
                this.owner.invokedActionContexts.Add(actionContext);
                return new FakeActionInvoker(this.owner, actionContext);
            }
        }

        /// <summary>
        /// Stands in for the MVC action invoker: it runs the module's controller through the capturing
        /// filter the way the real pipeline does, so that the module application can collect the result.
        /// </summary>
        private sealed class FakeActionInvoker : IActionInvoker
        {
            private readonly ModuleApplicationTests owner;
            private readonly ActionContext actionContext;

            public FakeActionInvoker(ModuleApplicationTests owner, ActionContext actionContext)
            {
                this.owner = owner;
                this.actionContext = actionContext;
            }

            public async Task InvokeAsync()
            {
                if (this.owner.invokeException is not null)
                {
                    throw this.owner.invokeException;
                }

                var capturingFilter = ResultCapturingActionInvoker.GetCurrent(this.actionContext.HttpContext);
                var controller = this.owner.executingController;
                if (capturingFilter is null || controller is null)
                {
                    return;
                }

                var filters = new List<IFilterMetadata>();
                var executingContext = new ActionExecutingContext(
                    this.actionContext,
                    filters,
                    new Dictionary<string, object>(),
                    controller);

                await capturingFilter.OnActionExecutionAsync(
                    executingContext,
                    () => Task.FromResult(new ActionExecutedContext(this.actionContext, filters, controller)));
            }
        }
    }
}
