// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Api
{
    using System.Net;

    using DotNetNuke.Web.Api.Auth;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Net.Http.Headers;

    using NUnit.Framework;

    [TestFixture]
    public class BasicAuthMessageHandlerTests
    {
        [Test]
        public void SetsWwwAuthenticateHeaderOn401()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;

            // Act
            var handler = new BasicAuthMessageHandler(true, false);
            handler.OnSendResponse(context);

            // Assert
            Assert.That(
                context.Response.Headers[HeaderNames.WWWAuthenticate].ToString(),
                Is.EqualTo("Basic realm=\"DNNAPI\""));
        }

        [Test]
        public void OmitsWwwAuthenticateHeaderOn401FromXmlHttpRequest()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Headers["X-REQUESTED-WITH"] = "XmlHttpRequest";
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;

            // Act
            var handler = new BasicAuthMessageHandler(true, false);
            handler.OnSendResponse(context);

            // Assert
            Assert.That(context.Response.Headers.ContainsKey(HeaderNames.WWWAuthenticate), Is.False);
        }

        [Test]
        public void MissingXmlHttpRequestValueDoesntThrowNullException()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Headers["X-REQUESTED-WITH"] = string.Empty;
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;

            // Act
            var handler = new BasicAuthMessageHandler(true, false);
            handler.OnSendResponse(context);

            // Assert
            Assert.That(
                context.Response.Headers[HeaderNames.WWWAuthenticate].ToString(),
                Is.EqualTo("Basic realm=\"DNNAPI\""));
        }

        [Test]
        public void ResponseWithNullRequestReturnsUnauthorized()
        {
            // Arrange
            // The legacy test set HttpResponseMessage.RequestMessage to null. An ASP.NET Core
            // HttpContext always carries a request, so the equivalent is a response with no
            // request-side setup at all: the challenge must still be emitted.
            var context = new DefaultHttpContext();
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;

            // Act
            var handler = new BasicAuthMessageHandler(true, false);
            handler.OnSendResponse(context);

            // Assert
            Assert.That(
                context.Response.Headers[HeaderNames.WWWAuthenticate].ToString(),
                Is.EqualTo("Basic realm=\"DNNAPI\""));
        }

        // todo unit test actual authentication code
        // very hard to unit test inbound authentication code as it dips into untestable bits of usercontroller etc.
        // need to write controllers with interfaces and servicelocator
    }
}
