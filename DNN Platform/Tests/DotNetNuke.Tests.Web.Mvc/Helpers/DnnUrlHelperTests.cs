// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// ReSharper disable ObjectCreationAsStatement
namespace DotNetNuke.Tests.Web.Mvc.Helpers
{
    using System;

    using DotNetNuke.ComponentModel;
    using DotNetNuke.Tests.Utilities.Fakes;
    using DotNetNuke.Tests.Utilities.Mocks;
    using DotNetNuke.Tests.Web.Mvc.Fakes;
    using DotNetNuke.UI.Modules;
    using DotNetNuke.Web.Mvc.Framework.Controllers;
    using DotNetNuke.Web.Mvc.Framework.Modules;
    using DotNetNuke.Web.Mvc.Helpers;
    using DotNetNuke.Web.Mvc.Routing;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.AspNetCore.Mvc.Routing;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;

    using Moq;

    using NUnit.Framework;

    /// <summary>
    /// Tests <see cref="DnnUrlHelper"/> against the ASP.NET Core API.
    /// </summary>
    /// <remarks>
    /// The MVC 5 request-context constructor overload is now
    /// <see cref="DnnUrlHelper(ActionContext, IDnnController)"/>, and the view-context overload resolves
    /// the controller through <see cref="ModuleApplication.GetCurrentDnnController"/> (that is,
    /// <c>HttpContext.Items</c>) because the Core <see cref="ViewContext"/> no longer carries the
    /// controller instance. <c>HttpRequest.RawUrl</c> has no Core equivalent, so <c>Action()</c>
    /// reconstructs the URL of the current request from the request path and query string.
    /// </remarks>
    [TestFixture]
    public class DnnUrlHelperTests
    {
        private FakeServiceProvider serviceProvider;

        [SetUp]
        public void SetUp()
        {
            MockComponentProvider.ResetContainer();

            // The Core DnnUrlHelper builds its IUrlHelper through IUrlHelperFactory, resolved from the
            // request services or, as here, from Globals.GetCurrentServiceProvider().
            this.serviceProvider = FakeServiceProvider.Setup(
                services => services.AddSingleton<IUrlHelperFactory, UrlHelperFactory>());
        }

        [TearDown]
        public void TearDown()
        {
            // Drop any ModuleRoutingProvider registered by a test so the fixture stays order independent.
            ComponentFactory.Container = null;
            MockComponentProvider.ResetContainer();
            this.serviceProvider.Dispose();
        }

        [Test]
        public void Constructor_Throws_On_Null_ViewContext()
        {
            // Act,Assert
            Assert.Throws<ArgumentNullException>(() => new DnnUrlHelper((ViewContext)null));
        }

        [Test]
        public void Constructor_Throws_On_Null_RequestContext()
        {
            // Act,Assert
            Assert.Throws<ArgumentNullException>(() => new DnnUrlHelper((ActionContext)null, new Mock<IDnnController>().Object));
        }

        [Test]
        public void Constructor_Throws_On_Null_Controller()
        {
            // Act,Assert
            Assert.Throws<ArgumentNullException>(() => new DnnUrlHelper(MockHelper.CreateMockControllerContext(), null));
        }

        [Test]
        public void Constructor_Throws_On_Invalid_Controller_Property()
        {
            // Arrange
            // A controller which is not an IDnnController cannot be resolved by the view-context constructor.
            var controllerContext = MockHelper.CreateMockControllerContext(new FakeController());
            var viewContext = MockHelper.CreateViewContext(controllerContext);

            // Act,Assert
            Assert.Throws<InvalidOperationException>(() => new DnnUrlHelper(viewContext));
        }

        [Test]
        public void ViewContext_Constructor_Sets_ModuleContext_Property()
        {
            // Arrange
            var expectedContext = new ModuleInstanceContext();
            var mockDnnController = new Mock<IDnnController>();
            mockDnnController.Setup(c => c.ModuleContext).Returns(expectedContext);

            var controllerContext = MockHelper.CreateMockControllerContext(mockDnnController.Object);
            var viewContext = MockHelper.CreateViewContext(controllerContext);

            // Act
            var helper = new DnnUrlHelper(viewContext);

            // Assert
            Assert.That(helper.ModuleContext, Is.EqualTo(expectedContext));
        }

        [Test]
        public void RequestContext_Constructor_Sets_ModuleContext_Property()
        {
            // Arrange
            var expectedContext = new ModuleInstanceContext();

            var mockController = new Mock<IDnnController>();
            mockController.SetupGet(c => c.ModuleContext)
                          .Returns(expectedContext);

            var actionContext = MockHelper.CreateMockControllerContext();

            // Act
            var helper = new DnnUrlHelper(actionContext, mockController.Object);

            // Assert
            Assert.That(helper, Is.Not.Null);
            Assert.That(helper.ModuleContext, Is.EqualTo(expectedContext));
        }

        [Test]
        public void Action_Method_ViewContext_RetrievesRawUrl()
        {
            // Arrange
            var expectedContext = new ModuleInstanceContext();
            var rawUrl = "http://base.url/page/path?key=value";
            var expectedUrl = "/page/path?key=value";
            var helper = ArrangeHelper(expectedContext, rawUrl);

            // Act
            var result = helper.Action();

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.EqualTo(expectedUrl));
        }

        [Test]
        public void Action_Method_RequestContext_RetrievesRawUrl()
        {
            // Arrange
            var expectedContext = new ModuleInstanceContext();
            var rawUrl = "http://base.url/page/path?key=value";
            var expectedUrl = "/page/path?key=value";
            var helper = ArrangeHelper(expectedContext, rawUrl, false);

            // Act
            var result = helper.Action();

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.EqualTo(expectedUrl));
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
            // Content() goes through IUrlHelper.Content, which resolves "~/" against the request path base.
            var context = new ModuleInstanceContext();
            var helper = ArrangeHelper(context, "http://foo.com/foo", pathBase: "/foo");
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

        /// <summary>Builds a <see cref="DnnUrlHelper"/> over a real ASP.NET Core context.</summary>
        /// <param name="expectedContext">The module context the DNN controller reports.</param>
        /// <param name="url">The absolute URL of the request.</param>
        /// <param name="isViewContext">
        /// <see langword="true"/> to use the <see cref="ViewContext"/> constructor (which resolves the
        /// controller from <c>HttpContext.Items</c>), <see langword="false"/> to use the
        /// <see cref="ActionContext"/> constructor.
        /// </param>
        /// <param name="pathBase">The application path base of the request, when the test needs one.</param>
        /// <returns>The URL helper under test.</returns>
        private static DnnUrlHelper ArrangeHelper(ModuleInstanceContext expectedContext, string url = null, bool isViewContext = true, string pathBase = null)
        {
            var mockDnnController = new Mock<IDnnController>();

            var routeData = new RouteData();
            routeData.Values["controller"] = "bar";
            routeData.Values["action"] = "foo";

            var httpContext = MockHelper.CreateMockHttpContext(url ?? "http://base/");
            if (pathBase is not null)
            {
                httpContext.Request.PathBase = new PathString(pathBase);
                httpContext.Request.Path = PathString.Empty;
            }

            var controllerContext = MockHelper.CreateMockControllerContext(httpContext, routeData, mockDnnController.Object);

            mockDnnController.Setup(c => c.ModuleContext).Returns(expectedContext);
            mockDnnController.Setup(c => c.ControllerContext).Returns(controllerContext);

            return isViewContext
                ? new DnnUrlHelper(MockHelper.CreateViewContext(controllerContext))
                : new DnnUrlHelper(controllerContext, mockDnnController.Object);
        }
    }
}
