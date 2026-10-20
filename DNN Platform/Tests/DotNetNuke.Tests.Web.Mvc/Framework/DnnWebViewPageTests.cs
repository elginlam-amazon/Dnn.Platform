// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Mvc.Framework
{
    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Tests.Utilities.Fakes;
    using DotNetNuke.Tests.Web.Mvc.Fakes;
    using DotNetNuke.UI.Modules;
    using DotNetNuke.Web.Mvc.Framework;
    using DotNetNuke.Web.Mvc.Framework.Modules;
    using DotNetNuke.Web.Mvc.Helpers;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc.Abstractions;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;

    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class DnnWebViewPageTests
    {
        private FakeServiceProvider serviceProvider;
        private ViewContext viewContext;

        [SetUp]
        public void SetUp()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Items[typeof(DnnModuleState)] = new DnnModuleState { ModuleContext = new ModuleInstanceContext() };

            var actionContext = new Microsoft.AspNetCore.Mvc.ActionContext(httpContext, new RouteData(), new ActionDescriptor());
            this.viewContext = new ViewContext
            {
                HttpContext = httpContext,
            };
            this.viewContext.RouteData = actionContext.RouteData;
            this.viewContext.ActionDescriptor = actionContext.ActionDescriptor;

            var htmlHelper = new Mock<IHtmlHelper<object>>();
            htmlHelper.SetupGet(h => h.ViewContext).Returns(this.viewContext);

            this.serviceProvider = FakeServiceProvider.Setup(services =>
            {
                services.AddSingleton(Mock.Of<IClientResourceController>());
                services.AddSingleton(Mock.Of<IApplicationStatusInfo>());
                services.AddSingleton(Mock.Of<IEventLogger>());
                services.AddSingleton(htmlHelper.Object);
            });

            httpContext.RequestServices = this.serviceProvider;
        }

        [TearDown]
        public void TearDown()
        {
            this.serviceProvider.Dispose();
        }

        [Test]
        public void Dnn_Property_Is_Created()
        {
            // Arrange
            var mockViewPage = new Mock<DnnWebViewPage>() { CallBase = true };
            mockViewPage.Object.ViewContext = this.viewContext;

            // Act, Assert
            Assert.That(mockViewPage.Object.Dnn, Is.Not.Null);
            Assert.That(mockViewPage.Object.Dnn, Is.InstanceOf<DnnHelper>());
        }

        [Test]
        public void Html_Property_Is_Created()
        {
            // Arrange
            var mockViewPage = new Mock<DnnWebViewPage>() { CallBase = true };
            mockViewPage.Object.ViewContext = this.viewContext;

            // Act, Assert
            Assert.That(mockViewPage.Object.Html, Is.Not.Null);
            Assert.That(mockViewPage.Object.Html, Is.InstanceOf<DnnHtmlHelper>());
        }

        [Test]
        public void Url_Property_Is_Created()
        {
            // Arrange
            var mockViewPage = new Mock<DnnWebViewPage>() { CallBase = true };
            mockViewPage.Object.ViewContext = this.viewContext;

            // Act, Assert
            Assert.That(mockViewPage.Object.Url, Is.Not.Null);
            Assert.That(mockViewPage.Object.Url, Is.InstanceOf<DnnUrlHelper>());
        }
    }
}
