// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.UI.WebControls
{
    using System;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Common;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI;
    using DotNetNuke.UI.Utilities;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;

    using Globals = DotNetNuke.Common.Globals;

    /// <summary>A form label control.</summary>
    /// <remarks>
    /// This was a <c>Panel</c> building its content in <c>CreateChildControls</c>. On ASP.NET Core
    /// it is a <see cref="DnnComponentBase"/> that renders the same <c>div.dnnLabel</c> tree
    /// (label &gt; span, the help anchor and the optional tooltip panel) into its
    /// <see cref="DnnComponentBase.Controls"/> collection.
    /// </remarks>
    public class DnnFormLabel : DnnComponentBase
    {
        private readonly IApplicationStatusInfo appStatus;
        private readonly IEventLogger eventLogger;

        /// <summary>Initializes a new instance of the <see cref="DnnFormLabel"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IApplicationStatusInfo. Scheduled removal in v12.0.0.")]
        public DnnFormLabel()
            : this(null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnFormLabel"/> class.</summary>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        public DnnFormLabel(IApplicationStatusInfo appStatus, IEventLogger eventLogger)
        {
            this.appStatus = appStatus ?? Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>();
            this.eventLogger = eventLogger ?? Globals.GetCurrentServiceProvider().GetRequiredService<IEventLogger>();
        }

        /// <summary>Gets or sets the ID of the associated control.</summary>
        [Parameter]
        public string AssociatedControlID { get; set; }

        /// <summary>Gets or sets the path to the local resource file.</summary>
        [Parameter]
        public string LocalResourceFile { get; set; }

        /// <summary>Gets or sets the resource key of the label.</summary>
        [Parameter]
        public string ResourceKey { get; set; }

        /// <summary>Gets or sets the resource key of the tooltip.</summary>
        [Parameter]
        public string ToolTipKey { get; set; }

        /// <summary>Gets or sets a value indicating whether the field is required.</summary>
        [Parameter]
        public bool RequiredField { get; set; }

        /// <inheritdoc />
        protected override string TagName => "div";

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        /// <inheritdoc />
        protected override void CreateChildControls()
        {
            string toolTipText = this.LocalizeString(this.ToolTipKey);
            if (string.IsNullOrEmpty(this.CssClass))
            {
                this.CssClass = "dnnLabel";
            }
            else if (!this.CssClass.Contains("dnnLabel"))
            {
                this.CssClass += " dnnLabel";
            }

            var outerLabel = new DnnGenericControl("label");
            if (!string.IsNullOrEmpty(this.AssociatedControlID))
            {
                outerLabel.Attributes["for"] = this.AssociatedControlID;
            }

            this.Controls.Add(outerLabel);

            var label = new DotNetNuke.UI.DnnLabel { ID = "Label", Text = this.LocalizeString(this.ResourceKey), };
            if (this.RequiredField)
            {
                label.CssClass = (label.CssClass + " dnnFormRequired").Trim();
            }

            outerLabel.Controls.Add(label);

            var link = new DnnGenericControl("a") { ID = "Link", CssClass = "dnnFormHelp", };
            link.Attributes["tabindex"] = "-1";
            link.Attributes["aria-label"] = "Help";
            this.Controls.Add(link);

            if (!string.IsNullOrEmpty(toolTipText))
            {
                var tooltipPanel = new DnnPanel { CssClass = "dnnTooltip", };
                this.Controls.Add(tooltipPanel);

                var panel = new DnnPanel { ID = "Help", CssClass = "dnnFormHelpContent dnnClear", };
                tooltipPanel.Controls.Add(panel);

                var helpLabel = new DotNetNuke.UI.DnnLabel { ID = "Text", CssClass = "dnnHelpText", Text = toolTipText, };
                panel.Controls.Add(helpLabel);

                var pinLink = new DnnGenericControl("a") { CssClass = "pinHelp", };
                pinLink.Attributes["href"] = "#";
                pinLink.Attributes["aria-label"] = "Pin";
                panel.Controls.Add(pinLink);

                JavaScript.RegisterClientReference(DotNetNuke.Common.HttpContextSource.Current, ClientAPI.ClientNamespaceReferences.dnn);
                JavaScript.RequestRegistration(this.appStatus, this.eventLogger, PortalSettings.Current, CommonJs.DnnPlugins);
            }
        }
        #pragma warning restore BL0005

        /// <summary>Gets the localized string corresponding to the <paramref name="key"/>.</summary>
        /// <param name="key">The resource key to find.</param>
        /// <returns>The localized text.</returns>
        protected string LocalizeString(string key)
        {
            return Localization.GetString(key, this.LocalResourceFile);
        }
    }
}
