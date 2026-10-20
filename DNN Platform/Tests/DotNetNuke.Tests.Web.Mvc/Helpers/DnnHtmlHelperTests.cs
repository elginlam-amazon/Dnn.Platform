// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// ReSharper disable ObjectCreationAsStatement
namespace DotNetNuke.Tests.Web.Mvc.Helpers
{
    using DotNetNuke.Framework;
    using DotNetNuke.Tests.Web.Mvc.Fakes;
    using DotNetNuke.UI.Modules;
    using DotNetNuke.Web.Mvc.Framework.Modules;
    using DotNetNuke.Web.Mvc.Helpers;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;

    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class DnnHtmlHelperTests
    {
        [Test]
        public void Constructor_Sets_ModuleContext_Property()
        {
            // Arrange
            var expectedContext = new ModuleInstanceContext();
            var htmlHelper = CreateHtmlHelper(expectedContext, new ViewDataDictionary<Dog>(new EmptyModelMetadataProvider(), new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary()));

            // Act
            var helper = new DnnHtmlHelper<Dog>(htmlHelper, Mock.Of<IServicesFramework>());

            // Assert
            Assert.That(helper.ModuleContext, Is.EqualTo(expectedContext));
        }

        [Test]
        public void ViewContext_Property_Returns_Passed_in_ViewContext()
        {
            // Arrange
            var viewData = new ViewDataDictionary<Dog>(new EmptyModelMetadataProvider(), new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary());
            var htmlHelper = CreateHtmlHelper(new ModuleInstanceContext(), viewData);
            var expectedViewContext = htmlHelper.ViewContext;

            // Act
            var helper = new DnnHtmlHelper<Dog>(htmlHelper, Mock.Of<IServicesFramework>());

            // Assert
            Assert.That(helper.ViewContext, Is.EqualTo(expectedViewContext));
        }

        [Test]
        public void Strongly_Typed_ViewData_Property_Returns_Passed_in_Model()
        {
            // Arrange
            var expectedModel = new Dog();
            var viewData = new ViewDataDictionary<Dog>(new EmptyModelMetadataProvider(), new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary())
            {
                Model = expectedModel,
            };
            var htmlHelper = CreateHtmlHelper(new ModuleInstanceContext(), viewData);

            // Act
            var helper = new DnnHtmlHelper<Dog>(htmlHelper, Mock.Of<IServicesFramework>());

            // Assert
            Assert.That(helper.ViewData.Model, Is.EqualTo(expectedModel));
        }

        [Test]
        public void AntiForgeryToken_Requests_AntiForgerySupport()
        {
            // Arrange
            var viewData = new ViewDataDictionary<Dog>(new EmptyModelMetadataProvider(), new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary());
            var htmlHelper = CreateHtmlHelper(new ModuleInstanceContext(), viewData);
            var servicesFramework = new Mock<IServicesFramework>();
            var helper = new DnnHtmlHelper<Dog>(htmlHelper, servicesFramework.Object);

            // Act
            helper.AntiForgeryToken();

            // Assert
            servicesFramework.Verify(f => f.RequestAjaxAntiForgerySupport());
        }

        private static IHtmlHelper<Dog> CreateHtmlHelper(ModuleInstanceContext moduleContext, ViewDataDictionary<Dog> viewData)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Items[typeof(DnnModuleState)] = new DnnModuleState { ModuleContext = moduleContext };

            var viewContext = new ViewContext { HttpContext = httpContext };

            var htmlHelper = new Mock<IHtmlHelper<Dog>>();
            htmlHelper.SetupGet(h => h.ViewContext).Returns(viewContext);
            htmlHelper.SetupGet(h => h.ViewData).Returns(viewData);
            return htmlHelper.Object;
        }
    }
}
