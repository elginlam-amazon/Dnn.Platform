// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DNNConnect.CKEditorProvider.Browser;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using DNNConnect.CKEditorProvider.Helper;
using DNNConnect.CKEditorProvider.Objects;
using DNNConnect.CKEditorProvider.Utilities;

using DotNetNuke.Common.Utilities;
using DotNetNuke.Entities.Tabs;
using DotNetNuke.Services.FileSystem;

using Microsoft.AspNetCore.Http;
using Microsoft.JSInterop;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using Convert = System.Convert;
using Globals = DotNetNuke.Common.Globals;
using Image = SixLabors.ImageSharp.Image;

/// <summary>
/// The page-link mode, the CKEditor callback protocol, the upload and the image editing half of the
/// file browser.
/// </summary>
/// <remarks>
/// <para>
/// This is the second partial of <c>Browser.razor</c>; the first one (<c>Browser.razor.cs</c>)
/// carries the page's state, its load and the browse/list half, and declares the partial methods
/// implemented here. Together they are the port of <c>Browser.aspx.cs</c>.
/// </para>
/// <para>The WebForms concepts of this half map as follows:</para>
/// <list type="bullet">
/// <item><description>Everything the page wrote through
/// <c>Response.Write("&lt;script&gt;...")</c>/<c>Response.End()</c> or
/// <c>Page.ClientScript.RegisterStartupScript</c> is a helper of
/// <c>Browser/js/browserInterop.js</c>, queued in <see cref="pendingInteropCalls"/> and invoked from
/// <c>OnAfterRenderAsync</c>. That includes the <c>CKEditorFuncNum</c> callback protocol itself:
/// the statement <c>GetJavaScriptCode</c>/<c>GetJsUploadCode</c> composed is now
/// <c>callCKEditorFunction</c>/<c>callCKEditorUploadFunction</c>, which receive the very same three
/// arguments (the function number, the file URL and the - always empty - error message).</description></item>
/// <item><description>The two client callbacks that posted back (<c>__doPostBack('cmdCropNow')</c>
/// after a crop and <c>__doPostBack('cmdUploadNow')</c> after an upload) are the
/// <c>[JSInvokable]</c> <see cref="CropNowAsync"/>/<see cref="UploadNowAsync"/>, reached through
/// the <see cref="dotNetRef"/> the interop set-up hands to the client.</description></item>
/// <item><description>The <c>System.Drawing</c> imaging pipeline (<c>Image.FromStream</c>,
/// <c>GetThumbnailImage</c>, <c>Graphics.DrawImage</c>, <c>ImageCodecInfo</c>/<c>Encoder.Quality</c>)
/// is ported onto <c>SixLabors.ImageSharp</c>, preserving the computed dimensions, the bicubic
/// interpolation, the JPEG quality (80 on upload, the <c>dDlQuality</c> selection on resize) and -
/// for every other format - the format of the source image.</description></item>
/// <item><description><c>HttpContext.Current.Request.QueryString</c> is <c>GetQueryValue</c> and
/// <c>Globals.GetDomainName(Request)</c> is the domain name captured during the pre-render
/// (<c>PersistedRequestState.DomainName</c>), because the circuit has no request.</description></item>
/// </list>
/// </remarks>
public partial class Browser
{
    /// <summary>The URL of the endpoint which replaces the <c>SetFile</c> page method.</summary>
    private const string SetFileUrl = SetFileEndpoint.SetFilePath;

    /// <summary>The URL of the endpoint which replaces <c>ProcessImage.ashx</c>.</summary>
    private const string ProcessImageUrl = ProcessImageEndpoint.ProcessImagePath;

    /// <summary>The client used by <see cref="FindAnchorsOnTabAsync"/>, which replaces <c>System.Net.WebClient</c>.</summary>
    private static readonly HttpClient AnchorHttpClient = new HttpClient();

    /// <summary>The start-up scripts queued during this render, flushed by <c>OnAfterRenderAsync</c>.</summary>
    /// <remarks>Replaces <c>Page.ClientScript.RegisterStartupScript</c> and the <c>Response.Write("&lt;script&gt;")</c> blocks.</remarks>
    private readonly List<PendingInteropCall> pendingInteropCalls = new List<PendingInteropCall>();

    /// <summary>The reference the client calls back on, replacing the two <c>__doPostBack</c> targets.</summary>
    private DotNetObjectReference<Browser> dotNetRef;

    // ---- the image editor's controls, as component state ------------------------------------

    /// <summary>Replaces <c>txtWidth.Text</c>; <c>jquery.ImageSlider.js</c> also writes it in the DOM.</summary>
    private string txtWidthText = string.Empty;

    /// <summary>Replaces <c>txtHeight.Text</c>; <c>jquery.ImageSlider.js</c> also writes it in the DOM.</summary>
    private string txtHeightText = string.Empty;

    /// <summary>Replaces <c>chkAspect.Checked</c>.</summary>
    private bool chkAspectChecked = true;

    /// <summary>Replaces <c>txtThumbName.Text</c>.</summary>
    private string txtThumbNameText = string.Empty;

    /// <summary>Replaces <c>txtCropImageName.Text</c>.</summary>
    private string txtCropImageNameText = string.Empty;

    /// <summary>Replaces <c>tbFolderName.Text</c>.</summary>
    private string tbFolderNameText = string.Empty;

    /// <summary>Replaces the items of the <c>dDlQuality</c> drop down.</summary>
    private List<BrowserListItem> qualityItems = new List<BrowserListItem>();

    /// <summary>Replaces <c>dDlQuality.SelectedValue</c>.</summary>
    private string qualitySelectedValue = "100";

    /// <summary>Replaces <c>imgOriginal.ImageUrl</c>.</summary>
    private string imgOriginalUrl;

    /// <summary>Replaces <c>imgOriginal.Visible</c>.</summary>
    private bool imgOriginalVisible;

    /// <summary>Replaces <c>imgOriginal.Width</c>.</summary>
    private int imgOriginalWidth;

    /// <summary>Replaces <c>imgOriginal.Height</c>.</summary>
    private int imgOriginalHeight;

    /// <summary>Replaces <c>imgOriginal.ToolTip</c>/<c>AlternateText</c>.</summary>
    private string imgOriginalToolTip;

    /// <summary>Replaces <c>imgResized.ImageUrl</c>.</summary>
    private string imgResizedUrl;

    /// <summary>Replaces <c>imgResized.Width</c>.</summary>
    private int imgResizedWidth;

    /// <summary>Replaces <c>imgResized.Height</c>.</summary>
    private int imgResizedHeight;

    /// <summary>Replaces <c>imgResized.ToolTip</c>/<c>AlternateText</c>.</summary>
    private string imgResizedToolTip;

    /// <summary>Replaces <c>lblCropInfo.Visible</c>.</summary>
    private bool lblCropInfoVisible;

    /// <summary>Replaces <c>cmdRotate.Visible</c>.</summary>
    private bool cmdRotateVisible;

    /// <summary>Replaces <c>cmdCrop.Visible</c>.</summary>
    private bool cmdCropVisible;

    /// <summary>Replaces <c>cmdZoom.Visible</c>.</summary>
    private bool cmdZoomVisible;

    /// <summary>Replaces <c>cmdResize2.Visible</c>.</summary>
    private bool cmdResize2Visible;

    /// <summary>Gets the zero based index of the selected anchor, replacing <c>AnchorList.SelectedIndex</c>.</summary>
    private int AnchorSelectedIndex =>
        this.anchorListItems.FindIndex(
            item => string.Equals(item.Value, this.anchorListSelectedValue, StringComparison.Ordinal));

    /// <summary>Gets the zero based index of the selected language, replacing <c>LanguageList.SelectedIndex</c>.</summary>
    private int LanguageSelectedIndex =>
        this.languageListItems.FindIndex(
            item => string.Equals(item.Value, this.languageListSelectedValue, StringComparison.Ordinal));

    /// <summary>Gets the domain name the page-link URLs are built from.</summary>
    /// <remarks>Replaces <c>$"http://{Globals.GetDomainName(this.Request, true)}"</c>; the domain name is captured while the page pre-renders.</remarks>
    private string DomainNameUrl => $"http://{this.persistedState.DomainName}";

    /// <summary>Reports the upload jQuery-File-Upload finished, re-reading the current folder.</summary>
    /// <returns>A task.</returns>
    /// <remarks>Replaces the <c>done: function () { __doPostBack('cmdUploadNow', ''); }</c> callback (<c>UploadNow_Click</c>).</remarks>
    [JSInvokable]
    public Task UploadNowAsync()
    {
        this.UploadNow_Click();
        this.StateHasChanged();
        return Task.CompletedTask;
    }

    /// <summary>Reports <c>ProcessImage</c> saved the cropped image.</summary>
    /// <returns>A task.</returns>
    /// <remarks>Replaces the <c>function() { javascript: __doPostBack('cmdCropNow', ''); }</c> callback (<c>CropNow_Click</c>).</remarks>
    [JSInvokable]
    public Task CropNowAsync()
    {
        this.CropNow_Click();
        this.StateHasChanged();
        return Task.CompletedTask;
    }

    /// <summary>Runs the client side calls queued while no circuit was connected.</summary>
    /// <param name="module">The loaded Browser/js/browserInterop.js module.</param>
    /// <returns>A task.</returns>
    /// <remarks>
    /// Replaces the <c>ClientScript.RegisterStartupScript</c> calls the WebForms page made from
    /// its command handlers, which ran on the next post back's response.
    /// </remarks>
    private partial async Task FlushPendingInteropCallsAsync(IJSObjectReference module)
    {
        if (this.pendingInteropCalls.Count == 0)
        {
            return;
        }

        var calls = this.pendingInteropCalls.ToArray();
        this.pendingInteropCalls.Clear();

        foreach (var call in calls)
        {
            await module.InvokeVoidAsync(call.Identifier, call.Arguments);
        }
    }

    /// <summary>Releases what the page-link/upload/image-editing half holds.</summary>
    /// <remarks>Called from the component's <c>Dispose</c> in Browser.razor.cs.</remarks>
    partial void DisposeEditing()
    {
        this.dotNetRef?.Dispose();
        this.dotNetRef = null;
    }

    /// <summary>Queues a start-up script of <c>browserInterop.js</c>.</summary>
    /// <param name="identifier">The exported function to call.</param>
    /// <param name="arguments">Its arguments.</param>
    private void QueueInterop(string identifier, params object[] arguments)
    {
        this.pendingInteropCalls.Add(new PendingInteropCall(identifier, arguments));
    }

    /// <summary>Gets the reference the client calls <see cref="UploadNowAsync"/>/<see cref="CropNowAsync"/> on.</summary>
    /// <returns>The reference.</returns>
    private DotNetObjectReference<Browser> GetDotNetRef()
    {
        return this.dotNetRef ??= DotNetObjectReference.Create(this);
    }

    /// <summary>Show Create New Folder Panel.</summary>
    partial void Create_Click()
    {
        this.panCreateVisible = true;

        if (this.panUploadDivVisible)
        {
            this.panUploadDivVisible = false;
        }

        if (this.panThumbVisible)
        {
            this.panThumbVisible = false;
        }
    }

    /// <summary>Hide Create New Folder Panel.</summary>
    private void CreateCancel_Click()
    {
        this.panCreateVisible = false;
    }

    /// <summary>Create a New Sub Folder.</summary>
    private void CreateFolder_Click()
    {
        if (!string.IsNullOrEmpty(this.tbFolderNameText))
        {
            if (Utility.ValidatePath(this.tbFolderNameText))
            {
                this.tbFolderNameText = Utility.CleanPath(this.tbFolderNameText);
            }

            this.tbFolderNameText = Utility.CleanPath(this.tbFolderNameText);
            var newFolderId = Null.NullInteger;

            try
            {
                var portalId = this.portalSettings.PortalId;
                var currentFolder = this.GetCurrentFolder();
                var newFolderPath = $"{currentFolder.FolderPath}{this.tbFolderNameText}/";

                var folderMapping = FolderMappingController.Instance.GetFolderMapping(currentFolder.FolderMappingID);

                if (!this.FolderManager.FolderExists(portalId, newFolderPath))
                {
                    var newFolder = this.FolderManager.AddFolder(folderMapping, newFolderPath);
                    newFolderId = newFolder.FolderID;
                    this.SetFolderPermission(newFolder.FolderID);
                    this.currentDirectoryText = newFolder.FolderPath;
                }
            }
            catch (Exception exception)
            {
                // Replaces Response.Write("<script>javascript:alert(...)</script>").
                this.pendingAlertMessage = exception.Message;
            }
            finally
            {
                this.FillFolderTree(this.StartingDir());

                if (newFolderId > Null.NullInteger)
                {
                    var tnNewFolder = this.FindNodeByValue(
                        this.folderTreeNodes, newFolderId.ToString(CultureInfo.InvariantCulture));
                    if (tnNewFolder != null)
                    {
                        this.folderTreeNodes.ClearSelection();
                        tnNewFolder.Selected = true;
                        this.selectedFolderNodeValue = tnNewFolder.Value;
                        this.CurrentFolderId = newFolderId;

                        var expandNode = tnNewFolder.Parent;
                        while (expandNode != null)
                        {
                            expandNode.Expanded = true;
                            expandNode = expandNode.Parent;
                        }
                    }
                }

                this.ShowFilesIn(this.GetCurrentFolder());
            }
        }

        this.panCreateVisible = false;
    }

    /// <summary>Synchronize Current Folder With the Database.</summary>
    partial void Syncronize_Click()
    {
        var currentFolderInfo = this.GetCurrentFolder();

        this.FolderManager.Synchronize(this.portalSettings.PortalId, currentFolderInfo.FolderPath, false, true);

        // Reload Folder
        this.ShowFilesIn(this.GetCurrentFolder());
    }

    /// <summary>Delete Selected File.</summary>
    partial void Delete_Click()
    {
        if (string.IsNullOrEmpty(this.selectedFileId))
        {
            return;
        }

        var deleteFile = this.FileManager.GetFile(int.Parse(this.selectedFileId, CultureInfo.InvariantCulture));

        var thumbFolder = Path.Combine(this.GetCurrentFolder().PhysicalPath, "_thumbs");

        var thumbPath =
            Path.Combine(thumbFolder, this.selectedFileName).Replace(
                this.selectedFileName.Substring(this.selectedFileName.LastIndexOf(".", StringComparison.Ordinal)), ".png");

        try
        {
            this.FileManager.DeleteFile(deleteFile);

            // Also Delete Thumbnail?);
            if (File.Exists(thumbPath))
            {
                File.Delete(thumbPath);
            }
        }
        catch (Exception exception)
        {
            this.pendingAlertMessage = exception.Message;
        }
        finally
        {
            this.ShowFilesIn(this.GetCurrentFolder());

            this.SetDefaultLinkTypeText();

            this.selectedFileId = null;
            this.selectedFileName = null;
        }
    }

    /// <summary>Download selected File.</summary>
    /// <remarks>
    /// Replaces <c>IFileManager.WriteFileToResponse(file, ContentDisposition.Attachment)</c>, which
    /// needs a response of its own: a Blazor circuit cannot write one. The file is served by the
    /// platform's own file handler through the <c>forcedownload</c> link
    /// <see cref="Globals.LinkClick(string,int,int,bool,bool)"/> builds - that handler sets the very
    /// same <c>Content-Disposition: attachment</c> - and the client is sent to it.
    /// </remarks>
    partial void Download_Click()
    {
        if (string.IsNullOrEmpty(this.selectedFileId))
        {
            return;
        }

        var downloadFile = this.FileManager.GetFile(int.Parse(this.selectedFileId, CultureInfo.InvariantCulture));

        var downloadUrl = Globals.LinkClick(
            $"fileid={downloadFile.FileId}",
            this.persistedState.TabId,
            Null.NullInteger,
            false,
            true);

        this.QueueInterop("downloadFile", downloadUrl, downloadFile.FileName);
    }

    /// <summary>Show Upload Controls.</summary>
    partial void Upload_Click()
    {
        this.panUploadDivVisible = true;

        if (this.panCreateVisible)
        {
            this.panCreateVisible = false;
        }

        if (this.panThumbVisible)
        {
            this.panThumbVisible = false;
        }

        this.SetupFileUpload();
    }

    /// <summary>Cancel Upload - Hide Upload Controls.</summary>
    private void UploadCancel_Click()
    {
        this.panUploadDivVisible = false;
    }

    /// <summary>Upload Selected File.</summary>
    private void UploadNow_Click()
    {
        this.filesTable = null;
        this.ShowFilesIn(this.GetCurrentFolder());

        this.panUploadDivVisible = false;
    }

    /// <summary>Queues the jQuery-File-Upload set-up of the open upload panel.</summary>
    /// <remarks>
    /// This is the inline <c>&lt;script&gt;</c> block at the bottom of <c>Browser.aspx</c>. A
    /// <c>&lt;script&gt;</c> element rendered by a Blazor component is never executed, so the block
    /// lives in <c>browserInterop.js</c> and receives the four values the page interpolated into it
    /// (the upload URL with its query string, the accepted file types, the maximum size and the
    /// current folder/portal), plus the reference the <c>done</c> callback posts back on.
    /// </remarks>
    private void SetupFileUpload()
    {
        var fileUploaderUrl =
            $"{Globals.ResolveUrl($"~{FileUploaderEndpoint.FileUploaderPath}")}" +
            $"?mode={Uri.EscapeDataString(this.GetQueryValue("mode") ?? string.Empty)}" +
            $"&portalid={Uri.EscapeDataString(this.GetQueryValue("PortalID") ?? string.Empty)}" +
            $"&tabid={Uri.EscapeDataString(this.GetQueryValue("tabid") ?? string.Empty)}" +
            $"&mid={Uri.EscapeDataString(this.GetQueryValue("mid") ?? string.Empty)}" +
            $"&ckId={Uri.EscapeDataString(this.GetQueryValue("CKEditor") ?? string.Empty)}";

        this.QueueInterop(
            "setupFileUpload",
            new
            {
                fileUploaderUrl,
                acceptFileTypes = this.AcceptFileTypes,
                maxFileSize = this.MaxUploadSize,
                currentFolderId = this.CurrentFolderId.ToString(CultureInfo.InvariantCulture),
                portalId = this.GetQueryValue("PortalID") ?? string.Empty,
                dotNetRef = this.GetDotNetRef(),
            });
    }

    /// <summary>Opens the Re-sizing Panel.</summary>
    partial void Resizer_Click()
    {
        if (string.IsNullOrEmpty(this.selectedFileName))
        {
            return;
        }

        // Hide Link Panel and show Image Editor
        this.panThumbVisible = true;
        this.panImagePreviewVisible = true;
        this.panImageEdHeadVisible = true;

        this.imgOriginalVisible = true;

        this.cmdRotateVisible = true;
        this.cmdCropVisible = true;
        this.cmdZoomVisible = true;
        this.cmdResize2Visible = false;

        this.panLinkModeVisible = false;
        this.browserModeVisible = false;

        this.localizedText["lblResizeHeader"] = this.LocalizeString("lblResizeHeader.Text");
        this.titleText = $"{this.Text("lblResizeHeader")} - DNNConnect.CKEditorProvider.FileBrowser";

        // Hide all Unwanted Elements from the Image Editor
        this.cmdCloseVisible = false;
        this.panInfoVisible = false;

        this.panImageEditorVisible = false;
        this.lblCropInfoVisible = false;

        var fileInfo = this.FileManager.GetFile(this.GetCurrentFolder(), this.selectedFileName);
        string sFilePath = this.FileManager.GetUrl(fileInfo);

        string sFileNameNoExt = Path.GetFileNameWithoutExtension(fileInfo.FileName);

        this.txtThumbNameText = $"{sFileNameNoExt}_resized";

        if (!BrowserMedia.AllowedImageExtensions.Contains(fileInfo.Extension))
        {
            return;
        }

        int imageWidth, imageHeight;
        using (var fs = this.FileManager.GetFileContent(fileInfo))
        {
            // Replaces Image.FromStream(...).Width/.Height: only the dimensions are read here.
            var imageInfo = Image.Identify(fs);
            imageWidth = imageInfo.Width;
            imageHeight = imageInfo.Height;
        }

        // Show Preview Images
        this.imgOriginalUrl = sFilePath;
        this.imgResizedUrl = sFilePath;

        int w = imageWidth;
        int h = imageHeight;

        int longestDimension = (w > h) ? w : h;
        int shortestDimension = (w < h) ? w : h;

        float factor = ((float)longestDimension) / shortestDimension;

        double newWidth = 400;
        double newHeight = 300 / factor;

        if (w < h)
        {
            newWidth = 400 / factor;
            newHeight = 300;
        }

        if (newWidth > imageWidth)
        {
            newWidth = imageWidth;
        }

        if (newHeight > imageHeight)
        {
            newHeight = imageHeight;
        }

        int iDefaultWidth, iDefaultHeight;

        if (this.currentSettings.ResizeWidth > 0)
        {
            iDefaultWidth = this.currentSettings.ResizeWidth;

            // Check if Default Value is greater the Image Value
            if (iDefaultWidth > imageWidth)
            {
                iDefaultWidth = imageWidth;
            }
        }
        else
        {
            iDefaultWidth = (int)newWidth;
        }

        if (this.currentSettings.ResizeHeight > 0)
        {
            iDefaultHeight = this.currentSettings.ResizeHeight;

            // Check if Default Value is greater the Image Value
            if (iDefaultHeight > imageHeight)
            {
                iDefaultHeight = imageHeight;
            }
        }
        else
        {
            iDefaultHeight = (int)newHeight;
        }

        this.txtHeightText = iDefaultHeight.ToString(CultureInfo.InvariantCulture);
        this.txtWidthText = iDefaultWidth.ToString(CultureInfo.InvariantCulture);

        this.imgOriginalHeight = (int)newHeight;
        this.imgOriginalWidth = (int)newWidth;

        this.imgResizedHeight = iDefaultHeight;
        this.imgResizedWidth = iDefaultWidth;

        this.imgOriginalToolTip = this.LocalizeString("imgOriginal.Text");
        this.imgResizedToolTip = this.LocalizeString("imgResized.Text");

        // Was the "SliderScript" start-up script: ResizeMe + the two SetupSlider calls.
        this.QueueInterop("setupSliders", imageWidth, imageHeight, iDefaultWidth, iDefaultHeight);
    }

    /// <summary>Hide Image Re-sizing Panel.</summary>
    private void ResizeCancel_Click()
    {
        // Hide Image Editor Panels
        this.panImagePreviewVisible = false;
        this.panImageEdHeadVisible = false;
        this.panImageEditorVisible = false;
        this.panThumbVisible = false;

        // Show Link Panel
        this.panLinkModeVisible = true;
        this.cmdCloseVisible = true;
        this.panInfoVisible = true;
        this.titleText = $"{this.browserModusText} - DNNConnect.CKEditorProvider.FileBrowser";

        if (string.Equals(this.browserModus, "Link", StringComparison.Ordinal))
        {
            this.browserModeVisible = true;
        }
    }

    /// <summary>Resize Image based on User Input.</summary>
    /// <returns>A task.</returns>
    /// <remarks>
    /// The <c>System.Drawing</c> half is ported onto ImageSharp: a JPEG source is still redrawn
    /// bicubically and encoded with the quality the <c>dDlQuality</c> drop down carries, and every
    /// other format is still thumbnailed and saved in the format of the source image.
    /// </remarks>
    private async Task ResizeNow_ClickAsync()
    {
        var file = this.FileManager.GetFile(this.GetCurrentFolder(), this.selectedFileName);
        string resizedFileName;

        // The two sliders of jquery.ImageSlider.js write straight into the text boxes, which is
        // what the post back used to carry; the live values are read back before resizing.
        var widthText = await this.ReadInputValueAsync("txtWidth", this.txtWidthText);
        var heightText = await this.ReadInputValueAsync("txtHeight", this.txtHeightText);

        using (var fileStream = this.FileManager.GetFileContent(file))
        {
            using var oldImage = Image.Load(fileStream);
            var sourceFormat = oldImage.Metadata.DecodedImageFormat;

            if (!int.TryParse(widthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var newWidth))
            {
                newWidth = oldImage.Width;
            }

            if (!int.TryParse(heightText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var newHeight))
            {
                newHeight = oldImage.Height;
            }

            if (!string.IsNullOrEmpty(this.txtThumbNameText))
            {
                resizedFileName = $"{this.txtThumbNameText}.{file.Extension}";
            }
            else
            {
                resizedFileName = $"{Path.GetFileNameWithoutExtension(file.FileName)}_resized.{file.Extension}";
            }

            // Create a Resized Thumbnail
            if (this.chkAspectChecked)
            {
                var finalHeight = Math.Abs(oldImage.Height * newWidth / oldImage.Width);

                if (finalHeight > newHeight)
                {
                    // Height resize if necessary
                    newWidth = oldImage.Width * newHeight / oldImage.Height;
                    finalHeight = newHeight;
                }

                newHeight = finalHeight;
            }

            var counter = 0;
            while (this.FileManager.FileExists(this.GetCurrentFolder(), resizedFileName))
            {
                counter++;
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(resizedFileName);
                resizedFileName = $"{fileNameWithoutExtension}_{counter}.{file.Extension}";
            }

            using var stream = new MemoryStream();

            using (var newImage = BrowserMedia.Resize(oldImage, newWidth, newHeight))
            {
                // Add Compression to Jpeg Images
                if (sourceFormat is JpegFormat)
                {
                    if (!int.TryParse(this.qualitySelectedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quality))
                    {
                        quality = 100;
                    }

                    newImage.Save(stream, BrowserMedia.GetEncoder(sourceFormat, quality));
                }
                else
                {
                    // Finally Create a new Resized Image, in the format of the source image.
                    newImage.Save(stream, BrowserMedia.GetEncoder(sourceFormat, 100));
                }
            }

            stream.Position = 0;

            this.FileManager.AddFile(this.GetCurrentFolder(), resizedFileName, stream, false, true, this.FileContentTypeManager.GetContentType(Path.GetExtension(resizedFileName)));
        }

        // Add new file to database
        var currentFolderInfo = this.GetCurrentFolder();

        this.FolderManager.Synchronize(this.portalSettings.PortalId, currentFolderInfo.FolderPath, false, true);

        // Hide Image Editor Panels
        this.panImagePreviewVisible = false;
        this.panImageEdHeadVisible = false;
        this.panImageEditorVisible = false;
        this.panThumbVisible = false;

        // Show Link Panel
        this.panLinkModeVisible = true;
        this.cmdCloseVisible = true;
        this.panInfoVisible = true;
        this.titleText = $"{this.browserModusText} - DNNConnect.CKEditorProvider.FileBrowser";

        if (string.Equals(this.browserModus, "Link", StringComparison.Ordinal))
        {
            this.browserModeVisible = true;
        }

        this.ShowFilesIn(this.GetCurrentFolder());

        this.GoToSelectedFile(resizedFileName);
    }

    /// <summary>Save the New Cropped Image.</summary>
    private void CropNow_Click()
    {
        // Hide Image Editor Panels
        this.panImagePreviewVisible = false;
        this.panImageEdHeadVisible = false;
        this.panImageEditorVisible = false;
        this.panThumbVisible = false;

        // Show Link Panel
        this.panLinkModeVisible = true;
        this.cmdCloseVisible = true;
        this.panInfoVisible = true;

        if (string.Equals(this.browserModus, "Link", StringComparison.Ordinal))
        {
            this.browserModeVisible = true;
        }

        this.titleText = $"{this.browserModusText} - DNNConnect.CKEditorProvider.FileBrowser";

        // Add new file to database
        var currentFolderInfo = this.GetCurrentFolder();

        this.FolderManager.Synchronize(this.portalSettings.PortalId, currentFolderInfo.FolderPath, false, true);

        this.ShowFilesIn(this.GetCurrentFolder());

        string sExtension = Path.GetExtension(this.selectedFileName);

        this.GoToSelectedFile($"{this.txtCropImageNameText}{sExtension}");
    }

    /// <summary>Hide Resize Panel and Show CropZoom Panel.</summary>
    private void Rotate_Click()
    {
        this.panThumbVisible = false;
        this.panImageEditorVisible = true;

        this.imgOriginalVisible = false;

        this.lblCropInfoVisible = true;

        this.cmdRotateVisible = false;
        this.cmdCropVisible = false;
        this.cmdZoomVisible = false;
        this.cmdResize2Visible = true;

        this.localizedText["lblResizeHeader"] = this.LocalizeString("lblResizeHeader2.Text");
        this.titleText = $"{this.Text("lblResizeHeader")} - DNNConnect.CKEditorProvider.FileBrowser";

        var file = this.FileManager.GetFile(this.GetCurrentFolder(), this.selectedFileName);
        string sFilePath = this.FileManager.GetUrl(file);

        string sFileNameNoExt = Path.GetFileNameWithoutExtension(file.FileName);
        this.txtCropImageNameText = $"{sFileNameNoExt}_Crop";

        // Was the "CropZoomScript" start-up script; the save, which posted back to cmdCropNow,
        // now calls CropNowAsync over the circuit.
        this.QueueInterop(
            "setupCropZoom",
            new
            {
                imageUrl = sFilePath,
                imageWidth = file.Width,
                imageHeight = file.Height,
                fileId = file.FileId,
                processImageUrl = Globals.ResolveUrl($"~{ProcessImageUrl}"),
            },
            this.GetDotNetRef());
    }

    /// <summary>Fill Quality Values 1-100 %.</summary>
    partial void FillQualityPercentages()
    {
        if (this.qualityItems.Count > 0)
        {
            return;
        }

        for (var i = 00; i < 101; i++)
        {
            var value = i.ToString(CultureInfo.InvariantCulture);
            this.qualityItems.Add(new BrowserListItem { Text = value, Value = value });
        }

        this.qualitySelectedValue = this.qualityItems[100].Value;
    }

    /// <summary>Switch Browser in Link Modus between Link and Page Mode.</summary>
    /// <param name="selectedValue">The selected value of the <c>BrowserMode</c> list.</param>
    partial void BrowserMode_SelectedIndexChanged(string selectedValue)
    {
        switch (selectedValue)
        {
            case "file":
                this.panLinkModeVisible = true;
                this.panPageModeVisible = false;
                this.browserModusText = string.Format(
                    CultureInfo.CurrentCulture, this.LocalizeString("BrowserModus.Text"), this.browserModus);
                break;
            case "page":
                this.panLinkModeVisible = false;
                this.panPageModeVisible = true;
                this.browserModusText = string.Format(
                    CultureInfo.CurrentCulture, this.LocalizeString("BrowserModus.Text"), $"Page {this.browserModus}");

                this.RenderTabs();
                break;
        }

        this.titleText = $"{this.browserModusText} - DNNConnect.CKEditorProvider.FileBrowser";

        this.SetDefaultLinkTypeText();
    }

    /// <summary>Load the Portal Tabs for the Page Links TreeView Selector.</summary>
    partial void RenderTabs()
    {
        if (this.tabTreeNodes.Count > 0)
        {
            return;
        }

        var allPortalTabsList = DotNetNuke.Entities.Tabs.TabController.GetPortalTabs(this.HostSettings, this.AppStatus, this.portalSettings.PortalId, -1, false, null, true, false, true, true, false);
        var allPortalTabs = new HashSet<TabInfo>(allPortalTabsList);

        var helper = new TreeViewHelper<int>();
        helper.LoadNodes(allPortalTabs, this.tabTreeNodes, GetNodeId, GetParentId, GetNodeText, GetNodeValue, GetNodeImageUrl, GetParentIdCheck);

        return;

        static int GetNodeId(TabInfo x) => x.TabID;
        static int GetParentId(TabInfo x) => x.ParentId;
        static string GetNodeText(TabInfo x) => x.TabName;
        static string GetNodeValue(TabInfo x) => x.TabID.ToString(CultureInfo.InvariantCulture);
        static bool GetParentIdCheck(int x) => x != -1;
        static string GetNodeImageUrl(TabInfo x) =>
            string.IsNullOrWhiteSpace(x.IconFile)
                ? "Images/Page.gif"
                : Globals.ResolveUrl(x.IconFile);
    }

    /// <summary>Show Preview of the Page links.</summary>
    /// <param name="node">The clicked page node.</param>
    /// <returns>A task.</returns>
    private async Task TreeTabs_NodeClickAsync(DnnTreeNode node)
    {
        if (node == null)
        {
            return;
        }

        this.tabTreeNodes.ClearSelection();
        node.Selected = true;
        this.selectedTabNodeValue = node.Value;

        this.SetDefaultLinkTypeText();

        var selectTab = this.TabController.GetTab(
            int.Parse(node.Value, CultureInfo.InvariantCulture), this.portalSettings.PortalId, true);

        string sDomainName = this.DomainNameUrl;

        // Add Language Parameter ?!
        var localeSelected = this.languageRowVisible && this.LanguageSelectedIndex > 0;

        if (this.humanFriendlyChecked)
        {
            var fileNamePath = localeSelected
                ? $"{Globals.ApplicationURL(selectTab.TabID)}&language={this.languageListSelectedValue}"
                : Globals.ApplicationURL(selectTab.TabID);
            var fileName = Globals.FriendlyUrl(selectTab, fileNamePath, this.portalSettings);

            // Relative Url
            fileName = Globals.ResolveUrl(Regex.Replace(fileName, sDomainName, "~", RegexOptions.IgnoreCase));

            this.linkTypeItems[0].Text = Regex.Replace(
                this.linkTypeItems[0].Text,
                "/Images/MyImage.jpg",
                Globals.ResolveUrl(Regex.Replace(fileName, sDomainName, "~", RegexOptions.IgnoreCase)),
                RegexOptions.IgnoreCase);

            // Absolute Url
            this.linkTypeItems[1].Text = Regex.Replace(
                this.linkTypeItems[1].Text,
                "http://www.MyWebsite.com/Images/MyImage.jpg",
                Regex.Replace(fileName, sDomainName, sDomainName, RegexOptions.IgnoreCase),
                RegexOptions.IgnoreCase);
        }
        else
        {
            string locale = localeSelected ? $"language/{this.languageListSelectedValue}/" : string.Empty;

            // Relative Url
            this.linkTypeItems[0].Text = Regex.Replace(
                this.linkTypeItems[0].Text,
                "/Images/MyImage.jpg",
                Globals.ResolveUrl($"~/tabid/{selectTab.TabID}/{locale}Default.aspx"),
                RegexOptions.IgnoreCase);

            // Absolute Url
            this.linkTypeItems[1].Text = Regex.Replace(
                this.linkTypeItems[1].Text,
                "http://www.MyWebsite.com/Images/MyImage.jpg",
                $"{sDomainName}/tabid/{selectTab.TabID}/{locale}Default.aspx",
                RegexOptions.IgnoreCase);
        }

        if (this.currentSettings.UseAnchorSelector)
        {
            await this.FindAnchorsOnTabAsync(selectTab);
        }

        // Was the "hideLoadingScript" start-up script: jQuery('#panelLoading').hide();
        this.panelLoadingVisible = false;
        this.QueueInterop("hideLoading");
    }

    /// <summary>Find and List all Anchors from the Selected Page.</summary>
    /// <param name="selectedTab">The selected tab.</param>
    /// <returns>A task.</returns>
    /// <remarks>
    /// <c>System.Net.WebClient</c> is obsolete on modern .NET and replaced by
    /// <see cref="HttpClient"/>; the parsing stays <c>Utilities/AnchorFinder.cs</c> and the "None"
    /// item is still inserted in front of the found anchors (and is the only item when the download
    /// or the parsing fails).
    /// </remarks>
    private async Task FindAnchorsOnTabAsync(TabInfo selectedTab)
    {
        // Clear Item list first...
        this.anchorListItems.Clear();
        this.anchorListSelectedValue = string.Empty;

        var noneText = this.LocalizeString("None.Text");

        try
        {
            var tabUrl = selectedTab.FullUrl;

            if (tabUrl.StartsWith("/", StringComparison.Ordinal))
            {
                tabUrl = this.BuildAbsoluteUrl(tabUrl);
            }

            var page = await AnchorHttpClient.GetStringAsync(tabUrl);

            foreach (LinkItem i in AnchorFinder.ListAll(page).Where(i => !string.IsNullOrEmpty(i.Anchor)))
            {
                this.anchorListItems.Add(new BrowserListItem { Text = i.Anchor, Value = i.Anchor });
            }

            // Add No Anchor item
            this.anchorListItems.Insert(0, new BrowserListItem { Text = noneText, Value = string.Empty });
        }
        catch (Exception)
        {
            // Add No Anchor item
            this.anchorListItems.Add(new BrowserListItem { Text = noneText, Value = string.Empty });
        }

        this.anchorListSelectedValue = this.anchorListItems[0].Value;
    }

    /// <summary>Close Browser Window.</summary>
    /// <remarks>
    /// This is <c>CmdCloseClick</c>. The two <c>Response.Write("&lt;script&gt;...")</c> +
    /// <c>Response.End()</c> branches are the <c>callCKEditorFunction</c> interop call (the
    /// <c>CKEditorFuncNum</c> protocol, which also closes the window) and - when nothing is
    /// selected - the plain <c>alert</c> of <c>Error5.Text</c>.
    /// </remarks>
    private void CmdCloseClick()
    {
        if (!this.panLinkModeVisible && this.panPageModeVisible)
        {
            if (string.IsNullOrEmpty(this.selectedTabNodeValue))
            {
                return;
            }

            var selectTab = this.TabController.GetTab(
                int.Parse(this.selectedTabNodeValue, CultureInfo.InvariantCulture), this.portalSettings.PortalId, true);

            string fileName = null;
            var domainName = this.DomainNameUrl;

            // Add Language Parameter ?!
            var localeSelected = this.languageRowVisible && this.LanguageSelectedIndex > 0;

            var friendlyUrlPath = localeSelected
                ? $"{Globals.ApplicationURL(selectTab.TabID)}&language={this.languageListSelectedValue}"
                : Globals.ApplicationURL(selectTab.TabID);

            var friendlyUrl = Globals.FriendlyUrl(selectTab, friendlyUrlPath, this.portalSettings);

            var locale = localeSelected
                ? $"language/{this.languageListSelectedValue}/"
                : string.Empty;

            // Relative or Absolute Url
            switch (this.linkTypeSelectedValue)
            {
                case "relLnk":
                    {
                        if (this.humanFriendlyChecked)
                        {
                            fileName = Globals.ResolveUrl(Regex.Replace(friendlyUrl, domainName, "~", RegexOptions.IgnoreCase));
                        }
                        else
                        {
                            fileName = Globals.ResolveUrl($"~/tabid/{selectTab.TabID}/{locale}Default.aspx");
                        }

                        break;
                    }

                case "absLnk":
                    {
                        if (this.humanFriendlyChecked)
                        {
                            fileName = Regex.Replace(friendlyUrl, domainName, domainName, RegexOptions.IgnoreCase);
                        }
                        else
                        {
                            fileName = $"{domainName}/tabid/{selectTab.TabID}/{locale}Default.aspx";
                        }
                    }

                    break;
            }

            // Add Page Anchor if one is selected
            if (this.AnchorSelectedIndex > 0 && this.anchorListItems.Count > 1)
            {
                fileName = $"{fileName}#{this.anchorListItems[this.AnchorSelectedIndex].Text}";
            }

            var callback = this.GetJavaScriptCode(fileName, null, true);

            this.QueueInterop("callCKEditorFunction", callback.FuncNum, callback.FileUrl, callback.ErrorMessage);
        }
        else if (this.panLinkModeVisible && !this.panPageModeVisible)
        {
            if (!string.IsNullOrEmpty(this.selectedFileName) && !string.IsNullOrEmpty(this.selectedFileId))
            {
                var fileInfo = this.FileManager.GetFile(int.Parse(this.selectedFileId, CultureInfo.InvariantCulture));

                var filePath = this.FileManager.GetUrl(fileInfo);

                if (this.linkTypeSelectedValue.Equals("absLnk", StringComparison.InvariantCultureIgnoreCase))
                {
                    filePath = this.BuildAbsoluteUrl(filePath);
                }

                var callback = this.GetJavaScriptCode(string.Empty, filePath, false);

                this.QueueInterop("callCKEditorFunction", callback.FuncNum, callback.FileUrl, callback.ErrorMessage);
            }
            else
            {
                this.pendingAlertMessage = this.LocalizeString("Error5.Text");
            }
        }
    }

    /// <summary>Exit Dialog.</summary>
    /// <remarks>Replaces the "closeScript" start-up script, <c>javascript:self.close();</c>.</remarks>
    private void Cancel_Click()
    {
        this.QueueInterop("closeWindow");
    }

    /// <summary>Gets the arguments of the CKEditor callback.</summary>
    /// <param name="fileName">Name of the file.</param>
    /// <param name="fileUrl">The file URL.</param>
    /// <param name="isPageLink">if set to <see langword="true"/> [is page link].</param>
    /// <returns>The function number, the file URL and the error message of the callback.</returns>
    /// <remarks>
    /// This is <c>GetJavaScriptCode</c>. It used to compose the statement
    /// <c>var E = window.top.opener;E.CKEDITOR.tools.callFunction(funcNum,'url','error');self.close();</c>;
    /// that statement is now <c>callCKEditorFunction</c> of <c>browserInterop.js</c>, so the method
    /// yields its three (unescaped - interop arguments are serialized, not interpolated) arguments.
    /// The URL mangling, the <c>CKEditorFuncNum</c> sanitizing and the always empty error message
    /// are unchanged.
    /// </remarks>
    private CKEditorCallback GetJavaScriptCode(string fileName, string fileUrl, bool isPageLink)
    {
        if (!string.IsNullOrEmpty(fileUrl) && !string.IsNullOrEmpty(fileName))
        {
            // If we have both, combine them
            fileUrl = !fileUrl.EndsWith("/", StringComparison.Ordinal)
                ? $"{fileUrl}/{fileName}"
                : $"{fileUrl}{fileName}";
        }
        else if (string.IsNullOrEmpty(fileUrl))
        {
            // If no URL, default to the file name
            fileUrl = fileName;
        }

        if (!fileUrl.Contains('?') && !isPageLink)
        {
            fileUrl = Utility.JavaScriptEscape(fileUrl);

            if (fileUrl.Contains("%3A"))
            {
                fileUrl = fileUrl.Replace("%3A", ":");
            }

            if (fileUrl.Contains(".aspx%23"))
            {
                fileUrl = fileUrl.Replace("aspx%23", "aspx#");
            }
        }

        ////string _CKEditorName = this.GetQueryValue("CKEditor");
        string funcNum = this.GetQueryValue("CKEditorFuncNum") ?? string.Empty;

        string errorMsg = string.Empty;

        funcNum = Regex.Replace(funcNum, @"[^0-9]", string.Empty, RegexOptions.None);

        return new CKEditorCallback(funcNum, fileUrl, errorMsg);
    }

    /// <summary>Reads the live value of one of the image editor's inputs.</summary>
    /// <param name="elementId">The ID of the input.</param>
    /// <param name="fallback">The value the component state carries.</param>
    /// <returns>The value.</returns>
    private async Task<string> ReadInputValueAsync(string elementId, string fallback)
    {
        try
        {
            var module = await this.JsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "/Providers/HtmlEditorProviders/DNNConnect.CKE/Browser/js/browserInterop.js");
            var value = await module.InvokeAsync<string>("getElementValue", elementId);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }
        catch (JSException)
        {
            return fallback;
        }
    }

    /// <summary>A queued call of a <c>browserInterop.js</c> helper.</summary>
    /// <param name="Identifier">The exported function to call.</param>
    /// <param name="Arguments">Its arguments.</param>
    private sealed record PendingInteropCall(string Identifier, object[] Arguments);

    /// <summary>The three arguments of the <c>CKEDITOR.tools.callFunction</c> callback.</summary>
    /// <param name="FuncNum">The <c>CKEditorFuncNum</c> the window was opened with, sanitized to digits.</param>
    /// <param name="FileUrl">The URL of the picked file or page.</param>
    /// <param name="ErrorMessage">The error message, which the page always left empty.</param>
    private sealed record CKEditorCallback(string FuncNum, string FileUrl, string ErrorMessage);
}
