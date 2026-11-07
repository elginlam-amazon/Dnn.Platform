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
    using DotNetNuke.Framework;
    using DotNetNuke.Tests.Utilities.Fakes;
    using DotNetNuke.Tests.Web.Mvc.Fakes;
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
    /// Tests the <see cref="DnnHtmlHelper"/> and <see cref="DnnHtmlHelper{TModel}"/>.
    /// </summary>
    /// <remarks>
    /// ASP.NET Core has no view-data container abstraction (the view data is carried by the
    /// <see cref="ViewContext"/>) and no <c>ViewContext.Controller</c> (the controller is resolved from
    /// <c>HttpContext.Items</c>). The <see cref="ViewContext"/> properties are not virtual either, so the
    /// tests use real view contexts instead of mocks.
    /// </remarks>
    [TestFixture]
    public class DnnHtmlHelperTests
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
        public void Constructor_Throws_On_Null_ViewContext()
        {
            // Act,Assert
            Assert.Throws<ArgumentNullException>(() => new DnnHtmlHelper(null, Mock.Of<IServicesFramework>()));
        }

        [Test]
        public void Strongly_Typed_Constructor_Throws_On_Null_ViewContext()
        {
            // Act,Assert
            Assert.Throws<ArgumentNullException>(() => new DnnHtmlHelper<Dog>(null, Mock.Of<IServicesFramework>()));
        }

        [Test]
        public void Constructor_Throws_On_Null_ViewDataContainer()
        {
            // Act,Assert
            // The view data container parameter is gone; the view data is carried by the view context in
            // ASP.NET Core, so the equivalent guard is the null check on that view data carrier.
            Assert.Throws<ArgumentNullException>(() => new DnnHtmlHelper(null, new RouteCollection(), Mock.Of<IServicesFramework>()));
        }

        [Test]
        public void Strongly_Typed_Constructor_Throws_On_Null_ViewDataContainer()
        {
            // Act,Assert
            Assert.Throws<ArgumentNullException>(() => new DnnHtmlHelper<Dog>(null, new RouteCollection(), Mock.Of<IServicesFramework>()));
        }

        [Test]
        public void Constructor_Throws_On_Null_RouteCollection()
        {
            // Arrange
            // A fully valid view context, so the only thing that can fail is the null route collection.
            var viewContext = CreateViewContext(CreateDnnController(new ModuleInstanceContext()), CreateViewData());

            // Act,Assert
            Assert.Throws<ArgumentNullException>(() => new DnnHtmlHelper(viewContext, null, Mock.Of<IServicesFramework>()));
        }

        [Test]
        public void Strongly_Typed_Constructor_Throws_On_Null_RouteCollection()
        {
            // Arrange
            var viewContext = CreateViewContext(CreateDnnController(new ModuleInstanceContext()), CreateViewData<Dog>());

            // Act,Assert
            Assert.Throws<ArgumentNullException>(() => new DnnHtmlHelper<Dog>(viewContext, null, Mock.Of<IServicesFramework>()));
        }

        [Test]
        public void Constructor_Throws_On_Invalid_Controller_Property()
        {
            // Arrange
            var viewContext = CreateViewContext(new Mock<ControllerBase>().Object, CreateViewData());

            // Act,Assert
            Assert.Throws<InvalidOperationException>(() => new DnnHtmlHelper(viewContext, Mock.Of<IServicesFramework>()));
        }

        [Test]
        public void Strongly_Typed_Constructor_Throws_On_Invalid_Controller_Property()
        {
            // Arrange
            var viewContext = CreateViewContext(new Mock<ControllerBase>().Object, CreateViewData<Dog>());

            // Act,Assert
            Assert.Throws<InvalidOperationException>(() => new DnnHtmlHelper<Dog>(viewContext, Mock.Of<IServicesFramework>()));
        }

        [Test]
        public void Constructor_Sets_ModuleContext_Property()
        {
            // Arrange
            var expectedContext = new ModuleInstanceContext();
            var viewContext = CreateViewContext(CreateDnnController(expectedContext), CreateViewData());

            // Act
            var helper = new DnnHtmlHelper(viewContext, Mock.Of<IServicesFramework>());

            // Assert
            Assert.That(helper.ModuleContext, Is.EqualTo(expectedContext));
        }

        [Test]
        public void Strongly_Typed_Constructor_Sets_ModuleContext_Property()
        {
            // Arrange
            var expectedContext = new ModuleInstanceContext();
            var viewContext = CreateViewContext(CreateDnnController(expectedContext), CreateViewData<Dog>());

            // Act
            var helper = new DnnHtmlHelper<Dog>(viewContext, Mock.Of<IServicesFramework>());

            // Assert
            Assert.That(helper.ModuleContext, Is.EqualTo(expectedContext));
        }

        [Test]
        public void ViewContext_Property_Returns_Passed_in_ViewContext()
        {
            // Arrange
            var viewContext = CreateViewContext(CreateDnnController(new ModuleInstanceContext()), CreateViewData());

            // Act
            var helper = new DnnHtmlHelper(viewContext, Mock.Of<IServicesFramework>());

            // Assert
            Assert.That(helper.ViewContext, Is.EqualTo(viewContext));
        }

        [Test]
        public void ViewDataContainer_Property_Returns_Passed_in_ViewDataContainer()
        {
            // Arrange
            var expectedViewData = CreateViewData();
            var viewContext = CreateViewContext(CreateDnnController(new ModuleInstanceContext()), expectedViewData);

            // Act
            var helper = new DnnHtmlHelper(viewContext, Mock.Of<IServicesFramework>());

            // Assert
            Assert.That(helper.ViewData, Is.EqualTo(expectedViewData));
        }

        [Test]
        public void Strongly_Typed_ViewDataContainer_Property_Returns_Passed_in_Model()
        {
            // Arrange
            var expectedModel = new Dog();
            var expectedViewData = CreateViewData<Dog>();
            expectedViewData.Model = expectedModel;
            var viewContext = CreateViewContext(CreateDnnController(new ModuleInstanceContext()), expectedViewData);

            // Act
            var helper = new DnnHtmlHelper<Dog>(viewContext, Mock.Of<IServicesFramework>());

            // Assert
            Assert.That(helper.ViewData.Model, Is.EqualTo(expectedModel));
        }

        [Test]
        public void EnableClientValidation_Updates_Context()
        {
            // Arrange
            var page = new FakeDnnWebViewPage(Mock.Of<IServicesFramework>());
            page.ViewContext = CreateViewContext(CreateDnnController(new ModuleInstanceContext()), CreateViewData<object>());

            // Act
            page.Html.EnableClientValidation(true);

            // Assert
            Assert.That(page.ViewContext.ClientValidationEnabled, Is.True);
        }

        [Test]
        public void EnableUnobtrusiveJavaScript_Updates_Context()
        {
            // Arrange
            var page = new FakeDnnWebViewPage(Mock.Of<IServicesFramework>());
            page.ViewContext = CreateViewContext(CreateDnnController(new ModuleInstanceContext()), CreateViewData<object>());

            // Act
            page.Html.EnableUnobtrusiveJavaScript(true);

            // Assert
            // The ASP.NET Core view context has no UnobtrusiveJavaScriptEnabled flag: client side
            // validation is always unobtrusive, so the unobtrusive JavaScript switch of the view context
            // is its ClientValidationEnabled flag.
            Assert.That(page.ViewContext.ClientValidationEnabled, Is.True);
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

        /// <summary>Creates a DNN controller test double with the given module context.</summary>
        /// <param name="moduleContext">The module instance context of the controller.</param>
        /// <returns>The controller.</returns>
        private static IDnnController CreateDnnController(ModuleInstanceContext moduleContext)
        {
            var mockDnnController = new Mock<IDnnController>();
            mockDnnController.Setup(c => c.ModuleContext).Returns(moduleContext);
            return mockDnnController.Object;
        }

        /// <summary>Creates an empty view data dictionary.</summary>
        /// <returns>The view data.</returns>
        private static ViewDataDictionary CreateViewData()
            => new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary());

        /// <summary>Creates an empty strongly typed view data dictionary.</summary>
        /// <typeparam name="TModel">The type of the view model.</typeparam>
        /// <returns>The view data.</returns>
        private static ViewDataDictionary<TModel> CreateViewData<TModel>()
            => new ViewDataDictionary<TModel>(new EmptyModelMetadataProvider(), new ModelStateDictionary());

        /// <summary>Builds a real view context, publishing the given controller onto the request.</summary>
        /// <param name="controller">The controller of the request.</param>
        /// <param name="viewData">The view data of the view.</param>
        /// <returns>The view context.</returns>
        private static ViewContext CreateViewContext(object controller, ViewDataDictionary viewData)
        {
            var httpContext = MockHelper.CreateMockHttpContext();
            httpContext.Items[ModuleApplication.CurrentControllerItemKey] = controller;

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
