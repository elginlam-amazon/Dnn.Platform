// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Mvc.Framework.Attributes
{
    using System;
    using System.Collections.Generic;

    using DotNetNuke.Tests.Utilities.Fakes;
    using DotNetNuke.Tests.Web.Mvc.Fakes;
    using DotNetNuke.Web.Mvc.Framework.ActionFilters;
    using DotNetNuke.Web.Mvc.Framework.Modules;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Abstractions;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.AspNetCore.Routing;

    using Moq;
    using Moq.Protected;
    using NUnit.Framework;

    [TestFixture]
    public class DnnHandleErrorAttributeTests
    {
        private FakeServiceProvider serviceProvider;

        /// <summary>Isolates the DNN static service provider so this fixture cannot leak into others.</summary>
        [SetUp]
        public void SetUp()
        {
            this.serviceProvider = FakeServiceProvider.Setup();
        }

        /// <summary>Restores the DNN static service provider.</summary>
        [TearDown]
        public void TearDown()
        {
            this.serviceProvider.Dispose();
        }

        [Test]
        public void OnException_Throws_InvalidOPerationExcption_IfNotUsedIn_IDnnController()
        {
            // Arrange
            const string expectedMessage = "This attribute can only be applied to Controllers that implement IDnnController";
            var sut = new DnnHandleErrorAttribute();

            // No DNN controller published on the request, which is the ASP.NET Core equivalent of the
            // MVC 5 "the controller is not an IDnnController" case.
            var exceptionContext = CreateExceptionContext(controller: null);

            // Act / Assert
            var ex = Assert.Throws<InvalidOperationException>(() => sut.OnException(exceptionContext));
            Assert.That(ex.Message, Is.EqualTo(expectedMessage));
        }

        [Test]
        public void OnException_Logs_TheException()
        {
            // Arrange
            var testException = new Exception();
            var exceptionContext = CreateExceptionContext(new FakeDnnController());
            exceptionContext.Exception = testException;

            var mockDnnHandleErrorAttribute = new Mock<DnnHandleErrorAttribute> { CallBase = true };
            mockDnnHandleErrorAttribute.Protected().Setup("LogException", testException);

            var sut = mockDnnHandleErrorAttribute.Object;

            // Act
            sut.OnException(exceptionContext);

            // Assert
            mockDnnHandleErrorAttribute.VerifyAll();
        }

        /// <summary>Creates the ASP.NET Core <see cref="ExceptionContext"/> the filter runs against.</summary>
        /// <param name="controller">The executing DNN controller, published on the request, or <see langword="null"/> for none.</param>
        /// <returns>The exception context.</returns>
        private static ExceptionContext CreateExceptionContext(object controller)
        {
            var httpContext = MockHelper.CreateMockHttpContext();
            if (controller is not null)
            {
                httpContext.Items[ModuleApplication.CurrentControllerItemKey] = controller;
            }

            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
            return new ExceptionContext(actionContext, new List<IFilterMetadata>());
        }
    }
}
