// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;

    using DotNetNuke.UI.Utilities;

    /// <summary>
    /// Base class of the controls this library provides.
    /// </summary>
    /// <remarks>
    /// Replaces <c>System.Web.UI.WebControls.WebControl</c>, which has no .NET 10 equivalent, by
    /// deriving from the <see cref="ClientApiControlBase"/> abstraction of the already migrated
    /// <c>DotNetNuke.WebUtility</c>. That base supplies the identifier composition
    /// (<see cref="ClientApiControlBase.Id"/>, <see cref="ClientApiControlBase.ClientId"/>,
    /// <see cref="ClientApiControlBase.UniqueId"/>), the attribute bag, the parent/child bookkeeping
    /// and the page reference the controls inherited from <c>WebControl</c>; this class adds the
    /// styling, visibility and rendering members they also used.
    /// <para>
    /// The original also carried an <c>__ENCAddToList</c> weak-reference list. That is a VB
    /// edit-and-continue debugging artifact emitted by the compiler with no observable behavior, so
    /// it is not carried over.
    /// </para>
    /// </remarks>
    public class BaseWebControl : ClientApiControlBase
    {
        private readonly Dictionary<string, string> style = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Initializes a new instance of the <see cref="BaseWebControl"/> class.</summary>
        public BaseWebControl()
            : this("span")
        {
        }

        /// <summary>Initializes a new instance of the <see cref="BaseWebControl"/> class.</summary>
        /// <param name="tagName">The name of the tag the control renders as.</param>
        /// <remarks>Replaces the <c>WebControl.New(HtmlTextWriterTag)</c> constructor.</remarks>
        public BaseWebControl(string tagName)
        {
            this.TagNameOverride = tagName;
        }

        /// <summary>
        /// Gets or sets the server side identifier of the control.
        /// </summary>
        /// <remarks>
        /// The original WebForms spelling of <see cref="ClientApiControlBase.Id"/>, kept so that the
        /// many existing call sites across the solution keep compiling.
        /// </remarks>
        [CLSCompliant(false)]
        public string ID
        {
            get { return this.Id; }
            set { this.Id = value; }
        }

        /// <summary>Gets the rendered client side identifier of the control.</summary>
        /// <remarks>The original WebForms spelling of <see cref="ClientApiControlBase.ClientId"/>.</remarks>
        [CLSCompliant(false)]
        public string ClientID
        {
            get { return this.ClientId; }
        }

        /// <summary>Gets the hierarchically qualified identifier of the control.</summary>
        /// <remarks>The original WebForms spelling of <see cref="ClientApiControlBase.UniqueId"/>.</remarks>
        [CLSCompliant(false)]
        public string UniqueID
        {
            get { return this.UniqueId; }
        }

        /// <summary>Gets or sets the CSS class rendered onto the control.</summary>
        public virtual string CssClass { get; set; } = string.Empty;

        /// <summary>Gets or sets a value indicating whether the control is enabled.</summary>
        public virtual bool Enabled { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the control renders.</summary>
        public virtual bool Visible { get; set; } = true;

        /// <summary>Gets or sets the tooltip rendered as the <c>title</c> attribute.</summary>
        public virtual string ToolTip { get; set; }

        /// <summary>Gets the inline style declarations rendered onto the control.</summary>
        /// <remarks>Replaces <c>WebControl.Style</c> (a <c>CssStyleCollection</c>).</remarks>
        public IDictionary<string, string> Style => this.style;

        /// <summary>
        /// Gets or sets the design-time site of the control.
        /// </summary>
        /// <remarks>
        /// Replaces <c>Control.Site</c>; the design-time notification code uses it to reach the
        /// <see cref="System.ComponentModel.Design.IComponentChangeService"/>.
        /// </remarks>
        [Browsable(false)]
        public ISite Site { get; set; }

        /// <summary>Gets the name of the tag this control renders as.</summary>
        protected virtual string TagName
        {
            get { return this.TagNameOverride; }
        }

        /// <summary>Gets or sets the tag name supplied through the constructor.</summary>
        protected string TagNameOverride { get; set; }

        /// <summary>
        /// Registers the client side initialization of a control.
        /// </summary>
        /// <param name="ctl">The control being initialized.</param>
        /// <param name="func">The name of the <c>dnn.controls</c> initialization function.</param>
        /// <param name="propJson">The marshalled client properties of the control.</param>
        /// <remarks>
        /// The original injected a start-up script of the shape
        /// <c>dnn.setVar('&lt;clientid&gt;_p', '&lt;json&gt;'); dnn.controls.&lt;func&gt;($get('&lt;clientid&gt;'));</c>
        /// through the Client API start-up script registration. Script blocks cannot be injected over a
        /// Blazor circuit, so the request is queued instead and the very same pair of calls is
        /// issued through <c>IJSRuntime</c> when a component drains
        /// <see cref="ClientInitializationQueue"/>. The escaping the original needed is gone with
        /// the script source: interop arguments are serialized.
        /// </remarks>
        public static void RegisterInitialize(IClientApiControl ctl, string func, string propJson)
        {
            ArgumentNullException.ThrowIfNull(ctl);

            ClientInitializationQueue.Enqueue(new ClientInitialization(ctl.ClientId, func, propJson));
        }

        /// <summary>Resolves an application relative URL for client side use.</summary>
        /// <param name="url">The URL to resolve.</param>
        /// <returns>The resolved URL.</returns>
        /// <remarks>
        /// Replaces <c>Control.ResolveUrl</c>. The tilde of an application relative URL is expanded
        /// with the request's path base, which is the ASP.NET Core equivalent of the application path.
        /// </remarks>
        public virtual string ResolveUrl(string url)
        {
            if (string.IsNullOrEmpty(url) || !url.StartsWith("~/", StringComparison.Ordinal))
            {
                return url;
            }

            var pathBase = DnnWebUtilityContext.Current?.Request.PathBase.Value ?? string.Empty;
            return string.Concat(pathBase.TrimEnd('/'), url.AsSpan(1));
        }

        /// <summary>Renders the control, honouring <see cref="Visible"/>.</summary>
        /// <param name="writer">The writer to render to.</param>
        public virtual void RenderControl(TextWriter writer)
        {
            if (!this.Visible)
            {
                return;
            }

            this.Render(writer);
        }

        /// <summary>
        /// Signals that the control takes part in the post back data phase.
        /// </summary>
        /// <remarks>
        /// Replaces <c>Page.RegisterRequiresPostBack(Me)</c>. That registration only made ASP.NET
        /// call <c>IPostBackDataHandler.LoadPostData</c>; every implementation in this library
        /// returned <see langword="false"/> without reading the post collection, so there is nothing
        /// left to register on ASP.NET Core and this is intentionally a no-op.
        /// </remarks>
        protected static void RegisterRequiresPostBack()
        {
        }

        /// <summary>Renders the opening tag, the contents and the closing tag of the control.</summary>
        /// <param name="writer">The writer to render to.</param>
        protected virtual void Render(TextWriter writer)
        {
            var html = HtmlMarkupWriter.For(writer);
            this.AddAttributesToRender(html);
            html.RenderBeginTag(this.TagName);
            this.RenderContents(html);
            html.RenderEndTag();
        }

        /// <summary>Buffers the attributes of the control onto the writer.</summary>
        /// <param name="writer">The writer to add the attributes to.</param>
        protected virtual void AddAttributesToRender(TextWriter writer)
        {
            var html = HtmlMarkupWriter.For(writer);
            if (!string.IsNullOrEmpty(this.Id))
            {
                html.AddAttribute("id", this.ClientId);
            }

            if (!string.IsNullOrEmpty(this.CssClass))
            {
                html.AddAttribute("class", this.CssClass);
            }

            if (!string.IsNullOrEmpty(this.ToolTip))
            {
                html.AddAttribute("title", this.ToolTip);
            }

            foreach (var attribute in this.Attributes)
            {
                html.AddAttribute(attribute.Key, attribute.Value);
            }

            foreach (var declaration in this.Style)
            {
                html.AddStyleAttribute(declaration.Key, declaration.Value);
            }
        }

        /// <summary>Renders the content of the control, which is its children by default.</summary>
        /// <param name="writer">The writer to render to.</param>
        protected virtual void RenderContents(TextWriter writer)
        {
            this.RenderChildren(writer);
        }

        /// <summary>Renders the child controls of this control.</summary>
        /// <param name="writer">The writer to render to.</param>
        protected virtual void RenderChildren(TextWriter writer)
        {
            if (!this.HasChildren)
            {
                return;
            }

            foreach (var child in this.Children)
            {
                if (child is BaseWebControl control)
                {
                    control.RenderControl(writer);
                }
            }
        }
    }
}
