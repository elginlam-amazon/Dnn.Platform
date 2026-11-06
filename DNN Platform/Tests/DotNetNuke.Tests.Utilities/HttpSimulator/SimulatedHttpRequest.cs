// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Instance.Utilities.HttpSimulator
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Primitives;

    /// <summary>Used to simulate an HttpRequest.</summary>
    /// <remarks>
    /// <para>
    /// On .NET Framework this derived from <c>System.Web.Hosting.SimpleWorkerRequest</c> and
    /// ASP.NET pulled the request details out of it through the <c>HttpWorkerRequest</c>
    /// overrides. ASP.NET Core has no worker-request abstraction, so this type is now a plain
    /// description of the request: it keeps exactly the same construction parameters and the same
    /// value-producing members (<see cref="GetHttpVerbName"/>, <see cref="GetServerName"/>,
    /// <see cref="GetLocalPort"/>, <see cref="IsSecure"/>, <see cref="GetUnknownRequestHeaders"/>,
    /// <see cref="GetKnownRequestHeader"/>, <see cref="GetFilePathTranslated"/>,
    /// <see cref="GetPreloadedEntityBody"/>, <see cref="IsEntireEntityBodyIsPreloaded"/>), and
    /// <see cref="ApplyTo"/> projects those values onto a
    /// <see cref="Microsoft.AspNetCore.Http.HttpContext"/>.
    /// </para>
    /// </remarks>
    public class SimulatedHttpRequest
    {
        /// <summary>The <c>HttpWorkerRequest</c> known-header index of the <c>Referer</c> header.</summary>
        public const int HeaderReferer = 0x24;

        /// <summary>The <c>HttpWorkerRequest</c> known-header index of the <c>Content-Type</c> header.</summary>
        public const int HeaderContentType = 12;

        /// <summary>The key under which <see cref="GetFilePathTranslated"/> is published on <see cref="HttpContext.Items"/>.</summary>
        public const string PhysicalFilePathItemKey = "DNN.Test.PhysicalFilePath";

        private readonly string host;
        private readonly string verb;
        private readonly int port;
        private readonly string physicalFilePath;
        private readonly string applicationPath;
        private readonly string physicalAppPath;
        private readonly string page;
        private readonly string query;
        private readonly TextWriter output;
        private Uri referer;

        /// <summary>
        /// Initializes a new instance of the <see cref="SimulatedHttpRequest"/> class.
        /// Creates a new <see cref="SimulatedHttpRequest"/> instance.
        /// </summary>
        /// <param name="applicationPath">App virtual dir.</param>
        /// <param name="physicalAppPath">Physical Path to the app.</param>
        /// <param name="physicalFilePath">Physical Path to the file.</param>
        /// <param name="page">The Part of the URL after the application.</param>
        /// <param name="query">Query.</param>
        /// <param name="output">Output.</param>
        /// <param name="host">Host.</param>
        /// <param name="port">Port to request.</param>
        /// <param name="verb">The HTTP Verb to use.</param>
        public SimulatedHttpRequest(string applicationPath, string physicalAppPath, string physicalFilePath, string page, string query, TextWriter output, string host, int port, string verb)
        {
            this.Form = new NameValueCollection();
            this.Headers = new NameValueCollection();
            if (host == null)
            {
                throw new ArgumentNullException("host", "Host cannot be null.");
            }

            if (host.Length == 0)
            {
                throw new ArgumentException("Host cannot be empty.", "host");
            }

            if (applicationPath == null)
            {
                throw new ArgumentNullException("applicationPath", "Can't create a request with a null application path. Try empty string.");
            }

            this.host = host;
            this.verb = verb;
            this.port = port;
            this.physicalFilePath = physicalFilePath;
            this.applicationPath = applicationPath;
            this.physicalAppPath = physicalAppPath;
            this.page = page;
            this.query = query;
            this.output = output;
        }

        /// <summary>Gets the headers.</summary>
        /// <value>The headers.</value>
        public NameValueCollection Headers { get; private set; }

        /// <summary>Gets the format exception.</summary>
        /// <value>The format exception.</value>
        public NameValueCollection Form { get; private set; }

        /// <summary>Gets the writer that captures everything written to the simulated response.</summary>
        /// <value>The response writer supplied at construction, which may be <see langword="null"/>.</value>
        public TextWriter Output
        {
            get { return this.output; }
        }

        /// <summary>Returns the specified member of the request header.</summary>
        /// <returns>
        /// The HTTP verb returned in the request
        /// header.
        /// </returns>
        public string GetHttpVerbName()
        {
            return this.verb;
        }

        /// <summary>Gets the name of the server.</summary>
        /// <returns>The simulated host name.</returns>
        public string GetServerName()
        {
            return this.host;
        }

        /// <summary>Gets the port the simulated request was made to.</summary>
        /// <returns>The port number.</returns>
        public int GetLocalPort()
        {
            return this.port;
        }

        /// <summary>Gets a value indicating whether the simulated request uses TLS.</summary>
        /// <returns><see langword="true"/> when the port is 443; otherwise <see langword="false"/>.</returns>
        public bool IsSecure()
        {
            return (this.port == 443) ? true : false;
        }

        /// <summary>Gets the application's virtual path (the legacy <c>GetAppPath</c>).</summary>
        /// <returns>The application virtual path.</returns>
        public string GetAppPath()
        {
            return this.applicationPath;
        }

        /// <summary>Gets the application's physical path (the legacy <c>GetAppPathTranslated</c>).</summary>
        /// <returns>The application physical path.</returns>
        public string GetAppPathTranslated()
        {
            return this.physicalAppPath;
        }

        /// <summary>Gets the portion of the URL after the application (the legacy <c>GetFilePath</c>).</summary>
        /// <returns>The page, relative to the application.</returns>
        public string GetPage()
        {
            return this.page;
        }

        /// <summary>Gets the query string of the simulated request, without the leading <c>?</c>.</summary>
        /// <returns>The query string.</returns>
        public string GetQueryString()
        {
            return this.query;
        }

        /// <summary>Get all nonstandard HTTP header name-value pairs.</summary>
        /// <returns>An array of header name-value pairs.</returns>
        public string[][] GetUnknownRequestHeaders()
        {
            if (this.Headers == null || this.Headers.Count == 0)
            {
                return null;
            }

            var headersArray = new string[this.Headers.Count][];
            for (var i = 0; i < this.Headers.Count; i++)
            {
                headersArray[i] = new string[2];
                headersArray[i][0] = this.Headers.Keys[i];
                headersArray[i][1] = this.Headers[i];
            }

            return headersArray;
        }

        /// <summary>Returns a well-known request header by its legacy <c>HttpWorkerRequest</c> index.</summary>
        /// <param name="index">The known-header index, for example <see cref="HeaderReferer"/>.</param>
        /// <returns>
        /// The header value; <see cref="string.Empty"/> for a referer that was never set, and
        /// <see langword="null"/> for any header this simulation does not produce (matching the
        /// old base-class behaviour).
        /// </returns>
        public string GetKnownRequestHeader(int index)
        {
            if (index == HeaderReferer)
            {
                return this.referer == null ? string.Empty : this.referer.ToString();
            }

            if (index == HeaderContentType && this.verb == "POST")
            {
                return "application/x-www-form-urlencoded";
            }

            return null;
        }

        /// <summary>Gets the physical path of the requested file.</summary>
        /// <returns>The translated file path.</returns>
        public string GetFilePathTranslated()
        {
            return this.physicalFilePath;
        }

        /// <summary>Reads request data from the client (when not preloaded).</summary>
        /// <returns>The URL-form-encoded form variables, UTF-8 encoded.</returns>
        public byte[] GetPreloadedEntityBody()
        {
            return Encoding.UTF8.GetBytes(this.Form.Keys.Cast<string>().Aggregate(string.Empty, (current, key) => current + string.Format("{0}={1}&", key, this.Form[key])));
        }

        /// <summary>
        /// Returns a value indicating whether all request data
        /// is available and no further reads from the client are required.
        /// </summary>
        /// <returns>
        ///     <see langword="true"/> if all request data is available; otherwise,
        /// <see langword="false"/>.
        /// </returns>
        public bool IsEntireEntityBodyIsPreloaded()
        {
            return true;
        }

        /// <summary>Gets the referer that was set on this request, if any.</summary>
        /// <returns>The referer, or <see langword="null"/>.</returns>
        public Uri GetReferer()
        {
            return this.referer;
        }

        /// <summary>
        /// Projects every value this request describes onto an ASP.NET Core
        /// <see cref="Microsoft.AspNetCore.Http.HttpContext"/>. This is the modern replacement
        /// for the <c>HttpWorkerRequest</c> overrides that ASP.NET used to call back into.
        /// </summary>
        /// <param name="context">The context to populate.</param>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
        public void ApplyTo(HttpContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var request = context.Request;

            request.Method = string.IsNullOrEmpty(this.verb) ? HttpMethods.Get : this.verb.ToUpperInvariant();
            request.Scheme = this.IsSecure() ? "https" : "http";
            request.Host = IsDefaultPort(this.port, this.IsSecure())
                ? new HostString(this.host)
                : new HostString(this.host, this.port);

            // The IIS virtual directory becomes PathBase; everything after it becomes Path.
            var appPath = NormalizePath(this.applicationPath).TrimEnd('/');
            request.PathBase = string.IsNullOrEmpty(appPath) ? PathString.Empty : new PathString(appPath);
            request.Path = new PathString(NormalizePath(this.page));

            request.QueryString = string.IsNullOrEmpty(this.query)
                ? QueryString.Empty
                : new QueryString("?" + this.query);

            // Unknown (non well-known) headers.
            var unknown = this.GetUnknownRequestHeaders();
            if (unknown != null)
            {
                foreach (var header in unknown)
                {
                    request.Headers[header[0]] = header[1];
                }
            }

            // Well-known headers, through the same indices the old overrides answered on.
            var refererHeader = this.GetKnownRequestHeader(HeaderReferer);
            if (!string.IsNullOrEmpty(refererHeader))
            {
                request.Headers["Referer"] = refererHeader;
            }

            var contentType = this.GetKnownRequestHeader(HeaderContentType);
            if (!string.IsNullOrEmpty(contentType))
            {
                request.ContentType = contentType;
            }

            // Form + entity body. IsEntireEntityBodyIsPreloaded() was always true, so the body
            // is fully materialized in memory here too.
            var body = this.GetPreloadedEntityBody();
            request.Body = new MemoryStream(body, writable: false);
            request.ContentLength = body.LongLength;

            if (this.Form.Count > 0)
            {
                var formValues = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
                foreach (string key in this.Form.Keys)
                {
                    if (key == null)
                    {
                        continue;
                    }

                    formValues[key] = new StringValues(this.Form.GetValues(key));
                }

                request.Form = new FormCollection(formValues);
            }

            context.Connection.RemoteIpAddress = IPAddress.Loopback;
            context.Connection.LocalIpAddress = IPAddress.Loopback;
            context.Connection.LocalPort = this.port;
            context.Connection.RemotePort = this.port;

            // GetFilePathTranslated() has no Core equivalent, so it is published where test code
            // (and DNN's path helpers) can still read it.
            context.Items[PhysicalFilePathItemKey] = this.physicalFilePath;
        }

        /// <summary>Sets the referer of this simulated request.</summary>
        /// <param name="referer">The referer URL.</param>
        internal void SetReferer(Uri referer)
        {
            this.referer = referer;
        }

        private static bool IsDefaultPort(int port, bool secure)
        {
            return port <= 0 || (secure ? port == 443 : port == 80);
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "/";
            }

            var normalized = path.Replace('\\', '/');
            if (!normalized.StartsWith("/", StringComparison.Ordinal))
            {
                normalized = "/" + normalized;
            }

            // The trailing slash is preserved: net48 produced Request.Path == "/Test/" for a
            // request to http://localhost/Test/, and DNN's friendly-URL/rewrite logic
            // distinguishes "/Test" from "/Test/". PathString represents both faithfully.
            return normalized;
        }
    }
}
