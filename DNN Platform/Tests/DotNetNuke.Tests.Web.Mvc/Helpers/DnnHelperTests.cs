// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// ReSharper disable ObjectCreationAsStatement
namespace DotNetNuke.Tests.Web.Mvc.Helpers
{
    using System;
    using System.IO;
    using System.Text.Encodings.Web;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Tests.Utilities.Fakes;
    using DotNetNuke.UI.Modules;
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
    /// Tests the <see cref="DnnHelper"/>.
    /// </summary>
    /// <remarks>
    /// ASP.NET Core has no view-data container abstraction: the view data is carried by the
    /// <see cref="ViewContext"/>, and the controller is resolved from the request
    /// (<c>HttpContext.Items</c>) instead of <c>ViewContext.Controller</c>, which does not exist anymore.
    /// </remarks>
    [TestFixture]
    public class DnnHelperTests
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
                AddHtmlHelperServices(services);
            });
        }

        [TearDown]
        public void TearDown()
        {
            this.serviceProvider.Dispose();
        }

        [Test]
        public void Constructor_Throws_On_Null_ViewContext()
        {
            // Act,Assert
            Assert.Throws<ArgumentNullException>(() => new DnnHelper(
                Mock.Of<IClientResourceController>(),
                Mock.Of<IApplicationStatusInfo>(),
                Mock.Of<IEventLogger>(),
                null));
        }

        [Test]
        public void Constructor_Throws_On_Null_ViewDataContainer()
        {
            // Act,Assert
            // The view data container parameter is gone; in ASP.NET Core the view data is carried by the
            // view context, so the equivalent guard is the null check on the view data carrier of the
            // route collection overload (the original test also passed a null view context).
            Assert.Throws<ArgumentNullException>(() => new DnnHelper(
                Mock.Of<IClientResourceController>(),
                Mock.Of<IApplicationStatusInfo>(),
                Mock.Of<IEventLogger>(),
                null,
                new RouteCollection()));
        }

        [Test]
        public void Constructor_Throws_On_Invalid_Controller_Property()
        {
            // Arrange
            var viewContext = CreateViewContext(new Mock<ControllerBase>().Object);

            // Act,Assert
            Assert.Throws<InvalidOperationException>(() => new DnnHelper(
                Mock.Of<IClientResourceController>(),
                Mock.Of<IApplicationStatusInfo>(),
                Mock.Of<IEventLogger>(),
                viewContext));
        }

        [Test]
        public void Constructor_Sets_ModuleContext_Property()
        {
            // Arrange
            var expectedContext = new ModuleInstanceContext();
            var mockDnnController = new Mock<IDnnController>();
            mockDnnController.Setup(c => c.ModuleContext).Returns(expectedContext);
            var viewContext = CreateViewContext(mockDnnController.Object);

            // Act
            var helper = new DnnHelper(
                Mock.Of<IClientResourceController>(),
                Mock.Of<IApplicationStatusInfo>(),
                Mock.Of<IEventLogger>(),
                viewContext);

            // Assert
            Assert.That(helper.ModuleContext, Is.EqualTo(expectedContext));
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

        /// <summary>Builds a real view context, publishing the given controller onto the request.</summary>
        /// <param name="controller">The controller of the request.</param>
        /// <returns>The view context.</returns>
        private static ViewContext CreateViewContext(object controller)
        {
            var httpContext = MockHelper.CreateMockHttpContext();
            httpContext.Items[ModuleApplication.CurrentControllerItemKey] = controller;

            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
            return new ViewContext(
                actionContext,
                Mock.Of<IView>(),
                new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()),
                new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>()),
                new StringWriter(),
                new HtmlHelperOptions());
        }
    }
}
