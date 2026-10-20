// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Mvc.Framework.Attributes
{
    using System;
    using System.Collections.Generic;

    using DotNetNuke.Web.Mvc.Framework.ActionFilters;

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
        [Test]
        public void OnException_Logs_TheException()
        {
            // Arrange
            var testException = new Exception();
            var actionContext = new ActionContext(
                MockHelper.CreateMockHttpContext(),
                new RouteData(),
                new ActionDescriptor());
            var exceptionContext = new ExceptionContext(actionContext, new List<IFilterMetadata>())
            {
                Exception = testException,
            };

            var mockDnnHandleErrorAttribute = new Mock<DnnHandleErrorAttribute> { CallBase = true };
            mockDnnHandleErrorAttribute.Protected().Setup("LogException", testException);

            var sut = mockDnnHandleErrorAttribute.Object;

            // Act
            sut.OnException(exceptionContext);

            // Assert
            mockDnnHandleErrorAttribute.VerifyAll();
        }
    }
}
