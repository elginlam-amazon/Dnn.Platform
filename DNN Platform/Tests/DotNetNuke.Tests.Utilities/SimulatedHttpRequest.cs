// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Instance.Utilities
{
    using System;
    using System.IO;

    using Microsoft.AspNetCore.Http;

    /// <summary>Used to simulate an HttpRequest.</summary>
    /// <remarks>
    /// On .NET Framework this derived from <c>System.Web.Hosting.SimpleWorkerRequest</c>, and
    /// ASP.NET read the request out of it through the <c>HttpWorkerRequest</c> overrides. There
    /// is no worker-request abstraction in ASP.NET Core, so this is now a plain description of
    /// the request with the same construction parameters and the same
    /// <see cref="GetServerName"/>/<see cref="MapPath"/> behaviour; <see cref="ApplyTo"/>
    /// projects it onto a <see cref="Microsoft.AspNetCore.Http.HttpContext"/>.
    /// </remarks>
    public class SimulatedHttpRequest
    {
        private readonly string host;
        private readonly string appVirtualDir;
        private readonly string appPhysicalDir;
        private readonly string page;
        private readonly string query;
        private readonly TextWriter output;

        /// <summary>
        /// Initializes a new instance of the <see cref="SimulatedHttpRequest"/> class.
        /// Creates a new <see cref="SimulatedHttpRequest"/> instance.
        /// </summary>
        /// <param name="appVirtualDir">App virtual dir.</param>
        /// <param name="appPhysicalDir">App physical dir.</param>
        /// <param name="page">Page.</param>
        /// <param name="query">Query.</param>
        /// <param name="output">Output.</param>
        /// <param name="host">Host.</param>
        public SimulatedHttpRequest(string appVirtualDir, string appPhysicalDir, string page, string query, TextWriter output, string host)
        {
            if (string.IsNullOrEmpty(host))
            {
                throw new ArgumentNullException("host", "Host cannot be null nor empty.");
            }

            this.host = host;
            this.appVirtualDir = appVirtualDir;
            this.appPhysicalDir = appPhysicalDir;
            this.page = page;
            this.query = query;
            this.output = output;
        }

        /// <summary>Gets the writer that captures everything written to the simulated response.</summary>
        /// <value>The writer supplied at construction, which may be <see langword="null"/>.</value>
        public TextWriter Output
        {
            get { return this.output; }
        }

        /// <summary>Gets the name of the server.</summary>
        /// <returns>The simulated host name.</returns>
        public string GetServerName()
        {
            return this.host;
        }

        /// <summary>Gets the application's virtual path.</summary>
        /// <returns>The application virtual path.</returns>
        public string GetAppPath()
        {
            return this.appVirtualDir;
        }

        /// <summary>Gets the application's physical path.</summary>
        /// <returns>The application physical path.</returns>
        public string GetAppPathTranslated()
        {
            return this.appPhysicalDir;
        }

        /// <summary>Gets the portion of the URL after the application.</summary>
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

        /// <summary>Maps the path to a filesystem path.</summary>
        /// <param name="virtualPath">Virtual path.</param>
        /// <returns>The physical path, or <see cref="string.Empty"/> when there is no application path.</returns>
        public string MapPath(string virtualPath)
        {
            var path = string.Empty;
            var appPath = this.GetAppPath();

            if (appPath != null)
            {
                path = Path.Combine(appPath, virtualPath);
            }

            return path;
        }

        /// <summary>
        /// Projects this simulated request onto an ASP.NET Core
        /// <see cref="Microsoft.AspNetCore.Http.HttpContext"/>, which is what the
        /// <c>HttpWorkerRequest</c> overrides used to do indirectly.
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
            request.Method = HttpMethods.Get;
            request.Scheme = "http";
            request.Host = new HostString(this.GetServerName());

            var basePath = NormalizePath(this.appVirtualDir).TrimEnd('/');
            request.PathBase = string.IsNullOrEmpty(basePath) ? PathString.Empty : new PathString(basePath);
            request.Path = new PathString(NormalizePath(this.page));
            request.QueryString = string.IsNullOrEmpty(this.query)
                ? QueryString.Empty
                : new QueryString("?" + this.query);
            request.Body = Stream.Null;
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
