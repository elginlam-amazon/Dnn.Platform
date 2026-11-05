// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections;
    using System.ComponentModel;
    using System.ComponentModel.Design;
    using System.Globalization;
    using System.IO;
    using System.Text;

    using DotNetNuke.UI.Utilities;
    using DotNetNuke.Web.Client;

    /// <summary>
    /// A floating tool bar of <see cref="DNNToolBarButton"/> children, attached to another control.
    /// </summary>
    /// <remarks>
    /// Converted from <c>Control</c> to <see cref="BaseWebControl"/>; the state-bag backed
    /// properties became ordinary auto-properties keeping the very same defaults the view state
    /// getters returned. The tool bar itself renders nothing server side — the client script builds
    /// it from the JSON produced by <see cref="ToJSON"/>. <see cref="IExtenderProvider"/> and
    /// <see cref="ProvidePropertyAttribute"/> live in <c>System.ComponentModel</c> and are kept.
    /// </remarks>
    [ProvideProperty("Toolbar", typeof(BaseWebControl))]
    [DefaultProperty("Buttons")]
    public class DNNToolBar : BaseWebControl, IExtenderProvider
    {
        private DNNToolBarButtonCollection buttons;
        private string defaultButtonCssClass = string.Empty;
        private string defaultButtonHoverCssClass = string.Empty;
        private int mouseOutDelay = 250;
        private bool reuseToolBar;

        /// <summary>Initializes a new instance of the <see cref="DNNToolBar"/> class.</summary>
        public DNNToolBar()
        {
            this.PreRender += this.DNNToolBar_PreRender;
        }

        /// <summary>Gets or sets the client side behavior identifier of the tool bar.</summary>
        [Bindable(true)]
        [ClientPropertyName("bid")]
        [ClientProperty]
        [DefaultValue("")]
        public string BehaviorID { get; set; } = string.Empty;

        /// <summary>Gets or sets the CSS class of the tool bar.</summary>
        [Category("Appearance")]
        public override string CssClass
        {
            get
            {
                return base.CssClass;
            }

            set
            {
                base.CssClass = value;
                if (WebControls.IsDesignMode())
                {
                    this.NotifyDesigner();
                }
            }
        }

        /// <summary>Gets or sets the CSS class the buttons fall back to.</summary>
        [Category("Appearance")]
        public string DefaultButtonCssClass
        {
            get
            {
                return this.defaultButtonCssClass;
            }

            set
            {
                this.defaultButtonCssClass = value;
                if (WebControls.IsDesignMode())
                {
                    this.NotifyDesigner();
                }
            }
        }

        /// <summary>Gets or sets the hover CSS class the buttons fall back to.</summary>
        [Category("Appearance")]
        public string DefaultButtonHoverCssClass
        {
            get
            {
                return this.defaultButtonHoverCssClass;
            }

            set
            {
                this.defaultButtonHoverCssClass = value;
                if (WebControls.IsDesignMode())
                {
                    this.NotifyDesigner();
                }
            }
        }

        /// <summary>Gets or sets the delay, in milliseconds, before the tool bar hides.</summary>
        [Category("Behavior")]
        [Description("Number of milliseconds to wait befor hiding sub-menu on mouse out")]
        [DefaultValue(250)]
        public int MouseOutDelay
        {
            get
            {
                return this.mouseOutDelay;
            }

            set
            {
                this.mouseOutDelay = value;
                if (WebControls.IsDesignMode())
                {
                    this.NotifyDesigner();
                }
            }
        }

        /// <summary>Gets the buttons of the tool bar.</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public virtual DNNToolBarButtonCollection Buttons
        {
            get
            {
                if (this.buttons is null)
                {
                    this.buttons = new DNNToolBarButtonCollection(this);
                }

                return this.buttons;
            }
        }

        /// <summary>Gets or sets a value indicating whether other controls may share this tool bar.</summary>
        [DefaultValue(false)]
        [Description("Allows toolbar structure to be reused by others sharing the same id")]
        [Category("Behavior")]
        public bool ReuseToolBar
        {
            get
            {
                return this.reuseToolBar;
            }

            set
            {
                this.reuseToolBar = value;
                if (WebControls.IsDesignMode())
                {
                    this.NotifyDesigner();
                }
            }
        }

        /// <summary>Gets or sets a value indicating whether the tool bar is shown.</summary>
        [Browsable(false)]
        [Bindable(false)]
        public override bool Visible
        {
            get { return base.Visible; }
            set { base.Visible = value; }
        }

        /// <summary>Gets the control this tool bar is attached to.</summary>
        [Browsable(false)]
        internal IClientApiControl AttachedControl
        {
            get { return this.FindAttachedControl(this.Page); }
        }

        /// <inheritdoc/>
        public bool CanExtend(object extendee)
        {
            return extendee is IDNNToolBar toolBar && toolBar.ToolBarId == this.ID;
        }

        /// <summary>Finds the tool bar a control is attached to.</summary>
        /// <param name="objControl">The control whose tool bar is wanted.</param>
        /// <returns>The tool bar, or <see langword="null"/> when it was not found.</returns>
        public DNNToolBar GetToolbar(IClientApiControl objControl)
        {
            ArgumentNullException.ThrowIfNull(objControl);

            var toolBar = (IDNNToolBar)objControl;
            IClientApiControl control = null;
            if (!string.IsNullOrEmpty(toolBar.ToolBarId))
            {
                control = objControl.Page?.FindControl(toolBar.ToolBarId);
            }

            return control as DNNToolBar;
        }

        /// <summary>Hooks the tool bar up to the client side events of a control.</summary>
        /// <param name="objAssociatedControl">The control showing the tool bar.</param>
        /// <param name="strShowEventName">The client side event that shows the tool bar.</param>
        /// <param name="strHideEventName">The client side event that hides the tool bar.</param>
        /// <param name="strToolBarActionHandler">The client side handler of the tool bar's actions.</param>
        /// <returns><see langword="true"/> when the browser can show the tool bar.</returns>
        public bool RegisterToolBar(IClientApiControl objAssociatedControl, string strShowEventName, string strHideEventName, string strToolBarActionHandler)
        {
            return this.RegisterToolBar(
                objAssociatedControl, strShowEventName, strHideEventName, strToolBarActionHandler, ClientAPI.ScriptPath);
        }

        /// <summary>Hooks the tool bar up to the client side events of a control.</summary>
        /// <param name="objAssociatedControl">The control showing the tool bar.</param>
        /// <param name="strShowEventName">The client side event that shows the tool bar.</param>
        /// <param name="strHideEventName">The client side event that hides the tool bar.</param>
        /// <param name="strToolBarActionHandler">The client side handler of the tool bar's actions.</param>
        /// <param name="toolBarScriptPath">The directory the tool bar's script is served from.</param>
        /// <returns><see langword="true"/> when the browser can show the tool bar.</returns>
        public bool RegisterToolBar(IClientApiControl objAssociatedControl, string strShowEventName, string strHideEventName, string strToolBarActionHandler, string toolBarScriptPath)
        {
            ArgumentNullException.ThrowIfNull(objAssociatedControl);

            if (!ClientAPI.BrowserSupportsFunctionality(ClientAPI.ClientFunctionality.DHTML))
            {
                return false;
            }

            ClientAPI.RegisterClientReference(this.Page, ClientAPI.ClientNamespaceReferences.dnn_dom);
#pragma warning disable CS0618 // the obsolete overloads are kept until the host injects IClientResourceController
            WebControls.RegisterClientScriptBlock(this.Page, "dnn.controls.js", DotNetNuke.Web.Client.FileOrder.Js.DnnControlsLabelEdit);
            WebControls.RegisterClientScriptBlock(this.Page, "dnn.controls.dnntoolbarstub.js");
#pragma warning restore CS0618

            var script =
                $"__dnn_toolbarHandler('{this.UniqueID}','{objAssociatedControl.ClientId}','{this.BehaviorID}'," +
                $"'{this.NamingContainer?.UniqueId}',{strToolBarActionHandler},'{strShowEventName}','{strHideEventName}')";

            // Both WebControl.Attributes and HtmlControl.Attributes are represented by the single
            // attribute bag of IClientApiControl, so the original type test is no longer needed.
            objAssociatedControl.Attributes[strShowEventName] = script;
            return true;
        }

        /// <summary>Applies the tool bar to its associated control.</summary>
        /// <remarks>Preserved from the original, where it was already empty.</remarks>
        public void SetToolbar()
        {
        }

        /// <summary>Serializes the tool bar and its buttons for the client script.</summary>
        /// <returns>The JSON object literal of the tool bar.</returns>
        internal string ToJSON()
        {
            var values = new Hashtable();
            if (!string.IsNullOrEmpty(this.CssClass))
            {
                values.Add("css", "'" + this.CssClass + "'");
            }

            if (!string.IsNullOrEmpty(this.DefaultButtonCssClass))
            {
                values.Add("cssb", "'" + this.DefaultButtonCssClass + "'");
            }

            if (!string.IsNullOrEmpty(this.DefaultButtonHoverCssClass))
            {
                values.Add("cssbh", "'" + this.DefaultButtonHoverCssClass + "'");
            }

            if (this.MouseOutDelay != 250)
            {
                values.Add("mod", this.MouseOutDelay);
            }

            // The original tested Strings.Len(Me.Visible) > 0, which VB evaluates by converting the
            // boolean to its string form ("True"/"False"); that is never empty, so the entry was
            // always written.
            values.Add("vis", this.Visible ? 1 : 0);

            var builder = new StringBuilder();
            builder.Append('[');
            var enumerator = this.Buttons.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    var current = (DNNToolBarButton)enumerator.Current;
                    if (builder.Length > 1)
                    {
                        builder.Append(',');
                    }

                    builder.Append(current.ToJSON());
                }
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }

            builder.Append(']');
            values.Add("btns", builder.ToString());

            builder = new StringBuilder();
            builder.Append('{');
            foreach (var key in values.Keys)
            {
                var name = Convert.ToString(key, CultureInfo.InvariantCulture);
                if (builder.Length > 1)
                {
                    builder.Append(',');
                }

                builder.Append(name).Append(':').Append(Convert.ToString(values[key], CultureInfo.InvariantCulture));
            }

            builder.Append('}');
            return builder.ToString();
        }

        /// <summary>
        /// Tells the design surface that the tool bar changed.
        /// </summary>
        /// <remarks>
        /// The original reached the designer through <c>System.Web.UI.Design.ControlDesigner</c>,
        /// which has no .NET 10 equivalent. The notification now goes through
        /// <see cref="IComponentChangeService"/>, the framework's own design-time change channel.
        /// </remarks>
        internal void NotifyDesigner()
        {
            if (!WebControls.IsDesignMode() || this.Site is null)
            {
                return;
            }

            var changeService = (IComponentChangeService)this.Site.GetService(typeof(IComponentChangeService));
            if (changeService is null)
            {
                return;
            }

            var member = TypeDescriptor.GetProperties(this)["Toolbar"];
            changeService.OnComponentChanged(this, member, null, this);
        }

        /// <summary>
        /// Renders the tool bar.
        /// </summary>
        /// <param name="writer">The writer to render to.</param>
        /// <remarks>
        /// Preserved from the original, where it was deliberately empty: the client script builds
        /// the tool bar from the registered JSON.
        /// </remarks>
        protected override void Render(TextWriter writer)
        {
        }

        /// <summary>Registers the tool bar's structure and scripts with the page.</summary>
        /// <param name="sender">The tool bar raising the event.</param>
        /// <param name="e">The arguments of the event.</param>
        private void DNNToolBar_PreRender(object sender, EventArgs e)
        {
            if (this.Visible)
            {
                this.RegisterToolbarScript();
            }
        }

        /// <summary>Finds the control that is attached to this tool bar.</summary>
        /// <param name="objControl">The control to start the search at.</param>
        /// <returns>The attached control, or <see langword="null"/>.</returns>
        private IClientApiControl FindAttachedControl(IClientApiControl objControl)
        {
            if (objControl is null)
            {
                return null;
            }

            if (objControl is IDNNToolBar toolBar && toolBar.ToolBarId == this.ID)
            {
                return objControl;
            }

            foreach (var child in objControl.Children)
            {
                var found = this.FindAttachedControl(child);
                if (found is not null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>Registers the tool bar's structure and scripts with the page.</summary>
        /// <returns>Always <see langword="null"/>, as in the original.</returns>
        private string RegisterToolbarScript()
        {
            var id = this.UniqueID;
            if (this.ReuseToolBar)
            {
                id = !string.IsNullOrEmpty(this.BehaviorID) ? this.BehaviorID : this.ID;
            }

            if (!ClientAPI.IsClientScriptBlockRegistered(this.Page, id + "_toolbar")
                && ClientAPI.BrowserSupportsFunctionality(ClientAPI.ClientFunctionality.DHTML))
            {
                ClientAPI.RegisterClientReference(this.Page, ClientAPI.ClientNamespaceReferences.dnn);

                // The original injected <script>dnn.controls.toolbars['id']={json};</script> as a
                // start-up script. Script blocks cannot be injected over a Blazor circuit, so the
                // statement is queued and run through IJSRuntime when a component drains the queue.
                ClientInitializationQueue.Enqueue(
                    new ClientInitialization(id, $"dnn.controls.toolbars['{id}']={this.ToJSON()};"));
            }

            // The two scripts used to be embedded assembly resources; they are now static web
            // assets of this Razor Class Library, referenced by URL.
            this.Page?.RegisterClientScriptInclude(
                "dnn.controls.dnntoolbar.js", WebControls.GetScriptUrl("dnn.controls.dnntoolbar.js"));
            this.Page?.RegisterClientScriptInclude(
                "dnn.controls.dnntoolbarstub.js", WebControls.GetScriptUrl("dnn.controls.dnntoolbarstub.js"));

            return null;
        }
    }
}
