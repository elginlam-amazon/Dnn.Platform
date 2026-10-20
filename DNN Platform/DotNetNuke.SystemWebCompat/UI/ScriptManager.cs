// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.UI
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;

    /// <summary>
    /// Replacement for the System.Web.Extensions <c>ScriptManager</c> on .NET 10.
    /// </summary>
    /// <remarks>
    /// The Microsoft AJAX partial rendering infrastructure (async postbacks of <see cref="UpdatePanel"/> controls) is not
    /// available on ASP.NET Core, so every postback is a full postback: <see cref="IsInAsyncPostBack"/> is always
    /// <see langword="false"/> and the script registration helpers delegate to the page's <see cref="ClientScriptManager"/>.
    /// </remarks>
    [NonVisualControl]
    [ParseChildren(true)]
    [PersistChildren(false)]
    public class ScriptManager : Control
    {
        private static readonly object ItemsKey = typeof(ScriptManager);

        private readonly List<Control> postBackControls = new List<Control>();
        private readonly List<Control> asyncPostBackControls = new List<Control>();
        private readonly Dictionary<Control, string> dataItems = new Dictionary<Control, string>();
        private ScriptReferenceCollection scripts;

        /// <summary>Gets a value indicating whether the current postback is an asynchronous (partial rendering) postback. Always <see langword="false"/> on .NET 10.</summary>
        [Browsable(false)]
        public bool IsInAsyncPostBack => false;

        /// <summary>Gets or sets a value indicating whether the client supports partial page rendering (kept for compatibility, partial rendering is not available on .NET 10).</summary>
        [Browsable(false)]
        public bool SupportsPartialRendering { get; set; }

        /// <summary>Gets or sets a value indicating whether partial page rendering is enabled (kept for compatibility, partial rendering is not available on .NET 10).</summary>
        public bool EnablePartialRendering { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the client script globalization is enabled.</summary>
        public bool EnableScriptGlobalization { get; set; }

        /// <summary>Gets or sets a value indicating whether the client script localization is enabled.</summary>
        public bool EnableScriptLocalization { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether scripts are loaded from a CDN.</summary>
        public bool EnableCdn { get; set; }

        /// <summary>Gets or sets a value indicating whether a fallback to the local script is rendered when the CDN is not available.</summary>
        public bool EnableCdnFallback { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the <c>PageMethods</c> client proxy of the page methods (see <see cref="PageMethodsHandler"/>) is rendered.</summary>
        public bool EnablePageMethods { get; set; }

        /// <summary>Gets or sets the asynchronous postback time out in seconds.</summary>
        public int AsyncPostBackTimeout { get; set; } = 90;

        /// <summary>Gets the script references that are rendered by this manager.</summary>
        [PersistenceMode(PersistenceMode.InnerProperty)]
        public ScriptReferenceCollection Scripts => this.scripts ??= new ScriptReferenceCollection();

        /// <summary>Gets the controls that were registered to always cause a full postback.</summary>
        internal IReadOnlyList<Control> PostBackControls => this.postBackControls;

        /// <summary>Gets the controls that were registered for asynchronous postbacks.</summary>
        internal IReadOnlyList<Control> AsyncPostBackControls => this.asyncPostBackControls;

        /// <summary>Gets the <see cref="ScriptManager"/> registered on the given page.</summary>
        /// <param name="page">The page.</param>
        /// <returns>The <see cref="ScriptManager"/> instance or <see langword="null"/>.</returns>
        public static ScriptManager GetCurrent(Page page)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            return page.Items[ItemsKey] as ScriptManager;
        }

        /// <summary>Registers a startup script block.</summary>
        /// <param name="page">The page.</param>
        /// <param name="type">The type used as the key scope.</param>
        /// <param name="key">The key.</param>
        /// <param name="script">The script.</param>
        /// <param name="addScriptTags">Whether to wrap the script in script tags.</param>
        public static void RegisterStartupScript(Page page, Type type, string key, string script, bool addScriptTags)
        {
            GetClientScript(page).RegisterStartupScript(type, key, script, addScriptTags);
        }

        /// <summary>Registers a startup script block.</summary>
        /// <param name="control">The control registering the script.</param>
        /// <param name="type">The type used as the key scope.</param>
        /// <param name="key">The key.</param>
        /// <param name="script">The script.</param>
        /// <param name="addScriptTags">Whether to wrap the script in script tags.</param>
        public static void RegisterStartupScript(Control control, Type type, string key, string script, bool addScriptTags)
        {
            RegisterStartupScript(GetPage(control), type, key, script, addScriptTags);
        }

        /// <summary>Registers a client script block.</summary>
        /// <param name="page">The page.</param>
        /// <param name="type">The type used as the key scope.</param>
        /// <param name="key">The key.</param>
        /// <param name="script">The script.</param>
        /// <param name="addScriptTags">Whether to wrap the script in script tags.</param>
        public static void RegisterClientScriptBlock(Page page, Type type, string key, string script, bool addScriptTags)
        {
            GetClientScript(page).RegisterClientScriptBlock(type, key, script, addScriptTags);
        }

        /// <summary>Registers a client script block.</summary>
        /// <param name="control">The control registering the script.</param>
        /// <param name="type">The type used as the key scope.</param>
        /// <param name="key">The key.</param>
        /// <param name="script">The script.</param>
        /// <param name="addScriptTags">Whether to wrap the script in script tags.</param>
        public static void RegisterClientScriptBlock(Control control, Type type, string key, string script, bool addScriptTags)
        {
            RegisterClientScriptBlock(GetPage(control), type, key, script, addScriptTags);
        }

        /// <summary>Registers a client script include.</summary>
        /// <param name="page">The page.</param>
        /// <param name="type">The type used as the key scope.</param>
        /// <param name="key">The key.</param>
        /// <param name="url">The script URL.</param>
        public static void RegisterClientScriptInclude(Page page, Type type, string key, string url)
        {
            GetClientScript(page).RegisterClientScriptInclude(type, key, url);
        }

        /// <summary>Registers a client script include.</summary>
        /// <param name="control">The control registering the script.</param>
        /// <param name="type">The type used as the key scope.</param>
        /// <param name="key">The key.</param>
        /// <param name="url">The script URL.</param>
        public static void RegisterClientScriptInclude(Control control, Type type, string key, string url)
        {
            RegisterClientScriptInclude(GetPage(control), type, key, url);
        }

        /// <summary>Registers an on submit statement.</summary>
        /// <param name="page">The page.</param>
        /// <param name="type">The type used as the key scope.</param>
        /// <param name="key">The key.</param>
        /// <param name="script">The script.</param>
        public static void RegisterOnSubmitStatement(Page page, Type type, string key, string script)
        {
            GetClientScript(page).RegisterOnSubmitStatement(type, key, script);
        }

        /// <summary>Registers an on submit statement.</summary>
        /// <param name="control">The control registering the script.</param>
        /// <param name="type">The type used as the key scope.</param>
        /// <param name="key">The key.</param>
        /// <param name="script">The script.</param>
        public static void RegisterOnSubmitStatement(Control control, Type type, string key, string script)
        {
            RegisterOnSubmitStatement(GetPage(control), type, key, script);
        }

        /// <summary>Registers a control that always causes a full postback.</summary>
        /// <param name="control">The control.</param>
        public void RegisterPostBackControl(Control control)
        {
            if (control != null && !this.postBackControls.Contains(control))
            {
                this.postBackControls.Add(control);
            }
        }

        /// <summary>Registers a control for asynchronous postbacks (results in a full postback on .NET 10).</summary>
        /// <param name="control">The control.</param>
        public void RegisterAsyncPostBackControl(Control control)
        {
            if (control != null && !this.asyncPostBackControls.Contains(control))
            {
                this.asyncPostBackControls.Add(control);
            }
        }

        /// <summary>Registers a data item which is sent to the client during an asynchronous postback.</summary>
        /// <param name="control">The control.</param>
        /// <param name="dataItem">The data item.</param>
        public void RegisterDataItem(Control control, string dataItem)
        {
            this.RegisterDataItem(control, dataItem, false);
        }

        /// <summary>Registers a data item which is sent to the client during an asynchronous postback.</summary>
        /// <param name="control">The control.</param>
        /// <param name="dataItem">The data item.</param>
        /// <param name="isJsonSerialized">Whether the data item is already JSON serialized.</param>
        public void RegisterDataItem(Control control, string dataItem, bool isJsonSerialized)
        {
            if (control == null)
            {
                throw new ArgumentNullException(nameof(control));
            }

            this.dataItems[control] = dataItem;
        }

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            if (this.Page != null && !this.DesignMode)
            {
                if (GetCurrent(this.Page) != null && !ReferenceEquals(GetCurrent(this.Page), this))
                {
                    throw new InvalidOperationException("Only one instance of a ScriptManager can be added to the page.");
                }

                this.Page.Items[ItemsKey] = this;
            }
        }

        /// <inheritdoc />
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
            if (this.Page == null)
            {
                return;
            }

            if (this.EnablePageMethods)
            {
                this.RegisterPageMethodsProxy();
            }

            foreach (var reference in this.Scripts)
            {
                var url = reference.GetUrl(this);
                if (!string.IsNullOrEmpty(url))
                {
                    this.Page.ClientScript.RegisterClientScriptInclude(typeof(ScriptManager), url, url);
                }
            }
        }

        /// <inheritdoc />
        protected override void Render(HtmlTextWriter writer)
        {
            // Nothing to render, the registered scripts are rendered through the page's ClientScriptManager.
        }

        private static string JavaScriptString(string value)
        {
            return HttpUtility.JavaScriptStringEncode(value, true);
        }

        private static Page GetPage(Control control)
        {
            if (control == null)
            {
                throw new ArgumentNullException(nameof(control));
            }

            return control.Page ?? throw new ArgumentException("The control must be added to a page.", nameof(control));
        }

        private static ClientScriptManager GetClientScript(Page page)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            return page.ClientScript;
        }

        private void RegisterPageMethodsProxy()
        {
            var methods = PageMethodsHandler.GetPageMethods(this.Page.GetType());
            if (methods.Length == 0)
            {
                return;
            }

            var script = new System.Text.StringBuilder();
            script.Append("var PageMethods = window.PageMethods || {};");
            script.Append("PageMethods._call = function (name, args, onSuccess, onFailure, userContext) {");
            script.Append("var xhr = new XMLHttpRequest();");
            script.Append("xhr.open('POST', ").Append(JavaScriptString(this.Page.Request.FilePath)).Append(" + '/' + name, true);");
            script.Append("xhr.setRequestHeader('Content-Type', 'application/json; charset=utf-8');");
            script.Append("xhr.onreadystatechange = function () { if (xhr.readyState !== 4) { return; }");
            script.Append("var response = null; try { response = JSON.parse(xhr.responseText); } catch (e) { }");
            script.Append("if (xhr.status >= 200 && xhr.status < 300) { if (onSuccess) { onSuccess(response ? response.d : null, userContext, name); } }");
            script.Append("else if (onFailure) { var message = response && response.Message ? response.Message : xhr.statusText;");
            script.Append("onFailure({ get_message: function () { return message; }, get_statusCode: function () { return xhr.status; } }, userContext, name); } };");
            script.Append("xhr.send(JSON.stringify(args)); };");
            foreach (var method in methods)
            {
                var parameters = method.GetParameters().Select(p => p.Name).ToArray();
                script.Append("PageMethods.").Append(method.Name).Append(" = function (");
                foreach (var parameter in parameters)
                {
                    script.Append(parameter).Append(", ");
                }

                script.Append("onSuccess, onFailure, userContext) { PageMethods._call(").Append(JavaScriptString(method.Name)).Append(", {");
                script.Append(string.Join(", ", parameters.Select(p => JavaScriptString(p) + ": " + p)));
                script.Append("}, onSuccess, onFailure, userContext); };");
            }

            this.Page.ClientScript.RegisterClientScriptBlock(typeof(ScriptManager), "PageMethods", script.ToString(), true);
        }
    }
}
