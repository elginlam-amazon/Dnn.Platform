// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Api
{
    using System.Net;

    using DotNetNuke.Security.Membership;
    using DotNetNuke.Tests.Utilities.Mocks;
    using DotNetNuke.Web.Api.Auth;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Net.Http.Headers;

    using NUnit.Framework;

#pragma warning disable CS0618 // DigestAuthMessageHandler is deprecated but still shipped, so its behavior stays under test
    [TestFixture]
    public class DigestAuthMessageHandlerTests
    {
        [SetUp]
        public void Setup()
        {
            var mockMembership = MockComponentProvider.CreateNew<MembershipProvider>();
            mockMembership.Setup(m => m.PasswordRetrievalEnabled).Returns(true);
        }

        [Test]
        public void SetsWwwAuthenticateHeaderOn401()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;

            // Act
            var handler = new DigestAuthMessageHandler(true, false);
            handler.OnSendResponse(context);

            // Assert
            AssertDigestChallenge(context);
        }

        [Test]
        public void OmitsWwwAuthenticateHeaderOn401FromXmlHttpRequest()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Headers["X-REQUESTED-WITH"] = "XmlHttpRequest";
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;

            // Act
            var handler = new DigestAuthMessageHandler(true, false);
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
            var handler = new DigestAuthMessageHandler(true, false);
            handler.OnSendResponse(context);

            // Assert
            AssertDigestChallenge(context);
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
            var handler = new DigestAuthMessageHandler(true, false);
            handler.OnSendResponse(context);

            // Assert
            AssertDigestChallenge(context);
        }

        private static void AssertDigestChallenge(HttpContext context)
        {
            var header = context.Response.Headers[HeaderNames.WWWAuthenticate].ToString();
            var separatorIndex = header.IndexOf(' ');

            Assert.Multiple(() =>
            {
                Assert.That(separatorIndex, Is.GreaterThan(0));
                Assert.That(header.Substring(0, separatorIndex), Is.EqualTo("Digest"));
                Assert.That(header.Substring(separatorIndex + 1), Does.Contain("realm=\"DNNAPI\""));
            });
        }

        // todo unit test actual authentication code
        // very hard to unit test inbound authentication code as it dips into untestable bits of usercontroller etc.
        // need to write controllers with interfaces and servicelocator
    }
#pragma warning restore CS0618
}
