// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.UI.WebControls.Internal
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.UI.Skins;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>This control is only for internal use, please don't reference it in any other place as it may be removed in the future.</summary>
    /// <remarks>
    /// The skins are still enumerated by <see cref="SkinController.GetSkins"/>, which is now backed
    /// by the hosting environment's file provider rather than by <c>Server.MapPath</c>. The
    /// <c>[L]</c>/<c>[G]</c> placeholders of a skin path are resolved to the portal home directory
    /// and to the host path respectively — this used to be done by an inline client script reading
    /// the <c>PortalPath</c>/<c>HostPath</c> attributes, and is now done server side when the item
    /// tooltips are built, so no script registration is needed.
    /// </remarks>
    public class DnnSkinComboBox : DnnComboBox
    {
        /// <summary>Initializes a new instance of the <see cref="DnnSkinComboBox"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IApplicationStatusInfo. Scheduled removal in v12.0.0.")]
        public DnnSkinComboBox()
            : this(null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnSkinComboBox"/> class.</summary>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="clientResourceController">The client resource controller.</param>
        public DnnSkinComboBox(IApplicationStatusInfo appStatus, IEventLogger eventLogger, IClientResourceController clientResourceController)
            : base(
                appStatus ?? Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>(),
                eventLogger ?? Globals.GetCurrentServiceProvider().GetRequiredService<IEventLogger>(),
                clientResourceController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IClientResourceController>())
        {
            this.PortalId = Null.NullInteger;
        }

        /// <summary>Gets or sets the portal ID.</summary>
        [Parameter]
        public int PortalId { get; set; }

        /// <summary>Gets or sets the root path.</summary>
        [Parameter]
        public string RootPath { get; set; }

        /// <summary>Gets or sets the skin scope.</summary>
        [Parameter]
        public SkinScope Scope { get; set; }

        /// <summary>Gets or sets a value indicating whether to include a "None Specified" item.</summary>
        [Parameter]
        public bool IncludeNoneSpecificItem { get; set; }

        /// <summary>Gets or sets the text of the "None Specified" item.</summary>
        [Parameter]
        public string NoneSpecificText { get; set; }

        private PortalInfo Portal => this.PortalId == Null.NullInteger ? null : PortalController.Instance.GetPortal(this.PortalId);

        /// <inheritdoc />
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            this.DataTextField = "Key";
            this.DataValueField = "Value";

            // Under Web Forms the list was only populated on a GET (`!Page.IsPostBack`) so that the
            // posted selection survived. A Blazor component keeps its items for the life of the
            // circuit, so the equivalent guard is "populate once".
            if (this.Items.Count == 0 && !string.IsNullOrEmpty(this.RootPath))
            {
                this.DataSource = SkinController.GetSkins(this.Portal, this.RootPath, this.Scope)
                                           .ToDictionary(skin => skin.Key, skin => skin.Value);

                if (string.IsNullOrEmpty(this.SelectedValue))
                {
                    this.DataBind();
                }
                else
                {
                    this.DataBind(this.SelectedValue);
                }

                if (this.IncludeNoneSpecificItem)
                {
                    this.InsertItem(0, this.NoneSpecificText, string.Empty);
                }
            }

            this.ResolveSkinPaths();
        }

        /// <inheritdoc />
        protected override void PerformDataBinding(IEnumerable dataSource, IList<string> previouslySelectedValues)
        {
            // do not select item during data binding, item will select later
            var selectedValue = this.SelectedValue;
            this.SelectedValue = null;

            base.PerformDataBinding(dataSource, previouslySelectedValues);

            this.SelectedValue = selectedValue;
        }

        /// <summary>
        /// Resolves the <c>[L]</c>/<c>[G]</c> placeholders of the skin paths into the item tooltips.
        /// </summary>
        /// <remarks>
        /// Replaces <c>AttachEvents</c>, which registered a client script doing the same string
        /// replacement over the <c>PortalPath</c>/<c>HostPath</c> attributes. Those attributes are
        /// still rendered because third party skins read them.
        /// </remarks>
        private void ResolveSkinPaths()
        {
            if (!UserController.Instance.GetCurrentUserInfo().IsSuperUser)
            {
                return;
            }

            var portalPath = this.Portal != null ? this.Portal.HomeDirectory : string.Empty;
            this.Attributes["PortalPath"] = portalPath;
            this.Attributes["HostPath"] = Globals.HostPath;

            foreach (var item in this.Items)
            {
                if (string.IsNullOrEmpty(item.Value))
                {
                    continue;
                }

                var tooltip = item.Value.Replace("[G]", Globals.HostPath).Replace("[L]", portalPath);
                if (string.Equals(item.Value, this.SelectedValue, StringComparison.Ordinal))
                {
                    this.Attributes["title"] = tooltip;
                }
            }
        }
    }
}
