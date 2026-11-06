// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.UI.WebControls
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Framework;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.Services.FileSystem;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI;
    using DotNetNuke.UI.UserControls;
    using DotNetNuke.Web.Common;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.JSInterop;

    /// <summary>A file picker uploader control.</summary>
    /// <remarks>
    /// This was a <c>UserControl</c> whose markup lived in <c>controls/filepickeruploader.ascx</c>
    /// and whose client side was bootstrapped by an inline <c>$(scope).dnnFileUpload(settings)</c>
    /// script. On ASP.NET Core the control owns its markup (<see cref="BuildRenderTree"/> emits the
    /// identical <c>dnnFileUploadScope</c> tree: folder picker + file picker + the file input,
    /// upload button, drop zone and progress bar) and the settings object is handed to
    /// <c>dnn.createFilePickerUploader</c> through JS interop. The
    /// <see cref="IFilePickerUploader"/> contract and the <c>FilePath</c>/<c>FileID</c> round-trip
    /// are unchanged.
    /// </remarks>
    public class DnnFilePickerUploader : DnnComponentBase, IFilePickerUploader
    {
        // ReSharper disable InconsistentNaming

        /// <summary>Gets or sets the files combo box.</summary>
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1306:FieldNamesMustBeginWithLowerCaseLetter", Justification = "Breaking Change")]
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DnnFileDropDownList FilesComboBox;

        /// <summary>Gets or sets the folders combo box.</summary>
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1306:FieldNamesMustBeginWithLowerCaseLetter", Justification = "Breaking Change")]
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DnnFolderDropDownList FoldersComboBox;

        /// <summary>Gets or sets the folders label.</summary>
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1306:FieldNamesMustBeginWithLowerCaseLetter", Justification = "Breaking Change")]
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DotNetNuke.UI.DnnLabel FoldersLabel;

        /// <summary>Gets or sets the file upload control.</summary>
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1306:FieldNamesMustBeginWithLowerCaseLetter", Justification = "Breaking Change")]
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DnnFileUpload FileUploadControl;

        // ReSharper restore InconsistentNaming
        private const string MyFileName = "filepickeruploader.ascx";
        private readonly IApplicationStatusInfo appStatus;
        private readonly IEventLogger eventLogger;
        private readonly IServicesFramework servicesFramework;
        private int? portalId;
        private string fileFilter;
        private string folderPath = string.Empty;
        private bool folderPathSet;
        private bool clientInitialized;

        /// <summary>Initializes a new instance of the <see cref="DnnFilePickerUploader"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IApplicationStatusInfo. Scheduled removal in v12.0.0.")]
        public DnnFilePickerUploader()
            : this(null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnFilePickerUploader"/> class.</summary>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="servicesFramework">The web API service framework.</param>
        public DnnFilePickerUploader(IApplicationStatusInfo appStatus, IEventLogger eventLogger, IServicesFramework servicesFramework)
        {
            this.appStatus = appStatus ?? Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>();
            this.eventLogger = eventLogger ?? Globals.GetCurrentServiceProvider().GetRequiredService<IEventLogger>();
            this.servicesFramework = servicesFramework ?? Globals.GetCurrentServiceProvider().GetRequiredService<IServicesFramework>();
        }

        /// <summary>Occurs when the selected file changed.</summary>
        /// <remarks>Replaces the postback the file combo box raised; hosts such as
        /// <c>DnnFileEditControl</c> listen to it to commit the new value.</remarks>
        public event EventHandler FileChanged;

        /// <summary>Gets or sets a value indicating whether to use the user's personal folder.</summary>
        [Parameter]
        public bool UsePersonalFolder { get; set; }

        // BL0007: the parameter keeps the property accessors of the original Web Forms control, which forward to the child control or to view state.
        #pragma warning disable BL0007
        /// <inheritdoc />
        [Parameter]
        public string FilePath
        {
            get
            {
                this.EnsureChildControls();

                var path = string.Empty;
                if (this.FoldersComboBox.SelectedFolder != null && this.FilesComboBox.SelectedFile != null)
                {
                    path = this.FilesComboBox.SelectedFile.RelativePath;
                }

                return path;
            }

            set
            {
                this.EnsureChildControls();
                if (!string.IsNullOrEmpty(value))
                {
                    var file = FileManager.Instance.GetFile(this.PortalId, value);
                    if (file != null)
                    {
                        this.FoldersComboBox.SelectedFolder = FolderManager.Instance.GetFolder(file.FolderId);
                        this.FilesComboBox.SelectedFile = file;
                    }
                }
                else
                {
                    this.FoldersComboBox.SelectedFolder = null;
                    this.FilesComboBox.SelectedFile = null;

                    this.LoadFolders();
                }
            }
        }
        #pragma warning restore BL0007

        // BL0007: the parameter keeps the property accessors of the original Web Forms control, which forward to the child control or to view state.
        #pragma warning disable BL0007
        /// <inheritdoc />
        [Parameter]
        public int FileID
        {
            get
            {
                this.EnsureChildControls();

                return this.FilesComboBox.SelectedFile?.FileId ?? Null.NullInteger;
            }

            set
            {
                this.EnsureChildControls();
                var file = FileManager.Instance.GetFile(value);
                if (file != null)
                {
                    this.FoldersComboBox.SelectedFolder = FolderManager.Instance.GetFolder(file.FolderId);
                    this.FilesComboBox.SelectedFile = file;
                }
            }
        }
        #pragma warning restore BL0007

        /// <summary>Gets or sets the folder path.</summary>
        public string FolderPath
        {
            get
            {
                this.EnsureChildControls();

                return this.folderPathSet
                            ? this.folderPath
                            : this.FoldersComboBox.SelectedFolder != null
                                ? this.FoldersComboBox.SelectedFolder.FolderPath
                                : string.Empty;
            }

            set
            {
                this.folderPath = value;
                this.folderPathSet = true;
            }
        }

        // BL0007: the parameter keeps the property accessors of the original Web Forms control, which forward to the child control or to view state.
        #pragma warning disable BL0007
        /// <inheritdoc />
        [Parameter]
        public string FileFilter
        {
            get
            {
                return this.fileFilter;
            }

            set
            {
                this.EnsureChildControls();
                this.fileFilter = value;
                if (!string.IsNullOrEmpty(value))
                {
                    this.FileUploadControl.Options.Extensions = value.Split(',').ToList();
                }
                else
                {
                    this.FileUploadControl.Options.Extensions.RemoveAll(t => true);
                }
            }
        }
        #pragma warning restore BL0007

        /// <summary>Gets or sets a callback raised in addition to the <see cref="FileChanged"/> event.</summary>
        [Parameter]
        public EventCallback<EventArgs> FileChangedCallback { get; set; }

        /// <summary>Gets or sets a value indicating whether the file is required.</summary>
        [Parameter]
        public bool Required { get; set; }

        /// <summary>Gets or sets the user.</summary>
        [Parameter]
        public UserInfo User { get; set; }

        // BL0007: the parameter keeps the property accessors of the original Web Forms control, which forward to the child control or to view state.
        #pragma warning disable BL0007
        /// <summary>Gets or sets the portal ID.</summary>
        [Parameter]
        public int PortalId
        {
            get => this.portalId ?? PortalSettings.Current.PortalId;
            set => this.portalId = value;
        }
        #pragma warning restore BL0007

        /// <summary>Gets or sets a value indicating whether the control support the host file system.</summary>
        public bool SupportHost
        {
            get
            {
                this.EnsureChildControls();
                return this.FileUploadControl.SupportHost;
            }

            set
            {
                this.EnsureChildControls();
                this.FileUploadControl.SupportHost = value;
            }
        }

        /// <summary>Gets the folder label.</summary>
        protected string FolderLabel => Localization.GetString("Folder", Localization.GetResourceFile(this, MyFileName));

        /// <summary>Gets the file label.</summary>
        protected string FileLabel => Localization.GetString("File", Localization.GetResourceFile(this, MyFileName));

        /// <summary>Gets upload file label.</summary>
        protected string UploadFileLabel => Localization.GetString("UploadFile", Localization.GetResourceFile(this, MyFileName));

        /// <summary>Gets the drop file label.</summary>
        protected string DropFileLabel => Localization.GetString("DropFile", Localization.GetResourceFile(this, MyFileName));

        /// <summary>Gets or sets the JavaScript runtime used to create the client component.</summary>
        [Inject]
        protected IJSRuntime JsRuntime { get; set; }

        private string ScopeClientId => this.ClientId + "_dnnFileUploadScope";

        private string DropZoneClientId => this.ClientId + "_dnnFileUploadDropZone";

        private string ProgressBarClientId => this.ClientId + "_dnnFileUploadProgressBar";

        /// <inheritdoc />
        protected override void CreateChildControls()
        {
            this.Controls.Clear();

            var services = Globals.GetCurrentServiceProvider();
            var clientResources = services.GetRequiredService<DotNetNuke.Abstractions.ClientResources.IClientResourceController>();

            this.FoldersComboBox = new DnnFolderDropDownList(clientResources, this.servicesFramework) { ID = "FoldersComboBox", };
            this.FoldersLabel = new DotNetNuke.UI.DnnLabel { ID = "FoldersLabel", CssClass = "dnnFoldersLabel", };
            this.FilesComboBox = new DnnFileDropDownList(clientResources, this.servicesFramework) { ID = "FilesComboBox", };
            this.FileUploadControl = new DnnFileUpload(
                clientResources,
                this.appStatus,
                this.eventLogger,
                services.GetRequiredService<DotNetNuke.Abstractions.Application.IHostSettings>(),
                services.GetRequiredService<DotNetNuke.Abstractions.Security.ICryptographyProvider>(),
                this.servicesFramework)
            { ID = "FileUploadControl", };

            this.Controls.Add(this.FoldersComboBox);
            this.Controls.Add(this.FoldersLabel);
            this.Controls.Add(this.FilesComboBox);
            this.Controls.Add(this.FileUploadControl);
        }

        /// <inheritdoc />
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            this.EnsureChildControls();

            this.FoldersComboBox.SelectItemDefaultText = (this.SupportHost && PortalSettings.Current.ActiveTab.IsSuperTab) ? DynamicSharedConstants.HostRootFolder : DynamicSharedConstants.RootFolder;
            this.FoldersComboBox.OnClientSelectionChanged.Add("dnn.dnnFileUpload.Folders_Changed");
            this.FoldersComboBox.Options.Services.Parameters.Add("permission", "READ,ADD");

            this.FilesComboBox.OnClientSelectionChanged.Add("dnn.dnnFileUpload.Files_Changed");
            this.FilesComboBox.SelectionChanged += this.OnFilesComboBoxSelectionChanged;
            this.FilesComboBox.SelectItemDefaultText = DynamicSharedConstants.Unspecified;
            this.FilesComboBox.IncludeNoneSpecificItem = true;
            this.FilesComboBox.Filter = this.FileFilter;

            if (UrlUtils.InPopUp())
            {
                this.FileUploadControl.Width = 630;
                this.FileUploadControl.Height = 400;
            }

            this.LoadFolders();
            JavaScript.RequestRegistration(this.appStatus, this.eventLogger, PortalSettings.Current, CommonJs.jQueryFileUpload);
            JavaScript.RequestRegistration(this.appStatus, this.eventLogger, PortalSettings.Current, CommonJs.DnnPlugins);
            this.servicesFramework.RequestAjaxAntiForgerySupport();
        }

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        /// <inheritdoc />
        protected override void OnPreRender(EventArgs e)
        {
            if (this.FoldersComboBox.SelectedFolder != null && this.FoldersComboBox.SelectedFolder.FolderPath.StartsWith("Users/", StringComparison.InvariantCultureIgnoreCase))
            {
                var userFolder = FolderManager.Instance.GetUserFolder(this.User ?? UserController.Instance.GetCurrentUserInfo());
                if (this.FoldersComboBox.SelectedFolder.FolderID == userFolder.FolderID)
                {
                    this.FoldersComboBox.SelectedItem = new DnnListItem
                    {
                        Text = FolderManager.Instance.MyFolderName,
                        Value = userFolder.FolderID.ToString(CultureInfo.InvariantCulture),
                    };
                }
                else if (this.UsePersonalFolder)
                {
                    // if UserPersonalFolder is true, make sure the file is under the user folder.
                    this.FoldersComboBox.SelectedItem = new DnnListItem
                    {
                        Text = FolderManager.Instance.MyFolderName,
                        Value = userFolder.FolderID.ToString(CultureInfo.InvariantCulture),
                    };

                    this.FilesComboBox.SelectedFile = null;
                }
            }

            this.FoldersLabel.Text = FolderManager.Instance.MyFolderName;

            this.FileUploadControl.Options.FolderPicker.Disabled = this.UsePersonalFolder;
            if (this.FileUploadControl.Options.FolderPicker.Disabled && this.FoldersComboBox.SelectedFolder != null)
            {
                var selectedItem = new SerializableKeyValuePair<string, string>(
                    this.FoldersComboBox.SelectedItem.Value, this.FoldersComboBox.SelectedItem.Text);

                this.FileUploadControl.Options.FolderPicker.InitialState = new DnnDropDownListState
                {
                    SelectedItem = selectedItem,
                };
                this.FileUploadControl.Options.FolderPath = this.FoldersComboBox.SelectedFolder.FolderPath;
            }

            base.OnPreRender(e);
        }
        #pragma warning restore BL0005

        // ASP0006: the render tree of this ported Web Forms control is built imperatively through helper methods that share a running sequence number, so literal sequence numbers cannot express its structure.
        #pragma warning disable ASP0006
        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            this.EnsureChildControls();

            if (!this.Visible)
            {
                return;
            }

            var sequence = 0;

            builder.OpenElement(sequence++, "div");
            builder.AddAttribute(sequence++, "id", this.ScopeClientId);
            builder.AddAttribute(sequence++, "class", "dnnFileUploadScope");

            builder.OpenElement(sequence++, "div");
            builder.AddAttribute(sequence++, "class", "dnnLeft");

            // folder picker row
            builder.OpenElement(sequence++, "div");
            builder.AddAttribute(sequence++, "class", "dnnFormItem dnnFileUploadFolder");
            builder.OpenElement(sequence++, "span");
            builder.AddContent(sequence++, this.FolderLabel);
            builder.CloseElement();
            RenderControl(builder, sequence++, this.FoldersComboBox);
            RenderControl(builder, sequence++, this.FoldersLabel);
            builder.CloseElement();

            // file picker row
            builder.OpenElement(sequence++, "div");
            builder.AddAttribute(sequence++, "class", "dnnFormItem");
            builder.OpenElement(sequence++, "span");
            builder.AddContent(sequence++, this.FileLabel);
            builder.CloseElement();
            RenderControl(builder, sequence++, this.FilesComboBox);
            builder.CloseElement();

            // upload row
            builder.OpenElement(sequence++, "div");
            builder.AddAttribute(sequence++, "class", "dnnFormItem");

            builder.OpenElement(sequence++, "input");
            builder.AddAttribute(sequence++, "type", "file");
            builder.AddAttribute(sequence++, "name", "postfile");
            builder.AddAttribute(sequence++, "multiple", true);
            builder.AddAttribute(sequence++, "style", "display: none");
            builder.AddAttribute(sequence++, "class", "normalFileUpload");
            builder.AddAttribute(sequence++, "aria-label", "Upload");
            builder.CloseElement();

            builder.OpenElement(sequence++, "input");
            builder.AddAttribute(sequence++, "type", "button");
            builder.AddAttribute(sequence++, "name", "uploadFileButton");
            builder.AddAttribute(sequence++, "value", this.UploadFileLabel);
            builder.CloseElement();

            RenderControl(builder, sequence++, this.FileUploadControl);
            builder.CloseElement();

            builder.CloseElement(); // div.dnnLeft

            // drop zone
            builder.OpenElement(sequence++, "div");
            builder.AddAttribute(sequence++, "class", "dnnLeft");
            builder.OpenElement(sequence++, "div");
            builder.AddAttribute(sequence++, "id", this.DropZoneClientId);
            builder.AddAttribute(sequence++, "class", "dnnFileUploadDropZone");
            builder.OpenElement(sequence++, "span");
            builder.AddContent(sequence++, this.DropFileLabel);
            builder.CloseElement();
            builder.CloseElement();
            builder.CloseElement();

            builder.OpenElement(sequence++, "div");
            builder.AddAttribute(sequence++, "class", "dnnClear");
            builder.CloseElement();

            // progress bar
            builder.OpenElement(sequence++, "div");
            builder.AddAttribute(sequence++, "class", "dnnFormItem");
            builder.OpenElement(sequence++, "div");
            builder.AddAttribute(sequence++, "id", this.ProgressBarClientId);
            builder.AddAttribute(sequence++, "class", "ui-progressbar");
            builder.OpenElement(sequence++, "div");
            builder.AddAttribute(sequence++, "class", "ui-progressbar-value");
            builder.CloseElement();
            builder.CloseElement();
            builder.CloseElement();

            builder.CloseElement(); // div.dnnFileUploadScope
        }
        #pragma warning restore ASP0006

        /// <inheritdoc />
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            if (this.clientInitialized || this.JsRuntime == null)
            {
                return;
            }

            this.clientInitialized = true;

            // Replaces the inline `$(scope).dnnFileUpload(settings)` script of the .ascx; the
            // settings object carries exactly the same members.
            var settings = new
            {
                fileFilter = this.FileFilter ?? string.Empty,
                required = this.Required,
                foldersComboId = this.FoldersComboBox.ClientId,
                filesComboId = this.FilesComboBox.ClientId,
                fileUploadId = this.FileUploadControl.ClientId,
                folder = this.FolderPath,
                selectedFolderId = this.FoldersComboBox.SelectedFolder != null ? this.FoldersComboBox.SelectedFolder.FolderID : -1,
                progressBarId = this.ProgressBarClientId,
                dropZoneId = this.DropZoneClientId,
            };

            await this.JsRuntime.InvokeVoidAsync("dnn.createFilePickerUploader", "#" + this.ScopeClientId, settings);
        }

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        private void LoadFolders()
        {
            if (this.UsePersonalFolder)
            {
                var user = this.User ?? UserController.Instance.GetCurrentUserInfo();
                var userFolder = FolderManager.Instance.GetUserFolder(user);
                this.FoldersComboBox.SelectedFolder = userFolder;
            }
            else
            {
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
                    folderPath = this.FolderPath;
                }

                this.FoldersComboBox.SelectedFolder = FolderManager.Instance.GetFolder(this.PortalId, folderPath);

                if (!string.IsNullOrEmpty(fileName))
                {
                    this.FilesComboBox.SelectedFile = FileManager.Instance.GetFile(this.FoldersComboBox.SelectedFolder, fileName);
                }
            }

            this.FoldersComboBox.Enabled = !this.UsePersonalFolder;
            this.FoldersLabel.Visible = this.UsePersonalFolder;
        }
        #pragma warning restore BL0005

        private void OnFilesComboBoxSelectionChanged(object sender, EventArgs e)
        {
            this.FileChanged?.Invoke(this, e);
            if (this.FileChangedCallback.HasDelegate)
            {
                this.FileChangedCallback.InvokeAsync(e);
            }
        }
    }
}
