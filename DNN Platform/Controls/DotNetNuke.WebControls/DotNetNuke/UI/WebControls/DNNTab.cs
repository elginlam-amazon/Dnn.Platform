// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections;
    using System.ComponentModel;
    using System.Globalization;
    using System.IO;

    using DotNetNuke.UI.Utilities;

    /// <summary>
    /// One tab of a <see cref="DNNTabStrip"/>, holding its label and its content.
    /// </summary>
    /// <remarks>
    /// Converted from <c>WebControl</c> to <see cref="BaseWebControl"/>; the state-bag backed
    /// properties became ordinary backing fields keeping the very same defaults the view state
    /// getters returned. <c>INamingContainer</c> is no longer needed: identifier composition is
    /// handled by <see cref="ClientApiControlBase"/>, which treats every parent as a naming
    /// container. The <c>ParseChildren</c> / <c>PersistChildren</c> annotations were ASPX-parser
    /// directives with no .NET 10 equivalent and are dropped.
    /// </remarks>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class DNNTab : BaseWebControl
    {
        private readonly DNNTabLabel label;
        private DNNTabStrip parentTabStrip;
        private bool isPostBack;
        private bool isCallBack;
        private DNNTabStrip.eTabRenderMode? tabRenderMode;
        private eTabCallbackPostMode? tabCallbackPostMode;
        private ClientAPICallBackResponse.CallBackTypeCode? callBackType;

        /// <summary>Initializes a new instance of the <see cref="DNNTab"/> class.</summary>
        public DNNTab()
            : base("div")
        {
            this.label = new DNNTabLabel();
        }

        /// <summary>Initializes a new instance of the <see cref="DNNTab"/> class.</summary>
        /// <param name="labelText">The text of the tab's label.</param>
        public DNNTab(string labelText)
            : this()
        {
            this.Label.Text = labelText;
        }

        /// <summary>Handles the tab being about to read its posted data.</summary>
        public delegate void PreLoadPostDataEventHandler();

        /// <summary>Handles the tab being about to render and needing its defaults.</summary>
        public delegate void SetupDefaultsEventHandler();

        /// <summary>Occurs right before the tab reads its posted data.</summary>
        public event PreLoadPostDataEventHandler PreLoadPostData;

        /// <summary>Occurs right before the tab renders.</summary>
        public event SetupDefaultsEventHandler SetupDefaults;

        /// <summary>Where the client script posts the data of a callback to.</summary>
        public enum eTabCallbackPostMode
        {
            /// <summary>Nothing is posted.</summary>
            None = 0,

            /// <summary>The Client API state field is posted.</summary>
            DNNVariable = 1,

            /// <summary>The children of the tab strip are posted.</summary>
            TabStrip = 2,

            /// <summary>The whole form is posted.</summary>
            Form = 3,
        }

        /// <summary>Gets or sets a value indicating whether the tab can be selected.</summary>
        [ClientPropertyName("enabled")]
        [ClientProperty]
        [DefaultValue(true)]
        public override bool Enabled
        {
            get { return base.Enabled; }
            set { base.Enabled = value; }
        }

        /// <summary>Gets a value indicating whether the tab was already rendered once.</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsPostBack
        {
            get { return this.isPostBack; }
        }

        /// <summary>Gets a value indicating whether the tab is rendering for a callback.</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsCallBack
        {
            get { return this.isCallBack; }
        }

        /// <summary>Gets the appearance of the tab's label.</summary>
        [NotifyParentProperty(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [Description("The style to be applied to Label")]
        [Category("Style")]
        public DNNTabLabel Label
        {
            get { return this.label; }
        }

        /// <summary>Gets or sets how the content of the tab reaches the browser.</summary>
        [Category("Behavior")]
        [DefaultValue(2)]
        public DNNTabStrip.eTabRenderMode TabRenderMode
        {
            get
            {
                if (this.tabRenderMode.HasValue)
                {
                    return this.tabRenderMode.Value;
                }

                return this.ParentControl is not null
                    ? this.ParentControl.TabRenderMode
                    : DNNTabStrip.eTabRenderMode.CallBack;
            }

            set
            {
                this.tabRenderMode = value;
            }
        }

        /// <summary>Gets or sets where the client script posts the data of a callback to.</summary>
        [DefaultValue(0)]
        [Category("Behavior")]
        public eTabCallbackPostMode TabCallbackPostMode
        {
            get { return this.tabCallbackPostMode ?? eTabCallbackPostMode.None; }
            set { this.tabCallbackPostMode = value; }
        }

        /// <summary>Gets or sets the kind of callback the tab uses.</summary>
        [DefaultValue(0)]
        [Description("Image to display during a callback")]
        [Bindable(true)]
        public ClientAPICallBackResponse.CallBackTypeCode CallBackType
        {
            get
            {
                if (this.callBackType.HasValue)
                {
                    return this.callBackType.Value;
                }

                return this.ParentControl is null
                    ? ClientAPICallBackResponse.CallBackTypeCode.Simple
                    : this.ParentControl.CallBackType;
            }

            set
            {
                this.callBackType = value;
            }
        }

        /// <summary>Gets a value indicating whether this tab is the selected one.</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal bool IsSelected
        {
            get
            {
                if (this.ParentControl is null)
                {
                    return true;
                }

                var selected = this.ParentControl.SelectedTab;
                return selected is not null && this.ID is not null && this.ID == selected.ID;
            }
        }

        /// <summary>Gets the strip this tab belongs to.</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        protected DNNTabStrip ParentControl
        {
            get
            {
                if (this.parentTabStrip is null && this.Parent is DNNTabStrip strip)
                {
                    this.parentTabStrip = strip;
                }

                return this.parentTabStrip;
            }
        }

        /// <summary>Collects the properties the client script needs to drive this tab.</summary>
        /// <returns>The marshalled properties.</returns>
        internal Hashtable MarshalledProperties()
        {
            var properties = new Hashtable { { "tid", this.ID }, { "tcid", this.ClientID } };
            if (WebControls.IsDesignMode() || this.ParentControl is null || this.ParentControl.IsDownLevel)
            {
                return properties;
            }

            if (this.TabRenderMode == DNNTabStrip.eTabRenderMode.CallBack)
            {
                switch (this.TabCallbackPostMode)
                {
                    case eTabCallbackPostMode.DNNVariable:
                        properties.Add("postmode", "__dnnVariable");
                        break;

                    case eTabCallbackPostMode.TabStrip:
                        properties.Add("postmode", this.ParentControl.ClientID);
                        break;

                    case eTabCallbackPostMode.Form:
                        // Replaces the walk up the control tree looking for an HtmlForm: the page
                        // abstraction exposes its form directly.
                        var form = this.Page?.Form
                            ?? throw new InvalidOperationException("Could not find form control");
                        properties.Add("postmode", form.ClientId);
                        break;

                    default:
                        break;
                }
            }

            if (this.CallBackType != this.ParentControl.CallBackType)
            {
                properties.Add("cbtype", ((int)this.CallBackType).ToString(CultureInfo.InvariantCulture));
            }

            return properties;
        }

        /// <summary>Raises the <see cref="PreLoadPostData"/> event.</summary>
        internal void RaisePreLoadPostData()
        {
            this.PreLoadPostData?.Invoke();
        }

        /// <summary>Raises the <see cref="SetupDefaults"/> event.</summary>
        /// <param name="isCallBack">Whether the tab is rendering for a callback.</param>
        internal void RaiseSetupDefaultsEvent(bool isCallBack)
        {
            this.isCallBack = isCallBack;
            this.SetupDefaults?.Invoke();
        }

        /// <summary>Renders the label of the tab.</summary>
        /// <param name="writer">The writer to render to.</param>
        internal void RenderLabel(TextWriter writer)
        {
            var hasParent = this.ParentControl is not null;
            var markup = new MarkupLabel { Id = this.ClientID + "_l" };
            string cssClass = null;

            if (!this.IsSelected)
            {
                cssClass = this.Enabled ? this.Label.CssClass : this.Label.CssClassDisabled;
            }
            else if (string.IsNullOrEmpty(this.Label.CssClassSelected) && hasParent)
            {
                cssClass = this.ParentControl.DefaultLabel.CssClassSelected;
            }

            if (hasParent)
            {
                if (!this.Enabled)
                {
                    if (string.IsNullOrEmpty(cssClass))
                    {
                        cssClass = this.ParentControl.DefaultLabel.CssClassDisabled;
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(cssClass))
                    {
                        cssClass = this.ParentControl.CssClass;
                    }

                    if (string.IsNullOrEmpty(cssClass))
                    {
                        cssClass = this.ParentControl.DefaultLabel.CssClass;
                    }
                }
            }

            if (!string.IsNullOrEmpty(cssClass))
            {
                markup.CssClass = cssClass;
            }

            if (!this.Visible)
            {
                markup.Style["display"] = "none";
            }

            if (hasParent && this.ParentControl.IsDownLevel)
            {
                markup.Attributes.Add(
                    "onclick",
                    ClientAPI.GetPostBackClientHyperlink(
                        this.ParentControl, this.ID + ClientAPI.COLUMN_DELIMITER + "OnDemand"));
            }

            if (!string.IsNullOrEmpty(this.Label.ImageUrl)
                || (hasParent && !string.IsNullOrEmpty(this.ParentControl.DefaultLabel.ImageUrl)))
            {
                var image = new MarkupImage
                {
                    Id = this.ClientID + "_i",
                    ImageUrl = this.Label.ImageUrl,
                };
                if (string.IsNullOrEmpty(image.ImageUrl))
                {
                    image.ImageUrl = this.ParentControl.DefaultLabel.ImageUrl;
                }

                markup.Controls.Add(image);
            }

            if (hasParent && !string.IsNullOrEmpty(this.ParentControl.WorkImage))
            {
                var workImage = new MarkupImage
                {
                    Id = this.ClientID + "_w",
                    ImageUrl = this.ParentControl.WorkImage,
                };
                workImage.Style["display"] = "none";
                markup.Controls.Add(workImage);
            }

            markup.Controls.Add(new MarkupLiteral(this.Label.Text));
            markup.RenderControl(writer);
        }

        /// <summary>Assigns the strip this tab belongs to.</summary>
        /// <param name="parent">The owning strip.</param>
        internal void SetParent(DNNTabStrip parent)
        {
            this.parentTabStrip = parent;
        }

        /// <summary>Records whether the tab was already rendered once.</summary>
        /// <param name="isPostBack">Whether the tab was already rendered.</param>
        internal void SetTabState(bool isPostBack)
        {
            this.isPostBack = isPostBack;
        }

        /// <inheritdoc/>
        protected override void AddAttributesToRender(TextWriter writer)
        {
            if (!this.IsSelected)
            {
                this.Style["display"] = "none";
            }

            base.AddAttributesToRender(writer);

            if (this.ParentControl is not null
                && !string.IsNullOrEmpty(this.ParentControl.DefaultContainerCssClass)
                && string.IsNullOrEmpty(this.CssClass))
            {
                HtmlMarkupWriter.For(writer).AddAttribute("class", this.ParentControl.DefaultContainerCssClass);
            }
        }
    }
}
