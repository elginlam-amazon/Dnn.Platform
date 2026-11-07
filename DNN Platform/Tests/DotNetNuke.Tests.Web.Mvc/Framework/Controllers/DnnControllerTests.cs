// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Mvc.Framework.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using DotNetNuke.Tests.Utilities.Fakes;
    using DotNetNuke.Tests.Web.Mvc.Fakes;
    using DotNetNuke.UI.Modules;
    using DotNetNuke.Web.Mvc.Framework.ActionResults;
    using DotNetNuke.Web.Mvc.Framework.Modules;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.AspNetCore.Mvc.ViewEngines;
    using Microsoft.AspNetCore.Routing;

    using Moq;

    using NUnit.Framework;

    [TestFixture]
    public class DnnControllerTests
    {
        private const string TestViewName = "Foo";

        private FakeServiceProvider serviceProvider;

        [SetUp]
        public void Setup()
        {
            // The Core DnnUrlHelper resolves IUrlHelperFactory from the request services,
            // falling back to Globals.GetCurrentServiceProvider().
            this.serviceProvider = FakeServiceProvider.Setup();
        }

        [TearDown]
        public void TearDown()
        {
            this.serviceProvider.Dispose();
        }

        [Test]
        public void ActivePage_Property_Is_Null_If_PortalSettings_Not_Set_In_Context()
        {
            // Arrange
            HttpContext httpContext = MockHelper.CreateMockHttpContext();

            // Act
            var controller = this.SetupController(httpContext);

            // Assert
            Assert.That(controller.ActivePage, Is.Null);
        }

        [Test]
        public void PortalSettings_Property_Is_Null_If_Not_Set_In_Context()
        {
            // Arrange
            HttpContext context = MockHelper.CreateMockHttpContext();

            // Act
            var controller = this.SetupController(context);

            // Assert
            Assert.That(controller.PortalSettings, Is.Null);
        }

        [Test]
        public async Task ResultOfLastExecute_Returns_ViewResult()
        {
            // Arrange
            HttpContext httpContext = MockHelper.CreateMockHttpContext();

            // Act
            var controller = this.SetupController(httpContext);

            // ASP.NET Core has no action invoker to call: the result of the last execution is captured by
            // the ResultCapturingActionInvoker filter, which DnnController.ResultOfLastExecute reads back
            // from the current request. Drive "Action1" through that filter the way ModuleApplication does.
            var capturingFilter = new ResultCapturingActionInvoker();
            capturingFilter.AttachTo(httpContext);
            await RunActionThroughCapturingFilter(capturingFilter, controller, controller.Action1);

            // Assert
            Assert.That(controller.ResultOfLastExecute, Is.Not.Null);
            Assert.That(controller.ResultOfLastExecute, Is.InstanceOf<DnnViewResult>());
        }

        [Test]
        public void User_Property_Is_Null_If_PortalSettings_Not_Set_In_Context()
        {
            // Arrange
            HttpContext httpContext = MockHelper.CreateMockHttpContext();

            // Act
            var controller = this.SetupController(httpContext);

            // Assert
            Assert.That(controller.User, Is.Null);
        }

        [Test]
        public void View_Returns_DnnViewResult()
        {
            // Arrange
            HttpContext httpContext = MockHelper.CreateMockHttpContext();

            // Act
            var controller = this.SetupController(httpContext);
            var viewResult = controller.Action1();

            // Assert
            Assert.That(viewResult, Is.InstanceOf<DnnViewResult>());
        }

        [Test]
        public void View_Returns_DnnViewResult_With_Correct_ViewName()
        {
            // Arrange
            HttpContext httpContext = MockHelper.CreateMockHttpContext();

            // Act
            var controller = this.SetupController(httpContext);
            var viewResult = controller.Action1();

            // Assert
            var dnnViewResult = viewResult as DnnViewResult;
            Assert.That(dnnViewResult, Is.Not.Null);
            Assert.That(dnnViewResult.ViewName, Is.EqualTo("Action1"));
        }

        [Test]
        public void View_Returns_DnnViewResult_With_Correct_MasterName()
        {
            // Arrange
            HttpContext httpContext = MockHelper.CreateMockHttpContext();

            // Act
            var controller = this.SetupController(httpContext);
            var viewResult = controller.Action2();

            // Assert
            var dnnViewResult = viewResult as DnnViewResult;
            Assert.That(dnnViewResult, Is.Not.Null);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(dnnViewResult.ViewName, Is.EqualTo("Action2"));
                Assert.That(dnnViewResult.MasterName, Is.EqualTo("Master2"));
            }
        }

        [Test]
        public void View_Returns_DnnViewResult_With_Correct_ViewData()
        {
            // Arrange
            HttpContext httpContext = MockHelper.CreateMockHttpContext();

            // Act
            var controller = this.SetupController(httpContext);
            controller.ViewData.Add("key", "value");
            var viewResult = controller.Action2();

            // Assert
            var dnnViewResult = viewResult as DnnViewResult;
            Assert.That(dnnViewResult, Is.Not.Null);
            Assert.That(dnnViewResult.ViewData["key"], Is.EqualTo("value"));
        }

        [Test]
        public void View_Returns_DnnViewResult_With_Correct_Model()
        {
            // Arrange
            var dog = new Dog() { Name = "Fluffy" };
            HttpContext httpContext = MockHelper.CreateMockHttpContext();

            // Act
            var controller = this.SetupController(httpContext);
            var viewResult = controller.Action3(dog);

            // Assert
            var dnnViewResult = viewResult as DnnViewResult;
            Assert.That(dnnViewResult, Is.Not.Null);
            Assert.That(dnnViewResult.ViewData.Model, Is.EqualTo(dog));
        }

        [Test]
        public void View_Returns_DnnViewResult_With_Correct_ViewEngines()
        {
            // Arrange
            var dog = new Dog() { Name = "Fluffy" };
            HttpContext httpContext = MockHelper.CreateMockHttpContext();

            // Act
            var controller = this.SetupController(httpContext);

            // The MVC 5 controller view-engine collection property is now IDnnController.ViewEngines,
            // which DnnController.View copies onto the result.
            controller.ViewEngines = new List<IViewEngine> { Mock.Of<IViewEngine>() };
            var viewResult = controller.Action3(dog);

            // Assert
            var dnnViewResult = viewResult as DnnViewResult;
            Assert.That(dnnViewResult, Is.Not.Null);
            Assert.That(dnnViewResult.ViewEngines, Is.EqualTo(controller.ViewEngines));
        }

        [Test]
        public void Initialize_CreatesInstance_Of_DnnUrlHelper()
        {
            // Arrange
            HttpContext httpContext = MockHelper.CreateMockHttpContext();

            // Act
            var controller = this.SetupController(httpContext);

            // The DnnUrlHelper(ActionContext, IDnnController) constructor reads the controller's
            // ModuleContext, so the controller must be initialized like the module host does.
            controller.ModuleContext = new ModuleInstanceContext();
            controller.MockInitialize(controller.ControllerContext);

            // Assert
            Assert.That(controller.Url, Is.Not.Null);
        }

        private static async Task RunActionThroughCapturingFilter(
            ResultCapturingActionInvoker capturingFilter,
            FakeDnnController controller,
            Func<IActionResult> action)
        {
            var filters = new List<IFilterMetadata>();
            var executingContext = new ActionExecutingContext(
                controller.ControllerContext,
                filters,
                new Dictionary<string, object>(),
                controller);

            await capturingFilter.OnActionExecutionAsync(
                executingContext,
                () => Task.FromResult(new ActionExecutedContext(controller.ControllerContext, filters, controller)
                {
                    Result = action(),
                }));

            var resultExecutingContext = new ResultExecutingContext(
                controller.ControllerContext,
                filters,
                capturingFilter.ResultOfLastInvoke ?? new EmptyResult(),
                controller);

            await capturingFilter.OnResultExecutionAsync(
                resultExecutingContext,
                () => Task.FromResult(new ResultExecutedContext(
                    controller.ControllerContext,
                    filters,
                    resultExecutingContext.Result,
                    controller)));
        }

        private FakeDnnController SetupController(HttpContext context)
        {
            var controller = new FakeDnnController();
            controller.ControllerContext = MockHelper.CreateMockControllerContext(context, new RouteData(), controller);
            return controller;
        }
    }
}
