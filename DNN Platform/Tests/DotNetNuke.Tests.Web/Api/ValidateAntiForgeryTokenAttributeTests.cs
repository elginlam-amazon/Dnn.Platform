// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Api
{
    using System;
    using System.Collections.Generic;

    using DotNetNuke.Web.Api;
    using DotNetNuke.Web.Api.Internal;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Abstractions;
    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.AspNetCore.Routing;

    using Moq;

    using NUnit.Framework;

    [TestFixture]
    public class ValidateAntiForgeryTokenAttributeTests
    {
        // ValidateAntiForgeryTokenAttribute was ported from a Web API filter (which read the anti-forgery
        // cookie/token from an HttpRequestMessage via AuthFilterContext) to an ASP.NET Core
        // ActionFilterAttribute whose OnActionExecuting reads the cookie/token from
        // Microsoft.AspNetCore.Http.HttpRequest. The tests build an ActionExecutingContext accordingly.
        [TearDown]
        public void TearDown()
        {
            AntiForgery.ClearInstance();
        }

        [Test]
        [TestCase("{0}={1}; ")] // simple
        [TestCase("foo=fee; {0}={1}; key=value")] // normal
        [TestCase("bad name=value; {0}={1}; good_name=value; ")] // bad whitespace
        [TestCase("foo=fee; {0}={1}")] // no terminating semi-colon
        public void LocateCookies(string cookieFormat)
        {
            // The cookie value must not contain raw spaces: ASP.NET Core cookie parsing (unlike the
            // legacy Web API parser) stops a cookie value at the first unencoded whitespace. The test
            // intent is locating the correct cookie by name, so a realistic space-free value is used.
            this.LocatesCookie(cookieFormat, "__RequestVerificationToken_thiscanbemany_characters_in_some_cases", "some_text_goes_here");
        }

        public void LocatesCookie(string cookieFormat, string cookieName, string cookieValue)
        {
            // Arrange
            var context = CreateActionExecutingContext(
                cookieHeader: string.Format(cookieFormat, cookieName, cookieValue),
                requestVerificationToken: "anything_is_fine");
            var mockAntiForgery = new Mock<IAntiForgery>();
            mockAntiForgery.Setup(x => x.CookieName).Returns(cookieName);
            AntiForgery.SetTestableInstance(mockAntiForgery.Object);

            // Act
            var vaft = new DotNetNuke.Web.Api.ValidateAntiForgeryTokenAttribute();
            Assert.DoesNotThrow(() => { vaft.OnActionExecuting(context); });

            // Assert
            mockAntiForgery.Verify(x => x.Validate(cookieValue, It.IsAny<string>()), Times.Once());
        }

        [Test]
        [TestCase("__RequestVerificationToken_L05lYnVsYQ__=C7z6azjbbWwBMjonTt/Np7Dm52/xvN3NS0G0mpCUy7cPiMB+1g6rt1ifKlaTDlRO8RsniY2DIAeMjet0nOPNwWJ6sByiWttFsPnbVlAV9qZ0YwFSsYge9dASbrQ=", "__RequestVerificationToken_L05lYnVsYQ__", "C7z6azjbbWwBMjonTt/Np7Dm52/xvN3NS0G0mpCUy7cPiMB+1g6rt1ifKlaTDlRO8RsniY2DIAeMjet0nOPNwWJ6sByiWttFsPnbVlAV9qZ0YwFSsYge9dASbrQ=")]
        public void MissingTokenDoesnotPassValidationTest(string cookieHeader, string cookieName, string cookieValue)
        {
            // Arrange - no RequestVerificationToken header is provided
            var context = CreateActionExecutingContext(cookieHeader: cookieHeader, requestVerificationToken: null);
            var mockAntiForgery = new Mock<IAntiForgery>();
            mockAntiForgery.Setup(x => x.CookieName).Returns(cookieName);
            AntiForgery.SetTestableInstance(mockAntiForgery.Object);

            // Act, Assert
            var vaft = new DotNetNuke.Web.Api.ValidateAntiForgeryTokenAttribute();
            Assert.Throws<UnauthorizedAccessException>(() => { vaft.OnActionExecuting(context); });
        }

        private static ActionExecutingContext CreateActionExecutingContext(string cookieHeader, string requestVerificationToken)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["Cookie"] = cookieHeader;
            if (requestVerificationToken != null)
            {
                httpContext.Request.Headers["RequestVerificationToken"] = requestVerificationToken;
            }

            var actionContext = new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor());
            return new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                controller: null);
        }
    }
}
