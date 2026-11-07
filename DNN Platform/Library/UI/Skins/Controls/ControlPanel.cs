// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.Skins.Controls
{
    using System;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.UI.ControlPanels;
    using DotNetNuke.Web.Client.ClientResourceManagement;

    using Microsoft.Extensions.DependencyInjection;

    /// <summary>The skin object that hosts the DNN control panel (the control bar).</summary>
    public class ControlPanel : SkinObjectBase
    {
        private readonly IHostSettings hostSettings;

        /// <summary>Initializes a new instance of the <see cref="ControlPanel"/> class.</summary>
        public ControlPanel()
            : this(null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ControlPanel"/> class.</summary>
        /// <param name="hostSettings">The host settings.</param>
        public ControlPanel(IHostSettings hostSettings)
        {
            this.hostSettings = hostSettings ?? Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettings>();
        }

        /// <summary>Gets or sets a value indicating whether the control panel renders as a dockable bar.</summary>
        public bool IsDockable { get; set; }

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            if (this.Request?.GetValue("dnnprintmode") != "true" && !UrlUtils.InPopUp())
            {
                var objControlPanel = ControlUtilities.LoadControl<ControlPanelBase>(this, this.hostSettings.ControlPanel);

                if (objControlPanel.IncludeInControlHierarchy)
                {
                    objControlPanel.IsDockable = this.IsDockable;
                    if (!this.hostSettings.ControlPanel.EndsWith("controlbar.ascx", StringComparison.InvariantCultureIgnoreCase))
                    {
                        this.Controls.Add(objControlPanel);
                    }
                    else
                    {
                        // Was: injected as the first control of the page's <form runat="server"> (or of
                        // the page itself). There is no server form on ASP.NET Core; the equivalent
                        // top-of-page position is the first control of the enclosing skin.
                        var parentSkin = Skin.GetParentSkin(this);
                        if (parentSkin != null)
                        {
                            parentSkin.Controls.AddAt(0, objControlPanel);
                        }
                        else
                        {
                            this.Controls.Add(objControlPanel);
                        }
                    }

                    // register admin.css
                    ClientResourceManager.RegisterAdminStylesheet(HttpContextSource.Current, Globals.ApplicationPath + "/portals/_default/admin.css");
                }
            }
        }
    }
}
