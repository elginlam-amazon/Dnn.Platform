// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Mvc.Framework.Controllers
{
    using DotNetNuke.Tests.Web.Mvc.Fakes;
    using DotNetNuke.Web.Mvc.Framework.ActionResults;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Abstractions;
    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.AspNetCore.Routing;

    using NUnit.Framework;

    [TestFixture]
    public class DnnControllerTests
    {
        [Test]
        public void ActivePage_Property_Is_Null_If_PortalSettings_Not_Set_In_Context()
        {
            // Arrange
            var httpContext = MockHelper.CreateMockHttpContext();

            // Act
            var controller = this.SetupController(httpContext);

            // Assert
            Assert.That(controller.ActivePage, Is.Null);
        }

        [Test]
        public void PortalSettings_Property_Is_Null_If_Not_Set_In_Context()
        {
            // Arrange
            var httpContext = MockHelper.CreateMockHttpContext();

            // Act
            var controller = this.SetupController(httpContext);

            // Assert
            Assert.That(controller.PortalSettings, Is.Null);
        }

        [Test]
        public void ResultOfLastExecute_Returns_ViewResult()
        {
            // Arrange
            var httpContext = MockHelper.CreateMockHttpContext();

            // Act
            var controller = this.SetupController(httpContext);
            controller.Action1();

            // Assert
            Assert.That(controller.ResultOfLastExecute, Is.Not.Null);
            Assert.That(controller.ResultOfLastExecute, Is.InstanceOf<DnnViewResult>());
        }

        [Test]
        public void User_Property_Is_Null_If_PortalSettings_Not_Set_In_Context()
        {
            // Arrange
            var httpContext = MockHelper.CreateMockHttpContext();

            // Act
            var controller = this.SetupController(httpContext);

            // Assert
            Assert.That(controller.User, Is.Null);
        }

        [Test]
        public void View_Returns_DnnViewResult()
        {
            // Arrange
            var httpContext = MockHelper.CreateMockHttpContext();

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
            var httpContext = MockHelper.CreateMockHttpContext();

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
            var httpContext = MockHelper.CreateMockHttpContext();

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
            var httpContext = MockHelper.CreateMockHttpContext();

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
            var httpContext = MockHelper.CreateMockHttpContext();

            // Act
            var controller = this.SetupController(httpContext);
            var viewResult = controller.Action3(dog);

            // Assert
            var dnnViewResult = viewResult as DnnViewResult;
            Assert.That(dnnViewResult, Is.Not.Null);
            Assert.That(dnnViewResult.ViewData.Model, Is.EqualTo(dog));
        }

        [Test]
        public void Initialize_CreatesInstance_Of_DnnUrlHelper()
        {
            // Arrange
            var httpContext = MockHelper.CreateMockHttpContext();

            // Act
            var controller = this.SetupController(httpContext);
            controller.MockInitialize();

            // Assert
            Assert.That(controller.Url, Is.Not.Null);
        }

        private FakeDnnController SetupController(HttpContext context)
        {
            var controller = new FakeDnnController();
            var actionContext = new ActionContext(context, new RouteData(), new ControllerActionDescriptor());
            controller.ControllerContext = new ControllerContext(actionContext);
            return controller;
        }
    }
}
