// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Mvc.Helpers
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Tests.Utilities.Fakes;
    using DotNetNuke.Web.Mvc.Framework.ActionResults;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using Microsoft.AspNetCore.Mvc.ViewEngines;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
    using Microsoft.AspNetCore.Routing;

    using Microsoft.Extensions.DependencyInjection;

    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class DnnViewResultTests
    {
        private FakeServiceProvider serviceProvider;

        [SetUp]
        public void Setup()
        {
            // The view lookup goes through ViewEngineCollectionExt, which caches through
            // CBO.GetCachedObject(IHostSettings, ...), and the renderer resolves the view data and temp
            // data services from DI. ViewDataDictionary builds a ModelExplorer from
            // IModelMetadataProvider, so a real provider is registered (an auto-mocked one returns null
            // metadata and the dictionary cannot be constructed).
            this.serviceProvider = FakeServiceProvider.Setup(
                services =>
                {
                    services.AddSingleton(Mock.Of<IHostSettings>());
                    services.AddSingleton<IModelMetadataProvider>(new EmptyModelMetadataProvider());
                    services.AddSingleton(Mock.Of<ITempDataProvider>());
                });
        }

        [TearDown]
        public void TearDown()
        {
            this.serviceProvider.Dispose();
        }

        [Test]
        public void ExecuteResult_Throws_With_Null_ControllerContext()
        {
            // Arrange
            var result = new DnnViewResult();

            // Act, Assert
            Assert.ThrowsAsync<ArgumentNullException>(() => result.ExecuteResultAsync(null, new StringWriter()));
        }

        [Test]
        public void ExecuteResult_Throws_With_Null_TextWriter()
        {
            // Arrange
            var result = new DnnViewResult();
            var context = MockHelper.CreateMockControllerContext();

            // Act, Assert
            Assert.ThrowsAsync<ArgumentNullException>(() => result.ExecuteResultAsync(context, null));
        }

        [Test]
        public void ExecuteResult_Throws_With_Empty_ViewName_And_No_Action()
        {
            // Arrange
            var result = new DnnViewResult();

            // The Core implementation falls back to the "action" route value when no view name is set, so
            // the context carries no action and the view cannot be resolved.
            var context = MockHelper.CreateMockControllerContext();

            // Act, Assert
            Assert.ThrowsAsync<InvalidOperationException>(() => result.ExecuteResultAsync(context, new StringWriter()));
        }

        [Test]
        public async Task ExecuteResult_Calls_ViewEngine_FindView_If_View_Is_Null()
        {
            // Arrange
            var result = new DnnViewResult();

            var routeData = new RouteData();
            routeData.Values["controller"] = "Controller";
            routeData.Values["action"] = "Action";
            routeData.Values["id"] = "Id";
            var context = MockHelper.CreateMockControllerContext(routeData);

            var mockView = new Mock<IView>();
            var mockViewEngine = new Mock<IViewEngine>();
            mockViewEngine.Setup(v => v.FindView(It.IsAny<ActionContext>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns((ActionContext actionContext, string viewName, bool isMainPage) => ViewEngineResult.Found(viewName, mockView.Object));

            // The MVC 5 view-engine collection became an ordered IReadOnlyList<IViewEngine>.
            result.ViewEngines = new[] { mockViewEngine.Object };

            // Act
            await result.ExecuteResultAsync(context, new StringWriter());

            // Assert
            Assert.That(result.View, Is.SameAs(mockView.Object));
        }
    }
}
