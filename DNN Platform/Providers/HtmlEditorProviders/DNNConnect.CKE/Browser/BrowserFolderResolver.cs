// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DNNConnect.CKEditorProvider.Browser;

using System;
using System.Globalization;
using System.IO;
using System.Linq;

using DNNConnect.CKEditorProvider.Objects;
using DNNConnect.CKEditorProvider.Utilities;

using DotNetNuke.Abstractions.Portals;
using DotNetNuke.Abstractions.Security.Permissions;
using DotNetNuke.Abstractions.Users;
using DotNetNuke.Entities.Users;
using DotNetNuke.Security.Permissions;
using DotNetNuke.Services.FileSystem;

using Convert = System.Convert;
using Globals = DotNetNuke.Common.Globals;

/// <summary>
/// The "starting directory" cascade of the file browser (<c>StartingDir</c>,
/// <c>GetUserFolderInfo</c> and the two folder permission helpers of <c>Browser.aspx.cs</c>).
/// </summary>
/// <remarks>
/// It lives outside the component because both the interactive browser
/// (<see cref="Browser"/>) and the CKEditor quick upload endpoint
/// (<see cref="QuickUploadEndpoint"/>, which short circuits the very same URL for the
/// <c>*Upload</c> commands) resolve the folder the same way; the bodies are the original ones,
/// with the page's fields and services handed in through the constructor.
/// </remarks>
internal sealed class BrowserFolderResolver
{
    private readonly IPortalSettings portalSettings;
    private readonly EditorProviderSettings currentSettings;
    private readonly EditorProviderSettings allPortalsSettings;
    private readonly string browserModus;
    private readonly IFolderManager folderManager;
    private readonly IUserController userController;
    private readonly IPermissionDefinitionService permissionDefinitionService;

    /// <summary>Initializes a new instance of the <see cref="BrowserFolderResolver"/> class.</summary>
    /// <param name="portalSettings">The portal settings.</param>
    /// <param name="currentSettings">The current editor provider settings.</param>
    /// <param name="allPortalsSettings">The host (all portals) editor provider settings.</param>
    /// <param name="browserModus">The browser modus (the <c>Type</c> query string value).</param>
    /// <param name="folderManager">The folder manager.</param>
    /// <param name="userController">The user controller.</param>
    /// <param name="permissionDefinitionService">The permission definition service.</param>
    public BrowserFolderResolver(
        IPortalSettings portalSettings,
        EditorProviderSettings currentSettings,
        EditorProviderSettings allPortalsSettings,
        string browserModus,
        IFolderManager folderManager,
        IUserController userController,
        IPermissionDefinitionService permissionDefinitionService)
    {
        this.portalSettings = portalSettings;
        this.currentSettings = currentSettings;
        this.allPortalsSettings = allPortalsSettings;
        this.browserModus = browserModus;
        this.folderManager = folderManager;
        this.userController = userController;
        this.permissionDefinitionService = permissionDefinitionService;
    }

    /// <summary>Formats a MapPath into relative MapUrl.</summary>
    /// <param name="path">MapPath Input string.</param>
    /// <param name="applicationMapPath">The physical path of the application root.</param>
    /// <returns>The output URL string.</returns>
    /// <remarks>
    /// Replaces <c>Server.MapPath("~")</c> with the injected application map path and
    /// <c>Request.ApplicationPath</c> with the application path <c>Globals.ApplicationPath</c>
    /// carries, which is what the request value resolved to.
    /// </remarks>
    public static string MapUrl(string path, string applicationMapPath)
    {
        return Globals.ApplicationPath +
               path.Replace(applicationMapPath, string.Empty).Replace(@"\", "/");
    }

    /// <summary>Get the Current Starting Directory.</summary>
    /// <returns>Returns the Starting Directory.</returns>
    public IFolderInfo StartingDir()
    {
        IFolderInfo startingFolderInfo = null;

        if (this.browserModus == "Image" && !string.IsNullOrEmpty(this.allPortalsSettings.HostBrowserRootDirForImg))
        {
            startingFolderInfo = Utility.EnsureGetFolder(
                this.portalSettings.PortalId,
                this.allPortalsSettings.HostBrowserRootDirForImg);
        }
        else if (this.browserModus == "Image" && !this.currentSettings.BrowserRootDirForImgId.Equals(-1))
        {
            var rootFolder = this.folderManager.GetFolder(this.currentSettings.BrowserRootDirForImgId);

            if (rootFolder != null)
            {
                startingFolderInfo = rootFolder;
            }
        }
        else if (!string.IsNullOrEmpty(this.allPortalsSettings.HostBrowserRootDir))
        {
            startingFolderInfo = Utility.EnsureGetFolder(
                this.portalSettings.PortalId,
                this.allPortalsSettings.HostBrowserRootDir);
        }
        else if (!this.currentSettings.BrowserRootDirId.Equals(-1))
        {
            var rootFolder = this.folderManager.GetFolder(this.currentSettings.BrowserRootDirId);

            if (rootFolder != null)
            {
                startingFolderInfo = rootFolder;
            }
        }
        else
        {
            startingFolderInfo = this.folderManager.GetFolder(this.portalSettings.PortalId, string.Empty);
        }

        if (Utility.IsInRoles(this.portalSettings.AdministratorRoleName, this.portalSettings))
        {
            return startingFolderInfo;
        }

        if (this.currentSettings.SubDirs)
        {
            startingFolderInfo = this.GetUserFolderInfo(startingFolderInfo.PhysicalPath);
        }
        else
        {
            return startingFolderInfo;
        }

        if (Directory.Exists(startingFolderInfo.PhysicalPath))
        {
            return startingFolderInfo;
        }

        var folderStart = startingFolderInfo.PhysicalPath;

        folderStart = folderStart.Substring(this.portalSettings.HomeDirectoryMapPath.Length).Replace(@"\", "/");

        startingFolderInfo = this.folderManager.AddFolder(this.portalSettings.PortalId, folderStart);

        Directory.CreateDirectory(startingFolderInfo.PhysicalPath);

        this.SetFolderPermission(startingFolderInfo);

        return startingFolderInfo;
    }

    /// <summary>Gets the user folder Info.</summary>
    /// <param name="startingDir">The Starting Directory.</param>
    /// <returns>Returns the user folder path.</returns>
    public IFolderInfo GetUserFolderInfo(string startingDir)
    {
        IFolderInfo userFolderInfo;

        var userFolderPath = Path.Combine(startingDir, "userfiles");

        // Create "userfiles" folder if not exists
        if (!Directory.Exists(userFolderPath))
        {
            var folderStart = userFolderPath;

            folderStart = folderStart.Substring(this.portalSettings.HomeDirectoryMapPath.Length).Replace(@"\", "/");

            userFolderInfo = this.folderManager.AddFolder(this.portalSettings.PortalId, folderStart);

            Directory.CreateDirectory(userFolderPath);

            this.SetFolderPermission(userFolderInfo);
        }

        // Create user folder based on the user id
        userFolderPath = Path.Combine(userFolderPath, $"{this.userController.GetCurrentUserInfo().UserID}\\");

        if (!Directory.Exists(userFolderPath))
        {
            var folderStart = userFolderPath;

            folderStart = folderStart.Substring(this.portalSettings.HomeDirectoryMapPath.Length).Replace(@"\", "/");

            userFolderInfo = this.folderManager.AddFolder(this.portalSettings.PortalId, folderStart);

            Directory.CreateDirectory(userFolderPath);

            this.SetFolderPermission(userFolderInfo);

            this.SetUserFolderPermission(userFolderInfo, this.userController.GetCurrentUserInfo());
        }
        else
        {
            userFolderInfo = Utility.ConvertFilePathToFolderInfo(userFolderPath, this.portalSettings);

            // make sure the user has the correct permissions set
            this.SetUserFolderPermission(userFolderInfo, this.userController.GetCurrentUserInfo());
        }

        return userFolderInfo;
    }

    /// <summary>Set Folder Permission.</summary>
    /// <param name="folderInfo">The folder info.</param>
    public void SetFolderPermission(IFolderInfo folderInfo)
    {
        this.folderManager.CopyParentFolderPermissions(folderInfo);
    }

    /// <summary>Set Folder Permission.</summary>
    /// <param name="folderId">The Folder ID.</param>
    public void SetFolderPermission(int folderId)
    {
        var folder = this.folderManager.GetFolder(folderId);

        this.SetFolderPermission(folder);
    }

    /// <summary>Set Folder Permission for the Current User.</summary>
    /// <param name="folderInfo">The folder info.</param>
    /// <param name="currentUserInfo">The current user info.</param>
    public void SetUserFolderPermission(IFolderInfo folderInfo, UserInfo currentUserInfo)
    {
        if (FolderPermissionController.CanManageFolder((FolderInfo)folderInfo))
        {
            return;
        }

        var folderPermissions =
            from permission in this.permissionDefinitionService.GetDefinitionsByFolder()
            where
                permission.PermissionKey.Equals("READ", StringComparison.OrdinalIgnoreCase)
                || permission.PermissionKey.Equals("WRITE", StringComparison.OrdinalIgnoreCase)
                || permission.PermissionKey.Equals("BROWSE", StringComparison.OrdinalIgnoreCase)
            select InitializeFolderPermission(permission, folderInfo, currentUserInfo);
        foreach (var folderPermission in folderPermissions)
        {
            folderInfo.FolderPermissions.Add(folderPermission, true);
        }

        FolderPermissionController.SaveFolderPermissions((FolderInfo)folderInfo);
        return;

        static FolderPermissionInfo InitializeFolderPermission(IPermissionDefinitionInfo permission, IFolderInfo folderInfo, IUserInfo currentUserInfo)
        {
            IFolderPermissionInfo folderPermission = new FolderPermissionInfo(permission) { AllowAccess = true, };
            folderPermission.FolderId = folderInfo.FolderID;
            folderPermission.UserId = currentUserInfo.UserID;
            folderPermission.RoleId = Convert.ToInt32(Globals.glbRoleNothing, CultureInfo.InvariantCulture);
            return (FolderPermissionInfo)folderPermission;
        }
    }
}
