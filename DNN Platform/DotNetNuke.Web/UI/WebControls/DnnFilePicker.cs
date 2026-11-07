// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Host;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Framework;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Security.Permissions;
    using DotNetNuke.Services.FileSystem;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI;
    using DotNetNuke.Web.Common.Internal;
    using DotNetNuke.Web.UI.WebControls.Extensions;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Forms;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    using FileInfo = DotNetNuke.Services.FileSystem.FileInfo;

    /// <summary>The FilePicker Class provides a File Picker Control for DotNetNuke.</summary>
    /// <remarks>
    /// This was a <c>CompositeControl, ILocalizable</c> that built its whole child tree in
    /// <c>CreateChildControls</c> and round-tripped its state through view state. On ASP.NET Core it
    /// is a <see cref="DnnComponentBase"/>: the tree (folder dropdown, file dropdown, upload input,
    /// the Upload/Save/Cancel commands, the message row and the preview image) is still built in
    /// <see cref="CreateChildControls"/> into <see cref="DnnComponentBase.Controls"/>, the former
    /// view state entries are fields of the component, the auto-postback
    /// <c>SelectedIndexChanged</c>/<c>Click</c> handlers are the same methods wired to the new
    /// events, and the upload reads the browser file through <see cref="InputFile"/> rather than
    /// <c>HtmlInputFile.PostedFile</c>. The permission gating of the commands
    /// (<see cref="FolderPermissionController"/> + the user-folder check) is unchanged.
    /// </remarks>
    public class DnnFilePicker : DnnComponentBase, ILocalizable
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<DnnFilePicker>();
        private readonly IHostSettings hostSettings;
        private readonly IPortalController portalController;
        private readonly IApplicationStatusInfo appStatus;
        private readonly IPortalGroupController portalGroupController;

        private DnnPanel pnlContainer;
        private DnnPanel pnlLeftDiv;
        private DnnPanel pnlFolder;

        private DotNetNuke.UI.DnnLabel lblFolder;
        private DnnListControl cboFolders;
        private DnnPanel pnlFile;
        private DotNetNuke.UI.DnnLabel lblFile;
        private DnnListControl cboFiles;
        private DnnPanel pnlUpload;
        private DnnPanel pnlButtons;
        private DnnTextButton cmdCancel;
        private DnnTextButton cmdSave;
        private DnnTextButton cmdUpload;
        private DnnPanel pnlMessage;
        private DotNetNuke.UI.DnnLabel lblMessage;
        private DnnPanel pnlRightDiv;
        private DnnImage imgPreview;
        private bool localize = true;

        // the state that used to live in view state
        private string commandCssClass;
        private string fileFilter = string.Empty;
        private int? fileId;
        private string filePath;
        private bool usePersonalFolder;
        private string labelCssClass;
        private string permissions;
        private bool required;
        private bool showFolders = true;
        private bool showUpLoad = true;
        private FileControlMode mode = FileControlMode.Normal;

        // the browser file the user picked, read into memory so that the file system API can consume it
        private string postedFileName;
        private byte[] postedFileContent;

        /// <summary>Initializes a new instance of the <see cref="DnnFilePicker"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IHostSettings. Scheduled removal in v12.0.0.")]
        public DnnFilePicker()
            : this(null, null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnFilePicker"/> class.</summary>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="portalController">The portal controller.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="portalGroupController">The portal group controller.</param>
        public DnnFilePicker(IHostSettings hostSettings, IPortalController portalController, IApplicationStatusInfo appStatus, IPortalGroupController portalGroupController)
        {
            this.hostSettings = hostSettings ?? Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettings>();
            this.portalController = portalController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IPortalController>();
            this.appStatus = appStatus ?? Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>();
            this.portalGroupController = portalGroupController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IPortalGroupController>();
        }

        /// <summary>  Represents a possible mode for the File Control.</summary>
        protected enum FileControlMode
        {
            /// <summary>  The File Control is in its Normal mode.</summary>
            Normal = 0,

            /// <summary>  The File Control is in the Upload File mode.</summary>
            UpLoadFile = 1,

            /// <summary>  The File Control is in the Preview mode.</summary>
            Preview = 2,
        }

        /// <summary>Gets or sets the maximum height in pixels.</summary>
        [Parameter]
        public int MaxHeight { get; set; } = 100;

        /// <summary>Gets or sets the maximum width in pixels.</summary>
        [Parameter]
        public int MaxWidth { get; set; } = 135;

        // BL0007: the parameter keeps the property accessors of the original Web Forms control, which forward to the child control or to view state.
        #pragma warning disable BL0007
        /// <summary>Gets or sets the class to be used for the Labels.</summary>
        /// <remarks>Defaults to 'CommandButton'.</remarks>
        /// <value>A String.</value>
        [Parameter]
        public string CommandCssClass
        {
            get => string.IsNullOrEmpty(this.commandCssClass) ? "dnnSecondaryAction" : this.commandCssClass;
            set => this.commandCssClass = value;
        }
        #pragma warning restore BL0007

        // BL0007: the parameter keeps the property accessors of the original Web Forms control, which forward to the child control or to view state.
        #pragma warning disable BL0007
        /// <summary>Gets or sets the file Filter to use.</summary>
        /// <remarks>Defaults to <see cref="string.Empty"/>.</remarks>
        /// <value>a comma separated list of file extensions no wildcards or periods e.g. "jpg,png,gif".</value>
        [Parameter]
        public string FileFilter
        {
            get => this.fileFilter ?? string.Empty;
            set => this.fileFilter = value;
        }
        #pragma warning restore BL0007

        /// <summary>  Gets or sets the FileID for the control.</summary>
        /// <value>An Integer.</value>
        public int FileID
        {
            get
            {
                this.EnsureChildControls();
                if (this.fileId == null)
                {
                    // Get FileId from the file combo
                    var selectedFileId = Null.NullInteger;
                    if (this.cboFiles.SelectedItem != null)
                    {
                        selectedFileId = int.Parse(this.cboFiles.SelectedItem.Value, CultureInfo.InvariantCulture);
                    }

                    this.fileId = selectedFileId;
                }

                return this.fileId.Value;
            }

            set
            {
                this.EnsureChildControls();
                this.fileId = value;
                if (string.IsNullOrEmpty(this.FilePath))
                {
                    var fileInfo = FileManager.Instance.GetFile(value);
                    if (fileInfo != null)
                    {
                        this.SetFilePath(fileInfo.Folder + fileInfo.FileName);
                    }
                }
            }
        }

        /// <summary>Gets or sets the FilePath for the control.</summary>
        /// <value>A String.</value>
        public string FilePath
        {
            get => this.filePath ?? string.Empty;
            set => this.filePath = value;
        }

        // BL0007: the parameter keeps the property accessors of the original Web Forms control, which forward to the child control or to view state.
        #pragma warning disable BL0007
        /// <summary>Gets or sets a value indicating whether to Include Personal Folder.</summary>
        /// <remarks>Defaults to <see langword="false"/>.</remarks>
        /// <value>A Boolean.</value>
        [Parameter]
        public bool UsePersonalFolder
        {
            get => this.usePersonalFolder;
            set => this.usePersonalFolder = value;
        }
        #pragma warning restore BL0007

        // BL0007: the parameter keeps the property accessors of the original Web Forms control, which forward to the child control or to view state.
        #pragma warning disable BL0007
        /// <summary>  Gets or sets the class to be used for the Labels.</summary>
        /// <value>A String.</value>
        [Parameter]
        public string LabelCssClass
        {
            get => this.labelCssClass ?? string.Empty;
            set => this.labelCssClass = value;
        }
        #pragma warning restore BL0007

        // BL0007: the parameter keeps the property accessors of the original Web Forms control, which forward to the child control or to view state.
        #pragma warning disable BL0007
        /// <summary>Gets or sets the permission keys.</summary>
        [Parameter]
        public string Permissions
        {
            get => string.IsNullOrEmpty(this.permissions) ? "BROWSE,ADD" : this.permissions;
            set => this.permissions = value;
        }
        #pragma warning restore BL0007

        // BL0007: the parameter keeps the property accessors of the original Web Forms control, which forward to the child control or to view state.
        #pragma warning disable BL0007
        /// <summary>Gets or sets a value indicating whether the combos have a "Not Specified" option.</summary>
        /// <remarks>Defaults to <see langword="true"/> (i.e. no "Not Specified").</remarks>
        /// <value>A Boolean.</value>
        [Parameter]
        public bool Required
        {
            get => this.required;
            set => this.required = value;
        }
        #pragma warning restore BL0007

        // BL0007: the parameter keeps the property accessors of the original Web Forms control, which forward to the child control or to view state.
        #pragma warning disable BL0007
        /// <summary>Gets or sets a value indicating whether to show folders.</summary>
        [Parameter]
        public bool ShowFolders
        {
            get => this.showFolders;
            set => this.showFolders = value;
        }
        #pragma warning restore BL0007

        // BL0007: the parameter keeps the property accessors of the original Web Forms control, which forward to the child control or to view state.
        #pragma warning disable BL0007
        /// <summary>Gets or sets a value indicating whether to Show the Upload Button.</summary>
        /// <remarks>Defaults to <see langword="true"/>.</remarks>
        /// <value>A Boolean.</value>
        [Parameter]
        public bool ShowUpLoad
        {
            get => this.showUpLoad;
            set => this.showUpLoad = value;
        }
        #pragma warning restore BL0007

        /// <summary>Gets or sets the user.</summary>
        [Parameter]
        public UserInfo User { get; set; }

        // BL0007: the parameter keeps the property accessors of the original Web Forms control, which forward to the child control or to view state.
        #pragma warning disable BL0007
        /// <inheritdoc />
        [Parameter]
        public bool Localize
        {
            get => this.localize;
            set => this.localize = value;
        }
        #pragma warning restore BL0007

        /// <inheritdoc />
        [Parameter]
        public string LocalResourceFile { get; set; }

        /// <summary>Gets a value indicating whether the control is on a Host or Portal Tab.</summary>
        protected bool IsHost
        {
            get
            {
                var isHost = Globals.IsHostTab(this.PortalSettings.ActiveTab.TabID);

                // if not host tab but current edit user is a host user, then return true
                if (!isHost && this.User != null && this.User.IsSuperUser)
                {
                    isHost = true;
                }

                return isHost;
            }
        }

        /// <summary>  Gets the root folder for the control.</summary>
        /// <value>A String.</value>
        protected string ParentFolder => this.IsHost ? Globals.HostMapPath : this.PortalSettings.HomeDirectoryMapPath;

        /// <summary>  Gets the file PortalId to use.</summary>
        /// <remarks>
        ///   Defaults to PortalSettings.PortalId.
        /// </remarks>
        /// <value>An Integer.</value>
        protected int PortalId
        {
            get
            {
                var requestedPortalId = this.Request?.Query["pid"].ToString();
                if (!string.IsNullOrEmpty(requestedPortalId) && (Globals.IsHostTab(this.PortalSettings.ActiveTab.TabID) || UserController.Instance.GetCurrentUserInfo().IsSuperUser))
                {
                    return int.Parse(requestedPortalId, CultureInfo.InvariantCulture);
                }

                if (!this.IsHost)
                {
                    return this.PortalSettings.PortalId;
                }

                return Null.NullInteger;
            }
        }

        /// <summary>Gets the current Portal Settings.</summary>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        protected PortalSettings PortalSettings => PortalSettings.Current;

        /// <summary>Gets or sets the current mode of the control.</summary>
        /// <remarks>Defaults to <see cref="FileControlMode.Normal"/>.</remarks>
        /// <value>A FileControlMode enum.</value>
        protected FileControlMode Mode
        {
            get => this.mode;
            set => this.mode = value;
        }

        /// <inheritdoc />
        public virtual void LocalizeStrings()
        {
        }

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            this.LocalResourceFile = Utilities.GetLocalResourceFile(this);
            this.EnsureChildControls();
        }

        /// <summary>
        ///   CreateChildControls overrides the Base class's method to correctly build the
        ///   control based on the configuration.
        /// </summary>
        protected override void CreateChildControls()
        {
            // First clear the controls collection
            this.Controls.Clear();

            this.pnlContainer = new DnnPanel { CssClass = "dnnFilePicker" };

            this.pnlLeftDiv = new DnnPanel { CssClass = "dnnLeft" };

            this.AddFolderArea();
            this.AddFileAndUploadArea();
            this.AddButtonArea();
            this.AddMessageRow();

            this.pnlContainer.Controls.Add(this.pnlLeftDiv);

            this.pnlRightDiv = new DnnPanel { CssClass = "dnnLeft" };

            this.GeneratePreviewImage();

            this.pnlContainer.Controls.Add(this.pnlRightDiv);

            this.Controls.Add(this.pnlContainer);
        }

        // ASP0006: the render tree of this ported Web Forms control is built imperatively through helper methods that share a running sequence number, so literal sequence numbers cannot express its structure.
        #pragma warning disable ASP0006
        /// <inheritdoc />
        /// <remarks>
        /// The upload input is an <see cref="InputFile"/> rendered inside the upload panel; the rest
        /// of the tree renders itself from <see cref="DnnComponentBase.Controls"/>.
        /// </remarks>
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            this.EnsureChildControls();

            if (!this.Visible)
            {
                return;
            }

            var sequence = 0;
            RenderControl(builder, sequence++, this.pnlContainer);

            if (this.pnlUpload.Visible)
            {
                builder.OpenRegion(sequence++);
                builder.OpenComponent<InputFile>(0);
                builder.AddAttribute(1, "OnChange", EventCallback.Factory.Create<InputFileChangeEventArgs>(this, this.OnFileSelectedAsync));
                builder.AddAttribute(2, "size", "13");
                builder.CloseComponent();
                builder.CloseRegion();
            }
        }
        #pragma warning restore ASP0006

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        /// <summary>OnPreRender runs just before the control is rendered.</summary>
        /// <param name="e">The event arguments.</param>
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);

            if (this.cboFolders.Items.Count > 0)
            {
                // Configure Labels
                this.lblFolder.Text = Utilities.GetLocalizedString("Folder");
                this.lblFolder.CssClass = this.LabelCssClass;
                this.lblFile.Text = Utilities.GetLocalizedString("File");
                this.lblFile.CssClass = this.LabelCssClass;

                // select folder
                string fileName;
                string folderPath;
                if (!string.IsNullOrEmpty(this.FilePath))
                {
                    fileName = this.FilePath.Substring(this.FilePath.LastIndexOf('/') + 1);
                    folderPath = string.IsNullOrEmpty(fileName) ? this.FilePath : this.FilePath.Replace(fileName, string.Empty);
                }
                else
                {
                    fileName = this.FilePath;
                    folderPath = string.Empty;
                }

                if (this.cboFolders.Items.FindByValue(folderPath) != null)
                {
                    this.cboFolders.SelectedIndex = -1;
                    this.cboFolders.Items.FindByValue(folderPath).Selected = true;
                }

                // Get Files
                this.LoadFiles();
                if (this.cboFiles.Items.FindByText(fileName) != null)
                {
                    this.cboFiles.Items.FindByText(fileName).Selected = true;
                }

                if (this.cboFiles.SelectedItem == null || string.IsNullOrEmpty(this.cboFiles.SelectedItem.Value))
                {
                    this.FileID = -1;
                }
                else
                {
                    this.FileID = int.Parse(this.cboFiles.SelectedItem.Value, CultureInfo.InvariantCulture);
                }

                if (this.cboFolders.Items.Count > 1 && this.ShowFolders)
                {
                    this.pnlFolder.Visible = true;
                }
                else
                {
                    this.pnlFolder.Visible = false;
                }

                // Configure Mode
                switch (this.Mode)
                {
                    case FileControlMode.Normal:
                        this.pnlFile.Visible = true;
                        this.pnlUpload.Visible = false;
                        this.pnlRightDiv.Visible = true;
                        this.ShowImage();

                        if ((FolderPermissionController.HasFolderPermission(this.PortalId, this.cboFolders.SelectedItem.Value, "ADD") || this.IsUserFolder(this.cboFolders.SelectedItem.Value)) && this.ShowUpLoad)
                        {
                            this.ShowButton(this.cmdUpload, "Upload");
                        }

                        break;

                    case FileControlMode.UpLoadFile:
                        this.pnlFile.Visible = false;
                        this.pnlUpload.Visible = true;
                        this.pnlRightDiv.Visible = false;
                        this.ShowButton(this.cmdSave, "Save");
                        this.ShowButton(this.cmdCancel, "Cancel");
                        break;
                }
            }
            else
            {
                this.lblMessage.Text = Utilities.GetLocalizedString("NoPermission");
            }

            // Show message Row
            this.pnlMessage.Visible = !string.IsNullOrEmpty(this.lblMessage.Text);
        }
        #pragma warning restore BL0005

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        /// <summary>  AddButton adds a button to the Command Row.</summary>
        /// <param name="button">The button to add to the Row.</param>
        private void AddButton(ref DnnTextButton button)
        {
            button = new DnnTextButton { Localize = false, CssClass = this.CommandCssClass, Visible = false, };

            this.pnlButtons.Controls.Add(button);
        }
        #pragma warning restore BL0005

        /// <summary>  AddCommandRow adds the Command Row.</summary>
        private void AddButtonArea()
        {
            this.pnlButtons = new DnnPanel { Visible = false };

            this.AddButton(ref this.cmdUpload);
            this.cmdUpload.Click += this.UploadFile;

            this.AddButton(ref this.cmdSave);
            this.cmdSave.Click += this.SaveFile;

            this.AddButton(ref this.cmdCancel);
            this.cmdCancel.Click += this.CancelUpload;

            this.pnlLeftDiv.Controls.Add(this.pnlButtons);
        }

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        /// <summary>  AddFileRow adds the Files Row.</summary>
        private void AddFileAndUploadArea()
        {
            // Create Url Div
            this.pnlFile = new DnnPanel { CssClass = "dnnFormItem" };

            // Create File Label
            this.lblFile = new DotNetNuke.UI.DnnLabel();
            this.pnlFile.Controls.Add(this.lblFile);

            // Create Files Combo
            this.cboFiles = new DnnListControl { ID = "File", DataTextField = "Text", DataValueField = "Value", AutoPostBack = true };
            this.cboFiles.SelectedIndexChanged += this.FileChanged;
            this.pnlFile.Controls.Add(this.cboFiles);

            this.pnlLeftDiv.Controls.Add(this.pnlFile);

            // Create Upload Div (the file input itself is rendered by BuildRenderTree)
            this.pnlUpload = new DnnPanel { CssClass = "dnnFormItem" };

            this.pnlLeftDiv.Controls.Add(this.pnlUpload);
        }
        #pragma warning restore BL0005

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        /// <summary>  AddFolderRow adds the Folders Row.</summary>
        private void AddFolderArea()
        {
            // Create Url Div
            this.pnlFolder = new DnnPanel { CssClass = "dnnFormItem" };

            // Create Folder Label
            this.lblFolder = new DotNetNuke.UI.DnnLabel();
            this.pnlFolder.Controls.Add(this.lblFolder);

            // Create Folders Combo
            this.cboFolders = new DnnListControl { ID = "Folder", AutoPostBack = true };
            this.cboFolders.SelectedIndexChanged += this.FolderChanged;
            this.pnlFolder.Controls.Add(this.cboFolders);

            // add to left div
            this.pnlLeftDiv.Controls.Add(this.pnlFolder);

            // Load Folders
            this.LoadFolders();
        }
        #pragma warning restore BL0005

        /// <summary>Adds an empty preview image to the <see cref="pnlRightDiv"/> control.</summary>
        private void GeneratePreviewImage()
        {
            this.imgPreview = new DnnImage();
            this.pnlRightDiv.Controls.Add(this.imgPreview);
        }

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        /// <summary>  AddMessageRow adds the Message Row.</summary>
        private void AddMessageRow()
        {
            this.pnlMessage = new DnnPanel { CssClass = "dnnFormMessage dnnFormWarning" };

            // Create Label
            this.lblMessage = new DotNetNuke.UI.DnnLabel { Text = string.Empty };
            this.pnlMessage.Controls.Add(this.lblMessage);

            this.pnlLeftDiv.Controls.Add(this.pnlMessage);
        }
        #pragma warning restore BL0005

        private bool IsUserFolder(string folderPath)
        {
            UserInfo user = this.User ?? UserController.Instance.GetCurrentUserInfo();
            return folderPath.StartsWith("users/", StringComparison.OrdinalIgnoreCase) && folderPath.EndsWith(string.Format(CultureInfo.InvariantCulture, "/{0}/", user.UserID), StringComparison.OrdinalIgnoreCase);
        }

        private void LoadFiles()
        {
            int effectivePortalId = this.PortalId;
            if (this.IsUserFolder(this.cboFolders.SelectedItem.Value))
            {
                effectivePortalId = PortalController.GetEffectivePortalId(this.portalController, this.appStatus, this.portalGroupController, this.PortalId);
            }

            this.cboFiles.DataSource = Globals.GetFileList(effectivePortalId, this.FileFilter, !this.Required, this.cboFolders.SelectedItem.Value);
            this.cboFiles.DataBind();
        }

        /// <summary>LoadFolders fetches the list of folders from the Database.</summary>
        private void LoadFolders()
        {
            UserInfo user = this.User ?? UserController.Instance.GetCurrentUserInfo();
            this.cboFolders.Items.Clear();

            // Add Personal Folder
            if (this.UsePersonalFolder)
            {
                var userFolder = FolderManager.Instance.GetUserFolder(user).FolderPath;
                var userFolderItem = this.cboFolders.Items.FindByValue(userFolder);
                if (userFolderItem != null)
                {
                    userFolderItem.Text = Utilities.GetLocalizedString("MyFolder");
                }
                else
                {
                    // Add DummyFolder
                    this.cboFolders.Items.Add(new DnnListItem(Utilities.GetLocalizedString("MyFolder"), userFolder));
                }
            }
            else
            {
                var folders = FolderManager.Instance.GetFolders(this.PortalId, "READ,ADD", user.UserID);
                foreach (FolderInfo folder in folders)
                {
                    var folderItem = new DnnListItem
                    {
                        Text =
                                                 folder.FolderPath == Null.NullString
                                                     ? Utilities.GetLocalizedString("PortalRoot")
                                                     : folder.DisplayPath,
                        Value = folder.FolderPath,
                    };
                    this.cboFolders.Items.Add(folderItem);
                }
            }
        }

        /// <summary>  SetFilePath sets the FilePath property.</summary>
        /// <remarks>
        ///   This overload uses the selected item in the Folder combo.
        /// </remarks>
        private void SetFilePath()
        {
            this.SetFilePath(this.cboFiles.SelectedItem.Text);
        }

        /// <summary>  SetFilePath sets the FilePath property.</summary>
        /// <remarks>
        ///   This overload allows the caller to specify a file.
        /// </remarks>
        /// <param name="fileName">The filename to use in setting the property.</param>
        private void SetFilePath(string fileName)
        {
            if (string.IsNullOrEmpty(this.cboFolders.SelectedItem.Value))
            {
                this.FilePath = fileName;
            }
            else
            {
                this.FilePath = (this.cboFolders.SelectedItem.Value + "/") + fileName;
            }
        }

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        /// <summary>  ShowButton configures and displays a button.</summary>
        /// <param name="button">The button to configure.</param>
        /// <param name="command">The command name (amd key) of the button.</param>
        private void ShowButton(DnnTextButton button, string command)
        {
            button.Visible = true;
            if (!string.IsNullOrEmpty(command))
            {
                button.Text = Utilities.GetLocalizedString(command);
            }

            AJAX.RegisterPostBackControl(button);
            this.pnlButtons.Visible = true;
        }
        #pragma warning restore BL0005

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        /// <summary>  ShowImage displays the Preview Image.</summary>
        private void ShowImage()
        {
            var image = (FileInfo)FileManager.Instance.GetFile(this.FileID);

            if (image != null)
            {
                this.imgPreview.ImageUrl = FileManager.Instance.GetUrl(image);
                try
                {
                    Utilities.CreateThumbnail(image, this.imgPreview, this.MaxWidth, this.MaxHeight);
                }
                catch (Exception)
                {
                    Logger.DnnFilePickerUnableToCreateThumbnail(image.PhysicalPath);
                    this.pnlRightDiv.Visible = false;
                }
            }
            else
            {
                this.imgPreview.Visible = false;

                var imageHolderPanel = new DnnPanel { CssClass = "dnnFilePickerImageHolder", };
                this.pnlRightDiv.Controls.Add(imageHolderPanel);
                this.pnlRightDiv.Visible = true;
            }
        }
        #pragma warning restore BL0005

        /// <summary>Reads the file the user picked into memory.</summary>
        /// <param name="args">The file selection arguments.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>Replaces <c>HtmlInputFile.PostedFile</c>, which no longer exists.</remarks>
        private async Task OnFileSelectedAsync(InputFileChangeEventArgs args)
        {
            var file = args?.FileCount > 0 ? args.File : null;
            if (file == null)
            {
                this.postedFileName = null;
                this.postedFileContent = null;
                return;
            }

            this.postedFileName = file.Name;

            using var buffer = new MemoryStream();
            await using (var source = file.OpenReadStream(Config.GetMaxUploadSize(this.appStatus)))
            {
                await source.CopyToAsync(buffer);
            }

            this.postedFileContent = buffer.ToArray();
        }

        private void CancelUpload(object sender, EventArgs e)
        {
            this.Mode = FileControlMode.Normal;
        }

        private void FileChanged(object sender, EventArgs e)
        {
            this.SetFilePath();
        }

        private void FolderChanged(object sender, EventArgs e)
        {
            this.LoadFiles();
            this.SetFilePath();
        }

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        private void SaveFile(object sender, EventArgs e)
        {
            // if file is selected exit
            if (!string.IsNullOrEmpty(this.postedFileName))
            {
                var extension = Path.GetExtension(this.postedFileName).Replace(".", string.Empty);

                if (!string.IsNullOrEmpty(this.FileFilter) && !this.FileFilter.Contains(extension, StringComparison.InvariantCultureIgnoreCase))
                {
                    // trying to upload a file not allowed for current filter
                    var localizedString = Localization.GetString("UploadError", this.LocalResourceFile);
                    if (string.IsNullOrEmpty(localizedString))
                    {
                        localizedString = Utilities.GetLocalizedString("UploadError");
                    }

                    this.lblMessage.Text = string.Format(CultureInfo.CurrentCulture, localizedString, this.FileFilter, extension);
                }
                else
                {
                    var folderManager = FolderManager.Instance;

                    var folderPath = PathUtils.Instance.GetRelativePath(this.PortalId, this.ParentFolder) + this.cboFolders.SelectedItem.Value;

                    // Check if this is a User Folder
                    IFolderInfo folder;
                    if (this.IsUserFolder(this.cboFolders.SelectedItem.Value))
                    {
                        // Make sure the user folder exists
                        folder = folderManager.GetFolder(PortalController.GetEffectivePortalId(this.portalController, this.appStatus, this.portalGroupController, this.PortalId), folderPath);
                        if (folder == null)
                        {
                            // Add User folder
                            var user = this.User ?? UserController.Instance.GetCurrentUserInfo();

                            // fix user's portal id
                            user.PortalID = this.PortalId;
                            folder = ((FolderManager)folderManager).AddUserFolder(user);
                        }
                    }
                    else
                    {
                        folder = folderManager.GetFolder(this.PortalId, folderPath);
                    }

                    var fileName = Path.GetFileName(this.postedFileName);

                    try
                    {
                        using var content = new MemoryStream(this.postedFileContent ?? Array.Empty<byte>());
                        FileManager.Instance.AddFile(folder, fileName, content, true, true, FileContentTypeManager.Instance.GetContentType(Path.GetExtension(fileName)));
                    }
                    catch (PermissionsNotMetException)
                    {
                        this.lblMessage.Text += "<br />" + string.Format(CultureInfo.CurrentCulture, CompositeFormatCache.Get(Localization.GetString("InsufficientFolderPermission")), folder.FolderPath);
                    }
                    catch (NoSpaceAvailableException)
                    {
                        this.lblMessage.Text += "<br />" + string.Format(CultureInfo.CurrentCulture, CompositeFormatCache.Get(Localization.GetString("DiskSpaceExceeded")), fileName);
                    }
                    catch (InvalidFileExtensionException)
                    {
                        this.lblMessage.Text += "<br />" + string.Format(CultureInfo.CurrentCulture, CompositeFormatCache.Get(Localization.GetString("RestrictedFileType")), fileName, this.hostSettings.AllowedExtensionAllowList.ToDisplayString());
                    }
                    catch (Exception ex)
                    {
                        Logger.DnnFilePickerAddFileException(ex);

                        this.lblMessage.Text += "<br />" + string.Format(CultureInfo.CurrentCulture, CompositeFormatCache.Get(Localization.GetString("SaveFileError")), fileName);
                    }
                }

                if (string.IsNullOrEmpty(this.lblMessage.Text))
                {
                    var fileName = this.postedFileName.Substring(this.postedFileName.LastIndexOf('\\') + 1);
                    this.SetFilePath(fileName);
                }
            }

            this.Mode = FileControlMode.Normal;
        }
        #pragma warning restore BL0005

        private void UploadFile(object sender, EventArgs e)
        {
            this.Mode = FileControlMode.UpLoadFile;
        }
    }
}
