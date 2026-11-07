// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.ComponentModel;
    using System.Data;
    using System.Globalization;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Common;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.Services.ClientDependency;
    using DotNetNuke.UI.Utilities;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.Extensions.DependencyInjection;

    using Globals = DotNetNuke.Common.Globals;

    /// <summary>
    /// The PropertyLabelControl control provides a standard UI component for displaying
    /// a label for a property. It contains a Label and Help Text and can be Data Bound.
    /// </summary>
    /// <remarks>
    /// The <c>LinkButton</c>/<c>Label</c>/<c>Panel</c> child controls are now rendered directly, with
    /// the same element structure, identifiers and css classes, so the DNN tooltip script
    /// (<c>initTooltips.js</c>, which binds on <c>.dnnFormHelp</c>/<c>.dnnFormHelpContent</c>) keeps
    /// working. <see cref="HelpStyle"/> and <see cref="LabelStyle"/> are now backed by fields instead
    /// of by the <c>ControlStyle</c> of those child controls, but remain read-only
    /// <see cref="Style"/> instances that the caller configures in place.
    /// </remarks>
    public class PropertyLabelControl : PropertyEditorComponentBase
    {
        private readonly IClientResourceController clientResourceController;
        private readonly IApplicationStatusInfo appStatus;
        private readonly IEventLogger eventLogger;

        private string resourceKey;

        /// <summary>Initializes a new instance of the <see cref="PropertyLabelControl"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IClientResourceController. Scheduled removal in v12.0.0.")]
        public PropertyLabelControl()
            : this(null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="PropertyLabelControl"/> class.</summary>
        /// <param name="clientResourceController">The client resource controller.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        public PropertyLabelControl(IClientResourceController clientResourceController, IApplicationStatusInfo appStatus, IEventLogger eventLogger)
        {
            this.clientResourceController = clientResourceController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IClientResourceController>();
            this.appStatus = appStatus ?? Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>();
            this.eventLogger = eventLogger ?? Globals.GetCurrentServiceProvider().GetRequiredService<IEventLogger>();
        }

        /// <summary>Gets the value of the Help Style.</summary>
        [Browsable(true)]
        [Category("Styles")]
        [Description("Set the Style for the Help Text.")]
        public Style HelpStyle { get; } = new Style();

        /// <summary>Gets the value of the Label Style.</summary>
        [Browsable(true)]
        [Category("Styles")]
        [Description("Set the Style for the Label Text")]
        public Style LabelStyle { get; } = new Style();

        /// <summary>Gets or sets the Caption Text if no ResourceKey is provided.</summary>
        /// <value>A string representing the Caption.</value>
        [Parameter]
        public string Caption { get; set; }

        /// <summary>Gets or sets the identifier of the element this label is associated with.</summary>
        /// <remarks>Replaces <c>Label.AssociatedControlID</c>; it is rendered as the <c>for</c>
        /// attribute when no <see cref="EditControl"/> is assigned.</remarks>
        [Parameter]
        public string AssociatedControlId { get; set; }

        /// <summary>Gets or sets the related Edit Control.</summary>
        /// <remarks>Was a <c>System.Web.UI.Control</c>; it is now the Blazor equivalent,
        /// <see cref="Microsoft.AspNetCore.Components.IComponent"/>, matching the <c>IModuleControl.Control</c> contract.</remarks>
        [Browsable(false)]
        [Parameter]
        public Microsoft.AspNetCore.Components.IComponent EditControl { get; set; }

        /// <summary>Gets or sets text is value of the Label Text if no ResourceKey is provided.</summary>
        /// <value>A string representing the Text.</value>
        [Parameter]
        public string HelpText { get; set; }

        /// <summary>Gets or sets resourceKey is the root localization key for this control.</summary>
        /// <value>A string representing the Resource Key.</value>
        /// <remarks>This control will "standardize" the resource key names, so for instance
        /// if the resource key is "Control", Control.Text is the label text key, Control.Help
        /// is the label help text, Control.ErrorMessage is the Validation Error Message for the
        /// control.
        /// </remarks>
        [Parameter]
        public string ResourceKey
        {
            get
            {
                return this.resourceKey;
            }

            set
            {
                this.resourceKey = value;
            }
        }

        /// <summary>Gets or sets a value indicating whether the Help icon is displayed.</summary>
        [Parameter]
        public bool ShowHelp { get; set; } = true;

        /// <summary>Gets or sets the value of the Field that is bound to the Label.</summary>
        /// <value>A string representing the Name of the Field.</value>
        [Parameter]
        public string DataField { get; set; }

        /// <summary>Gets or sets the DataSource that is bound to this control.</summary>
        /// <value>The DataSource object.</value>
        [Browsable(false)]
        [Parameter]
        public object DataSource { get; set; }

        /// <summary>Gets or sets a value indicating whether the associated property is required.</summary>
        [Parameter]
        public bool Required { get; set; }

        /// <summary>
        /// OnDataBinding runs when the Control is being Data Bound (It is triggered by
        /// a call to Control.DataBind()).
        /// </summary>
        /// <param name="e">The event arguments.</param>
        protected override void OnDataBinding(EventArgs e)
        {
            // If there is a DataSource bind the relevant Properties
            if (this.DataSource != null)
            {
                if (!string.IsNullOrEmpty(this.DataField))
                {
                    // DataBind the Label (via the Resource Key)
                    var dataRow = (DataRowView)this.DataSource;
                    if (this.ResourceKey == string.Empty)
                    {
                        this.ResourceKey = Convert.ToString(dataRow[this.DataField], CultureInfo.InvariantCulture);
                    }
                }
            }
        }

        /// <inheritdoc />
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // The client reference and the tooltip script are per-request registrations;
            // HttpContextSource.Current is null once the Blazor circuit is interactive, in which case
            // they were already registered by the render that served the page.
            var context = HttpContextSource.Current;
            if (context != null)
            {
                JavaScript.RegisterClientReference(context, ClientAPI.ClientNamespaceReferences.dnn);
            }

            JavaScript.RequestRegistration(this.appStatus, this.eventLogger, PortalSettings.Current, CommonJs.DnnPlugins);
            this.clientResourceController.RegisterScript("~/resources/Shared/Scripts/initTooltips.js");
        }

        /// <inheritdoc />
        protected override void RenderContent(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", (this.CssClass ?? string.Empty) + "dnnLabel");
            if (this.AdditionalAttributes != null)
            {
                builder.AddMultipleAttributes(2, this.AdditionalAttributes);
            }

            // The label and its caption.
            builder.OpenElement(10, "label");
            var forId = this.GetAssociatedElementId();
            if (!string.IsNullOrEmpty(forId))
            {
                builder.AddAttribute(11, "for", forId);
            }

            builder.OpenElement(12, "span");
            builder.AddAttribute(13, "id", this.ClientID + "_label");
            var labelCssClass = this.LabelStyle.CssClass;
            if (this.Required)
            {
                labelCssClass += " dnnFormRequired";
            }

            if (!string.IsNullOrEmpty(labelCssClass))
            {
                builder.AddAttribute(14, "class", labelCssClass);
            }

            var labelInline = this.LabelStyle.GetInlineStyle();
            if (labelInline != null)
            {
                builder.AddAttribute(15, "style", labelInline);
            }

            if (!string.IsNullOrEmpty(this.resourceKey))
            {
                // PageBase localizes and then strips the resourcekey attributes after the first
                // render, which replaces the Web Forms render-time control tree walk.
                builder.AddAttribute(16, "resourcekey", this.resourceKey + ".Text");
            }

            builder.AddContent(17, this.Caption);
            builder.CloseElement();
            builder.CloseElement();

            // The help icon.
            if (this.ShowHelp)
            {
                builder.OpenElement(20, "a");
                builder.AddAttribute(21, "id", this.ClientID + "_cmdHelp");
                builder.AddAttribute(22, "class", "dnnFormHelp");
                builder.AddAttribute(23, "href", "#");
                builder.AddAttribute(24, "aria-label", "Help");
                builder.AddAttribute(25, "tabindex", "-1");
                builder.CloseElement();
            }

            // The tooltip holding the help text.
            builder.OpenElement(30, "div");
            builder.AddAttribute(31, "class", "dnnTooltip");

            builder.OpenElement(32, "div");
            builder.AddAttribute(33, "id", this.ClientID + "_pnlHelp");
            builder.AddAttribute(34, "class", this.GetHelpCssClass());
            var helpInline = this.HelpStyle.GetInlineStyle();
            if (helpInline != null)
            {
                builder.AddAttribute(35, "style", helpInline);
            }

            builder.OpenElement(36, "span");
            builder.AddAttribute(37, "id", this.ClientID + "_lblHelp");
            if (!string.IsNullOrEmpty(this.resourceKey))
            {
                builder.AddAttribute(38, "resourcekey", this.resourceKey + ".Help");
            }

            builder.AddContent(39, this.HelpText);
            builder.CloseElement();

            builder.OpenElement(40, "a");
            builder.AddAttribute(41, "class", "pinHelp");
            builder.AddAttribute(42, "href", "#");
            builder.AddAttribute(43, "aria-label", "Pin");
            builder.CloseElement();

            builder.CloseElement();
            builder.CloseElement();

            builder.CloseElement();
        }

        /// <summary>Builds the css class of the help container.</summary>
        /// <returns>The css class, which always contains the classes the tooltip script binds on.</returns>
        private string GetHelpCssClass()
        {
            var cssClass = this.HelpStyle.CssClass ?? string.Empty;

            // make sure the help container have the default css class to active js handler.
            if (!cssClass.Contains("dnnClear", StringComparison.Ordinal))
            {
                cssClass = $"dnnClear {cssClass}";
            }

            if (!cssClass.Contains("dnnFormHelpContent", StringComparison.Ordinal))
            {
                cssClass = $"dnnFormHelpContent {cssClass}";
            }

            return cssClass.Trim();
        }

        /// <summary>Resolves the identifier this label's <c>for</c> attribute points at.</summary>
        /// <returns>The element identifier, or <see langword="null"/> when there is none.</returns>
        private string GetAssociatedElementId()
        {
            switch (this.EditControl)
            {
                case EditControl editControl:
                    return editControl.EditControlClientId;
                case PropertyEditorComponentBase component:
                    return component.ClientID;
                default:
                    return this.AssociatedControlId;
            }
        }
    }
}
