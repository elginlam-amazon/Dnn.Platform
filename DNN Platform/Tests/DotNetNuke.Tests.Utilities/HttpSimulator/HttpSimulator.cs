// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Instance.Utilities.HttpSimulator
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using DotNetNuke.Common;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Features;
    using Microsoft.Extensions.FileProviders;

    /// <summary>The HTTP verb of a simulated request.</summary>
    public enum HttpVerb
    {
        /// <summary>An HTTP GET.</summary>
        GET = 0,

        /// <summary>An HTTP HEAD.</summary>
        HEAD = 1,

        /// <summary>An HTTP POST.</summary>
        POST = 2,

        /// <summary>An HTTP PUT.</summary>
        PUT = 3,

        /// <summary>An HTTP DELETE.</summary>
        DELETE = 4,
    }

    /// <summary>
    /// Useful class for simulating the HttpContext. This does not actually
    /// make an HttpRequest, it merely simulates the state that your code
    /// would be in "as if" handling a request. Thus the
    /// <see cref="HttpContextSource.Current"/> property is populated.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On .NET Framework this class built a <c>System.Web.HttpContext</c> around a
    /// <c>SimpleWorkerRequest</c> and then reached into <c>HttpRuntime</c>,
    /// <c>HostingEnvironment</c> and <c>HttpApplicationFactory</c> private fields by reflection
    /// to make the ambient statics agree with the simulation. None of those types exist on
    /// modern .NET, so the simulation is now built on
    /// <see cref="DefaultHttpContext"/> and published through the
    /// <see cref="HttpContextSource"/> seam:
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>HttpContext.Current</c> → <see cref="HttpContextSource.RegisterInstance(HttpContext)"/>.</description></item>
    /// <item><description><c>HttpRuntime.AppDomainAppPath</c> / <c>HostingEnvironment.ApplicationPhysicalPath</c> → a fake
    /// <see cref="IWebHostEnvironment"/> registered via <see cref="HttpContextSource.RegisterWebHostEnvironment"/>,
    /// which is what backs <see cref="HttpContextSource.ApplicationPhysicalPath"/> and <see cref="HttpContextSource.MapPath"/>.</description></item>
    /// <item><description><c>HostingEnvironment.ApplicationVirtualPath</c> → <see cref="HttpContextSource.ApplicationVirtualPath"/>,
    /// which reads <c>Request.PathBase</c>.</description></item>
    /// <item><description>The <c>IHttpSessionState</c> fake → <see cref="FakeHttpSessionState"/>, an in-memory
    /// <see cref="ISession"/> assigned to <see cref="HttpContext.Session"/>.</description></item>
    /// <item><description><c>HttpContext.Current.Application</c> (the <c>HttpApplicationFactory._state</c> reflection) →
    /// the process-wide <see cref="ApplicationState"/> dictionary.</description></item>
    /// </list>
    /// </remarks>
    public class HttpSimulator : IDisposable
    {
        /// <summary>The key under which <see cref="ApplicationState"/> is published on <see cref="HttpContext.Items"/>.</summary>
        public const string ApplicationStateItemKey = "DNN.Test.ApplicationState";

        private static readonly ConcurrentDictionary<string, object> ApplicationStateStore =
            new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The configured physical path of the website under test. On net48 this was
        /// <c>ConfigurationManager.AppSettings["DefaultPhysicalAppPath"]</c>; it is now resolved
        /// through the single shared <see cref="TestConfiguration.DefaultPhysicalAppPath"/>
        /// lookup, which is <c>IConfiguration</c>/<c>appsettings.json</c> backed.
        /// </summary>
        private static readonly string WebsitePhysicalAppPath = TestConfiguration.DefaultPhysicalAppPath;

        private readonly NameValueCollection formVars = new NameValueCollection();
        private readonly NameValueCollection headers = new NameValueCollection();
        private StringBuilder builder;
        private Uri referer;

        private string applicationPath = "/";

        private string physicalApplicationPath = WebsitePhysicalAppPath;

        private string physicalPath = WebsitePhysicalAppPath;

        /// <summary>Initializes a new instance of the <see cref="HttpSimulator"/> class for the root application.</summary>
        public HttpSimulator()
            : this("/", WebsitePhysicalAppPath)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="HttpSimulator"/> class.</summary>
        /// <param name="applicationPath">The IIS virtual directory of the simulated application.</param>
        public HttpSimulator(string applicationPath)
            : this(applicationPath, WebsitePhysicalAppPath)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="HttpSimulator"/> class.</summary>
        /// <param name="applicationPath">The IIS virtual directory of the simulated application.</param>
        /// <param name="physicalApplicationPath">The physical path of the simulated application.</param>
        public HttpSimulator(string applicationPath, string physicalApplicationPath)
        {
            this.ApplicationPath = applicationPath;
            this.PhysicalApplicationPath = physicalApplicationPath;
        }

        /// <summary>
        /// Gets the process-wide application-state dictionary that replaces the legacy
        /// <c>HttpContext.Current.Application</c> (<c>HttpApplicationFactory._state</c>).
        /// </summary>
        /// <remarks>
        /// ASP.NET Core has no per-application state bag, so this static dictionary takes its
        /// place. It is intentionally exposed so that tests which asserted on
        /// <c>HttpContext.Current.Application["key"]</c> keep an equivalent assertion target.
        /// It is also published on <see cref="HttpContext.Items"/> under
        /// <see cref="ApplicationStateItemKey"/> for every simulated request.
        /// </remarks>
        public static IDictionary<string, object> ApplicationState => ApplicationStateStore;

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
                this.ResponseWriter?.Flush();
                return (this.builder ?? new StringBuilder()).ToString();
            }
        }

        /// <summary>Gets the host name of the simulated request.</summary>
        public string Host { get; private set; }

        /// <summary>Gets the local path of the simulated request's URL.</summary>
        public string LocalPath { get; private set; }

        /// <summary>Gets the port of the simulated request.</summary>
        public int Port { get; private set; }

        /// <summary>Gets portion of the URL after the application.</summary>
        public string Page { get; private set; }

        /// <summary>
        /// Gets or sets the same thing as the IIS Virtual directory. It's
        /// what gets returned by <c>Request.PathBase</c> (and therefore by
        /// <see cref="HttpContextSource.ApplicationVirtualPath"/>).
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

                // Strip the trailing separator and re-append the host's own separator: the
                // simulated application path stands in for Request.PhysicalApplicationPath, which
                // always uses the running host's separator, and the code under test combines and
                // trims it with the Path APIs.
                this.physicalApplicationPath =
                    StripTrailingBackSlashes(this.physicalApplicationPath) + Path.DirectorySeparatorChar;
            }
        }

        /// <summary>Gets or sets the writer that captures everything written to the simulated response.</summary>
        public TextWriter ResponseWriter { get; set; }

        /// <summary>Gets the description of the simulated request that was projected onto <see cref="Context"/>.</summary>
        public SimulatedHttpRequest WorkerRequest { get; private set; }

        /// <summary>Gets the simulated <see cref="HttpContext"/> that was published through <see cref="HttpContextSource"/>.</summary>
        /// <value>The context, or <see langword="null"/> before <c>SimulateRequest</c> is called.</value>
        public HttpContext Context { get; private set; }

        /// <summary>Gets the in-memory session of the simulated request.</summary>
        /// <value>The session, or <see langword="null"/> before <c>SimulateRequest</c> is called.</value>
        public FakeHttpSessionState Session { get; private set; }

        /// <summary>Gets the fake hosting environment registered for the simulated request.</summary>
        /// <value>The environment, or <see langword="null"/> before <c>SimulateRequest</c> is called.</value>
        public IWebHostEnvironment HostingEnvironment { get; private set; }

        /// <summary>Sets up the HttpContext objects to simulate a GET request.</summary>
        /// <remarks>
        /// Simulates a request to http://localhost/.
        /// </remarks>
        /// <returns>This instance, for chaining.</returns>
        public HttpSimulator SimulateRequest()
        {
            return this.SimulateRequest(new Uri("http://localhost/"));
        }

        /// <summary>Sets up the HttpContext objects to simulate a GET request.</summary>
        /// <param name="url">The URL to simulate.</param>
        /// <returns>This instance, for chaining.</returns>
        public HttpSimulator SimulateRequest(Uri url)
        {
            return this.SimulateRequest(url, HttpVerb.GET);
        }

        /// <summary>Sets up the HttpContext objects to simulate a request.</summary>
        /// <param name="url">The URL to simulate.</param>
        /// <param name="httpVerb">The HTTP verb to simulate.</param>
        /// <returns>This instance, for chaining.</returns>
        public HttpSimulator SimulateRequest(Uri url, HttpVerb httpVerb)
        {
            return this.SimulateRequest(url, httpVerb, null, null);
        }

        /// <summary>Sets up the HttpContext objects to simulate a POST request.</summary>
        /// <param name="url">The URL to simulate.</param>
        /// <param name="formVariables">The form variables to post.</param>
        /// <returns>This instance, for chaining.</returns>
        public HttpSimulator SimulateRequest(Uri url, NameValueCollection formVariables)
        {
            return this.SimulateRequest(url, HttpVerb.POST, formVariables, null);
        }

        /// <summary>Sets up the HttpContext objects to simulate a POST request.</summary>
        /// <param name="url">The URL to simulate.</param>
        /// <param name="formVariables">The form variables to post.</param>
        /// <param name="headers">The request headers.</param>
        /// <returns>This instance, for chaining.</returns>
        public HttpSimulator SimulateRequest(Uri url, NameValueCollection formVariables, NameValueCollection headers)
        {
            return this.SimulateRequest(url, HttpVerb.POST, formVariables, headers);
        }

        /// <summary>Sets up the HttpContext objects to simulate a request.</summary>
        /// <param name="url">The URL to simulate.</param>
        /// <param name="httpVerb">The HTTP verb to simulate.</param>
        /// <param name="headers">The request headers.</param>
        /// <returns>This instance, for chaining.</returns>
        public HttpSimulator SimulateRequest(Uri url, HttpVerb httpVerb, NameValueCollection headers)
        {
            return this.SimulateRequest(url, httpVerb, null, headers);
        }

        /// <summary>Sets the referer for the request. Uses a fluent interface.</summary>
        /// <param name="referer">The referer URL, written as the <c>Referer</c> request header.</param>
        /// <returns>This instance, for chaining.</returns>
        public HttpSimulator SetReferer(Uri referer)
        {
            if (this.WorkerRequest != null)
            {
                this.WorkerRequest.SetReferer(referer);

                // The context has already been built, so write the header through directly.
                if (this.Context != null)
                {
                    if (referer == null)
                    {
                        this.Context.Request.Headers.Remove("Referer");
                    }
                    else
                    {
                        this.Context.Request.Headers["Referer"] = referer.ToString();
                    }
                }
            }

            this.referer = referer;
            return this;
        }

        /// <summary>Sets a form variable.</summary>
        /// <param name="name">The form field name.</param>
        /// <param name="value">The form field value.</param>
        /// <returns>This instance, for chaining.</returns>
        public HttpSimulator SetFormVariable(string name, string value)
        {
            if (this.WorkerRequest != null)
            {
                throw new InvalidOperationException("Cannot set form variables after calling Simulate().");
            }

            this.formVars.Add(name, value);

            return this;
        }

        /// <summary>Sets a header value.</summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The header value.</param>
        /// <returns>This instance, for chaining.</returns>
        public HttpSimulator SetHeader(string name, string value)
        {
            if (this.WorkerRequest != null)
            {
                throw new InvalidOperationException("Cannot set headers after calling Simulate().");
            }

            this.headers.Add(name, value);

            return this;
        }

        /// <summary>Maps a virtual path to a physical path, the way the old <c>ConfigMapPath</c> did.</summary>
        /// <param name="virtualPath">The virtual path to map.</param>
        /// <returns>The physical path.</returns>
        /// <remarks>
        /// This is the behaviour the old <c>internal class ConfigMapPath : IConfigMapPath</c>
        /// installed into <c>HostingEnvironment._configMapPath</c>: strip the application path
        /// off the front and combine the remainder with the physical application path.
        /// </remarks>
        public string MapPath(string virtualPath)
        {
            var requestedPage = StripPrecedingSlashes(RightAfter(virtualPath ?? string.Empty, this.ApplicationPath));
            return Path.Combine(this.PhysicalApplicationPath, requestedPage.Replace("/", @"\"));
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        /// <filterpriority>2.</filterpriority>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Gets the hosting environment that backs <see cref="HttpContextSource.MapPath"/>,
        /// registering a fake one for the default website path when the host has not registered
        /// one yet.
        /// </summary>
        /// <returns>The registered <see cref="IWebHostEnvironment"/>, never <see langword="null"/>.</returns>
        /// <remarks>
        /// This is the modern replacement for
        /// <c>protected static HostingEnvironment GetHostingEnvironment()</c>, which tried to
        /// construct a <c>HostingEnvironment</c> and fell back to reading the
        /// <c>_theHostingEnvironment</c> static field by reflection.
        /// </remarks>
        protected static IWebHostEnvironment GetHostingEnvironment()
        {
            try
            {
                return HttpContextSource.WebHostEnvironment;
            }
            catch (InvalidOperationException)
            {
                // Nothing registered yet — stand one up so callers get a usable environment,
                // mirroring the old fallback that dug the singleton out by reflection.
                var environment = new FakeWebHostEnvironment(WebsitePhysicalAppPath);
                HttpContextSource.RegisterWebHostEnvironment(environment);
                return environment;
            }
        }

        /// <summary>Normalizes the slashes of a virtual path.</summary>
        /// <param name="s">The path to normalize.</param>
        /// <returns>The normalized path, always starting with (and never ending with) a single slash.</returns>
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

        /// <summary>Strips every leading forward slash from a string.</summary>
        /// <param name="s">The string to strip.</param>
        /// <returns>The stripped string.</returns>
        protected static string StripPrecedingSlashes(string s)
        {
            return Regex.Replace(s, "^/*(.*)", "$1");
        }

        /// <summary>Strips every trailing forward slash from a string.</summary>
        /// <param name="s">The string to strip.</param>
        /// <returns>The stripped string.</returns>
        protected static string StripTrailingSlashes(string s)
        {
            return Regex.Replace(s, "(.*)/*$", "$1", RegexOptions.RightToLeft);
        }

        /// <summary>Strips every trailing backslash from a string.</summary>
        /// <param name="s">The string to strip.</param>
        /// <returns>The stripped string.</returns>
        protected static string StripTrailingBackSlashes(string s)
        {
            // Both separators, not just the Windows one: on a non-Windows host the incoming
            // path ends with '/'.
            return string.IsNullOrEmpty(s) ? string.Empty : s.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '\\');
        }

        /// <summary>Sets up the HttpContext objects to simulate a request.</summary>
        /// <param name="url">The URL to simulate.</param>
        /// <param name="httpVerb">The HTTP verb to simulate.</param>
        /// <param name="formVariables">The form variables to post, if any.</param>
        /// <param name="headers">The request headers, if any.</param>
        /// <returns>This instance, for chaining.</returns>
        protected virtual HttpSimulator SimulateRequest(Uri url, HttpVerb httpVerb, NameValueCollection formVariables, NameValueCollection headers)
        {
            HttpContextSource.RegisterInstance(null);
            this.Context = null;

            this.ParseRequestUrl(url);

            if (this.ResponseWriter == null)
            {
                this.builder = new StringBuilder();
                this.ResponseWriter = new StringWriter(this.builder);
            }

            this.SetHostingEnvironment();

            var query = ExtractQueryStringPart(url);

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

            this.WorkerRequest = new SimulatedHttpRequest(this.ApplicationPath, this.PhysicalApplicationPath, this.PhysicalPath, this.Page, query, this.ResponseWriter, this.Host, this.Port, httpVerb.ToString());

            this.WorkerRequest.Form.Add(this.formVars);
            this.WorkerRequest.Headers.Add(this.headers);

            if (this.referer != null)
            {
                this.WorkerRequest.SetReferer(this.referer);
            }

            var context = new DefaultHttpContext();
            context.Response.Body = new ResponseCaptureStream(this.ResponseWriter);

            this.WorkerRequest.ApplyTo(context);

            this.InitializeSession(context);

            InitializeApplication(context);

            this.Context = context;
            HttpContextSource.RegisterInstance(context);

            return this;
        }

        /// <summary>Releases the resources used by this simulator and clears the published context.</summary>
        /// <param name="disposing"><see langword="true"/> when called from <see cref="Dispose()"/>.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            // The net48 implementation set HttpContext.Current = null here; the modern
            // equivalent is to clear the instance registered with HttpContextSource.
            HttpContextSource.RegisterInstance(null);
            this.Context = null;
        }

        private static void InitializeApplication(HttpContext context)
        {
            // net48 reflected HttpApplicationFactory._theApplicationFactory._state into place so
            // that HttpContext.Current.Application worked. There is no application-state bag in
            // ASP.NET Core, so the process-wide dictionary is published on Items instead.
            context.Items[ApplicationStateItemKey] = ApplicationState;
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

            // mod to add one onto the search length - otherwise strange results??
            // return original.Substring(original.IndexOf(search) + search.Length + 1);
            // return original.Substring(original.IndexOf(search) + search.Length);
            // original = original.Substring(searchIndex, original.Length - searchIndex);
            // return original.Replace(search,"");
            var regexMatch = @"(" + search + ")(?<keep>.+)";
            const string regexReplace = @"${keep}";
            var result = Regex.Replace(original, regexMatch, regexReplace, RegexOptions.IgnoreCase);
            return result;
        }

        private static string ExtractQueryStringPart(Uri url)
        {
            var query = url.Query;
            return query.StartsWith("?", StringComparison.Ordinal) ? query.Substring(1) : query;
        }

        private void InitializeSession(HttpContext context)
        {
            context.Items.Clear();
            this.Session = new FakeHttpSessionState();
            context.Features.Set<ISessionFeature>(new SimulatedSessionFeature { Session = this.Session });
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
            this.physicalPath = Path.Combine(this.physicalApplicationPath, this.Page.Replace('/', Path.DirectorySeparatorChar));
        }

        private void SetHostingEnvironment()
        {
            // net48 cheated by using reflection to poke HttpRuntime._appDomainAppPath,
            // HttpRuntime._appDomainAppVPath, HttpRuntime._codegenDir,
            // HostingEnvironment._appPhysicalPath / _appVirtualPath / _configMapPath.
            // The modern equivalent is a single registered IWebHostEnvironment: it backs
            // HttpContextSource.ApplicationPhysicalPath and HttpContextSource.MapPath, while the
            // virtual path comes from Request.PathBase (see SimulatedHttpRequest.ApplyTo).
            this.HostingEnvironment = new FakeWebHostEnvironment(this.PhysicalApplicationPath);
            HttpContextSource.RegisterWebHostEnvironment(this.HostingEnvironment);
        }

        /// <summary>
        /// An in-memory <see cref="ISession"/> that replaces the net48
        /// <c>NameObjectCollectionBase, IHttpSessionState</c> fake.
        /// </summary>
        /// <remarks>
        /// The get/set, remove, clear and enumeration behaviour of the old fake is preserved.
        /// Values are stored as the <see langword="byte"/> arrays <see cref="ISession"/> deals in,
        /// so the standard <c>ISession.SetString</c>/<c>GetString</c> extensions work; the
        /// <see cref="this[string]"/> indexer keeps the old object-oriented ergonomics by
        /// round-tripping strings through UTF-8.
        /// </remarks>
        public class FakeHttpSessionState : ISession
        {
            private readonly Dictionary<string, byte[]> store = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            private readonly string sessionId = Guid.NewGuid().ToString();
            private readonly object syncRoot = new object();
            private int timeout = 30; // minutes

            /// <summary>Gets the unique session identifier for the session.</summary>
            /// <returns>The session ID.</returns>
            public string SessionID
            {
                get { return this.sessionId; }
            }

            /// <inheritdoc />
            public string Id
            {
                get { return this.sessionId; }
            }

            /// <inheritdoc />
            public bool IsAvailable
            {
                get { return true; }
            }

            /// <inheritdoc />
            public IEnumerable<string> Keys
            {
                get
                {
                    lock (this.syncRoot)
                    {
                        return new List<string>(this.store.Keys);
                    }
                }
            }

            /// <summary>Gets a value indicating whether the session was created with the current request.</summary>
            /// <returns><see langword="true"/>; a simulated session is always brand new.</returns>
            public bool IsNewSession
            {
                get { return true; }
            }

            /// <summary>Gets a value indicating whether the session ID is embedded in the URL.</summary>
            /// <returns><see langword="false"/>; the simulation always uses cookies.</returns>
            public bool IsCookieless
            {
                get { return false; }
            }

            /// <summary>Gets an object that can be used to synchronize access to the collection of session-state values.</summary>
            /// <returns>An object that can be used to synchronize access to the collection.</returns>
            public object SyncRoot
            {
                get { return this.syncRoot; }
            }

            /// <summary>Gets a value indicating whether access to the collection of session-state values is synchronized (thread safe).</summary>
            /// <returns><see langword="true"/> if access to the collection is synchronized (thread safe); otherwise, <see langword="false"/>.</returns>
            public bool IsSynchronized
            {
                get { return true; }
            }

            /// <summary>Gets a value indicating whether the session is read-only.</summary>
            /// <returns><see langword="false"/>; the simulated session is writable.</returns>
            public bool IsReadOnly
            {
                get { return false; }
            }

            /// <summary>Gets the number of items in the session-state item collection.</summary>
            public int Count
            {
                get
                {
                    lock (this.syncRoot)
                    {
                        return this.store.Count;
                    }
                }
            }

            /// <summary>Gets or sets the time-out period (in minutes) allowed between requests before the session-state provider terminates the session.</summary>
            /// <returns>The time-out period, in minutes.</returns>
            public int Timeout
            {
                get { return this.timeout; }
                set { this.timeout = value; }
            }

            /// <summary>Gets or sets the locale identifier (LCID) of the current session.</summary>
            public int LCID { get; set; }

            /// <summary>Gets or sets the code-page identifier for the current session.</summary>
            public int CodePage { get; set; }

            /// <summary>Gets or sets a session-state item value by name.</summary>
            /// <param name="name">The key name of the session-state item value.</param>
            /// <returns>The session-state item value specified in the name parameter, or <see langword="null"/> when absent.</returns>
            public object this[string name]
            {
                get
                {
                    if (name == null)
                    {
                        return null;
                    }

                    lock (this.syncRoot)
                    {
                        return this.store.TryGetValue(name, out var value) && value != null
                            ? Encoding.UTF8.GetString(value)
                            : null;
                    }
                }

                set
                {
                    if (name == null)
                    {
                        throw new ArgumentNullException(nameof(name));
                    }

                    lock (this.syncRoot)
                    {
                        if (value == null)
                        {
                            this.store.Remove(name);
                        }
                        else
                        {
                            this.store[name] = Encoding.UTF8.GetBytes(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
                        }
                    }
                }
            }

            /// <summary>Gets or sets a session-state item value by numerical index.</summary>
            /// <param name="index">The numerical index of the session-state item value.</param>
            /// <returns>The session-state item value specified in the index parameter.</returns>
            public object this[int index]
            {
                get { return this[this.KeyAt(index)]; }
                set { this[this.KeyAt(index)] = value; }
            }

            /// <summary>Ends the current session.</summary>
            public void Abandon()
            {
                this.Clear();
            }

            /// <summary>Adds a new item to the session-state collection.</summary>
            /// <param name="name">The name of the item to add to the session-state collection.</param>
            /// <param name="value">The value of the item to add to the session-state collection.</param>
            public void Add(string name, object value)
            {
                this[name] = value;
            }

            /// <summary>Deletes an item from the session-state item collection.</summary>
            /// <param name="name">The name of the item to delete from the session-state item collection.</param>
            public void Remove(string name)
            {
                if (name == null)
                {
                    return;
                }

                lock (this.syncRoot)
                {
                    this.store.Remove(name);
                }
            }

            /// <summary>Deletes an item at a specified index from the session-state item collection.</summary>
            /// <param name="index">The index of the item to remove from the session-state collection.</param>
            public void RemoveAt(int index)
            {
                this.Remove(this.KeyAt(index));
            }

            /// <summary>Clears all values from the session-state item collection.</summary>
            public void Clear()
            {
                lock (this.syncRoot)
                {
                    this.store.Clear();
                }
            }

            /// <summary>Clears all values from the session-state item collection.</summary>
            public void RemoveAll()
            {
                this.Clear();
            }

            /// <summary>Copies the collection of session-state item values to a one-dimensional array, starting at the specified index in the array.</summary>
            /// <param name="array">The <see cref="Array"/> that receives the session values.</param>
            /// <param name="index">The index in array where copying starts.</param>
            public void CopyTo(Array array, int index)
            {
                if (array == null)
                {
                    throw new ArgumentNullException(nameof(array));
                }

                lock (this.syncRoot)
                {
                    var i = index;
                    foreach (var key in this.store.Keys)
                    {
                        array.SetValue(key, i++);
                    }
                }
            }

            /// <inheritdoc />
            public void Set(string key, byte[] value)
            {
                if (key == null)
                {
                    throw new ArgumentNullException(nameof(key));
                }

                lock (this.syncRoot)
                {
                    this.store[key] = value;
                }
            }

            /// <inheritdoc />
            public bool TryGetValue(string key, out byte[] value)
            {
                lock (this.syncRoot)
                {
                    return this.store.TryGetValue(key ?? string.Empty, out value);
                }
            }

            /// <inheritdoc />
            public Task LoadAsync(CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            /// <inheritdoc />
            public Task CommitAsync(CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            private string KeyAt(int index)
            {
                lock (this.syncRoot)
                {
                    var i = 0;
                    foreach (var key in this.store.Keys)
                    {
                        if (i++ == index)
                        {
                            return key;
                        }
                    }
                }

                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        /// <summary>
        /// A fake <see cref="IWebHostEnvironment"/> whose content root and web root both point at
        /// the simulated application's physical path. Replaces the net48
        /// <c>HostingEnvironment</c> reflection and the <c>ConfigMapPath : IConfigMapPath</c>
        /// implementation that used to be installed into it.
        /// </summary>
        public class FakeWebHostEnvironment : IWebHostEnvironment
        {
            /// <summary>Initializes a new instance of the <see cref="FakeWebHostEnvironment"/> class.</summary>
            /// <param name="physicalApplicationPath">The physical path of the simulated application.</param>
            public FakeWebHostEnvironment(string physicalApplicationPath)
            {
                var root = string.IsNullOrEmpty(physicalApplicationPath)
                    ? AppContext.BaseDirectory
                    : physicalApplicationPath;

                this.ApplicationName = typeof(HttpSimulator).Assembly.GetName().Name;
                this.EnvironmentName = "Development";
                this.ContentRootPath = root;
                this.WebRootPath = root;
                this.ContentRootFileProvider = CreateFileProvider(root);
                this.WebRootFileProvider = this.ContentRootFileProvider;
            }

            /// <inheritdoc />
            public string ApplicationName { get; set; }

            /// <inheritdoc />
            public IFileProvider ContentRootFileProvider { get; set; }

            /// <inheritdoc />
            public string ContentRootPath { get; set; }

            /// <inheritdoc />
            public string EnvironmentName { get; set; }

            /// <inheritdoc />
            public IFileProvider WebRootFileProvider { get; set; }

            /// <inheritdoc />
            public string WebRootPath { get; set; }

            private static IFileProvider CreateFileProvider(string root)
            {
                try
                {
                    return Directory.Exists(root)
                        ? new PhysicalFileProvider(root)
                        : (IFileProvider)new NullFileProvider();
                }
                catch (ArgumentException)
                {
                    // The simulated path is frequently a Windows-shaped literal such as
                    // "c:\inetpub\" which is not a valid rooted path on every OS.
                    return new NullFileProvider();
                }
            }
        }

        /// <summary>Carries the simulated <see cref="ISession"/> on <see cref="HttpContext.Features"/>.</summary>
        private sealed class SimulatedSessionFeature : ISessionFeature
        {
            public ISession Session { get; set; }
        }

        /// <summary>
        /// A write-only <see cref="Stream"/> that forwards everything written to the simulated
        /// <c>Response.Body</c> into the simulator's <see cref="HttpSimulator.ResponseWriter"/>,
        /// so <see cref="HttpSimulator.ResponseText"/> keeps working exactly as it did when the
        /// net48 <c>SimpleWorkerRequest</c> wrote into the same <see cref="TextWriter"/>.
        /// </summary>
        private sealed class ResponseCaptureStream : Stream
        {
            private readonly TextWriter writer;
            private long length;

            public ResponseCaptureStream(TextWriter writer)
            {
                this.writer = writer;
            }

            public override bool CanRead => false;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length => this.length;

            public override long Position
            {
                get => this.length;
                set => throw new NotSupportedException("The simulated response stream is write-only and not seekable.");
            }

            public override void Flush()
            {
                this.writer?.Flush();
            }

            public override Task FlushAsync(CancellationToken cancellationToken)
            {
                this.Flush();
                return Task.CompletedTask;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException("The simulated response stream is write-only.");
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException("The simulated response stream is not seekable.");
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException("The simulated response stream is not seekable.");
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (buffer == null)
                {
                    throw new ArgumentNullException(nameof(buffer));
                }

                this.Write(new ReadOnlySpan<byte>(buffer, offset, count));
            }

            public override void Write(ReadOnlySpan<byte> buffer)
            {
                if (buffer.IsEmpty)
                {
                    return;
                }

                this.writer?.Write(Encoding.UTF8.GetString(buffer));
                this.length += buffer.Length;
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                this.Write(buffer, offset, count);
                return Task.CompletedTask;
            }

            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                this.Write(buffer.Span);
                return default;
            }
        }
    }
}
