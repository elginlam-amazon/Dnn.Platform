// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Website.Components
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using DotNetNuke.Common.Utilities;
    using DotNetNuke.UI.Utilities;

    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// The host's <see cref="IClientApiPage"/>: the request-scoped replacement for the
    /// <c>System.Web.UI.Page</c> (and its <c>ClientScript</c> manager) that the DNN Client API was
    /// written against.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Client API is reached from three directions, and all three need a page:
    /// <c>DotNetNuke.WebControls</c> (<c>DNNMultiStateBox</c>, <c>DNNTabStrip</c>) read the posted
    /// request through <see cref="GetRequestValue"/>;
    /// <c>Library/UI/WebControls/PropertyEditor/PropertyEditorClientApiControl.cs</c> and
    /// <c>UI/Skins/Skin.cs</c> register keyed client scripts on it; and
    /// <c>UseDnnWebUtility()</c> resolves it from the request services to dispatch a Client API
    /// callback (<c>ClientAPI.HandleClientAPICallbackEvent</c>).
    /// </para>
    /// <para>
    /// WebForms kept the script registrations on the page and emitted them while the page rendered.
    /// Here they are kept per request/circuit on this scoped instance and emitted by
    /// <c>ClientApiScripts.razor</c>, which the host document renders at the end of
    /// <c>&lt;body&gt;</c> — the position the legacy <c>DnnFormBottomProvider</c> used.
    /// </para>
    /// <para>
    /// The control tree members come from <see cref="ClientApiControlBase"/>: this page is the root
    /// (it has no parent and is its own <see cref="Page"/>) and exposes a <see cref="Form"/> child
    /// with the identifier the WebForms <c>Default.aspx</c> form carried (<c>Form</c>), because
    /// client scripts address fields by <c>Form_&lt;id&gt;</c>. Controls that take part in a
    /// callback add themselves to <see cref="ClientApiControlBase.Children"/>, which is what
    /// <c>Globals.FindControlRecursive</c> walks.
    /// </para>
    /// </remarks>
    public sealed class HttpContextClientApiPage : ClientApiControlBase, IClientApiPage
    {
        /// <summary>The identifier of the DNN page form, as rendered by the legacy <c>Default.aspx</c>.</summary>
        public const string FormId = "Form";

        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly PageControl form;
        private readonly Dictionary<object, object> detachedItems = new Dictionary<object, object>();
        private readonly HashSet<string> registeredScriptKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<ClientScriptRegistration> scriptBlocks = new List<ClientScriptRegistration>();
        private readonly List<ClientScriptRegistration> startupScripts = new List<ClientScriptRegistration>();
        private readonly List<string> scriptIncludes = new List<string>();
        private readonly List<KeyValuePair<string, string>> hiddenFields = new List<KeyValuePair<string, string>>();

        /// <summary>Initializes a new instance of the <see cref="HttpContextClientApiPage"/> class.</summary>
        /// <param name="httpContextAccessor">The accessor that replaces <c>HttpContext.Current</c>.</param>
        public HttpContextClientApiPage(IHttpContextAccessor httpContextAccessor)
        {
            this.httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));

            this.Id = "DnnPage";
            this.form = new PageControl { Id = FormId };
            this.Children.Add(this.form);
        }

        /// <inheritdoc/>
        public HttpContext HttpContext => this.httpContextAccessor.HttpContext;

        /// <inheritdoc/>
        /// <remarks>
        /// Backed by <c>HttpContext.Items</c> during a request; an interactive Blazor circuit has no
        /// <c>HttpContext</c>, so the per-circuit bag of this scoped instance is used instead (the
        /// Client API only ever uses it to de-duplicate registrations within one page).
        /// </remarks>
        public IDictionary<object, object> Items => this.HttpContext?.Items ?? this.detachedItems;

        /// <inheritdoc/>
        public IServiceProvider RequestServices => this.HttpContext?.RequestServices;

        /// <inheritdoc/>
        public IClientApiControl Form => this.form;

        /// <inheritdoc/>
        /// <remarks>This instance IS the page, so it is its own <see cref="Page"/> — like a WebForms <c>Page</c>.</remarks>
        public override IClientApiPage Page => this;

        /// <summary>Gets the keyed script blocks registered for this page, in registration order.</summary>
        public IReadOnlyList<ClientScriptRegistration> ScriptBlocks => this.scriptBlocks;

        /// <summary>Gets the keyed start-up scripts registered for this page, in registration order.</summary>
        public IReadOnlyList<ClientScriptRegistration> StartupScripts => this.startupScripts;

        /// <summary>Gets the URLs of the script files registered for this page, in registration order.</summary>
        public IReadOnlyList<string> ScriptIncludes => this.scriptIncludes;

        /// <summary>Gets the hidden fields registered for this page, in registration order.</summary>
        public IReadOnlyList<KeyValuePair<string, string>> HiddenFields => this.hiddenFields;

        /// <summary>
        /// Gets the render method the Client API took over the page rendering with, or
        /// <see langword="null"/> when it did not.
        /// </summary>
        /// <remarks>
        /// Replaces the delegate <c>Page.SetRenderMethodDelegate</c> installed on the WebForms page.
        /// The rendering page component honours it in place of its own body when it is set.
        /// </remarks>
        public Action<IClientApiHtmlWriter, IClientApiControl> RenderMethod { get; private set; }

        /// <inheritdoc/>
        /// <remarks>
        /// Mirrors the WebForms <c>Request(key)</c> indexer, which probed the query string, then the
        /// form, then the cookies. The form is only read when the request actually has a form body —
        /// touching <c>Request.Form</c> otherwise throws on ASP.NET Core.
        /// </remarks>
        public string GetRequestValue(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            var request = this.HttpContext?.Request;
            if (request is null)
            {
                return null;
            }

            if (request.Query.TryGetValue(key, out var queried) && queried.Count > 0)
            {
                return queried[queried.Count - 1];
            }

            if (request.HasFormContentType &&
                request.Form.TryGetValue(key, out var posted) && posted.Count > 0)
            {
                return posted[posted.Count - 1];
            }

            return request.Cookies.TryGetValue(key, out var cookie) ? cookie : null;
        }

        /// <inheritdoc/>
        public bool IsClientScriptBlockRegistered(Type type, string key) =>
            this.registeredScriptKeys.Contains(ScopedKey(type, key));

        /// <inheritdoc/>
        public bool IsClientScriptBlockRegistered(string key) =>
            this.IsClientScriptBlockRegistered(this.GetType(), key);

        /// <inheritdoc/>
        public void RegisterClientScriptBlock(Type type, string key, string script)
        {
            if (!this.registeredScriptKeys.Add(ScopedKey(type, key)))
            {
                // WebForms registered a keyed block exactly once per page; later calls were no-ops.
                return;
            }

            this.scriptBlocks.Add(new ClientScriptRegistration(key, script));
        }

        /// <inheritdoc/>
        public void RegisterClientScriptInclude(string key, string url)
        {
            if (string.IsNullOrEmpty(url) ||
                !this.registeredScriptKeys.Add(ScopedKey(typeof(HttpContextClientApiPage), "include:" + key)))
            {
                return;
            }

            this.scriptIncludes.Add(url);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// The embedded scripts of <c>DotNetNuke.WebUtility</c> are served at <c>/js/&lt;name&gt;</c>
        /// by <c>MapDnnWebUtilityScripts()</c>; everything else is a Razor class library static
        /// asset, served from <c>_content/&lt;assembly&gt;/</c>.
        /// </remarks>
        public void RegisterClientScriptResource(Type type, string resourceName)
        {
            this.RegisterClientScriptInclude(resourceName, this.GetWebResourceUrl(type, resourceName));
        }

        /// <inheritdoc/>
        public void RegisterStartupScript(Type type, string key, string script, bool addScriptTags)
        {
            if (!this.registeredScriptKeys.Add(ScopedKey(type, "startup:" + key)))
            {
                return;
            }

            this.startupScripts.Add(new ClientScriptRegistration(key, script, addScriptTags));
        }

        /// <inheritdoc/>
        public void RegisterHiddenField(string name, string value)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            var existing = this.hiddenFields.FindIndex(field => string.Equals(field.Key, name, StringComparison.Ordinal));
            var field = new KeyValuePair<string, string>(name, value);
            if (existing >= 0)
            {
                this.hiddenFields[existing] = field;
                return;
            }

            this.hiddenFields.Add(field);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Emits the same call the WebForms <c>Page.ClientScript.GetPostBackEventReference</c>
        /// produced, so the DNN client scripts that embed the returned string (the Client API
        /// post-back controls) keep working unchanged against the posted
        /// <c>__EVENTTARGET</c>/<c>__EVENTARGUMENT</c> fields the Client API reads back.
        /// </remarks>
        public string GetPostBackEventReference(IClientApiControl control, string argument)
        {
            var target = control?.UniqueId ?? this.UniqueId;
            var encoder = System.Text.Encodings.Web.JavaScriptEncoder.Default;

            return string.Format(
                CultureInfo.InvariantCulture,
                "__doPostBack('{0}','{1}')",
                encoder.Encode(target ?? string.Empty),
                encoder.Encode(argument ?? string.Empty));
        }

        /// <inheritdoc/>
        public string GetWebResourceUrl(Type type, string resourceName)
        {
            if (string.IsNullOrEmpty(resourceName))
            {
                return null;
            }

            // The WebResource.axd served scripts of DotNetNuke.WebUtility are mapped at /js/<name>.
            var fileName = resourceName.Split('.').Length > 1 && resourceName.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
                ? resourceName.Substring(resourceName.LastIndexOf('/') + 1)
                : resourceName;

            if (resourceName.EndsWith(".js", StringComparison.OrdinalIgnoreCase) &&
                ScriptEndpointRouteBuilderExtensions.EmbeddedScriptNames
                    .Any(name => string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase)))
            {
                return "/js/" + fileName;
            }

            // Everything else ships as a static web asset of the declaring library.
            var assembly = (type ?? this.GetType()).Assembly.GetName().Name;

            return "/_content/" + assembly + "/" + resourceName.Replace('\\', '/').TrimStart('/');
        }

        /// <inheritdoc/>
        public void SetRenderMethodDelegate(Action<IClientApiHtmlWriter, IClientApiControl> renderMethod)
        {
            this.RenderMethod = renderMethod;
        }

        private static string ScopedKey(Type type, string key) =>
            (type?.FullName ?? string.Empty) + "|" + (key ?? string.Empty);

        /// <summary>A plain control of this page's tree (the page form and anything added to it).</summary>
        private sealed class PageControl : ClientApiControlBase
        {
        }
    }
}
