// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// ReSharper disable ObjectCreationAsStatement
namespace DotNetNuke.Tests.Web.Mvc.Helpers
{
    using System;

    using DotNetNuke.ComponentModel;
    using DotNetNuke.Tests.Utilities.Mocks;
    using DotNetNuke.UI.Modules;
    using DotNetNuke.Web.Mvc.Framework.Controllers;
    using DotNetNuke.Web.Mvc.Helpers;
    using DotNetNuke.Web.Mvc.Routing;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Abstractions;
    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
    using Microsoft.AspNetCore.Routing;

    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class DnnUrlHelperTests
    {
        [SetUp]
        public void SetUp()
        {
            MockComponentProvider.ResetContainer();
        }

        [Test]
        public void Constructor_Throws_On_Null_ViewContext()
        {
            // Act,Assert
            Assert.Throws<ArgumentNullException>(() => new DnnUrlHelper((ViewContext)null));
        }

        [Test]
        public void Constructor_Throws_On_Null_Controller()
        {
            // Act,Assert
            Assert.Throws<ArgumentNullException>(() => new DnnUrlHelper((IDnnController)null));
        }

        [Test]
        public void ViewContext_Constructor_Sets_ModuleContext_Property()
        {
            // Arrange
            var expectedContext = new ModuleInstanceContext();
            var mockController = new Mock<IDnnController>();
            mockController.Setup(c => c.ModuleContext).Returns(expectedContext);

            var httpContext = new DefaultHttpContext();
            httpContext.Items[typeof(IDnnController)] = mockController.Object;
            var viewContext = new ViewContext { HttpContext = httpContext };

            // Act
            var helper = new DnnUrlHelper(viewContext);

            // Assert
            Assert.That(helper.ModuleContext, Is.EqualTo(expectedContext));
        }

        [Test]
        public void Controller_Constructor_Sets_ModuleContext_Property()
        {
            // Arrange
            var expectedContext = new ModuleInstanceContext();

            var mockController = new Mock<IDnnController>();
            mockController.SetupGet(c => c.ModuleContext).Returns(expectedContext);

            // Act
            var helper = new DnnUrlHelper(mockController.Object);

            // Assert
            Assert.That(helper, Is.Not.Null);
            Assert.That(helper.ModuleContext, Is.EqualTo(expectedContext));
        }

        [Test]
        public void Action_Method_ViewContext_RetrievesRawUrl()
        {
            // Arrange
            var expectedContext = new ModuleInstanceContext();
            var rawUrl = "http://base.url/foo?id=5";
            var helper = ArrangeHelper(expectedContext, rawUrl);

            // Act
            var result = helper.Action();

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.EqualTo("/foo?id=5"));
        }

        [Test]
        public void Action_Method_Controller_RetrievesRawUrl()
        {
            // Arrange
            var expectedContext = new ModuleInstanceContext();
            var rawUrl = "http://base.url/foo?id=5";
            var helper = ArrangeHelper(expectedContext, rawUrl, false);

            // Act
            var result = helper.Action();

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.EqualTo("/foo?id=5"));
        }

        [Test]
        public void Action_Method_Calls_ModuleRouteProvider()
        {
            // Arrange
            var expectedContext = new ModuleInstanceContext();
            var helper = ArrangeHelper(expectedContext);

            var mockRouteProvider = new Mock<ModuleRoutingProvider>();
            ComponentFactory.RegisterComponentInstance<ModuleRoutingProvider>(mockRouteProvider.Object);

            // Act
            helper.Action("foo");

            // Assert
            mockRouteProvider.Verify(p => p.GenerateUrl(It.IsAny<RouteValueDictionary>(), expectedContext));
        }

        [Test]
        public void Action_Overload_1__Method_Calls_ModuleRouteProvider()
        {
            // Arrange
            var expectedContext = new ModuleInstanceContext();
            var helper = ArrangeHelper(expectedContext);

            var mockRouteProvider = new Mock<ModuleRoutingProvider>();
            ComponentFactory.RegisterComponentInstance<ModuleRoutingProvider>(mockRouteProvider.Object);

            // Act
            helper.Action("foo", new RouteValueDictionary());

            // Assert
            mockRouteProvider.Verify(p => p.GenerateUrl(It.IsAny<RouteValueDictionary>(), expectedContext));
        }

        [Test]
        public void Action_Overload_2_Method_Calls_ModuleRouteProvider()
        {
            // Arrange
            var expectedContext = new ModuleInstanceContext();
            var helper = ArrangeHelper(expectedContext);

            var mockRouteProvider = new Mock<ModuleRoutingProvider>();
            ComponentFactory.RegisterComponentInstance<ModuleRoutingProvider>(mockRouteProvider.Object);

            // Act
            helper.Action("foo", "bar");

            // Assert
            mockRouteProvider.Verify(p => p.GenerateUrl(It.IsAny<RouteValueDictionary>(), expectedContext));
        }

        [Test]
        public void Action_Overload_3_Method_Calls_ModuleRouteProvider()
        {
            // Arrange
            var expectedContext = new ModuleInstanceContext();
            var helper = ArrangeHelper(expectedContext);

            var mockRouteProvider = new Mock<ModuleRoutingProvider>();
            ComponentFactory.RegisterComponentInstance<ModuleRoutingProvider>(mockRouteProvider.Object);

            // Act
            helper.Action("foo", "bar", new RouteValueDictionary());

            // Assert
            mockRouteProvider.Verify(p => p.GenerateUrl(It.IsAny<RouteValueDictionary>(), expectedContext));
        }

        [Test]
        public void Action_Overload_4_Method_Calls_ModuleRouteProvider()
        {
            // Arrange
            var expectedContext = new ModuleInstanceContext();
            var helper = ArrangeHelper(expectedContext);

            var mockRouteProvider = new Mock<ModuleRoutingProvider>();
            ComponentFactory.RegisterComponentInstance<ModuleRoutingProvider>(mockRouteProvider.Object);

            // Act
            helper.Action("foo", new { id = 5 });

            // Assert
            mockRouteProvider.Verify(p => p.GenerateUrl(It.IsAny<RouteValueDictionary>(), expectedContext));
        }

        [Test]
        public void Action_Overload_5_Method_Calls_ModuleRouteProvider()
        {
            // Arrange
            var expectedContext = new ModuleInstanceContext();
            var helper = ArrangeHelper(expectedContext);

            var mockRouteProvider = new Mock<ModuleRoutingProvider>();
            ComponentFactory.RegisterComponentInstance<ModuleRoutingProvider>(mockRouteProvider.Object);

            // Act
            helper.Action("foo", "bar", new { id = 5 });

            // Assert
            mockRouteProvider.Verify(p => p.GenerateUrl(It.IsAny<RouteValueDictionary>(), expectedContext));
        }

        [Test]
        public void Content_Method_Calls_Returns_Correct_Url()
        {
            // Arrange
            var context = new ModuleInstanceContext();
            var helper = ArrangeHelper(context, "http://foo.com/foo");
            string expectedResult = "/foo/test.css";

            // Act
            var url = helper.Content("~/test.css");

            using (Assert.EnterMultipleScope())
            {
                // Assert
                Assert.That(url, Is.Not.Null);
                Assert.That(url, Is.EqualTo(expectedResult));
            }
        }

        [Test]
        public void IsLocalUrl_Method_Calls_Returns_Correct_Result()
        {
            // Arrange
            var context = new ModuleInstanceContext();
            var helper = ArrangeHelper(context, "http://foo.com");

            // Act
            var withOuterUrl = helper.IsLocalUrl("http://dnnsoftware.com");
            var withLocalUrl = helper.IsLocalUrl("~/foo/foo.html");

            using (Assert.EnterMultipleScope())
            {
                // Assert
                Assert.That(withOuterUrl, Is.False);
                Assert.That(withLocalUrl, Is.True);
            }
        }

        [Test]
        public void GenerateUrl_Method_Passes_Correct_RouteValueCollection_To_ModuleRouteProvider()
        {
            // Arrange
            var expectedContext = new ModuleInstanceContext();
            var helper = ArrangeHelper(expectedContext);

            RouteValueDictionary routeValues = null;
            var mockRouteProvider = new Mock<ModuleRoutingProvider>();
            mockRouteProvider.Setup(p => p.GenerateUrl(It.IsAny<RouteValueDictionary>(), expectedContext))
                                .Callback<RouteValueDictionary, ModuleInstanceContext>((r, c) => routeValues = r);

            ComponentFactory.RegisterComponentInstance<ModuleRoutingProvider>(mockRouteProvider.Object);

            // Act
            helper.Action("foo", "bar", new { id = 5 });

            using (Assert.EnterMultipleScope())
            {
                // Assert
                Assert.That(routeValues.Values, Has.Count.EqualTo(3));
                Assert.That(routeValues, Does.ContainKey("action"));
                Assert.That(routeValues, Does.ContainKey("controller"));
                Assert.That(routeValues, Does.ContainKey("id"));
                Assert.That((string)routeValues["action"], Is.EqualTo("foo"));
                Assert.That((string)routeValues["controller"], Is.EqualTo("bar"));
                Assert.That((int)routeValues["id"], Is.EqualTo(5));
            }
        }

        private static DnnUrlHelper ArrangeHelper(ModuleInstanceContext expectedContext, string url = null, bool isViewContext = true)
        {
            var routeData = new RouteData();
            routeData.Values["controller"] = "bar";
            routeData.Values["action"] = "foo";

            var httpContext = MockHelper.CreateMockHttpContext(url ?? "http://base/");
            var actionContext = new ActionContext(httpContext, routeData, new ControllerActionDescriptor());
            var controllerContext = new ControllerContext(actionContext);

            var mockController = new Mock<IDnnController>();
            mockController.Setup(c => c.ModuleContext).Returns(expectedContext);
            mockController.Setup(c => c.ControllerContext).Returns(controllerContext);

            if (isViewContext)
            {
                httpContext.Items[typeof(IDnnController)] = mockController.Object;
                var viewContext = new ViewContext
                {
                    HttpContext = httpContext,
                    RouteData = routeData,
                };
                return new DnnUrlHelper(viewContext);
            }

            return new DnnUrlHelper(mockController.Object);
        }
    }
}
