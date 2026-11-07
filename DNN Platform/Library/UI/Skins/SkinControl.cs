// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.Skins
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Framework;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI.WebControls;

    /// <summary>The skin/container picker used by the admin UI.</summary>
    /// <remarks>
    /// The base class changed from <c>DotNetNuke.Framework.UserControlBase</c> to
    /// <see cref="DnnComponentBase"/> because the control builds its own child controls; the
    /// <c>DropDownList</c>/<c>RadioButton</c> fields are the DNN component equivalents
    /// (<see cref="DnnDropDownList"/>/<see cref="DnnRadioButton"/>) and the WebForms view state is
    /// now component state.
    /// </remarks>
    public class SkinControl : DnnComponentBase
    {
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DnnDropDownList cboSkin;
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected CommandButton cmdPreview;
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DnnRadioButton optHost;
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DnnRadioButton optSite;

        private string defaultKey = "System";
        private string skinRoot;
        private string skinSrc;
        private string width = string.Empty;
        private string localResourceFile;
        private PortalInfo objPortal;
        private string skinControlWidth;
        private string selectedSkinRoot;
        private string selectedSkinSrc;
        private bool skinsLoaded;

        /// <summary>Gets the portal settings of the current portal.</summary>
        /// <remarks>Was inherited from <c>DotNetNuke.Framework.UserControlBase</c>.</remarks>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public PortalSettings PortalSettings => PortalSettings.Current;

        public string DefaultKey
        {
            get => this.defaultKey;
            set => this.defaultKey = value;
        }

        /// <summary>Gets or sets the width of the skin list.</summary>
        /// <remarks>Was backed by <c>ViewState["SkinControlWidth"]</c>.</remarks>
        public string Width
        {
            get => this.skinControlWidth ?? string.Empty;
            set => this.width = value;
        }

        /// <summary>Gets or sets the skin root (<c>Skins</c> or <c>Containers</c>).</summary>
        /// <remarks>Was backed by <c>ViewState["SkinRoot"]</c>.</remarks>
        public string SkinRoot
        {
            get => this.selectedSkinRoot ?? string.Empty;
            set => this.skinRoot = value;
        }

        public string SkinSrc
        {
            get
            {
                if (this.cboSkin.SelectedItem != null)
                {
                    return this.cboSkin.SelectedItem.Value;
                }
                else
                {
                    return string.Empty;
                }
            }

            set
            {
                this.skinSrc = value;
            }
        }

        public string LocalResourceFile
        {
            get
            {
                string fileRoot;
                if (string.IsNullOrEmpty(this.localResourceFile))
                {
                    fileRoot = this.TemplateSourceDirectory + "/" + Localization.LocalResourceDirectory + "/SkinControl.ascx";
                }
                else
                {
                    fileRoot = this.localResourceFile;
                }

                return fileRoot;
            }

            set
            {
                this.localResourceFile = value;
            }
        }

        /// <inheritdoc />
        protected override void CreateChildControls()
        {
            base.CreateChildControls();

            // The WebForms version declared these in SkinControl.ascx; the component builds them.
            this.cboSkin = new DnnDropDownList { ID = "cboSkin" };
            this.optHost = new DnnRadioButton { ID = "optHost", GroupName = "SkinScope", Checked = true };
            this.optSite = new DnnRadioButton { ID = "optSite", GroupName = "SkinScope" };
            this.cmdPreview = new CommandButton { ID = "cmdPreview" };

            this.Controls.Add(this.optHost);
            this.Controls.Add(this.optSite);
            this.Controls.Add(this.cboSkin);
            this.Controls.Add(this.cmdPreview);
        }

        /// <summary>The Page_Load server event handler on this page is used to populate the role information for the page.</summary>
        /// <param name="e">The event arguments.</param>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            this.optHost.CheckedChanged += this.optHost_CheckedChanged;
            this.optSite.CheckedChanged += this.optSite_CheckedChanged;
            this.cmdPreview.Click += this.cmdPreview_Click;
            try
            {
                var portalIdParameter = this.Request?.GetValue("pid");
                if (portalIdParameter != null && (Globals.IsHostTab(this.PortalSettings.ActiveTab.TabID) || UserController.Instance.GetCurrentUserInfo().IsSuperUser))
                {
                    this.objPortal = PortalController.Instance.GetPortal(int.Parse(portalIdParameter, CultureInfo.InvariantCulture));
                }
                else
                {
                    this.objPortal = PortalController.Instance.GetPortal(this.PortalSettings.PortalId);
                }

                // Was !Page.IsPostBack: the persistent values and the list are initialized once,
                // when the component first loads, and survive in the component's own state.
                if (!this.skinsLoaded)
                {
                    this.skinsLoaded = true;

                    // save persistent values
                    this.skinControlWidth = this.width;
                    this.selectedSkinRoot = this.skinRoot;
                    this.selectedSkinSrc = this.skinSrc;

                    // set width of control
                    if (!string.IsNullOrEmpty(this.width))
                    {
                        this.cboSkin.Width = this.width;
                    }

                    // set selected skin
                    if (!string.IsNullOrEmpty(this.skinSrc))
                    {
                        switch (this.skinSrc.Substring(0, 3))
                        {
                            case "[L]":
                                this.optHost.Checked = false;
                                this.optSite.Checked = true;
                                break;
                            case "[G]":
                                this.optSite.Checked = false;
                                this.optHost.Checked = true;
                                break;
                        }
                    }
                    else
                    {
                        // no skin selected, initialized to site skin if any exists
                        string strRoot = this.objPortal.HomeDirectoryMapPath + this.SkinRoot;
                        if (Directory.Exists(strRoot) && Directory.GetDirectories(strRoot).Length > 0)
                        {
                            this.optHost.Checked = false;
                            this.optSite.Checked = true;
                        }
                    }

                    this.LoadSkins();
                }
            }
            catch (Exception exc)
            {
                // Module failed to load
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Breaking Change")]
        [SuppressMessage("Microsoft.Design", "CA1707:IdentifiersShouldNotContainUnderscores", Justification = "Breaking change")]

        // ReSharper disable once InconsistentNaming
        protected void optHost_CheckedChanged(object sender, EventArgs e)
        {
            this.LoadSkins();
        }

        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Breaking Change")]
        [SuppressMessage("Microsoft.Design", "CA1707:IdentifiersShouldNotContainUnderscores", Justification = "Breaking change")]

        // ReSharper disable once InconsistentNaming
        protected void optSite_CheckedChanged(object sender, EventArgs e)
        {
            this.LoadSkins();
        }

        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Breaking Change")]
        [SuppressMessage("Microsoft.Design", "CA1707:IdentifiersShouldNotContainUnderscores", Justification = "Breaking change")]

        // ReSharper disable once InconsistentNaming
        protected void cmdPreview_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(this.SkinSrc))
            {
                string strType = this.SkinRoot.Substring(0, this.SkinRoot.Length - 1);

                string strURL = Globals.ApplicationURL() + "&" + strType + "Src=" + Globals.QueryStringEncode(this.SkinSrc.Replace(".ascx", string.Empty));

                if (this.SkinRoot == SkinController.RootContainer)
                {
                    var moduleIdParameter = this.Request?.GetValue("ModuleId");
                    if (moduleIdParameter != null)
                    {
                        strURL += "&ModuleId=" + moduleIdParameter;
                    }
                }

                this.Response.Redirect(strURL, true);
            }
        }

        private void LoadSkins()
        {
            this.cboSkin.Items.Clear();

            if (this.optHost.Checked)
            {
                // load host skins
                foreach (KeyValuePair<string, string> skin in SkinController.GetSkins(this.objPortal, this.SkinRoot, SkinScope.Host))
                {
                    this.cboSkin.Items.Add(new DnnListItem(skin.Key, skin.Value));
                }
            }

            if (this.optSite.Checked)
            {
                // load portal skins
                foreach (KeyValuePair<string, string> skin in SkinController.GetSkins(this.objPortal, this.SkinRoot, SkinScope.Site))
                {
                    this.cboSkin.Items.Add(new DnnListItem(skin.Key, skin.Value));
                }
            }

            this.cboSkin.Items.Insert(0, new DnnListItem("<" + Localization.GetString(this.DefaultKey, this.LocalResourceFile) + ">", string.Empty));

            // select current skin
            for (int intIndex = 0; intIndex < this.cboSkin.Items.Count; intIndex++)
            {
                if (this.cboSkin.Items[intIndex].Value.Equals(this.selectedSkinSrc ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    this.cboSkin.Items[intIndex].Selected = true;
                    break;
                }
            }
        }
    }
}
