// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Tests.Web.Mvc.Framework
{
    using System.Collections.Generic;
    using System.Linq;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Tests.Utilities.Fakes;
    using DotNetNuke.Web.Mvc.Framework;
    using DotNetNuke.Web.Mvc.Framework.Controllers;
    using DotNetNuke.Web.Mvc.Framework.Modules;
    using DotNetNuke.Web.Mvc.Routing;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.AspNetCore.Mvc.ViewEngines;

    using Microsoft.Extensions.DependencyInjection;

    using Moq;

    using NUnit.Framework;

    [TestFixture]
    public class ModuleDelegatingViewEngineTests
    {
        private FakeServiceProvider serviceProvider;

        [SetUp]
        public void Setup()
        {
            this.serviceProvider = FakeServiceProvider.Setup(
                services =>
                {
                    // The MVC 5 DefaultControllerFactory is gone; ModuleApplication resolves the Core
                    // Microsoft.AspNetCore.Mvc.Controllers.IControllerFactory from the service provider.
                    services.AddSingleton(Mock.Of<IControllerFactory>());
                    services.AddSingleton(Mock.Of<IHostSettings>());
                });
        }

        [TearDown]
        public void TearDown()
        {
            this.serviceProvider.Dispose();
        }

        [Test]
        public void Should_Forward_FindPartialView_To_Current_ModuleApplication_ViewEngineCollection()
        {
            // Arrange
            var mockEngine = new Mock<IViewEngine>();
            var controller = new Mock<DnnController>();
            controller.SetupAllProperties();
            var context = MockHelper.CreateMockControllerContext(controller.Object);

            const string viewName = "Foo";

            // A partial view lookup is FindView(..., isMainPage: false) in ASP.NET Core; a failed lookup
            // carries the searched locations.
            mockEngine.Setup(e => e.FindView(context, viewName, false))
                      .Returns(ViewEngineResult.NotFound(viewName, new[] { "foo", "bar", "baz" }));

            SetupMockModuleApplication(context, controller.Object, new[] { mockEngine.Object });

            var viewEngine = new ModuleDelegatingViewEngine(Mock.Of<IHostSettings>());

            // Act
            var engineResult = viewEngine.FindView(context, viewName, false);

            // Assert
            mockEngine.Verify(e => e.FindView(context, viewName, false));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(engineResult.SearchedLocations.ElementAt(0), Is.EqualTo("foo"));
                Assert.That(engineResult.SearchedLocations.ElementAt(1), Is.EqualTo("bar"));
                Assert.That(engineResult.SearchedLocations.ElementAt(2), Is.EqualTo("baz"));
            }
        }

        [Test]
        public void Should_Forward_FindView_To_Current_ModuleApplication_ViewEngineCollection()
        {
            // Arrange
            var mockEngine = new Mock<IViewEngine>();
            var controller = new Mock<DnnController>();
            controller.SetupAllProperties();
            var context = MockHelper.CreateMockControllerContext(controller.Object);

            const string viewName = "Bar";

            // The master name no longer takes part in the lookup in ASP.NET Core (a Razor view selects its
            // own layout), it only enters the view cache key.
            mockEngine.Setup(e => e.FindView(context, viewName, true))
                      .Returns(ViewEngineResult.NotFound(viewName, new[] { "foo", "bar", "baz" }));

            SetupMockModuleApplication(context, controller.Object, new[] { mockEngine.Object });

            var viewEngine = new ModuleDelegatingViewEngine(Mock.Of<IHostSettings>());

            // Act
            var engineResult = viewEngine.FindView(context, viewName, true);

            // Assert
            mockEngine.Verify(e => e.FindView(context, viewName, true));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(engineResult.SearchedLocations.ElementAt(0), Is.EqualTo("foo"));
                Assert.That(engineResult.SearchedLocations.ElementAt(1), Is.EqualTo("bar"));
                Assert.That(engineResult.SearchedLocations.ElementAt(2), Is.EqualTo("baz"));
            }
        }

        [Test]
        public void Should_Track_ViewEngine_View_Pairs_On_FindView_And_Releases_View_Appropriately()
        {
            // Arrange
            var mockEngine = new Mock<IViewEngine>();
            var mockView = new Mock<IView>();
            var controller = new Mock<DnnController>();
            controller.SetupAllProperties();
            var context = MockHelper.CreateMockControllerContext(controller.Object);

            const string viewName = "Baz";

            mockEngine.Setup(e => e.FindView(context, viewName, true))
                      .Returns(ViewEngineResult.Found(viewName, mockView.Object));

            SetupMockModuleApplication(context, controller.Object, new[] { mockEngine.Object });

            var viewEngine = new ModuleDelegatingViewEngine(Mock.Of<IHostSettings>());

            // Act
            var engineResult = viewEngine.FindView(context, viewName, true);

            // Assert
            // ASP.NET Core's IViewEngine has no ReleaseView: view engines do not own the view instances,
            // the Razor view engine caches and disposes them itself. The engine/view pairing is therefore
            // asserted on the returned result: the view of the module's own engine is handed back.
            mockEngine.Verify(e => e.FindView(context, viewName, true));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(engineResult.Success, Is.True);
                Assert.That(engineResult.View, Is.SameAs(mockView.Object));
                Assert.That(engineResult.ViewName, Is.EqualTo(viewName));
            }
        }

        [Test]
        public void Should_Track_ViewEngine_View_Pairs_On_FindPartialView_And_Releases_View_Appropriately()
        {
            // Arrange
            var mockEngine = new Mock<IViewEngine>();
            var mockView = new Mock<IView>();
            var controller = new Mock<DnnController>();
            controller.SetupAllProperties();
            var context = MockHelper.CreateMockControllerContext(controller.Object);

            const string viewName = "Qux";

            mockEngine.Setup(e => e.FindView(context, viewName, false))
                      .Returns(ViewEngineResult.Found(viewName, mockView.Object));

            SetupMockModuleApplication(context, controller.Object, new[] { mockEngine.Object });

            var viewEngine = new ModuleDelegatingViewEngine(Mock.Of<IHostSettings>());

            // Act
            var engineResult = viewEngine.FindView(context, viewName, false);

            // Assert
            // See the remark on the FindView counterpart above: ReleaseView has no Core equivalent, so the
            // engine/view pair is asserted through the returned result.
            mockEngine.Verify(e => e.FindView(context, viewName, false));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(engineResult.Success, Is.True);
                Assert.That(engineResult.View, Is.SameAs(mockView.Object));
                Assert.That(engineResult.ViewName, Is.EqualTo(viewName));
            }
        }

        [Test]
        public void Should_Return_Failed_ViewEngineResult_For_FindView_If_No_Current_Module_Application()
        {
            // Arrange
            var viewEngine = new ModuleDelegatingViewEngine(Mock.Of<IHostSettings>());
            var controller = new Mock<DnnController>();
            controller.SetupAllProperties();
            var context = MockHelper.CreateMockControllerContext(controller.Object);

            // Act
            var engineResult = viewEngine.FindView(context, "Foo", true);

            // Assert
            Assert.That(engineResult, Is.Not.Null);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(engineResult.View, Is.Null);
                Assert.That(engineResult.SearchedLocations.Count(), Is.EqualTo(0));
            }
        }

        [Test]
        public void Should_Return_Failed_ViewEngineResult_For_FindPartialView_If_No_Current_Module_Application()
        {
            // Arrange
            var viewEngine = new ModuleDelegatingViewEngine(Mock.Of<IHostSettings>());
            var controller = new Mock<DnnController>();
            controller.SetupAllProperties();
            var context = MockHelper.CreateMockControllerContext(controller.Object);

            // Act
            var engineResult = viewEngine.FindView(context, "Foo", false);

            // Assert
            Assert.That(engineResult, Is.Not.Null);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(engineResult.View, Is.Null);
                Assert.That(engineResult.SearchedLocations.Count(), Is.EqualTo(0));
            }
        }

        private static void SetupMockModuleApplication(ControllerContext context, IDnnController controller, IReadOnlyList<IViewEngine> engines)
        {
            // The MVC 5 view-engine collection has no Core equivalent: the module's engines are an ordered
            // IReadOnlyList<IViewEngine> carried by the module application and by its controller, which is
            // where ModuleDelegatingViewEngine reads them from (through the HttpContext).
            var mockApp = new Mock<ModuleApplication>();
            mockApp.Object.ViewEngines = new List<IViewEngine>(engines);
            controller.ViewEngines = engines;

            var activeModuleRequest = new ModuleRequestResult
            {
                ModuleApplication = mockApp.Object,
                ActionContext = context,
            };

            context.HttpContext.SetModuleRequestResult(activeModuleRequest);
        }
    }
}
