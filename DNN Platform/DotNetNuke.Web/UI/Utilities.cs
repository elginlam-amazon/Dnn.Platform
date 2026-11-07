// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Reflection;

    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Common;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Services.ClientDependency;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    using FileInfo = DotNetNuke.Services.FileSystem.FileInfo;
    using FileOrder = DotNetNuke.Abstractions.ClientResources.FileOrder;

    /// <summary>Provides utility methods for UI elements within DNN.</summary>
    public partial class Utilities
    {
        /// <summary>Applies a custom CSS file for a control using a consistent naming pattern.</summary>
        /// <param name="targetControl">The control that should have a skin injected.</param>
        /// <param name="controlSubSkinName">An optional sub-skin.</param>
        /// <param name="controlName">An optional control name that might differ from the type.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IClientResourceController. Scheduled removal in v12.0.0.")]
        public static void ApplyControlSkin(DotNetNuke.UI.Utilities.IClientApiControl targetControl, string controlSubSkinName = "", string controlName = "")
            => ApplyControlSkin(Globals.GetCurrentServiceProvider().GetRequiredService<IClientResourceController>(), targetControl, controlSubSkinName, controlName);

        /// <summary>Applies a custom CSS file for a control using a consistent naming pattern.</summary>
        /// <param name="clientResourceController">The client resource controller.</param>
        /// <param name="targetControl">The control that should have a skin injected.</param>
        /// <param name="controlSubSkinName">An optional sub-skin.</param>
        /// <param name="controlName">An optional control name that might differ from the type.</param>
        public static void ApplyControlSkin(IClientResourceController clientResourceController, DotNetNuke.UI.Utilities.IClientApiControl targetControl, string controlSubSkinName = "", string controlName = "")
        {
            string fallBackEmbeddedSkinName = string.Empty;
            PropertyInfo skinProperty = null;
            PropertyInfo enableEmbeddedSkinsProperty = null;
            bool skinApplied = false;

            try
            {
                skinProperty = targetControl.GetType().GetProperty("Skin");
                enableEmbeddedSkinsProperty = targetControl.GetType().GetProperty("EnableEmbeddedSkins");

                if (string.IsNullOrEmpty(controlName))
                {
                    controlName = targetControl.GetType().BaseType.Name;
                    if (controlName.StartsWith("Rad", StringComparison.Ordinal) || controlName.StartsWith("Dnn", StringComparison.Ordinal))
                    {
                        controlName = controlName.Substring(3);
                    }
                }

                string skinVirtualFolder = string.Empty;
                if (PortalSettings.Current != null)
                {
                    skinVirtualFolder = PortalSettings.Current.ActiveTab.SkinPath.Replace('\\', '/').Replace("//", "/");
                }
                else
                {
                    skinVirtualFolder = Globals.ResolveUrl("~/portals/_default/skins/_default/Aphelia"); // developer skin Aphelia
                }

                string skinName = string.Empty;
                string webControlSkinName = string.Empty;
                if (skinProperty != null)
                {
                    var v = skinProperty.GetValue(targetControl, null);
                    if (v != null)
                    {
                        webControlSkinName = v.ToString();
                    }
                }

                if (string.IsNullOrEmpty(webControlSkinName))
                {
                    webControlSkinName = "default";
                }

                if (skinVirtualFolder.EndsWith('/'))
                {
                    skinVirtualFolder = skinVirtualFolder.Substring(0, skinVirtualFolder.Length - 1);
                }

                int lastIndex = skinVirtualFolder.LastIndexOf('/');
                if (lastIndex > -1 && skinVirtualFolder.Length > lastIndex)
                {
                    skinName = skinVirtualFolder.Substring(skinVirtualFolder.LastIndexOf('/') + 1);
                }

                string systemWebControlSkin = string.Empty;
                if (!string.IsNullOrEmpty(skinName) && !string.IsNullOrEmpty(skinVirtualFolder))
                {
                    systemWebControlSkin = MapVirtualPath(skinVirtualFolder);
                    systemWebControlSkin = Path.Combine(systemWebControlSkin, "WebControlSkin");
                    systemWebControlSkin = Path.Combine(systemWebControlSkin, skinName);
                    systemWebControlSkin = Path.Combine(systemWebControlSkin, controlSubSkinName);
                    systemWebControlSkin = Path.Combine(systemWebControlSkin, $"{controlName}.{webControlSkinName}.css");

                    // Check if the selected skin has the webcontrol skin
                    if (!File.Exists(systemWebControlSkin))
                    {
                        systemWebControlSkin = string.Empty;
                    }

                    // No skin, try default folder
                    if (string.IsNullOrEmpty(systemWebControlSkin))
                    {
                        skinVirtualFolder = Globals.ResolveUrl("~/portals/_default/Skins/_default");
                        skinName = "Default";

                        if (skinVirtualFolder.EndsWith('/'))
                        {
                            skinVirtualFolder = skinVirtualFolder.Substring(0, skinVirtualFolder.Length - 1);
                        }

                        if (!string.IsNullOrEmpty(skinName) && !string.IsNullOrEmpty(skinVirtualFolder))
                        {
                            systemWebControlSkin = MapVirtualPath(skinVirtualFolder);
                            systemWebControlSkin = Path.Combine(systemWebControlSkin, "WebControlSkin");
                            systemWebControlSkin = Path.Combine(systemWebControlSkin, skinName);
                            systemWebControlSkin = Path.Combine(systemWebControlSkin, controlSubSkinName);
                            systemWebControlSkin = Path.Combine(systemWebControlSkin, $"{controlName}.{webControlSkinName}.css");

                            if (!File.Exists(systemWebControlSkin))
                            {
                                systemWebControlSkin = string.Empty;
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(systemWebControlSkin))
                {
                    string filePath = Path.Combine(skinVirtualFolder, "WebControlSkin");
                    filePath = Path.Combine(filePath, skinName);
                    filePath = Path.Combine(filePath, controlSubSkinName);
                    filePath = Path.Combine(filePath, $"{controlName}.{webControlSkinName}.css");
                    filePath = filePath.Replace('\\', '/').Replace("//", "/").TrimEnd('/');

                    if (HttpContextSource.Current != null)
                    {
                        clientResourceController.RegisterStylesheet(filePath, FileOrder.Css.ResourceCss);
                    }

                    if (skinProperty != null && enableEmbeddedSkinsProperty != null)
                    {
                        skinApplied = true;
                        skinProperty.SetValue(targetControl, webControlSkinName, null);
                        enableEmbeddedSkinsProperty.SetValue(targetControl, false, null);
                    }
                }
            }
            catch (Exception ex)
            {
                Exceptions.LogException(ex);
            }

            if (skinProperty != null && enableEmbeddedSkinsProperty != null && !skinApplied)
            {
                if (string.IsNullOrEmpty(fallBackEmbeddedSkinName))
                {
                    fallBackEmbeddedSkinName = "Simple";
                }

                // Set fall back skin Embedded Skin
                skinProperty.SetValue(targetControl, fallBackEmbeddedSkinName, null);
                enableEmbeddedSkinsProperty.SetValue(targetControl, true, null);
            }
        }

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        /// <summary>Create a thumbnail of an image.</summary>
        /// <param name="image">The image file.</param>
        /// <param name="img">The image to resize.</param>
        /// <param name="maxWidth">The maximum width in pixels.</param>
        /// <param name="maxHeight">The maximum height in pixels.</param>
        public static void CreateThumbnail(FileInfo image, DotNetNuke.Web.UI.WebControls.DnnImage img, int maxWidth, int maxHeight)
        {
            if (image.Width > image.Height)
            {
                // Landscape
                if (image.Width > maxWidth)
                {
                    img.Width = maxWidth;
                    img.Height = Convert.ToInt32((image.Height * maxWidth) / (float)image.Width);
                }
                else
                {
                    img.Width = image.Width;
                    img.Height = image.Height;
                }
            }
            else
            {
                // Portrait
                if (image.Height > maxHeight)
                {
                    img.Width = Convert.ToInt32((image.Width * maxHeight) / (float)image.Height);
                    img.Height = maxHeight;
                }
                else
                {
                    img.Width = image.Width;
                    img.Height = image.Height;
                }
            }
        }
        #pragma warning restore BL0005

        /// <summary>Gets a script to display an alert.</summary>
        /// <param name="ctrl">The control.</param>
        /// <param name="message">The message.</param>
        /// <returns>A script.</returns>
        public static string GetClientAlert(DotNetNuke.UI.Utilities.IClientApiControl ctrl, string message)
        {
            return GetClientAlert(ctrl, new MessageWindowParameters(message));
        }

        /// <summary>Gets a script to display an alert.</summary>
        /// <param name="ctrl">The control.</param>
        /// <param name="message">The message.</param>
        /// <returns>A script.</returns>
        public static string GetClientAlert(DotNetNuke.UI.Utilities.IClientApiControl ctrl, MessageWindowParameters message)
        {
            return "jQuery(document).ready(function($){$.dnnAlert({ okText: '" + GetLocalizedString("Ok.Text") + "', text: '" + message.Message + "', title: '" + message.Title + "'});});";
        }

        /// <summary>Gets the localized string corresponding to the <paramref name="key"/>.</summary>
        /// <param name="key">The resource key to find.</param>
        /// <returns>The localized text.</returns>
        public static string GetLocalizedString(string key)
        {
            string resourceFile = "/App_GlobalResources/WebControls.resx";
            return Localization.GetString(key, resourceFile);
        }

        /// <summary>Gets the path to the local resource file associated to the control.</summary>
        /// <param name="ctrl">The control.</param>
        /// <returns>The path to the resource file.</returns>
        public static string GetLocalResourceFile(DotNetNuke.UI.Utilities.IClientApiControl ctrl)
        {
            return UIUtilities.GetLocalResourceFile(ctrl);
        }

        /// <summary>Gets the localized string corresponding to the <paramref name="key"/>, using the resource file of the <paramref name="control"/>.</summary>
        /// <param name="key">The resource key to find.</param>
        /// <param name="control">The control use to find the resource file.</param>
        /// <returns>The localized text.</returns>
        public static string GetLocalizedStringFromParent(string key, DotNetNuke.UI.Utilities.IClientApiControl control)
        {
            string returnValue = key;
            string resourceFileName = GetLocalResourceFile(control.Parent);

            if (!string.IsNullOrEmpty(resourceFileName))
            {
                returnValue = Localization.GetString(key, resourceFileName);
            }

            return returnValue;
        }

        /// <summary>Gets a script for a click confirmation.</summary>
        /// <param name="ctrl">The control.</param>
        /// <param name="message">The message.</param>
        /// <returns>A script.</returns>
        [DnnDeprecated(10, 2, 2, "Please use overload with IClientResourceController")]
        public static partial string GetOnClientClickConfirm(DotNetNuke.UI.Utilities.IClientApiControl ctrl, string message)
            => GetOnClientClickConfirm(Globals.GetCurrentServiceProvider().GetRequiredService<IClientResourceController>(), ctrl, message);

        /// <summary>Gets a script for a click confirmation.</summary>
        /// <param name="clientResourceController">The client resource controller.</param>
        /// <param name="ctrl">The control.</param>
        /// <param name="message">The message.</param>
        /// <returns>A script.</returns>
        public static string GetOnClientClickConfirm(IClientResourceController clientResourceController, DotNetNuke.UI.Utilities.IClientApiControl ctrl, string message)
            => GetOnClientClickConfirm(clientResourceController, ctrl, new MessageWindowParameters(message));

        /// <summary>Gets a script for a click confirmation.</summary>
        /// <param name="ctrl">The control.</param>
        /// <param name="message">The message.</param>
        /// <returns>A script.</returns>
        [DnnDeprecated(10, 2, 2, "Please use overload with IClientResourceController")]
        public static partial string GetOnClientClickConfirm(DotNetNuke.UI.Utilities.IClientApiControl ctrl, MessageWindowParameters message)
            => GetOnClientClickConfirm(Globals.GetCurrentServiceProvider().GetRequiredService<IClientResourceController>(), ctrl, message);

        /// <summary>Gets a script for a click confirmation.</summary>
        /// <param name="clientResourceController">The client resource controller.</param>
        /// <param name="ctrl">The control.</param>
        /// <param name="message">The message.</param>
        /// <returns>A script.</returns>
        public static string GetOnClientClickConfirm(IClientResourceController clientResourceController, DotNetNuke.UI.Utilities.IClientApiControl ctrl, MessageWindowParameters message)
        {
            AddMessageWindow(clientResourceController, ctrl);

            // function(text, mozEvent, oWidth, oHeight, callerObj, oTitle)
            return string.Format(
                CultureInfo.InvariantCulture,
                "return postBackConfirm('{0}', event, '{1}', '{2}', '', '{3}');",
                JavaScriptStringEncode(message.Message),
                JavaScriptStringEncode(message.WindowWidth.ToString(CultureInfo.InvariantCulture)),
                JavaScriptStringEncode(message.WindowHeight.ToString(CultureInfo.InvariantCulture)),
                JavaScriptStringEncode(message.Title));
        }

        /// <summary>Encodes a string so that it is safe inside a single quoted JavaScript literal.</summary>
        /// <param name="value">The value to encode.</param>
        /// <returns>The encoded value.</returns>
        /// <remarks>
        /// Replaces <c>System.Web.HttpUtility.JavaScriptStringEncode</c>, which is part of the
        /// <c>System.Web</c> surface this project no longer references.
        /// </remarks>
        public static string JavaScriptStringEncode(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var builder = new System.Text.StringBuilder(value.Length + 8);
            foreach (var character in value)
            {
                switch (character)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\'':
                        builder.Append("\\u0027");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '<':
                        builder.Append("\\u003c");
                        break;
                    case '>':
                        builder.Append("\\u003e");
                        break;
                    case '&':
                        builder.Append("\\u0026");
                        break;
                    default:
                        if (character < ' ')
                        {
                            builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }

                        break;
                }
            }

            return builder.ToString();
        }

        /// <summary>Converts an application-relative (<c>~/…</c>) virtual path to an absolute one.</summary>
        /// <param name="virtualPath">The virtual path.</param>
        /// <returns>An absolute URL path.</returns>
        /// <remarks>
        /// The single shared replacement for <c>System.Web.VirtualPathUtility.ToAbsolute</c>, which
        /// does not exist on modern .NET. The semantics are preserved: a <c>~/</c> path is rooted at
        /// the application path and an already rooted (<c>/</c>) or relative path is returned
        /// unchanged.
        /// </remarks>
        public static string ToAbsolute(string virtualPath)
        {
            if (string.IsNullOrEmpty(virtualPath))
            {
                return virtualPath;
            }

            if (virtualPath.StartsWith("~/", StringComparison.Ordinal))
            {
                var applicationPath = Globals.ApplicationPath;
                return string.IsNullOrEmpty(applicationPath)
                    ? virtualPath.Substring(1)
                    : string.Concat(applicationPath, virtualPath.AsSpan(1));
            }

            return virtualPath;
        }

        /// <summary>Gets the <paramref name="value"/> as a <see cref="string"/>.</summary>
        /// <param name="value">The view state object.</param>
        /// <param name="defaultValue">The default value is <paramref name="value"/> is <see langword="null"/>.</param>
        /// <returns>The string.</returns>
        public static string GetViewStateAsString(object value, string defaultValue)
        {
            if (value != null)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            return defaultValue;
        }

        /// <summary>Register a client script to display an alert.</summary>
        /// <param name="ctrl">The control.</param>
        /// <param name="message">The message.</param>
        public static void RegisterAlertOnPageLoad(DotNetNuke.UI.Utilities.IClientApiControl ctrl, string message)
        {
            RegisterAlertOnPageLoad(ctrl, new MessageWindowParameters(message));
        }

        /// <summary>Register a client script to display an alert.</summary>
        /// <param name="ctrl">The control.</param>
        /// <param name="message">The message.</param>
        public static void RegisterAlertOnPageLoad(DotNetNuke.UI.Utilities.IClientApiControl ctrl, MessageWindowParameters message)
        {
            var page = ctrl?.Page;
            if (page == null)
            {
                return;
            }

            page.RegisterClientScriptBlock(ctrl.GetType(), ctrl.Id + "_AlertOnPageLoad", GetClientAlert(ctrl, message));
        }

        /// <summary>Resolves a virtual path to a physical path under the application root.</summary>
        /// <param name="virtualPath">The application relative (or rooted) virtual path.</param>
        /// <returns>The physical path.</returns>
        /// <remarks>Replaces <c>HttpContext.Current.Server.MapPath</c>, which does not exist on ASP.NET Core.</remarks>
        internal static string MapVirtualPath(string virtualPath)
        {
            var relative = (virtualPath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar).TrimStart('~', Path.DirectorySeparatorChar);
            var appPath = Globals.GetCurrentServiceProvider().GetRequiredService<DotNetNuke.Abstractions.Application.IApplicationStatusInfo>().ApplicationMapPath;
            return Path.Combine(appPath, relative);
        }

        private static void AddMessageWindow(IClientResourceController clientResourceController, DotNetNuke.UI.Utilities.IClientApiControl ctrl)
        {
            clientResourceController.RegisterScript(ToAbsolute("~/js/dnn.postbackconfirm.js"));
        }
    }
}
