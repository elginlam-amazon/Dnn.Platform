// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Mvc.Framework
{
    using System.IO;
    using System.Text.Encodings.Web;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Framework;
    using DotNetNuke.Tests.Utilities.Fakes;
    using DotNetNuke.Tests.Web.Mvc.Fakes;
    using DotNetNuke.Web.Mvc.Framework.Controllers;
    using DotNetNuke.Web.Mvc.Framework.Modules;
    using DotNetNuke.Web.Mvc.Helpers;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Abstractions;
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.AspNetCore.Mvc.ViewEngines;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
    using Microsoft.AspNetCore.Mvc.ViewFeatures.Buffers;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;

    using Moq;
    using NUnit.Framework;

    /// <summary>
    /// Tests the helper properties of the DNN Razor view base classes.
    /// </summary>
    /// <remarks>
    /// The MVC 5 <c>InitHelpers()</c> hook does not exist in ASP.NET Core: the <c>Dnn</c>, <c>Html</c>
    /// and <c>Url</c> helpers are created lazily the first time they are read, once the view context has
    /// been assigned. The tests keep their original names and assertions and simply read the property
    /// instead of calling <c>InitHelpers()</c>.
    /// </remarks>
    [TestFixture]
    public class DnnWebViewPageTests
    {
        private FakeServiceProvider serviceProvider;

        [SetUp]
        public void SetUp()
        {
            this.serviceProvider = FakeServiceProvider.Setup(services =>
            {
                services.AddSingleton(Mock.Of<IClientResourceController>());
                services.AddSingleton(Mock.Of<IApplicationStatusInfo>());
                services.AddSingleton(Mock.Of<IEventLogger>());
                services.AddSingleton(Mock.Of<IServicesFramework>());
                AddHtmlHelperServices(services);
            });
        }

        [TearDown]
        public void TearDown()
        {
            this.serviceProvider.Dispose();
        }

        [Test]
        public void InitHelpers_Sets_Dnn_Property()
        {
            // Arrange
            var page = new FakeDnnWebViewPage(Mock.Of<IServicesFramework>());
            page.ViewContext = CreateViewContext(new ViewDataDictionary<object>(new EmptyModelMetadataProvider(), new ModelStateDictionary()));

            // Act
            var dnn = page.Dnn;

            // Assert
            Assert.That(dnn, Is.Not.Null);
            Assert.That(dnn, Is.InstanceOf<DnnHelper>());
        }

        [Test]
        public void InitHelpers_Sets_Html_Property()
        {
            // Arrange
            var page = new FakeDnnWebViewPage(Mock.Of<IServicesFramework>());
            page.ViewContext = CreateViewContext(new ViewDataDictionary<object>(new EmptyModelMetadataProvider(), new ModelStateDictionary()));

            // Act
            var html = page.Html;

            // Assert
            Assert.That(html, Is.Not.Null);
            Assert.That(html, Is.InstanceOf<DnnHtmlHelper>());
        }

        [Test]
        public void InitHelpers_Sets_Dnn_Property_For_Strongly_Typed_Helper()
        {
            // Arrange
            var page = new FakeDnnWebViewPage<Dog>(Mock.Of<IServicesFramework>());
            page.ViewContext = CreateViewContext(new ViewDataDictionary<Dog>(new EmptyModelMetadataProvider(), new ModelStateDictionary()));

            // Act
            var html = page.Html;

            // Assert
            Assert.That(html, Is.Not.Null);
            Assert.That(html, Is.InstanceOf<DnnHtmlHelper<Dog>>());
        }

        [Test]
        public void InitHelpers_Sets_Url_Property()
        {
            // Arrange
            var page = new FakeDnnWebViewPage(Mock.Of<IServicesFramework>());
            page.ViewContext = CreateViewContext(new ViewDataDictionary<object>(new EmptyModelMetadataProvider(), new ModelStateDictionary()));

            // Act
            var url = page.Url;

            // Assert
            Assert.That(url, Is.Not.Null);
            Assert.That(url, Is.InstanceOf<DnnUrlHelper>());
        }

        /// <summary>Registers the ASP.NET Core HTML helper services the DNN helpers are built on top of.</summary>
        /// <param name="services">The service collection to register into.</param>
        private static void AddHtmlHelperServices(IServiceCollection services)
        {
            services.AddSingleton<IModelMetadataProvider, EmptyModelMetadataProvider>();
            services.AddSingleton<ModelExpressionProvider>();
            services.AddSingleton(Mock.Of<IHtmlGenerator>());
            services.AddSingleton(Mock.Of<ICompositeViewEngine>());
            services.AddSingleton(Mock.Of<IViewBufferScope>());
            services.AddSingleton(HtmlEncoder.Default);
            services.AddSingleton(UrlEncoder.Default);
            services.AddTransient<IHtmlHelper, HtmlHelper>();
            services.AddTransient(typeof(IHtmlHelper<>), typeof(HtmlHelper<>));
        }

        /// <summary>Builds a real view context with a DNN controller published onto the request.</summary>
        /// <param name="viewData">The view data of the view.</param>
        /// <returns>The view context.</returns>
        private static ViewContext CreateViewContext(ViewDataDictionary viewData)
        {
            var httpContext = MockHelper.CreateMockHttpContext();
            httpContext.Items[ModuleApplication.CurrentControllerItemKey] = Mock.Of<IDnnController>();

            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
            return new ViewContext(
                actionContext,
                Mock.Of<IView>(),
                viewData,
                new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>()),
                new StringWriter(),
                new HtmlHelperOptions());
        }
    }
}
