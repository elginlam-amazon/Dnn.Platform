// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;

    using DotNetNuke.Common;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Tabs;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>Base class for Dnn specific WebControls.</summary>
    /// <remarks>
    /// This was a <c>System.Web.UI.WebControls.WebControl</c>. On ASP.NET Core the platform renders with
    /// Blazor, so the base type is <see cref="ComponentBase"/>: <see cref="HtmlOutput"/> is still the
    /// single abstract member a derived control implements, and it is now emitted by
    /// <see cref="BuildRenderTree"/> instead of by <c>RenderContents(HtmlTextWriter)</c>. The rendered
    /// element is still a <c>span</c> (the default <c>WebControl</c> tag), and
    /// <see cref="AdditionalAttributes"/> replaces <c>WebControl.Attributes</c>.
    /// </remarks>
    public abstract class WebControlBase : ComponentBase
    {
        private string styleSheetUrl = string.Empty;
        private string theme = string.Empty;

        /// <summary>Gets or sets the attributes rendered onto this control's element.</summary>
        /// <remarks>Replaces <c>WebControl.Attributes</c>/<c>CssClass</c>/<c>Style</c>.</remarks>
        [Parameter(CaptureUnmatchedValues = true)]
        public IDictionary<string, object> AdditionalAttributes { get; set; }

        /// <summary>Gets a url to the web control resources folder.</summary>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public string ResourcesFolderUrl => Globals.ResolveUrl("~/resources/");

        /// <summary>Gets a value indicating whether the control is currently in a page visible only to hosts.</summary>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public bool IsHostMenu => Globals.IsHostTab(TabController.CurrentPage.TabID);

        /// <summary>Gets the portal settings for the portal where the WebControl is displayed.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Obsolete("Deprecated in DotNetNuke 9.8.0. Use PortalController.Instance.GetCurrentSettings() instead, if you need access to the ActiveTab, use TabController.CurrentPage. Scheduled for removal in v11.0.0.")]
        public PortalSettings PortalSettings => PortalSettings.Current;

        /// <summary>Gets the HTML content for this WebControl rendering.</summary>
        public abstract string HtmlOutput { get; }

        /// <summary>Gets a value indicating whether this WebControl is currently displayed in the admin menu.</summary>
        [Obsolete("Deprecated in DotNetNuke 7.0.0. There is no longer the concept of an Admin Page.  All pages are controlled by Permissions. Scheduled for removal in v11.0.0.", true)]
        public bool IsAdminMenu => false;

        /// <summary>Gets or sets a string representing the theme to use for this WebControl.</summary>
        [Parameter]
        public string Theme
        {
            get => this.theme;
            set => this.theme = value;
        }

        /// <summary>Gets or sets the url to the stylesheet to use for this WebControl.</summary>
        [Parameter]
        public string StyleSheetUrl
        {
            get
            {
                if (this.styleSheetUrl.StartsWith("~", StringComparison.Ordinal))
                {
                    return Globals.ResolveUrl(this.styleSheetUrl);
                }
                else
                {
                    return this.styleSheetUrl;
                }
            }

            set
            {
                this.styleSheetUrl = value;
            }
        }

        /// <summary>Renders the html for this WebControl to the output.</summary>
        /// <param name="builder">The render tree builder to write to.</param>
        /// <remarks>Replaces <c>RenderContents(HtmlTextWriter output)</c>, which wrote <see cref="HtmlOutput"/> verbatim.</remarks>
        [SuppressMessage("Microsoft.Naming", "CA1725:ParameterNamesShouldMatchBaseDeclaration", Justification = "Breaking change")]
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.OpenElement(0, "span");
            if (this.AdditionalAttributes is not null)
            {
                builder.AddMultipleAttributes(1, this.AdditionalAttributes);
            }

            builder.AddMarkupContent(2, this.HtmlOutput);
            builder.CloseElement();
        }
    }
}
