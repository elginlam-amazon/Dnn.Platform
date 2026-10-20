// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// ReSharper disable ObjectCreationAsStatement
namespace DotNetNuke.Tests.Web.Mvc.Helpers
{
    using System;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.UI.Modules;
    using DotNetNuke.Web.Mvc.Framework.Modules;
    using DotNetNuke.Web.Mvc.Helpers;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc.Rendering;

    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class DnnHelperTests
    {
        [Test]
        public void Constructor_Throws_If_No_ModuleState_In_Context()
        {
            // Arrange
            var htmlHelper = CreateHtmlHelper(moduleContext: null, includeModuleState: false);

            // Act, Assert
            Assert.Throws<InvalidOperationException>(() => new DnnHelper<object>(
                Mock.Of<IClientResourceController>(),
                Mock.Of<IApplicationStatusInfo>(),
                Mock.Of<IEventLogger>(),
                htmlHelper));
        }

        [Test]
        public void Constructor_Sets_ModuleContext_Property()
        {
            // Arrange
            var expectedContext = new ModuleInstanceContext();
            var htmlHelper = CreateHtmlHelper(expectedContext, includeModuleState: true);

            // Act
            var helper = new DnnHelper<object>(
                Mock.Of<IClientResourceController>(),
                Mock.Of<IApplicationStatusInfo>(),
                Mock.Of<IEventLogger>(),
                htmlHelper);

            // Assert
            Assert.That(helper.ModuleContext, Is.EqualTo(expectedContext));
        }

        private static IHtmlHelper<object> CreateHtmlHelper(ModuleInstanceContext moduleContext, bool includeModuleState)
        {
            var httpContext = new DefaultHttpContext();
            if (includeModuleState)
            {
                httpContext.Items[typeof(DnnModuleState)] = new DnnModuleState { ModuleContext = moduleContext };
            }

            var viewContext = new ViewContext { HttpContext = httpContext };

            var htmlHelper = new Mock<IHtmlHelper<object>>();
            htmlHelper.SetupGet(h => h.ViewContext).Returns(viewContext);
            return htmlHelper.Object;
        }
    }
}
