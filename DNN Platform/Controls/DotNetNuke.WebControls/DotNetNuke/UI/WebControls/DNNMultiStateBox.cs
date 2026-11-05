// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.ComponentModel.Design;
    using System.IO;

    using DotNetNuke.UI.Utilities;
    using DotNetNuke.Web.Client;

    /// <summary>
    /// A check box that cycles through the states of its <see cref="States"/> collection instead of
    /// just being checked or unchecked.
    /// </summary>
    /// <remarks>
    /// Converted from <c>System.Web.UI.WebControls.CheckBox</c> to <see cref="BaseWebControl"/>: the
    /// control renders an <c>input</c> element itself, since the WebForms check box it used to
    /// inherit the rendering from has no .NET 10 equivalent. The state-bag backed properties
    /// became ordinary backing fields keeping the very same defaults the view state getters
    /// returned, and the posted value is read through
    /// <see cref="IClientApiPage.GetRequestValue(string)"/> rather than <c>Page.Request.Form</c>.
    /// </remarks>
    public class DNNMultiStateBox : BaseWebControl
    {
        private DNNMultiStateCollection states;
        private bool enabled = true;
        private bool valueSet;
        private string multiStateBoxScriptPath;
        private string imagePath = "images/";

        /// <summary>Initializes a new instance of the <see cref="DNNMultiStateBox"/> class.</summary>
        public DNNMultiStateBox()
            : base("input")
        {
            this.PreRender += this.DNNMultiStateBox_PreRender;
        }

        /// <summary>Gets or sets the text rendered next to the box.</summary>
        /// <remarks>Replaces <c>CheckBox.Text</c>.</remarks>
        [DefaultValue("")]
        public string Text { get; set; } = string.Empty;

        /// <summary>Gets or sets the key of the state the box currently shows.</summary>
        [DefaultValue("")]
        [ClientProperty]
        [ClientPropertyName("state")]
        public string SelectedStateKey
        {
            get
            {
                if (!this.valueSet)
                {
                    var posted = this.Page?.GetRequestValue(this.UniqueID);
                    if (!string.IsNullOrEmpty(posted))
                    {
                        this.SelectedStateKey = posted;
                    }
                }

                this.Attributes.TryGetValue("value", out var value);
                if (string.IsNullOrEmpty(value) || value == "on")
                {
                    return this.States.Count <= 0 ? string.Empty : this.States[0].Key;
                }

                return value;
            }

            set
            {
                this.valueSet = true;
                this.Attributes["value"] = value;
            }
        }

        /// <summary>Gets the state the box currently shows.</summary>
        public DNNMultiState SelectedState
        {
            get
            {
                var selectedStateKey = this.SelectedStateKey;
                if (string.IsNullOrEmpty(selectedStateKey))
                {
                    return null;
                }

                var enumerator = this.States.GetEnumerator();
                try
                {
                    while (enumerator.MoveNext())
                    {
                        var current = (DNNMultiState)enumerator.Current;
                        if (current.Key == selectedStateKey)
                        {
                            return current;
                        }
                    }
                }
                finally
                {
                    (enumerator as IDisposable)?.Dispose();
                }

                return null;
            }
        }

        /// <summary>Gets or sets the client side behavior identifier of the box.</summary>
        [DefaultValue("")]
        [ClientProperty]
        [ClientPropertyName("bid")]
        public string BehaviorID { get; set; } = string.Empty;

        /// <summary>Gets or sets the directory the Client API scripts are served from.</summary>
        [Description("Location of ClientAPI js files")]
        [Category("Paths")]
        [DefaultValue("")]
        public string ClientAPIScriptPath
        {
            get { return ClientAPI.ScriptPath; }
            set { ClientAPI.ScriptPath = value; }
        }

        /// <summary>Gets or sets the directory the box's script is served from.</summary>
        [Description("Location of dnn.controls.DNNMultiStateBox.js file")]
        [Category("Paths")]
        [DefaultValue("")]
        public string MultiStateBoxScriptPath
        {
            get
            {
                return !string.IsNullOrEmpty(this.multiStateBoxScriptPath)
                    ? this.multiStateBoxScriptPath
                    : this.ClientAPIScriptPath;
            }

            set
            {
                this.multiStateBoxScriptPath = value;
            }
        }

        /// <summary>Gets or sets the directory holding the images of the states.</summary>
        [ClientPropertyName("imgpath")]
        [UrlProperty]
        [ClientProperty]
        [DefaultValue("images/")]
        [Description("Directory to find the images for the MultiStateBox.")]
        [Category("Paths")]
        public string ImagePath
        {
            get { return !string.IsNullOrEmpty(this.imagePath) ? this.imagePath : "images/"; }
            set { this.imagePath = value; }
        }

        /// <summary>Gets the states the box cycles through.</summary>
        public virtual DNNMultiStateCollection States
        {
            get
            {
                if (this.states is null)
                {
                    this.states = new DNNMultiStateCollection(this);
                }

                return this.states;
            }
        }

        /// <summary>Gets or sets a value indicating whether the box can be cycled.</summary>
        [DefaultValue(true)]
        [ClientPropertyName("enabled")]
        [ClientProperty]
        public override bool Enabled
        {
            get { return this.enabled; }
            set { this.enabled = value; }
        }

        /// <summary>
        /// Loads the posted state key of the box.
        /// </summary>
        /// <param name="postDataKey">The key the value was posted under.</param>
        /// <param name="postCollection">The posted values.</param>
        /// <returns><see langword="true"/> when the state changed.</returns>
        /// <remarks>
        /// Replaces <c>IPostBackDataHandler.LoadPostData</c>, which the original inherited from
        /// <c>CheckBox</c>. The <see cref="SelectedStateKey"/> getter already reads the posted value
        /// on its own; this method keeps the entry point available to callers.
        /// </remarks>
        public bool LoadPostData(string postDataKey, NameValueCollection postCollection)
        {
            var posted = postCollection?[postDataKey];
            if (string.IsNullOrEmpty(posted) || posted == this.SelectedStateKey)
            {
                return false;
            }

            this.SelectedStateKey = posted;
            return true;
        }

        /// <summary>Signals that the posted state key of the box changed.</summary>
        /// <remarks>Replaces <c>IPostBackDataHandler.RaisePostDataChangedEvent</c>.</remarks>
        public void RaisePostDataChangedEvent()
        {
        }

        /// <summary>Registers the client side scripts the box needs.</summary>
        public void RegisterClientScript()
        {
            ClientAPI.RegisterClientReference(this.Page, ClientAPI.ClientNamespaceReferences.dnn_dom);
#pragma warning disable CS0618 // the obsolete overloads are kept until the host injects IClientResourceController
            WebControls.RegisterClientScriptBlock(this.Page, "dnn.controls.js", DotNetNuke.Web.Client.FileOrder.Js.DnnControlsLabelEdit);
            WebControls.RegisterClientScriptBlock(this.Page, "dnn.controls.dnnmultistatebox.js");
#pragma warning restore CS0618
        }

        /// <summary>
        /// Tells the design surface that the states of the box changed.
        /// </summary>
        /// <remarks>
        /// The original reached the designer through <c>System.Web.UI.Design.ControlDesigner</c>,
        /// which has no .NET 10 equivalent. The notification now goes through
        /// <see cref="IComponentChangeService"/>, the framework's own design-time change channel.
        /// The original also swallowed any failure to look the property up, which is preserved.
        /// </remarks>
        internal void NotifyDesigner()
        {
            if (!WebControls.IsDesignMode() || this.states is null || this.Site is null)
            {
                return;
            }

            var changeService = (IComponentChangeService)this.Site.GetService(typeof(IComponentChangeService));
            if (changeService is null)
            {
                return;
            }

            PropertyDescriptor member;
            try
            {
                member = TypeDescriptor.GetProperties(this)["States"];
            }
            catch (Exception)
            {
                return;
            }

            changeService.OnComponentChanged(this, member, null, this.States);
        }

        /// <summary>Collects the properties the client script needs to drive the box.</summary>
        /// <returns>The marshalled properties.</returns>
        protected Hashtable MarshalledProperties()
        {
            return new Hashtable { { "states", MSAJAX.Serialize(this.States) } };
        }

        /// <inheritdoc/>
        protected override void Render(TextWriter writer)
        {
            var html = HtmlMarkupWriter.For(writer);
            html.AddAttribute("value", this.SelectedStateKey);

            // Replaces CheckBox.Render: the check box input and its label are emitted directly.
            html.AddAttribute("type", "checkbox");
            html.AddAttribute("id", this.ClientID);
            html.AddAttribute("name", this.UniqueID);
            if (!this.Enabled)
            {
                html.AddAttribute("disabled", "disabled");
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
                if (!string.Equals(attribute.Key, "value", StringComparison.OrdinalIgnoreCase))
                {
                    html.AddAttribute(attribute.Key, attribute.Value);
                }
            }

            foreach (var declaration in this.Style)
            {
                html.AddStyleAttribute(declaration.Key, declaration.Value);
            }

            html.RenderSelfClosingTag("input");

            if (!string.IsNullOrEmpty(this.Text))
            {
                html.AddAttribute("for", this.ClientID);
                html.RenderBeginTag("label");
                html.Write(this.Text);
                html.RenderEndTag();
            }

            var properties = this.MarshalledProperties();
            var json = ScriptGenerator.GetMarshalledPropertyJSON(this, properties);
            RegisterInitialize(this, "initMultiStateBox", json);
        }

        /// <summary>Prepares the box for rendering.</summary>
        /// <param name="sender">The box raising the event.</param>
        /// <param name="e">The arguments of the event.</param>
        private void DNNMultiStateBox_PreRender(object sender, EventArgs e)
        {
            this.RegisterClientScript();
            if (this.SelectedState is not null)
            {
                this.Text = this.SelectedState.Text;
            }
        }
    }
}
