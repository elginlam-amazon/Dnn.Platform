// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Security.Claims;

    using DotNetNuke.Common;
    using DotNetNuke.Tests.Instance.Utilities.HttpSimulator;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Features;
    using Microsoft.Extensions.Primitives;

    using Moq;

    /// <summary>
    /// Creates and registers a fully pre-populated <see cref="HttpContext"/> test double, so that
    /// code under test which reads <see cref="HttpContextSource.Current"/> finds a context whose
    /// request, response, session, items, features and connection are all non-<see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Usage is unchanged from the net48 version: keep the returned mock and override individual
    /// members, e.g.
    /// <code>
    /// var mockContext = HttpContextHelper.RegisterMockHttpContext();
    /// var mockRequest = Mock.Get(mockContext.Object.Request);
    /// mockRequest.SetupGet(x =&gt; x.Headers).Returns(new HeaderDictionary());
    /// </code>
    /// </para>
    /// <para>
    /// <strong>System.Web to ASP.NET Core member mapping</strong> — the members the net48 helper
    /// pre-populated map onto the Core context as follows:
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>HttpRequestBase.Headers</c> (<c>NameValueCollection</c>) → <see cref="HttpRequest.Headers"/> (<see cref="IHeaderDictionary"/>).</description></item>
    /// <item><description><c>HttpRequestBase.QueryString</c> and <c>HttpRequestBase.Params</c> → <see cref="HttpRequest.Query"/> (<see cref="IQueryCollection"/>) plus <see cref="HttpRequest.QueryString"/>; <c>Params</c> has no single Core equivalent, read <c>Query</c>, <c>Form</c> and <c>Headers</c> instead.</description></item>
    /// <item><description><c>HttpRequestBase.InputStream</c> → <see cref="HttpRequest.Body"/>.</description></item>
    /// <item><description><c>HttpRequestBase.UserAgent</c> → <c>Request.Headers["User-Agent"]</c>.</description></item>
    /// <item><description><c>HttpRequestBase.UserLanguages</c> → <c>Request.Headers["Accept-Language"]</c>.</description></item>
    /// <item><description><c>HttpRequestBase.UserHostAddress</c> → <see cref="ConnectionInfo.RemoteIpAddress"/>.</description></item>
    /// <item><description><c>HttpRequestBase.ServerVariables</c> has <em>no</em> Core equivalent. Each variable now lives on a specific member — <c>REMOTE_ADDR</c>/<c>REMOTE_HOST</c> on <see cref="HttpContext.Connection"/>, <c>SERVER_PORT</c>/<c>SERVER_PORT_SECURE</c>/<c>HTTPS</c> on <see cref="HttpContext.Connection"/> and <see cref="HttpRequest.Scheme"/>, <c>SERVER_NAME</c>/<c>HTTP_HOST</c> on <see cref="HttpRequest.Host"/>, <c>REQUEST_METHOD</c> on <see cref="HttpRequest.Method"/>, <c>SERVER_PROTOCOL</c> on <see cref="HttpRequest.Protocol"/>, <c>URL</c>/<c>PATH_INFO</c> on <see cref="HttpRequest.Path"/>, <c>APPL_PHYSICAL_PATH</c> on the hosting environment's content root (see <see cref="HttpContextSource.ApplicationPhysicalPath"/>) and every <c>HTTP_*</c> variable on <see cref="HttpRequest.Headers"/>. Use <see cref="SetServerVariable"/> to write one by its legacy name.</description></item>
    /// <item><description><c>HttpSessionStateBase</c> → <see cref="HttpContext.Session"/> (an in-memory <see cref="ISession"/>).</description></item>
    /// <item><description><c>HttpServerUtilityBase.MapPath</c> → <see cref="HttpContextSource.MapPath(string)"/>, backed by the fake <see cref="IWebHostEnvironment"/> this helper registers (see <see cref="MapPath"/>).</description></item>
    /// </list>
    /// </remarks>
    public class HttpContextHelper
    {
        /// <summary>
        /// Creates a pre-populated <see cref="HttpContext"/> test double and registers it as
        /// <see cref="HttpContextSource.Current"/>.
        /// </summary>
        /// <returns>
        /// The mock, so the caller can keep overriding members with
        /// <c>SetupGet</c>. Its <c>Items</c> dictionary is real and writable, so
        /// <c>DotNetNuke.Common.Extensions.HttpContextDependencyInjectionExtensions.SetScope</c>
        /// and the <c>PortalSettings</c> stash both round-trip through it.
        /// </returns>
        public static Mock<HttpContext> RegisterMockHttpContext()
        {
            var mock = CreateMockHttpContext();
            HttpContextSource.RegisterInstance(mock.Object);
            return mock;
        }

        /// <summary>
        /// Writes a legacy <c>Request.ServerVariables[name]</c> value onto the ASP.NET Core
        /// member that replaced it, for tests that used to do
        /// <c>SetupGet(x =&gt; x.ServerVariables).Returns(...)</c>.
        /// </summary>
        /// <param name="context">A context mock created by <see cref="RegisterMockHttpContext"/>.</param>
        /// <param name="name">The legacy server-variable name, for example <c>REMOTE_ADDR</c> or <c>HTTP_REFERER</c>.</param>
        /// <param name="value">The value to write.</param>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="name"/> is <see langword="null"/>.</exception>
        public static void SetServerVariable(Mock<HttpContext> context, string name, string value)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            var request = Mock.Get(context.Object.Request);
            var connection = Mock.Get(context.Object.Connection);

            switch (name.ToUpperInvariant())
            {
                case "REMOTE_ADDR":
                case "REMOTE_HOST":
                    connection.SetupGet(x => x.RemoteIpAddress)
                              .Returns(string.IsNullOrEmpty(value) ? null : IPAddress.Parse(value));
                    break;
                case "LOCAL_ADDR":
                    connection.SetupGet(x => x.LocalIpAddress)
                              .Returns(string.IsNullOrEmpty(value) ? null : IPAddress.Parse(value));
                    break;
                case "SERVER_PORT":
                    connection.SetupGet(x => x.LocalPort).Returns(int.Parse(value));
                    break;
                case "SERVER_NAME":
                case "HTTP_HOST":
                    request.SetupGet(x => x.Host).Returns(new HostString(value));
                    break;
                case "REQUEST_METHOD":
                    request.SetupGet(x => x.Method).Returns(value);
                    break;
                case "SERVER_PROTOCOL":
                    request.SetupGet(x => x.Protocol).Returns(value);
                    break;
                case "HTTPS":
                case "SERVER_PORT_SECURE":
                    {
                        var secure = string.Equals(value, "on", StringComparison.OrdinalIgnoreCase) || value == "1";
                        request.SetupGet(x => x.IsHttps).Returns(secure);
                        request.SetupGet(x => x.Scheme).Returns(secure ? "https" : "http");
                        break;
                    }

                case "URL":
                case "PATH_INFO":
                case "SCRIPT_NAME":
                    request.SetupGet(x => x.Path).Returns(new PathString(value));
                    break;
                case "QUERY_STRING":
                    request.SetupGet(x => x.QueryString)
                           .Returns(new QueryString(string.IsNullOrEmpty(value) ? string.Empty : (value.StartsWith("?", StringComparison.Ordinal) ? value : "?" + value)));
                    break;
                default:
                    // Every remaining server variable is a request header, with the HTTP_ prefix
                    // stripped and underscores turned back into dashes (HTTP_USER_AGENT →
                    // User-Agent), which is exactly how IIS built the collection.
                    context.Object.Request.Headers[ToHeaderName(name)] = new StringValues(value);
                    break;
            }
        }

        /// <summary>
        /// Resolves a virtual path to a physical path — the replacement for the
        /// <c>HttpServerUtilityBase.MapPath</c> member the net48 helper used to mock
        /// (<c>HttpServerUtilityBase</c> does not exist on ASP.NET Core).
        /// </summary>
        /// <param name="virtualPath">The application-relative virtual path, for example <c>~/Portals/0</c>.</param>
        /// <returns>The physical path, resolved against the registered <see cref="IWebHostEnvironment"/>.</returns>
        public static string MapPath(string virtualPath)
        {
            EnsureWebHostEnvironment();
            return HttpContextSource.MapPath(virtualPath);
        }

        private static Mock<HttpContext> CreateMockHttpContext()
        {
            EnsureWebHostEnvironment();

            var context = new Mock<HttpContext>();
            var request = GetMockRequest(context);
            var response = GetMockResponse(context);

            context.SetupGet(x => x.Items).Returns(new Dictionary<object, object>());
            context.SetupGet(x => x.Features).Returns(new FeatureCollection());
            context.SetupGet(x => x.Request).Returns(request);
            context.SetupGet(x => x.Response).Returns(response);
            context.SetupGet(x => x.Session).Returns(GetSession());
            context.SetupGet(x => x.Connection).Returns(GetMockConnectionInfo());
            context.SetupGet(x => x.User).Returns(new ClaimsPrincipal(new ClaimsIdentity()));
            context.SetupGet(x => x.TraceIdentifier).Returns(Guid.NewGuid().ToString());
            context.SetupGet(x => x.RequestAborted).Returns(default(System.Threading.CancellationToken));
            return context;
        }

        /// <summary>Creates the in-memory session that replaces the mocked <c>HttpSessionStateBase</c>.</summary>
        /// <returns>An in-memory <see cref="ISession"/>.</returns>
        private static ISession GetSession()
        {
            return new HttpSimulator.FakeHttpSessionState();
        }

        /// <summary>Creates the connection double that carries the former <c>UserHostAddress</c>/<c>REMOTE_ADDR</c> values.</summary>
        /// <returns>A <see cref="ConnectionInfo"/> double.</returns>
        private static ConnectionInfo GetMockConnectionInfo()
        {
            var connection = new Mock<ConnectionInfo>();
            connection.SetupAllProperties();
            connection.SetupGet(x => x.Id).Returns(Guid.NewGuid().ToString());
            connection.SetupGet(x => x.RemoteIpAddress).Returns(IPAddress.Loopback);
            connection.SetupGet(x => x.RemotePort).Returns(0);
            connection.SetupGet(x => x.LocalIpAddress).Returns(IPAddress.Loopback);
            connection.SetupGet(x => x.LocalPort).Returns(80);
            return connection.Object;
        }

        /// <summary>Creates the response double, with a writable body, headers and status code.</summary>
        /// <param name="context">The owning context mock.</param>
        /// <returns>An <see cref="HttpResponse"/> double.</returns>
        private static HttpResponse GetMockResponse(Mock<HttpContext> context)
        {
            var response = new Mock<HttpResponse>();
            response.SetupAllProperties();
            response.SetupGet(x => x.HttpContext).Returns(() => context.Object);
            response.SetupGet(x => x.Headers).Returns(new HeaderDictionary());
            response.SetupGet(x => x.Cookies).Returns(new Mock<IResponseCookies>().Object);
            response.Object.Body = new MemoryStream();
            response.Object.StatusCode = (int)HttpStatusCode.OK;
            response.Object.ContentType = string.Empty;
            return response.Object;
        }

        /// <summary>Creates the request double, with every member the net48 helper pre-populated kept non-<see langword="null"/>.</summary>
        /// <param name="context">The owning context mock.</param>
        /// <returns>An <see cref="HttpRequest"/> double.</returns>
        private static HttpRequest GetMockRequest(Mock<HttpContext> context)
        {
            var headers = new HeaderDictionary
            {
                // UserAgent and UserLanguages were empty strings on the net48 double.
                { "User-Agent", string.Empty },
                { "Accept-Language", string.Empty },
            };

            var request = new Mock<HttpRequest>();
            request.SetupAllProperties();
            request.SetupGet(x => x.HttpContext).Returns(() => context.Object);
            request.SetupGet(x => x.Headers).Returns(headers);
            request.SetupGet(x => x.Cookies).Returns(new Mock<IRequestCookieCollection>().Object);
            request.SetupGet(x => x.HasFormContentType).Returns(false);
            request.Object.Body = new MemoryStream();
            request.Object.Query = new QueryCollection();
            request.Object.QueryString = QueryString.Empty;
            request.Object.Form = new FormCollection(new Dictionary<string, StringValues>());
            request.Object.Method = HttpMethods.Get;
            request.Object.Scheme = "http";
            request.Object.IsHttps = false;
            request.Object.Protocol = "HTTP/1.1";
            request.Object.Host = new HostString("localhost");
            request.Object.Path = PathString.Empty;
            request.Object.PathBase = PathString.Empty;
            request.Object.ContentType = string.Empty;
            request.Object.RouteValues = new Microsoft.AspNetCore.Routing.RouteValueDictionary();
            return request.Object;
        }

        /// <summary>
        /// Makes sure <see cref="HttpContextSource.MapPath(string)"/> works inside tests by
        /// registering a fake <see cref="IWebHostEnvironment"/> rooted at the test host's base
        /// directory when the process has none yet. An environment registered by
        /// <c>HttpSimulator</c> (or by a host) is left alone.
        /// </summary>
        private static void EnsureWebHostEnvironment()
        {
            try
            {
                _ = HttpContextSource.WebHostEnvironment;
            }
            catch (InvalidOperationException)
            {
                HttpContextSource.RegisterWebHostEnvironment(
                    new HttpSimulator.FakeWebHostEnvironment(AppContext.BaseDirectory));
            }
        }

        /// <summary>Converts a legacy <c>HTTP_*</c> server-variable name into its header name.</summary>
        /// <param name="name">The server-variable name.</param>
        /// <returns>The equivalent request-header name.</returns>
        private static string ToHeaderName(string name)
        {
            var headerName = name.StartsWith("HTTP_", StringComparison.OrdinalIgnoreCase)
                ? name.Substring("HTTP_".Length)
                : name;
            return headerName.Replace('_', '-');
        }
    }
}
