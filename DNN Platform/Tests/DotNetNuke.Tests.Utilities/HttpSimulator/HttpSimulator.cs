// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Instance.Utilities.HttpSimulator
{
    using System;
    using System.Collections.Specialized;
    using System.Configuration;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;

    using Microsoft.AspNetCore.Http;

    using SystemWeb = System.Web;

    public enum HttpVerb
    {
        GET = 0,
        HEAD = 1,
        POST = 2,
        PUT = 3,
        DELETE = 4,
    }

    /// <summary>
    /// Useful class for simulating the HttpContext. This does not actually
    /// make an HttpRequest, it merely simulates the state that your code
    /// would be in "as if" handling a request. Thus the HttpContext.Current
    /// property is populated.
    /// </summary>
    /// <remarks>
    /// On .NET (formerly .NET Core), the legacy ASP.NET <c>HttpRuntime</c>/<c>HostingEnvironment</c>
    /// hosting model no longer exists. This implementation builds a
    /// <see cref="Microsoft.AspNetCore.Http.DefaultHttpContext"/> and exposes it through the
    /// <see cref="System.Web.HttpContext"/> adapter so that code relying on
    /// <see cref="System.Web.HttpContext.Current"/> continues to work in unit tests.
    /// </remarks>
    public class HttpSimulator : IDisposable
    {
        private static readonly string WebsitePhysicalAppPath = ConfigurationManager.AppSettings["DefaultPhysicalAppPath"];
        private readonly NameValueCollection formVars = new NameValueCollection();
        private readonly NameValueCollection headers = new NameValueCollection();
        private StringBuilder builder;
        private Uri referer;
        private bool requestSimulated;

        private string applicationPath = "/";

        private string physicalApplicationPath = WebsitePhysicalAppPath;

        private string physicalPath = WebsitePhysicalAppPath;

        public HttpSimulator()
            : this("/", WebsitePhysicalAppPath)
        {
        }

        public HttpSimulator(string applicationPath)
            : this(applicationPath, WebsitePhysicalAppPath)
        {
        }

        public HttpSimulator(string applicationPath, string physicalApplicationPath)
        {
            this.ApplicationPath = applicationPath;
            this.PhysicalApplicationPath = physicalApplicationPath;
        }

        /// <summary>Gets physical path to the requested file (used for simulation purposes).</summary>
        public string PhysicalPath
        {
            get { return this.physicalPath; }
        }

        /// <summary>Gets the text from the response to the simulated request.</summary>
        public string ResponseText
        {
            get
            {
                return (this.builder ?? new StringBuilder()).ToString();
            }
        }

        public string Host { get; private set; }

        public string LocalPath { get; private set; }

        public int Port { get; private set; }

        /// <summary>Gets portion of the URL after the application.</summary>
        public string Page { get; private set; }

        /// <summary>
        /// Gets or sets the same thing as the IIS Virtual directory. It's
        /// what gets returned by Request.ApplicationPath.
        /// </summary>
        public string ApplicationPath
        {
            get { return this.applicationPath; }

            set
            {
                this.applicationPath = value ?? "/";
                this.applicationPath = NormalizeSlashes(this.applicationPath);
            }
        }

        /// <summary>Gets or sets physical path to the application (used for simulation purposes).</summary>
        public string PhysicalApplicationPath
        {
            get { return this.physicalApplicationPath; }

            set
            {
                this.physicalApplicationPath = value ?? WebsitePhysicalAppPath;

                // strip trailing backslashes.
                this.physicalApplicationPath = StripTrailingBackSlashes(this.physicalApplicationPath) + @"\";
            }
        }

        public TextWriter ResponseWriter { get; set; }

        /// <summary>Sets up the HttpContext objects to simulate a GET request.</summary>
        /// <remarks>
        /// Simulates a request to http://localhost/.
        /// </remarks>
        /// <returns>This instance.</returns>
        public HttpSimulator SimulateRequest()
        {
            return this.SimulateRequest(new Uri("http://localhost/"));
        }

        /// <summary>Sets up the HttpContext objects to simulate a GET request.</summary>
        /// <param name="url">The URL of the simulated request.</param>
        /// <returns>This instance.</returns>
        public HttpSimulator SimulateRequest(Uri url)
        {
            return this.SimulateRequest(url, HttpVerb.GET);
        }

        /// <summary>Sets up the HttpContext objects to simulate a request.</summary>
        /// <param name="url">The URL of the simulated request.</param>
        /// <param name="httpVerb">The HTTP verb of the simulated request.</param>
        /// <returns>This instance.</returns>
        public HttpSimulator SimulateRequest(Uri url, HttpVerb httpVerb)
        {
            return this.SimulateRequest(url, httpVerb, null, null);
        }

        /// <summary>Sets up the HttpContext objects to simulate a POST request.</summary>
        /// <param name="url">The URL of the simulated request.</param>
        /// <param name="formVariables">The form variables to post.</param>
        /// <returns>This instance.</returns>
        public HttpSimulator SimulateRequest(Uri url, NameValueCollection formVariables)
        {
            return this.SimulateRequest(url, HttpVerb.POST, formVariables, null);
        }

        /// <summary>Sets up the HttpContext objects to simulate a POST request.</summary>
        /// <param name="url">The URL of the simulated request.</param>
        /// <param name="formVariables">The form variables to post.</param>
        /// <param name="headers">The request headers.</param>
        /// <returns>This instance.</returns>
        public HttpSimulator SimulateRequest(Uri url, NameValueCollection formVariables, NameValueCollection headers)
        {
            return this.SimulateRequest(url, HttpVerb.POST, formVariables, headers);
        }

        /// <summary>Sets up the HttpContext objects to simulate a request.</summary>
        /// <param name="url">The URL of the simulated request.</param>
        /// <param name="httpVerb">The HTTP verb of the simulated request.</param>
        /// <param name="headers">The request headers.</param>
        /// <returns>This instance.</returns>
        public HttpSimulator SimulateRequest(Uri url, HttpVerb httpVerb, NameValueCollection headers)
        {
            return this.SimulateRequest(url, httpVerb, null, headers);
        }

        /// <summary>Sets the referer for the request. Uses a fluent interface.</summary>
        /// <param name="referer">The referer URL.</param>
        /// <returns>This instance.</returns>
        public HttpSimulator SetReferer(Uri referer)
        {
            this.referer = referer;
            if (this.requestSimulated && SystemWeb.HttpContext.Current != null && referer != null)
            {
                SystemWeb.HttpContext.Current.Request.Headers["Referer"] = referer.ToString();
            }

            return this;
        }

        /// <summary>Sets a form variable.</summary>
        /// <param name="name">The form variable name.</param>
        /// <param name="value">The form variable value.</param>
        /// <returns>This instance.</returns>
        public HttpSimulator SetFormVariable(string name, string value)
        {
            if (this.requestSimulated)
            {
                throw new InvalidOperationException("Cannot set form variables after calling Simulate().");
            }

            this.formVars.Add(name, value);

            return this;
        }

        /// <summary>Sets a header value.</summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The header value.</param>
        /// <returns>This instance.</returns>
        public HttpSimulator SetHeader(string name, string value)
        {
            if (this.requestSimulated)
            {
                throw new InvalidOperationException("Cannot set headers after calling Simulate().");
            }

            this.headers.Add(name, value);

            return this;
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            SystemWeb.HttpContext.Current = null;
            GC.SuppressFinalize(this);
        }

        protected static string NormalizeSlashes(string s)
        {
            if (string.IsNullOrEmpty(s) || s == "/")
            {
                return "/";
            }

            s = s.Replace(@"\", "/");

            // Reduce multiple slashes in row to single.
            var normalized = Regex.Replace(s, "(/)/+", "$1");

            // Strip left.
            normalized = StripPrecedingSlashes(normalized);

            // Strip right.
            normalized = StripTrailingSlashes(normalized);
            return "/" + normalized;
        }

        protected static string StripPrecedingSlashes(string s)
        {
            return Regex.Replace(s, "^/*(.*)", "$1");
        }

        protected static string StripTrailingSlashes(string s)
        {
            return Regex.Replace(s, "(.*)/*$", "$1", RegexOptions.RightToLeft);
        }

        protected static string StripTrailingBackSlashes(string s)
        {
            return string.IsNullOrEmpty(s) ? string.Empty : Regex.Replace(s, @"(.*)\\*$", "$1", RegexOptions.RightToLeft);
        }

        /// <summary>Sets up the HttpContext objects to simulate a request.</summary>
        /// <param name="url">The URL of the simulated request.</param>
        /// <param name="httpVerb">The HTTP verb of the simulated request.</param>
        /// <param name="formVariables">The form variables to post.</param>
        /// <param name="headers">The request headers.</param>
        /// <returns>This instance.</returns>
        protected virtual HttpSimulator SimulateRequest(Uri url, HttpVerb httpVerb, NameValueCollection formVariables, NameValueCollection headers)
        {
            SystemWeb.HttpContext.Current = null;

            this.ParseRequestUrl(url);

            if (this.ResponseWriter == null)
            {
                this.builder = new StringBuilder();
                this.ResponseWriter = new StringWriter(this.builder);
            }

            if (formVariables != null)
            {
                this.formVars.Add(formVariables);
            }

            if (this.formVars.Count > 0)
            {
                httpVerb = HttpVerb.POST; // Need to enforce this.
            }

            if (headers != null)
            {
                this.headers.Add(headers);
            }

            var coreContext = new DefaultHttpContext();
            var coreRequest = coreContext.Request;
            coreRequest.Method = httpVerb.ToString();
            coreRequest.Scheme = url.Scheme;
            coreRequest.Host = new HostString(url.Host, url.Port);
            coreRequest.Path = url.AbsolutePath;
            coreRequest.PathBase = string.Empty;

            var query = ExtractQueryStringPart(url);
            coreRequest.QueryString = string.IsNullOrEmpty(query) ? QueryString.Empty : QueryString.FromUriComponent("?" + query);

            foreach (string key in this.headers)
            {
                coreRequest.Headers[key] = this.headers[key];
            }

            if (this.referer != null)
            {
                coreRequest.Headers["Referer"] = this.referer.ToString();
            }

            if (this.formVars.Count > 0)
            {
                coreRequest.ContentType = "application/x-www-form-urlencoded";
            }

            SystemWeb.HttpContext.Current = coreContext;
            this.requestSimulated = true;

            return this;
        }

        private static string ExtractQueryStringPart(Uri url)
        {
            var query = url.Query;
            return query.StartsWith("?", StringComparison.Ordinal) ? query.Substring(1) : query;
        }

        private static string RightAfter(string original, string search)
        {
            if (search.Length > original.Length || search.Length == 0)
            {
                return original;
            }

            original = original.Trim();
            search = search.Trim();
            var searchIndex = original.IndexOf(search, 0, StringComparison.InvariantCultureIgnoreCase);

            if (searchIndex < 0)
            {
                return original;
            }

            var regexMatch = @"(" + search + ")(?<keep>.+)";
            const string regexReplace = @"${keep}";
            var result = Regex.Replace(original, regexMatch, regexReplace, RegexOptions.IgnoreCase);
            return result;
        }

        private void ParseRequestUrl(Uri url)
        {
            if (url == null)
            {
                return;
            }

            this.Host = url.Host;
            this.Port = url.Port;
            this.LocalPath = url.LocalPath;
            this.Page = StripPrecedingSlashes(RightAfter(url.LocalPath, this.ApplicationPath));
            this.physicalPath = Path.Combine(this.physicalApplicationPath, this.Page.Replace("/", @"\"));
        }
    }
}
