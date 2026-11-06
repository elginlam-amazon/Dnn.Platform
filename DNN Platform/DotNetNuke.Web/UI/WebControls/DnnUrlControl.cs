// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.UI.WebControls
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.Portals;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Framework;
    using DotNetNuke.Security;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.FileSystem;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI;
    using DotNetNuke.UI.UserControls;
    using DotNetNuke.UI.Utilities;
    using DotNetNuke.Web.Client.ClientResourceManagement;
    using DotNetNuke.Web.Common;
    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;

    using Globals = DotNetNuke.Common.Globals;

    /// <summary>A URL control.</summary>
    /// <remarks>
    /// This was an <c>abstract UserControlBase</c> whose markup lived in
    /// <c>controls/DnnUrlControl.ascx</c>. On ASP.NET Core it is a <see cref="DnnComponentBase"/>
    /// that owns its child tree (built in <see cref="CreateChildControls"/> with exactly the same
    /// panels, labels, inputs and commands as the .ascx) and holds the former view state entries in
    /// fields of the component. The URL-type selector, the per-type panel visibility
    /// (<c>DoRenderTypeControls</c>), the <c>UrlType</c>/<c>Url</c> round-trip and the
    /// track-clicks/log-activity/new-window checkboxes behave exactly as before; the auto-postback
    /// events became component events.
    /// </remarks>
    public abstract class DnnUrlControl : DnnComponentBase
    {
        // ReSharper disable InconsistentNaming

        /// <summary>Gets or sets the error row panel.</summary>
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1306:FieldNamesMustBeginWithLowerCaseLetter", Justification = "Breaking Change")]
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DnnPanel ErrorRow;

        /// <summary>Gets or sets the file row panel.</summary>
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1306:FieldNamesMustBeginWithLowerCaseLetter", Justification = "Breaking Change")]
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DnnPanel FileRow;

        /// <summary>Gets or sets the images row panel.</summary>
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1306:FieldNamesMustBeginWithLowerCaseLetter", Justification = "Breaking Change")]
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DnnPanel ImagesRow;

        /// <summary>Gets or sets the tab row panel.</summary>
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1306:FieldNamesMustBeginWithLowerCaseLetter", Justification = "Breaking Change")]
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DnnPanel TabRow;

        /// <summary>Gets or sets the type row panel.</summary>
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1306:FieldNamesMustBeginWithLowerCaseLetter", Justification = "Breaking Change")]
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DnnPanel TypeRow;

        /// <summary>Gets or sets the URL row panel.</summary>
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1306:FieldNamesMustBeginWithLowerCaseLetter", Justification = "Breaking Change")]
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DnnPanel URLRow;

        /// <summary>Gets or sets the user row panel.</summary>
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1306:FieldNamesMustBeginWithLowerCaseLetter", Justification = "Breaking Change")]
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DnnPanel UserRow;

        // ReSharper restore InconsistentNaming

        /// <summary>Gets or sets the images dropdown list.</summary>
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DnnListControl cboImages;

        /// <summary>Gets or sets the tabs dropdown list.</summary>
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DnnPageDropDownList cboTabs;

        /// <summary>Gets or sets the URLs dropdown list.</summary>
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DnnListControl cboUrls;

        /// <summary>Gets or sets the log checkbox.</summary>
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DnnCheckBox chkLog;

        /// <summary>Gets or sets the new window checkbox.</summary>
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DnnCheckBox chkNewWindow;

        /// <summary>Gets or sets the track checkbox.</summary>
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DnnCheckBox chkTrack;

        /// <summary>Gets or sets the add button.</summary>
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DnnTextButton cmdAdd;

        /// <summary>Gets or sets the delete button.</summary>
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DnnTextButton cmdDelete;

        /// <summary>Gets or sets the select button.</summary>
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DnnTextButton cmdSelect;

        /// <summary>Gets or sets the images label.</summary>
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DotNetNuke.UI.DnnLabel lblImages;

        /// <summary>Gets or sets the message label.</summary>
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DotNetNuke.UI.DnnLabel lblMessage;

        /// <summary>Gets or sets the tab label.</summary>
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DotNetNuke.UI.DnnLabel lblTab;

        /// <summary>Gets or sets the URL label.</summary>
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DotNetNuke.UI.DnnLabel lblURL;

        /// <summary>Gets or sets the URL type label.</summary>
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DotNetNuke.UI.DnnLabel lblURLType;

        /// <summary>Gets or sets the user label.</summary>
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DotNetNuke.UI.DnnLabel lblUser;

        /// <summary>Gets or sets the type radio button list.</summary>
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DnnRadioButtonList optType;

        /// <summary>Gets or sets the URL textbox.</summary>
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DnnTextBox txtUrl;

        /// <summary>Gets or sets the user textbox.</summary>
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DnnTextBox txtUser;

        /// <summary>Gets or sets the file picker uploader.</summary>
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DnnFilePickerUploader ctlFile;

        private DnnPanel urlControlContainer;
        private string viewStateFileFilter = string.Empty;
        private bool? viewStateIncludeActiveTab;
        private int? viewStateModuleId;
        private bool? viewStateRequired;
        private bool? viewStateShowFiles;
        private bool? viewStateShowImages;
        private bool? viewStateShowNone;
        private bool? viewStateShowTabs;
        private bool? viewStateShowUpLoad;
        private bool? viewStateShowUrls;
        private bool? viewStateShowUsers;
        private string viewStateUrl;
        private string viewStateUrlType;
        private string viewStateSkinControlWidth;
        private bool isUrlControlLoaded;
        private bool isInitialized;
        private bool doChangeURL;
        private bool doRenderTypeControls;
        private bool doRenderTypes;
        private string localResourceFile;
        private PortalInfo objPortal;

        /// <summary>Initializes a new instance of the <see cref="DnnUrlControl"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IApplicationStatusInfo. Scheduled removal in v12.0.0.")]
        protected DnnUrlControl()
            : this(null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnUrlControl"/> class.</summary>
        /// <param name="appStatus">The application status.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.4. Please use overload with IHostSettings. Scheduled removal in v12.0.0.")]
        protected DnnUrlControl(IApplicationStatusInfo appStatus)
            : this(appStatus, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnUrlControl"/> class.</summary>
        /// <param name="appStatus">The application status.</param>
        /// <param name="hostSettings">The host settings.</param>
        protected DnnUrlControl(IApplicationStatusInfo appStatus, IHostSettings hostSettings)
        {
            this.AppStatus = appStatus ?? Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>();
            this.HostSettings = hostSettings ?? Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettings>();
        }

        /// <summary>Gets a value indicating whether to add logging to the URL.</summary>
        public bool Log
        {
            get
            {
                if (this.chkLog.Visible)
                {
                    return this.chkLog.Checked;
                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>Gets a value indicating whether to add tracking to the URL.</summary>
        public bool Track
        {
            get
            {
                if (this.chkTrack.Visible)
                {
                    return this.chkTrack.Checked;
                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>Gets or sets the file filter.</summary>
        public string FileFilter
        {
            get => this.viewStateFileFilter ?? string.Empty;

            set => this.viewStateFileFilter = value;
        }

        /// <summary>Gets or sets a value indicating whether to include the current page.</summary>
        public bool IncludeActiveTab
        {
            get => this.viewStateIncludeActiveTab ?? false; // Set as default

            set
            {
                this.viewStateIncludeActiveTab = value;
                if (this.isInitialized)
                {
                    this.doRenderTypeControls = true;
                }
            }
        }

        /// <summary>Gets or sets the local resource file path.</summary>
        public string LocalResourceFile
        {
            get
            {
                string fileRoot;
                if (string.IsNullOrEmpty(this.localResourceFile))
                {
                    fileRoot = this.TemplateSourceDirectory + "/" + Localization.LocalResourceDirectory + "/URLControl.ascx";
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

        /// <summary>Gets or sets the module ID.</summary>
        public int ModuleID
        {
            get
            {
                int myMid = -2;
                if (this.viewStateModuleId != null)
                {
                    myMid = this.viewStateModuleId.Value;
                }
                else
                {
                    var moduleId = this.Request?.Query["mid"].ToString();
                    if (!string.IsNullOrEmpty(moduleId) && !int.TryParse(moduleId, out myMid))
                    {
                        myMid = -2;
                    }
                }

                return myMid;
            }

            set
            {
                this.viewStateModuleId = value;
            }
        }

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        /// <summary>Gets or sets a value indicating whether to open the URL in a new window.</summary>
        public bool NewWindow
        {
            get => this.chkNewWindow.Visible && this.chkNewWindow.Checked;
            set => this.chkNewWindow.Checked = this.chkNewWindow.Visible && value;
        }
        #pragma warning restore BL0005

        /// <summary>Gets or sets a value indicating whether selecting a URL is required.</summary>
        public bool Required
        {
            get => this.viewStateRequired ?? true; // Set as default in the old variable

            set
            {
                this.viewStateRequired = value;
                if (this.isInitialized)
                {
                    this.doRenderTypeControls = true;
                }
            }
        }

        /// <summary>Gets or sets a value indicating whether to show files.</summary>
        public bool ShowFiles
        {
            get => this.viewStateShowFiles ?? true;

            set
            {
                this.viewStateShowFiles = value;
                if (this.isInitialized)
                {
                    this.doRenderTypes = true;
                }
            }
        }

        /// <summary>Gets or sets a value indicating whether to show images.</summary>
        public bool ShowImages
        {
            get => this.viewStateShowImages ?? false;

            set
            {
                this.viewStateShowImages = value;
                if (this.isInitialized)
                {
                    this.doRenderTypes = true;
                }
            }
        }

        /// <summary>Gets or sets a value indicating whether to show the log option.</summary>
        public bool ShowLog
        {
            get => this.chkLog.Visible;
            set => this.chkLog.Visible = value;
        }

        /// <summary>Gets or sets a value indicating whether to show the new window option.</summary>
        public bool ShowNewWindow
        {
            get => this.chkNewWindow.Visible;
            set => this.chkNewWindow.Visible = value;
        }

        /// <summary>Gets or sets a value indicating whether to show a None option.</summary>
        public bool ShowNone
        {
            get => this.viewStateShowNone ?? false;

            set
            {
                this.viewStateShowNone = value;
                if (this.isInitialized)
                {
                    this.doRenderTypes = true;
                }
            }
        }

        /// <summary>Gets or sets a value indicating whether to show pages.</summary>
        public bool ShowTabs
        {
            get => this.viewStateShowTabs ?? true;

            set
            {
                this.viewStateShowTabs = value;
                if (this.isInitialized)
                {
                    this.doRenderTypes = true;
                }
            }
        }

        /// <summary>Gets or sets a value indicating whether a tracking option.</summary>
        public bool ShowTrack
        {
            get => this.chkTrack.Visible;
            set => this.chkTrack.Visible = value;
        }

        /// <summary>Gets or sets a value indicating whether to show an upload option.</summary>
        public bool ShowUpLoad
        {
            get => this.viewStateShowUpLoad ?? true;

            set
            {
                this.viewStateShowUpLoad = value;
                if (this.isInitialized)
                {
                    this.doRenderTypeControls = true;
                }
            }
        }

        /// <summary>Gets or sets a value indicating whether to show URLs.</summary>
        public bool ShowUrls
        {
            get => this.viewStateShowUrls ?? true;

            set
            {
                this.viewStateShowUrls = value;
                if (this.isInitialized)
                {
                    this.doRenderTypes = true;
                }
            }
        }

        /// <summary>Gets or sets a value indicating whether to show users.</summary>
        public bool ShowUsers
        {
            get => this.viewStateShowUsers ?? false;

            set
            {
                this.viewStateShowUsers = value;
                if (this.isInitialized)
                {
                    this.doRenderTypes = true;
                }
            }
        }

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        /// <summary>Gets or sets the URL.</summary>
        public string Url
        {
            get
            {
                string r = string.Empty;
                string strCurrentType = string.Empty;
                if (this.optType.Items.Count > 0 && this.optType.SelectedIndex >= 0)
                {
                    strCurrentType = this.optType.SelectedItem.Value;
                }

                switch (strCurrentType)
                {
                    case "I":
                        if (this.cboImages.SelectedItem != null)
                        {
                            r = this.cboImages.SelectedItem.Value;
                        }

                        break;
                    case "U":
                        if (this.cboUrls.Visible)
                        {
                            if (this.cboUrls.SelectedItem != null)
                            {
                                r = this.cboUrls.SelectedItem.Value;
                                this.txtUrl.Text = r;
                            }
                        }
                        else
                        {
                            string mCustomUrl = this.txtUrl.Text;
                            if (mCustomUrl.Equals("http://", StringComparison.OrdinalIgnoreCase))
                            {
                                r = string.Empty;
                            }
                            else
                            {
                                r = Globals.AddHTTP(mCustomUrl);
                            }
                        }

                        break;
                    case "T":
                        string strTab = string.Empty;
                        if (this.cboTabs.SelectedItem != null)
                        {
                            strTab = this.cboTabs.SelectedItem.Value;
                            int id;
                            if (int.TryParse(strTab, out id) && id >= 0)
                            {
                                r = strTab;
                            }
                        }

                        break;
                    case "F":
                        if (this.ctlFile.FileID > Null.NullInteger)
                        {
                            r = "FileID=" + this.ctlFile.FileID;
                        }
                        else
                        {
                            r = string.Empty;
                        }

                        break;
                    case "M":
                        if (!string.IsNullOrEmpty(this.txtUser.Text))
                        {
                            UserInfo objUser = UserController.GetCachedUser(this.HostSettings, ((IPortalInfo)this.objPortal).PortalId, this.txtUser.Text);
                            if (objUser != null)
                            {
                                r = "UserID=" + objUser.UserID;
                            }
                            else
                            {
                                this.lblMessage.Text = Localization.GetString("NoUser", this.LocalResourceFile);
                                this.ErrorRow.Visible = true;
                                this.txtUser.Text = string.Empty;
                            }
                        }

                        break;
                }

                return r;
            }

            set
            {
                this.viewStateUrl = value;
                this.txtUrl.Text = string.Empty;

                if (this.isInitialized)
                {
                    this.doChangeURL = true;
                }
            }
        }
        #pragma warning restore BL0005

        /// <summary>Gets or sets the URL type.</summary>
        public string UrlType
        {
            get => this.viewStateUrlType ?? string.Empty;

            set
            {
                if (value != null && !string.IsNullOrEmpty(value.Trim()))
                {
                    this.viewStateUrlType = value;
                    if (this.isInitialized)
                    {
                        this.doChangeURL = true;
                    }
                }
            }
        }

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        /// <summary>Gets or sets the control width.</summary>
        public string Width
        {
            get => this.viewStateSkinControlWidth ?? string.Empty;

            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    this.EnsureChildControls();
                    this.cboUrls.Width = value;
                    this.txtUrl.Width = value;
                    this.cboImages.Width = value;
                    this.cboTabs.Width = value;
                    this.txtUser.Width = value;
                    this.viewStateSkinControlWidth = value;
                }
            }
        }
        #pragma warning restore BL0005

        /// <summary>Gets the application status.</summary>
        protected IApplicationStatusInfo AppStatus { get; }

        /// <summary>Gets the host settings.</summary>
        protected IHostSettings HostSettings { get; }

        // Protected API inherited by derived controls (replaces UserControlBase.PortalSettings); making it static would be a breaking change.
        #pragma warning disable CA1822 // Mark members as static
        /// <summary>Gets the current portal settings.</summary>
        /// <remarks>Replaces <c>UserControlBase.PortalSettings</c>.</remarks>
        protected PortalSettings PortalSettings => PortalSettings.Current;
        #pragma warning restore CA1822 // Mark members as static

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        /// <inheritdoc />
        /// <remarks>
        /// Builds the identical tree the <c>DnnUrlControl.ascx</c> declared: a <c>div.urlControl</c>
        /// holding the type row, the url row (dropdown + textbox + Select/Delete/Add commands), the
        /// page row, the file row, the images row, the user row, the error row and the
        /// track/log/new-window checkboxes.
        /// </remarks>
        protected override void CreateChildControls()
        {
            this.Controls.Clear();

            this.urlControlContainer = new DnnPanel { CssClass = "urlControl" };

            this.TypeRow = new DnnPanel { ID = "TypeRow", CssClass = "urlControlLinkType dnnClear" };
            this.lblURLType = new DotNetNuke.UI.DnnLabel { ID = "lblURLType", CssClass = "dnnFormLabel" };
            this.optType = new DnnRadioButtonList { ID = "optType", AutoPostBack = true, RepeatDirection = "Vertical", CssClass = "ucLinkTypeRadioButtons" };
            this.TypeRow.Controls.Add(this.lblURLType);
            this.TypeRow.Controls.Add(this.optType);
            this.urlControlContainer.Controls.Add(this.TypeRow);

            this.URLRow = new DnnPanel { ID = "URLRow", CssClass = "urlControlFile dnnClear" };
            this.lblURL = new DotNetNuke.UI.DnnLabel { ID = "lblURL" };
            this.URLRow.Controls.Add(this.lblURL);
            var urlInner = new DnnPanel();
            this.cboUrls = new DnnListControl { ID = "cboUrls", DataTextField = "Url", DataValueField = "Url" };
            this.txtUrl = new DnnTextBox { ID = "txtUrl" };
            this.cmdSelect = new DnnTextButton { ID = "cmdSelect", Text = "Select", CssClass = "dnnSecondaryAction" };
            this.cmdDelete = new DnnTextButton { ID = "cmdDelete", Text = "Delete", CssClass = "dnnSecondaryAction" };
            this.cmdAdd = new DnnTextButton { ID = "cmdAdd", Text = "Add", CssClass = "dnnSecondaryAction" };
            urlInner.Controls.Add(this.cboUrls);
            urlInner.Controls.Add(this.txtUrl);
            urlInner.Controls.Add(this.cmdSelect);
            urlInner.Controls.Add(this.cmdDelete);
            urlInner.Controls.Add(this.cmdAdd);
            this.URLRow.Controls.Add(urlInner);
            this.urlControlContainer.Controls.Add(this.URLRow);

            this.TabRow = new DnnPanel { ID = "TabRow", CssClass = "urlControlTab dnnClear" };
            var tabInner = new DnnPanel { CssClass = "dnnFormItem" };
            this.lblTab = new DotNetNuke.UI.DnnLabel { ID = "lblTab" };
            var services = Globals.GetCurrentServiceProvider();
            this.cboTabs = new DnnPageDropDownList(
                services.GetRequiredService<DotNetNuke.Abstractions.ClientResources.IClientResourceController>(),
                services.GetRequiredService<IServicesFramework>())
            { ID = "cboTabs" };
            tabInner.Controls.Add(this.lblTab);
            tabInner.Controls.Add(this.cboTabs);
            this.TabRow.Controls.Add(tabInner);
            this.urlControlContainer.Controls.Add(this.TabRow);

            this.FileRow = new DnnPanel { ID = "FileRow", CssClass = "urlControlFileRow dnnClear" };
            this.ctlFile = new DnnFilePickerUploader(
                this.AppStatus,
                services.GetRequiredService<DotNetNuke.Abstractions.Logging.IEventLogger>(),
                services.GetRequiredService<IServicesFramework>())
            { ID = "ctlFile" };
            this.FileRow.Controls.Add(this.ctlFile);
            this.urlControlContainer.Controls.Add(this.FileRow);

            this.ImagesRow = new DnnPanel { ID = "ImagesRow", CssClass = "dnnFormItem urlControlImagesRow dnnClear" };
            this.lblImages = new DotNetNuke.UI.DnnLabel { ID = "lblImages", CssClass = "dnnFormLabel" };
            this.cboImages = new DnnListControl { ID = "cboImages" };
            this.ImagesRow.Controls.Add(this.lblImages);
            this.ImagesRow.Controls.Add(this.cboImages);
            this.urlControlContainer.Controls.Add(this.ImagesRow);

            this.UserRow = new DnnPanel { ID = "UserRow", CssClass = "urlControlUseRow dnnClear" };
            this.lblUser = new DotNetNuke.UI.DnnLabel { ID = "lblUser" };
            this.txtUser = new DnnTextBox { ID = "txtUser" };
            this.UserRow.Controls.Add(this.lblUser);
            this.UserRow.Controls.Add(this.txtUser);
            this.urlControlContainer.Controls.Add(this.UserRow);

            this.ErrorRow = new DnnPanel { ID = "ErrorRow", CssClass = "dnnFormMessage", Visible = false };
            this.lblMessage = new DotNetNuke.UI.DnnLabel { ID = "lblMessage", CssClass = "dnnFormError" };
            this.ErrorRow.Controls.Add(this.lblMessage);
            this.urlControlContainer.Controls.Add(this.ErrorRow);

            var optionsRow = new DnnPanel { CssClass = "dnnClear" };
            this.chkTrack = new DnnCheckBox { ID = "chkTrack", Text = "Track?" };
            this.chkLog = new DnnCheckBox { ID = "chkLog", Text = "Log?" };
            this.chkNewWindow = new DnnCheckBox { ID = "chkNewWindow", Text = "New Window?", Visible = false };
            optionsRow.Controls.Add(this.chkTrack);
            optionsRow.Controls.Add(this.chkLog);
            optionsRow.Controls.Add(this.chkNewWindow);
            this.urlControlContainer.Controls.Add(optionsRow);

            this.Controls.Add(this.urlControlContainer);
        }
        #pragma warning restore BL0005

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            // prevent unauthorized access
            if (this.Request?.HttpContext?.User?.Identity?.IsAuthenticated != true)
            {
                this.Visible = false;
            }

            // `ClientResourceManager.EnableAsyncPostBackHandler()` existed only to re-register
            // resources after an UpdatePanel async postback; Blazor re-renders in place, so there is
            // nothing to re-enable.
            this.EnsureChildControls();
            this.isInitialized = true;
        }

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        /// <inheritdoc />
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            this.optType.SelectedIndexChanged += this.optType_SelectedIndexChanged;
            this.cmdAdd.Click += this.cmdAdd_Click;
            this.cmdDelete.Click += this.cmdDelete_Click;
            this.cmdSelect.Click += this.cmdSelect_Click;

            this.ErrorRow.Visible = false;

            try
            {
                var requestedPortalId = this.Request?.Query["pid"].ToString();
                if (!string.IsNullOrEmpty(requestedPortalId) && (Globals.IsHostTab(this.PortalSettings.ActiveTab.TabID) || UserController.Instance.GetCurrentUserInfo().IsSuperUser))
                {
                    this.objPortal = PortalController.Instance.GetPortal(int.Parse(requestedPortalId, CultureInfo.InvariantCulture));
                }
                else
                {
                    this.objPortal = PortalController.Instance.GetPortal(this.PortalSettings.PortalId);
                }

                if (!this.isUrlControlLoaded)
                {
                    // If Not Page.IsPostBack Then
                    // let's make at least an initialization
                    // The type radio button must be initialized
                    // The url must be initialized no matter its value
                    this.doRenderTypes = true;
                    this.doChangeURL = true;

                    // Replaces ClientAPI.AddButtonConfirm: DnnTextButton confirms through JS interop.
                    this.cmdDelete.ConfirmMessage = Localization.GetString("DeleteItem");

                    // The following line was mover to the pre-render event to ensure render for the first time
                    // ViewState("IsUrlControlLoaded") = "Loaded"
                }
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }
        #pragma warning restore BL0005

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        /// <inheritdoc />
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);

            try
            {
                if (this.doRenderTypes)
                {
                    this.DoRenderTypes();
                }

                if (this.doChangeURL)
                {
                    this.DoChangeURL();
                }

                this.DoCorrectRadioButtonList();

                if (this.doRenderTypeControls)
                {
                    this.DoRenderTypeControls();
                }

                this.viewStateUrl = null;
                this.isUrlControlLoaded = true;

                this.ctlFile.FileFilter = this.FileFilter;
            }
            catch (Exception exc)
            {
                // Let's detect possible problems
                Exceptions.LogException(new RenderException("Error rendering URLControl subcontrols.", exc));
            }
        }
        #pragma warning restore BL0005

        /// <summary>Handles the click event on <see cref="cmdAdd"/>.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event args.</param>
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Breaking Change")]
        [SuppressMessage("Microsoft.Design", "CA1707:IdentifiersShouldNotContainUnderscores", Justification = "Breaking change")]

        // ReSharper disable once InconsistentNaming
        protected void cmdAdd_Click(object sender, EventArgs e)
        {
            this.cboUrls.Visible = false;
            this.cmdSelect.Visible = true;
            this.txtUrl.Visible = true;
            this.cmdAdd.Visible = false;
            this.cmdDelete.Visible = false;
            this.doRenderTypeControls = false; // Must not render on this postback
            this.doRenderTypes = false;
            this.doChangeURL = false;
        }

        /// <summary>Handles the click event on <see cref="cmdDelete"/>.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event args.</param>
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Breaking Change")]
        [SuppressMessage("Microsoft.Design", "CA1707:IdentifiersShouldNotContainUnderscores", Justification = "Breaking change")]

        // ReSharper disable once InconsistentNaming
        protected void cmdDelete_Click(object sender, EventArgs e)
        {
            if (this.cboUrls.SelectedItem != null)
            {
                var objUrls = new UrlController();
                objUrls.DeleteUrl(((IPortalInfo)this.objPortal).PortalId, this.cboUrls.SelectedItem.Value);
                this.LoadUrls(); // we must reload the url list
            }

            this.doRenderTypeControls = false; // Must not render on this postback
            this.doRenderTypes = false;
            this.doChangeURL = false;
        }

        /// <summary>Handles the click event on <see cref="cmdSelect"/>.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event args.</param>
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Breaking Change")]
        [SuppressMessage("Microsoft.Design", "CA1707:IdentifiersShouldNotContainUnderscores", Justification = "Breaking change")]

        // ReSharper disable once InconsistentNaming
        protected void cmdSelect_Click(object sender, EventArgs e)
        {
            this.cboUrls.Visible = true;
            this.cmdSelect.Visible = false;
            this.txtUrl.Visible = false;
            this.cmdAdd.Visible = true;
            this.cmdDelete.Visible = PortalSecurity.IsInRole(this.objPortal.AdministratorRoleName);
            this.LoadUrls();
            if (this.cboUrls.Items.FindByValue(this.txtUrl.Text) != null)
            {
                this.cboUrls.ClearSelection();
                this.cboUrls.Items.FindByValue(this.txtUrl.Text).Selected = true;
            }

            this.doRenderTypeControls = false; // Must not render on this postback
            this.doRenderTypes = false;
            this.doChangeURL = false;
        }

        /// <summary>Handles the selection change event on <see cref="optType"/>.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event args.</param>
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Breaking Change")]
        [SuppressMessage("Microsoft.Design", "CA1707:IdentifiersShouldNotContainUnderscores", Justification = "Breaking change")]

        // ReSharper disable once InconsistentNaming
        protected void optType_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Type changed, render the correct control set
            this.viewStateUrlType = this.optType.SelectedItem.Value;
            this.doRenderTypeControls = true;
        }

        private void LoadUrls()
        {
            var objUrls = new UrlController();
            this.cboUrls.Items.Clear();
            this.cboUrls.DataSource = objUrls.GetUrls(((IPortalInfo)this.objPortal).PortalId);
            this.cboUrls.DataBind();
        }

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        private void DoChangeURL()
        {
            string url = this.viewStateUrl ?? string.Empty;
            string urltype = this.viewStateUrlType ?? string.Empty;
            if (!string.IsNullOrEmpty(url))
            {
                var objUrls = new UrlController();
                string trackingUrl = url;

                urltype = Globals.GetURLType(url).ToString("g").Substring(0, 1);
                if (urltype == "U" && url.StartsWith("~/" + this.PortalSettings.DefaultIconLocation, StringComparison.InvariantCultureIgnoreCase))
                {
                    urltype = "I";
                }

                this.viewStateUrlType = urltype;
                if (urltype == "F")
                {
                    if (url.StartsWith("fileid=", StringComparison.InvariantCultureIgnoreCase))
                    {
                        trackingUrl = url;
                        var objFile = FileManager.Instance.GetFile(int.Parse(url.AsSpan(7), CultureInfo.InvariantCulture));
                        if (objFile != null)
                        {
                            url = objFile.Folder + objFile.FileName;
                        }
                    }
                    else
                    {
                        // to handle legacy scenarios before the introduction of the FileServerHandler
                        var fileName = Path.GetFileName(url);
                        var folderPath = url.Substring(0, url.LastIndexOf(fileName, StringComparison.OrdinalIgnoreCase));
                        var folder = FolderManager.Instance.GetFolder(((IPortalInfo)this.objPortal).PortalId, folderPath);
                        var fileId = -1;
                        if (folder != null)
                        {
                            var file = FileManager.Instance.GetFile(folder, fileName);
                            if (file != null)
                            {
                                fileId = file.FileId;
                            }
                        }

                        trackingUrl = "FileID=" + fileId.ToString(CultureInfo.InvariantCulture);
                    }
                }

                if (urltype == "M")
                {
                    if (url.StartsWith("userid=", StringComparison.InvariantCultureIgnoreCase))
                    {
                        UserInfo objUser = UserController.GetUserById(this.HostSettings, ((IPortalInfo)this.objPortal).PortalId, int.Parse(url.AsSpan(7), CultureInfo.InvariantCulture));
                        if (objUser != null)
                        {
                            url = objUser.Username;
                        }
                    }
                }

                UrlTrackingInfo objUrlTracking = objUrls.GetUrlTracking(((IPortalInfo)this.objPortal).PortalId, trackingUrl, this.ModuleID);
                if (objUrlTracking != null)
                {
                    this.chkNewWindow.Checked = objUrlTracking.NewWindow;
                    this.chkTrack.Checked = objUrlTracking.TrackClicks;
                    this.chkLog.Checked = objUrlTracking.LogActivity;
                }
                else
                {
                    // the url does not exist in the tracking table
                    this.chkTrack.Checked = false;
                    this.chkLog.Checked = false;
                }

                this.viewStateUrl = url;
            }
            else
            {
                if (!string.IsNullOrEmpty(urltype))
                {
                    this.optType.ClearSelection();
                    if (this.optType.Items.FindByValue(urltype) != null)
                    {
                        this.optType.Items.FindByValue(urltype).Selected = true;
                    }
                    else
                    {
                        this.optType.Items[0].Selected = true;
                    }
                }
                else
                {
                    if (this.optType.Items.Count > 0)
                    {
                        this.optType.ClearSelection();
                        this.optType.Items[0].Selected = true;
                    }
                }

                this.chkNewWindow.Checked = false; // Need check
                this.chkTrack.Checked = false; // Need check
                this.chkLog.Checked = false; // Need check
            }

            // Url type changed, then we must draw the controlos for that type
            this.doRenderTypeControls = true;
        }
        #pragma warning restore BL0005

        private void DoRenderTypes()
        {
            // We must clear the list to keep the same item order
            string strCurrent = string.Empty;
            if (this.optType.SelectedIndex >= 0)
            {
                strCurrent = this.optType.SelectedItem.Value; // Save current selected value
            }

            this.optType.Items.Clear();
            if (this.ShowNone)
            {
                if (this.optType.Items.FindByValue("N") == null)
                {
                    this.optType.Items.Add(new DnnListItem(Localization.GetString("NoneType", this.LocalResourceFile), "N"));
                }
            }
            else
            {
                if (this.optType.Items.FindByValue("N") != null)
                {
                    this.optType.Items.Remove(this.optType.Items.FindByValue("N"));
                }
            }

            if (this.ShowUrls)
            {
                if (this.optType.Items.FindByValue("U") == null)
                {
                    this.optType.Items.Add(new DnnListItem(Localization.GetString("URLType", this.LocalResourceFile), "U"));
                }
            }
            else
            {
                if (this.optType.Items.FindByValue("U") != null)
                {
                    this.optType.Items.Remove(this.optType.Items.FindByValue("U"));
                }
            }

            if (this.ShowTabs)
            {
                if (this.optType.Items.FindByValue("T") == null)
                {
                    this.optType.Items.Add(new DnnListItem(Localization.GetString("TabType", this.LocalResourceFile), "T"));
                }
            }
            else
            {
                if (this.optType.Items.FindByValue("T") != null)
                {
                    this.optType.Items.Remove(this.optType.Items.FindByValue("T"));
                }
            }

            if (this.ShowFiles)
            {
                if (this.optType.Items.FindByValue("F") == null)
                {
                    this.optType.Items.Add(new DnnListItem(Localization.GetString("FileType", this.LocalResourceFile), "F"));
                }
            }
            else
            {
                if (this.optType.Items.FindByValue("F") != null)
                {
                    this.optType.Items.Remove(this.optType.Items.FindByValue("F"));
                }
            }

            if (this.ShowImages)
            {
                if (this.optType.Items.FindByValue("I") == null)
                {
                    this.optType.Items.Add(new DnnListItem(Localization.GetString("ImageType", this.LocalResourceFile), "I"));
                }
            }
            else
            {
                if (this.optType.Items.FindByValue("I") != null)
                {
                    this.optType.Items.Remove(this.optType.Items.FindByValue("I"));
                }
            }

            if (this.ShowUsers)
            {
                if (this.optType.Items.FindByValue("M") == null)
                {
                    this.optType.Items.Add(new DnnListItem(Localization.GetString("UserType", this.LocalResourceFile), "M"));
                }
            }
            else
            {
                if (this.optType.Items.FindByValue("M") != null)
                {
                    this.optType.Items.Remove(this.optType.Items.FindByValue("M"));
                }
            }

            if (this.optType.Items.Count > 0)
            {
                if (!string.IsNullOrEmpty(strCurrent))
                {
                    if (this.optType.Items.FindByValue(strCurrent) != null)
                    {
                        this.optType.Items.FindByValue(strCurrent).Selected = true;
                    }
                    else
                    {
                        this.optType.Items[0].Selected = true;
                        this.doRenderTypeControls = true; // Type changed, re-draw
                    }
                }
                else
                {
                    this.optType.Items[0].Selected = true;
                    this.doRenderTypeControls = true; // Type changed, re-draw
                }

                this.TypeRow.Visible = this.optType.Items.Count > 1;
            }
            else
            {
                this.TypeRow.Visible = false;
            }
        }

        private void DoCorrectRadioButtonList()
        {
            string urltype = this.viewStateUrlType ?? string.Empty;

            if (this.optType.Items.Count > 0)
            {
                this.optType.ClearSelection();
                if (!string.IsNullOrEmpty(urltype))
                {
                    if (this.optType.Items.FindByValue(urltype) != null)
                    {
                        this.optType.Items.FindByValue(urltype).Selected = true;
                    }
                    else
                    {
                        this.optType.Items[0].Selected = true;
                        urltype = this.optType.Items[0].Value;
                        this.viewStateUrlType = urltype;
                    }
                }
                else
                {
                    this.optType.Items[0].Selected = true;
                    urltype = this.optType.Items[0].Value;
                    this.viewStateUrlType = urltype;
                }
            }
        }

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        private void DoRenderTypeControls()
        {
            string url = this.viewStateUrl ?? string.Empty;
            string urltype = this.viewStateUrlType ?? string.Empty;
            var objUrls = new UrlController();
            if (!string.IsNullOrEmpty(urltype))
            {
                // load listitems
                switch (this.optType.SelectedItem.Value)
                {
                    case "N": // None
                        this.URLRow.Visible = false;
                        this.TabRow.Visible = false;
                        this.FileRow.Visible = false;
                        this.UserRow.Visible = false;
                        this.ImagesRow.Visible = false;
                        break;
                    case "I": // System Image
                        this.URLRow.Visible = false;
                        this.TabRow.Visible = false;
                        this.FileRow.Visible = false;
                        this.UserRow.Visible = false;
                        this.ImagesRow.Visible = true;

                        this.cboImages.Items.Clear();

                        string strImagesFolder = Path.Combine(this.AppStatus.ApplicationMapPath, this.PortalSettings.DefaultIconLocation.Replace('/', '\\'));
                        foreach (string strImage in Directory.GetFiles(strImagesFolder))
                        {
                            string img = strImage.Replace(strImagesFolder, string.Empty).Trim('/').Trim('\\');
                            this.cboImages.Items.Add(new DnnListItem(img, $"~/{this.PortalSettings.DefaultIconLocation}/{img}".ToLowerInvariant()));
                        }

                        DnnListItem selecteItem = this.cboImages.Items.FindByValue(url.ToLowerInvariant());
                        if (selecteItem != null)
                        {
                            selecteItem.Selected = true;
                        }

                        break;

                    case "U": // Url
                        this.URLRow.Visible = true;
                        this.TabRow.Visible = false;
                        this.FileRow.Visible = false;
                        this.UserRow.Visible = false;
                        this.ImagesRow.Visible = false;
                        if (string.IsNullOrEmpty(this.txtUrl.Text))
                        {
                            this.txtUrl.Text = url;
                        }

                        if (string.IsNullOrEmpty(this.txtUrl.Text))
                        {
                            this.txtUrl.Text = "http://";
                        }

                        this.txtUrl.Visible = true;

                        this.cmdSelect.Visible = true;

                        this.cboUrls.Visible = false;
                        this.cmdAdd.Visible = false;
                        this.cmdDelete.Visible = false;
                        break;
                    case "T": // tab
                        this.URLRow.Visible = false;
                        this.TabRow.Visible = true;
                        this.FileRow.Visible = false;
                        this.UserRow.Visible = false;
                        this.ImagesRow.Visible = false;

                        this.cboTabs.IncludeAllTabTypes = false;
                        this.cboTabs.IncludeActiveTab = this.IncludeActiveTab;
                        this.cboTabs.IncludeDisabledTabs = true;
                        this.cboTabs.DisabledNotSelectable = true;
                        this.cboTabs.UndefinedItem = new DnnListItem(DynamicSharedConstants.Unspecified, string.Empty);

                        if (!string.IsNullOrEmpty(url))
                        {
                            var settings = PortalController.Instance.GetCurrentSettings();
                            var tabId = int.Parse(url, CultureInfo.InvariantCulture);
                            var page = TabController.Instance.GetTab(tabId, settings.PortalId);
                            this.cboTabs.SelectedPage = page;
                        }

                        break;
                    case "F": // file
                        this.URLRow.Visible = false;
                        this.TabRow.Visible = false;
                        this.FileRow.Visible = true;
                        this.UserRow.Visible = false;
                        this.ImagesRow.Visible = false;

                        // select folder
                        // We Must check if selected folder has changed because of a property change (Secure, Database)
                        string fileName = string.Empty;
                        string folderPath = string.Empty;
                        string lastFileName = string.Empty;
                        string lastFolderPath = string.Empty;

                        // Let's try to remember last selection. The "LastFolderPath"/"LastFileName"
                        // view state slots this used to read were never written by any version of
                        // this control, so the remembered values are always empty — the behaviour is
                        // preserved by leaving them empty here.
                        if (url != string.Empty)
                        {
                            // Let's use the new URL
                            fileName = url.Substring(url.LastIndexOf('/') + 1);
                            folderPath = url.Replace(fileName, string.Empty);
                        }
                        else
                        {
                            // Use last settings
                            fileName = lastFileName;
                            folderPath = lastFolderPath;
                        }

                        this.ctlFile.FilePath = folderPath + fileName;

                        this.txtUrl.Visible = false;
                        break;
                    case "M": // membership users
                        this.URLRow.Visible = false;
                        this.TabRow.Visible = false;
                        this.FileRow.Visible = false;
                        this.UserRow.Visible = true;
                        this.ImagesRow.Visible = false;
                        if (string.IsNullOrEmpty(this.txtUser.Text))
                        {
                            this.txtUser.Text = url;
                        }

                        break;
                }
            }
            else
            {
                this.URLRow.Visible = false;
                this.ImagesRow.Visible = false;
                this.TabRow.Visible = false;
                this.FileRow.Visible = false;
                this.UserRow.Visible = false;
            }
        }
        #pragma warning restore BL0005
    }
}
