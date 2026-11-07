// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Utilities
{
    using System;
    using System.Collections;
    using System.Globalization;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Services.Personalization;

    using Microsoft.AspNetCore.Http;

    /// <summary>Library responsible for interacting with DNN Client API.</summary>
    /// <remarks>
    /// The WebForms signatures took <c>System.Web.UI.Page</c> and <c>System.Web.UI.Control</c>. They now
    /// take the Client API abstractions <see cref="IClientApiPage"/> and <see cref="IClientApiControl"/>
    /// (declared in <c>DotNetNuke.WebUtility</c>), which is the ASP.NET Core seam for per-page client
    /// state. Nothing else about the behaviour changed.
    /// </remarks>
    public partial class DNNClientAPI
    {
        private static readonly Hashtable ObjEnabledClientPersonalizationKeys = new Hashtable();

        public enum MinMaxPersistanceType
        {
            /// <summary>No min-max persistence.</summary>
            None = 0,

            /// <summary>Store min-max via page state.</summary>
            Page = 1,

            /// <summary>Store min-max via cookie.</summary>
            Cookie = 2,

            /// <summary>Store min-max via personalization.</summary>
            Personalization = 3,
        }

        public enum PageCallBackType
        {
            /// <summary>Get personalization.</summary>
            GetPersonalization = 0,

            /// <summary>Set personalization.</summary>
            SetPersonalization = 1,
        }

        /// <summary>Sets the initial client side focus to the given control.</summary>
        /// <param name="objPage">The page.</param>
        /// <param name="control">The control to focus.</param>
        public static void SetInitialFocus(IClientApiPage objPage, IClientApiControl control)
        {
            AddBodyOnLoad(objPage, "__dnn_SetInitialFocus", "__dnn_SetInitialFocus('" + control.ClientId + "');");
        }

        /// <summary>Restores the scroll position of the page on load.</summary>
        /// <param name="objPage">The page.</param>
        public static void SetScrollTop(IClientApiPage objPage)
        {
            AddBodyOnLoad(objPage, "__dnn_setScrollTop", "__dnn_setScrollTop();");
        }

        /// <summary>Enables the client side drag and drop behaviour on the page.</summary>
        /// <param name="objPage">The page.</param>
        public static void EnableDragDrop(IClientApiPage objPage)
        {
            AddBodyOnLoad(objPage, "__dnn_enableDragDrop", "__dnn_enableDragDrop();");
        }

        /// <summary>Shows the given URL in DNN's modal popup once the page has loaded.</summary>
        /// <param name="objPage">The page.</param>
        /// <param name="url">The URL to show.</param>
        public static void ShowModalPage(IClientApiPage objPage, string url)
        {
            AddBodyOnLoad(objPage, "__dnn_ShowModalPage", "__dnn_ShowModalPage('" + url + "');");
        }

        /// <summary>Initializes the client side file manager.</summary>
        /// <param name="objPage">The page.</param>
        public static void InitFileManager(IClientApiPage objPage)
        {
            AddBodyOnLoad(objPage, "initFileManager", "initFileManager();");
        }

        /// <summary>Allows any module to have drag and drop functionality enabled.</summary>
        /// <param name="objTitle">Title element that responds to the click and dragged.</param>
        /// <param name="objContainer">Container.</param>
        /// <param name="moduleID">Module ID.</param>
        /// <remarks>
        /// This sub also will send down information to notify the client of the panes that have been defined in the current skin.
        /// </remarks>
        public static void EnableContainerDragAndDrop(IClientApiControl objTitle, IClientApiControl objContainer, int moduleID)
        {
            if (ClientAPI.ClientAPIDisabled() == false && ClientAPI.BrowserSupportsFunctionality(ClientAPI.ClientFunctionality.Positioning))
            {
                var page = objTitle.Page;
                EnableDragDrop(page);

                JavaScript.RegisterClientReference(page?.HttpContext ?? HttpContextSource.Current, ClientAPI.ClientNamespaceReferences.dnn_dom_positioning);
                ClientAPI.RegisterClientVariable(page, "__dnn_dragDrop", objContainer.ClientId + " " + objTitle.ClientId + " " + moduleID + ";", false);
                string strPanes = string.Empty;
                string strPaneNames = string.Empty;
                var objPortalSettings = PortalSettings.Current;

                if (objPortalSettings?.ActiveTab != null)
                {
                    foreach (string strPane in objPortalSettings.ActiveTab.Panes)
                    {
                        // Common.Globals.FindControlRecursive has no Blazor analogue and was removed; the Client API
                        // keeps its own control-tree walker over the IClientApiControl abstraction.
                        var objCtl = Globals.FindControlRecursive(objContainer.Parent, strPane);
                        if (objCtl != null)
                        {
                            strPanes += objCtl.ClientId + ";";
                        }

                        strPaneNames += strPane + ";";
                    }
                }

                ClientAPI.RegisterClientVariable(page, "__dnn_Panes", strPanes, true);
                ClientAPI.RegisterClientVariable(page, "__dnn_PaneNames", strPaneNames, true);
            }
        }

        /// <summary>Allows a button and a content area to support client side min/max functionality.</summary>
        /// <param name="objButton">Control that when clicked causes content area to be hidden/shown.</param>
        /// <param name="objContent">Content area that is hidden/shown.</param>
        /// <param name="blnDefaultMin">If content area is to be defaulted to minimized pass in true.</param>
        /// <param name="ePersistanceType">How to store current state of min/max.  Cookie, Page, None.</param>
        /// <remarks>
        /// This method's purpose is to provide a higher level of abstraction between the ClientAPI and the module developer.
        /// </remarks>
        public static void EnableMinMax(IClientApiControl objButton, IClientApiControl objContent, bool blnDefaultMin, MinMaxPersistanceType ePersistanceType)
        {
            EnableMinMax(objButton, objContent, -1, blnDefaultMin, string.Empty, string.Empty, ePersistanceType);
        }

        /// <summary>Allows a button and a content area to support client side min/max functionality.</summary>
        /// <param name="objButton">Control that when clicked causes content area to be hidden/shown.</param>
        /// <param name="objContent">Content area that is hidden/shown.</param>
        /// <param name="intModuleId">Module id of button/content, used only for persistance type of Cookie.</param>
        /// <param name="blnDefaultMin">If content area is to be defaulted to minimized pass in true.</param>
        /// <param name="ePersistanceType">How to store current state of min/max.  Cookie, Page, None.</param>
        public static void EnableMinMax(IClientApiControl objButton, IClientApiControl objContent, int intModuleId, bool blnDefaultMin, MinMaxPersistanceType ePersistanceType)
        {
            EnableMinMax(objButton, objContent, intModuleId, blnDefaultMin, string.Empty, string.Empty, ePersistanceType);
        }

        /// <summary>Allows a button and a content area to support client side min/max functionality.</summary>
        /// <param name="objButton">Control that when clicked causes content area to be hidden/shown.</param>
        /// <param name="objContent">Content area that is hidden/shown.</param>
        /// <param name="blnDefaultMin">If content area is to be defaulted to minimized pass in true.</param>
        /// <param name="strMinIconLoc">Location of minimized icon.</param>
        /// <param name="strMaxIconLoc">Location of maximized icon.</param>
        /// <param name="ePersistanceType">How to store current state of min/max.  Cookie, Page, None.</param>
        public static void EnableMinMax(IClientApiControl objButton, IClientApiControl objContent, bool blnDefaultMin, string strMinIconLoc, string strMaxIconLoc, MinMaxPersistanceType ePersistanceType)
        {
            EnableMinMax(objButton, objContent, -1, blnDefaultMin, strMinIconLoc, strMaxIconLoc, ePersistanceType);
        }

        /// <summary>Allows a button and a content area to support client side min/max functionality.</summary>
        /// <param name="objButton">Control that when clicked causes content area to be hidden/shown.</param>
        /// <param name="objContent">Content area that is hidden/shown.</param>
        /// <param name="intModuleId">Module id of button/content, used only for persistance type of Cookie.</param>
        /// <param name="blnDefaultMin">If content area is to be defaulted to minimized pass in true.</param>
        /// <param name="strMinIconLoc">Location of minimized icon.</param>
        /// <param name="strMaxIconLoc">Location of maximized icon.</param>
        /// <param name="ePersistanceType">How to store current state of min/max.  Cookie, Page, None.</param>
        /// <remarks>
        /// This method's purpose is to provide a higher level of abstraction between the ClientAPI and the module developer.
        /// </remarks>
        public static void EnableMinMax(IClientApiControl objButton, IClientApiControl objContent, int intModuleId, bool blnDefaultMin, string strMinIconLoc, string strMaxIconLoc, MinMaxPersistanceType ePersistanceType)
        {
            EnableMinMax(objButton, objContent, intModuleId, blnDefaultMin, strMinIconLoc, strMaxIconLoc, ePersistanceType, 5);
        }

        /// <summary>Allows a button and a content area to support client side min/max functionality.</summary>
        /// <param name="objButton">Control that when clicked causes content area to be hidden/shown.</param>
        /// <param name="objContent">Content area that is hidden/shown.</param>
        /// <param name="blnDefaultMin">If content area is to be defaulted to minimized pass in true.</param>
        /// <param name="strMinIconLoc">Location of minimized icon.</param>
        /// <param name="strMaxIconLoc">Location of maximized icon.</param>
        /// <param name="ePersistanceType">How to store current state of min/max.  Cookie, Page, None.</param>
        /// <param name="strPersonalizationNamingCtr">The personalization naming container.</param>
        /// <param name="strPersonalizationKey">The personalization key.</param>
        public static void EnableMinMax(IClientApiControl objButton, IClientApiControl objContent, bool blnDefaultMin, string strMinIconLoc, string strMaxIconLoc, MinMaxPersistanceType ePersistanceType, string strPersonalizationNamingCtr, string strPersonalizationKey)
        {
            EnableMinMax(objButton, objContent, -1, blnDefaultMin, strMinIconLoc, strMaxIconLoc, ePersistanceType, 5, strPersonalizationNamingCtr, strPersonalizationKey);
        }

        /// <summary>Allows a button and a content area to support client side min/max functionality.</summary>
        /// <param name="objButton">Control that when clicked causes content area to be hidden/shown.</param>
        /// <param name="objContent">Content area that is hidden/shown.</param>
        /// <param name="intModuleId">Module id of button/content, used only for persistance type of Cookie.</param>
        /// <param name="blnDefaultMin">If content area is to be defaulted to minimized pass in true.</param>
        /// <param name="strMinIconLoc">Location of minimized icon.</param>
        /// <param name="strMaxIconLoc">Location of maximized icon.</param>
        /// <param name="ePersistanceType">How to store current state of min/max.  Cookie, Page, None.</param>
        /// <param name="intAnimationFrames">The number of animation frames.</param>
        public static void EnableMinMax(IClientApiControl objButton, IClientApiControl objContent, int intModuleId, bool blnDefaultMin, string strMinIconLoc, string strMaxIconLoc, MinMaxPersistanceType ePersistanceType, int intAnimationFrames)
        {
            EnableMinMax(objButton, objContent, intModuleId, blnDefaultMin, strMinIconLoc, strMaxIconLoc, ePersistanceType, intAnimationFrames, null, null);
        }

        /// <summary>Allows a button and a content area to support client side min/max functionality.</summary>
        /// <param name="objButton">Control that when clicked causes content area to be hidden/shown.</param>
        /// <param name="objContent">Content area that is hidden/shown.</param>
        /// <param name="intModuleId">Module id of button/content, used only for persistance type of Cookie.</param>
        /// <param name="blnDefaultMin">If content area is to be defaulted to minimized pass in true.</param>
        /// <param name="strMinIconLoc">Location of minimized icon.</param>
        /// <param name="strMaxIconLoc">Location of maximized icon.</param>
        /// <param name="ePersistanceType">How to store current state of min/max.  Cookie, Page, None.</param>
        /// <param name="intAnimationFrames">The number of animation frames.</param>
        /// <param name="strPersonalizationNamingCtr">The personalization naming container.</param>
        /// <param name="strPersonalizationKey">The personalization key.</param>
        public static void EnableMinMax(IClientApiControl objButton, IClientApiControl objContent, int intModuleId, bool blnDefaultMin, string strMinIconLoc, string strMaxIconLoc, MinMaxPersistanceType ePersistanceType, int intAnimationFrames, string strPersonalizationNamingCtr, string strPersonalizationKey)
        {
            if (ClientAPI.BrowserSupportsFunctionality(ClientAPI.ClientFunctionality.DHTML))
            {
                JavaScript.RegisterClientReference(objButton.Page?.HttpContext ?? HttpContextSource.Current, ClientAPI.ClientNamespaceReferences.dnn_dom);
                switch (ePersistanceType)
                {
                    case MinMaxPersistanceType.None:
                        AddAttribute(objButton, "onclick", "if (__dnn_SectionMaxMin(this,  '" + objContent.ClientId + "')) return false;");
                        if (!string.IsNullOrEmpty(strMinIconLoc))
                        {
                            AddAttribute(objButton, "max_icon", strMaxIconLoc);
                            AddAttribute(objButton, "min_icon", strMinIconLoc);
                        }

                        break;
                    case MinMaxPersistanceType.Page:
                        AddAttribute(objButton, "onclick", "if (__dnn_SectionMaxMin(this,  '" + objContent.ClientId + "')) return false;");
                        if (!string.IsNullOrEmpty(strMinIconLoc))
                        {
                            AddAttribute(objButton, "max_icon", strMaxIconLoc);
                            AddAttribute(objButton, "min_icon", strMinIconLoc);
                        }

                        break;
                    case MinMaxPersistanceType.Cookie:
                        if (intModuleId != -1)
                        {
                            AddAttribute(objButton, "onclick", "if (__dnn_ContainerMaxMin_OnClick(this, '" + objContent.ClientId + "')) return false;");
                            ClientAPI.RegisterClientVariable(objButton.Page, "containerid_" + objContent.ClientId, intModuleId.ToString(CultureInfo.InvariantCulture), true);
                            ClientAPI.RegisterClientVariable(objButton.Page, "cookieid_" + objContent.ClientId, "_Module" + intModuleId + "_Visible", true);

                            ClientAPI.RegisterClientVariable(objButton.Page, "min_icon_" + intModuleId, strMinIconLoc, true);
                            ClientAPI.RegisterClientVariable(objButton.Page, "max_icon_" + intModuleId, strMaxIconLoc, true);

                            ClientAPI.RegisterClientVariable(objButton.Page, "max_text", Localization.GetString("Maximize"), true);
                            ClientAPI.RegisterClientVariable(objButton.Page, "min_text", Localization.GetString("Minimize"), true);

                            if (blnDefaultMin)
                            {
                                ClientAPI.RegisterClientVariable(objButton.Page, "__dnn_" + intModuleId + ":defminimized", "true", true);
                            }
                        }

                        break;
                    case MinMaxPersistanceType.Personalization:
                        // Regardless if we determine whether or not the browser supports client-side personalization
                        // we need to store these keys to properly display or hide the content (They are needed in MinMaxContentVisible)
                        AddAttribute(objButton, "userctr", strPersonalizationNamingCtr);
                        AddAttribute(objButton, "userkey", strPersonalizationKey);
                        if (EnableClientPersonalization(strPersonalizationNamingCtr, strPersonalizationKey, objButton.Page))
                        {
                            AddAttribute(objButton, "onclick", "if (__dnn_SectionMaxMin(this,  '" + objContent.ClientId + "')) return false;");
                            if (!string.IsNullOrEmpty(strMinIconLoc))
                            {
                                AddAttribute(objButton, "max_icon", strMaxIconLoc);
                                AddAttribute(objButton, "min_icon", strMinIconLoc);
                            }
                        }

                        break;
                }
            }

            if (MinMaxContentVisibile(objButton, intModuleId, blnDefaultMin, ePersistanceType))
            {
                // The WebForms fallback for a non-DHTML browser flipped Control.Visible, which has no
                // analogue on the Client API abstraction; the rendered result (shown/hidden) is the same.
                AddStyleAttribute(objContent, "display", string.Empty);

                if (!string.IsNullOrEmpty(strMinIconLoc))
                {
                    SetMinMaxProperties(objButton, strMinIconLoc, Localization.GetString("Minimize"), Localization.GetString("Minimize"));
                }
            }
            else
            {
                AddStyleAttribute(objContent, "display", "none");

                if (!string.IsNullOrEmpty(strMaxIconLoc))
                {
                    SetMinMaxProperties(objButton, strMaxIconLoc, Localization.GetString("Maximize"), Localization.GetString("Maximize"));
                }
            }

            if (intAnimationFrames != 5)
            {
                ClientAPI.RegisterClientVariable(objButton.Page, "animf_" + objContent.ClientId, intAnimationFrames.ToString(CultureInfo.InvariantCulture), true);
            }
        }

        /// <summary>Determines whether the min/max content area is currently visible.</summary>
        /// <param name="objButton">The min/max button.</param>
        /// <param name="blnDefaultMin">Whether the content defaults to minimized.</param>
        /// <param name="ePersistanceType">How the current state is stored.</param>
        /// <returns><see langword="true"/> when the content is visible.</returns>
        public static bool MinMaxContentVisibile(IClientApiControl objButton, bool blnDefaultMin, MinMaxPersistanceType ePersistanceType)
        {
            return MinMaxContentVisibile(objButton, -1, blnDefaultMin, ePersistanceType);
        }

        /// <summary>Stores whether the min/max content area is visible.</summary>
        /// <param name="objButton">The min/max button.</param>
        /// <param name="blnDefaultMin">Whether the content defaults to minimized.</param>
        /// <param name="ePersistanceType">How the current state is stored.</param>
        /// <param name="value">The new visibility.</param>
        public static void MinMaxContentVisibile(IClientApiControl objButton, bool blnDefaultMin, MinMaxPersistanceType ePersistanceType, bool value)
        {
            MinMaxContentVisibile(objButton, -1, blnDefaultMin, ePersistanceType, value);
        }

        /// <summary>Determines whether the min/max content area is currently visible.</summary>
        /// <param name="objButton">The min/max button.</param>
        /// <param name="intModuleId">The module id, used only for the cookie persistence type.</param>
        /// <param name="blnDefaultMin">Whether the content defaults to minimized.</param>
        /// <param name="ePersistanceType">How the current state is stored.</param>
        /// <returns><see langword="true"/> when the content is visible.</returns>
        public static bool MinMaxContentVisibile(IClientApiControl objButton, int intModuleId, bool blnDefaultMin, MinMaxPersistanceType ePersistanceType)
        {
            var context = HttpContextSource.Current;
            if (context != null)
            {
                switch (ePersistanceType)
                {
                    case MinMaxPersistanceType.Page:
                        string sExpanded = ClientAPI.GetClientVariable(objButton.Page, objButton.ClientId + ":exp");
                        if (!string.IsNullOrEmpty(sExpanded))
                        {
                            return sExpanded == "1" ? true : false;
                        }
                        else
                        {
                            return !blnDefaultMin;
                        }

                    case MinMaxPersistanceType.Cookie:
                        if (intModuleId != -1)
                        {
                            var moduleVisible = context.Request.Cookies["_Module" + intModuleId + "_Visible"];
                            if (moduleVisible != null)
                            {
                                return moduleVisible != "false";
                            }
                            else
                            {
                                return !blnDefaultMin;
                            }
                        }
                        else
                        {
                            return true;
                        }

                    case MinMaxPersistanceType.Personalization:
                        string strVisible = Convert.ToString(Personalization.GetProfile(Globals.GetAttribute(objButton, "userctr"), Globals.GetAttribute(objButton, "userkey")), CultureInfo.InvariantCulture);
                        if (string.IsNullOrEmpty(strVisible))
                        {
                            return blnDefaultMin;
                        }
                        else
                        {
                            return Convert.ToBoolean(strVisible);
                        }

                    default:
                        return !blnDefaultMin;
                }
            }

            return Null.NullBoolean;
        }

        /// <summary>Stores whether the min/max content area is visible.</summary>
        /// <param name="objButton">The min/max button.</param>
        /// <param name="intModuleId">The module id, used only for the cookie persistence type.</param>
        /// <param name="blnDefaultMin">Whether the content defaults to minimized.</param>
        /// <param name="ePersistanceType">How the current state is stored.</param>
        /// <param name="value">The new visibility.</param>
        public static void MinMaxContentVisibile(IClientApiControl objButton, int intModuleId, bool blnDefaultMin, MinMaxPersistanceType ePersistanceType, bool value)
        {
            var context = HttpContextSource.Current;
            if (context != null)
            {
                switch (ePersistanceType)
                {
                    case MinMaxPersistanceType.Page:
                        ClientAPI.RegisterClientVariable(objButton.Page, objButton.ClientId + ":exp", Convert.ToInt32(value).ToString(CultureInfo.InvariantCulture), true);
                        break;
                    case MinMaxPersistanceType.Cookie:
                        context.Response.Cookies.Append(
                            "_Module" + intModuleId + "_Visible",
                            value.ToString().ToLowerInvariant(),
                            new CookieOptions
                            {
                                Expires = DateTimeOffset.MaxValue,
                                Path = !string.IsNullOrEmpty(Common.Globals.ApplicationPath) ? Common.Globals.ApplicationPath : "/",
                            });
                        break;
                    case MinMaxPersistanceType.Personalization:
                        Personalization.SetProfile(Globals.GetAttribute(objButton, "userctr"), Globals.GetAttribute(objButton, "userkey"), value.ToString());
                        break;
                }
            }
        }

        /// <summary>Enables callbacks for the request and registers a personalization key to be accessible from the client.</summary>
        /// <param name="strNamingContainer">The naming container of the personalization key.</param>
        /// <param name="strKey">The personalization key.</param>
        /// <param name="objPage">The page.</param>
        /// <returns><see langword="true"/> when the browser is capable of callbacks.</returns>
        public static bool EnableClientPersonalization(string strNamingContainer, string strKey, IClientApiPage objPage)
        {
            if (ClientAPI.BrowserSupportsFunctionality(ClientAPI.ClientFunctionality.XMLHTTP))
            {
                // Instead of sending the callback js function down to the client, we are hardcoding
                // it on the client.  DNN owns the interface, so there is no worry about an outside
                // entity changing it on us.  We are simply calling this here to register all the appropriate
                // js libraries
                ClientAPI.GetCallbackEventReference(objPage, string.Empty, string.Empty, string.Empty, string.Empty);

                // in order to limit the keys that can be accessed and written we are storing the enabled keys
                // in this shared hash table
                lock (ObjEnabledClientPersonalizationKeys.SyncRoot)
                {
                    if (IsPersonalizationKeyRegistered(strNamingContainer + ClientAPI.CUSTOM_COLUMN_DELIMITER + strKey) == false)
                    {
                        ObjEnabledClientPersonalizationKeys.Add(strNamingContainer + ClientAPI.CUSTOM_COLUMN_DELIMITER + strKey, string.Empty);
                    }
                }

                return true;
            }

            return false;
        }

        /// <summary>Determines whether the given personalization key was registered for client side access.</summary>
        /// <param name="strKey">The key.</param>
        /// <returns><see langword="true"/> when the key is registered.</returns>
        public static bool IsPersonalizationKeyRegistered(string strKey)
        {
            return ObjEnabledClientPersonalizationKeys.ContainsKey(strKey);
        }

        private static void AddBodyOnLoad(IClientApiPage objPage, string scriptKey, string strJSFunction)
        {
            if (objPage == null)
            {
                throw new InvalidOperationException(
                    $"Cannot register the '{scriptKey}' body onload script: no client API page was supplied. " +
                    "Register a scoped IClientApiPage in the host (see UseDnnWebUtility).");
            }

            JavaScript.RegisterClientReference(objPage.HttpContext ?? HttpContextSource.Current, ClientAPI.ClientNamespaceReferences.dnn);
            objPage.RegisterStartupScript(objPage.GetType(), scriptKey, strJSFunction, true);
        }

        private static void SetMinMaxProperties(IClientApiControl objButton, string strImage, string strToolTip, string strAltText)
        {
            // WebForms inspected the concrete control type (LinkButton / Image / ImageButton). The Client API
            // abstraction has no typed controls, so the image properties are written as the markup attributes
            // those controls rendered: a link button wrapping an image gets the tooltip and delegates the
            // image attributes to its first child; an image control carries them itself.
            if (objButton == null)
            {
                return;
            }

            objButton.Attributes["title"] = strToolTip;

            if (objButton.HasChildren)
            {
                SetImageProperties(objButton.Children[0], strImage, strToolTip, strAltText);
            }
            else
            {
                SetImageProperties(objButton, strImage, strToolTip, strAltText);
            }
        }

        private static void SetImageProperties(IClientApiControl objControl, string strImage, string strToolTip, string strAltText)
        {
            if (objControl == null)
            {
                return;
            }

            objControl.Attributes["src"] = strImage;
            objControl.Attributes["alt"] = strAltText;
            objControl.Attributes["title"] = strToolTip;
        }

        private static void AddAttribute(IClientApiControl objControl, string strName, string strValue)
        {
            if (objControl != null)
            {
                objControl.Attributes[strName] = strValue;
            }
        }

        private static void AddStyleAttribute(IClientApiControl objControl, string strName, string strValue)
        {
            if (objControl == null)
            {
                return;
            }

            // WebForms kept a parsed style bag (HtmlControl.Style / WebControl.Style). On the Client API
            // abstraction there is only the raw attribute bag, so the inline style declarations are parsed,
            // the single declaration is added/removed, and the bag is written back.
            objControl.Attributes.TryGetValue("style", out var existingStyle);
            var declarations = new System.Collections.Specialized.OrderedDictionary(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(existingStyle))
            {
                foreach (var declaration in existingStyle.Split(';'))
                {
                    if (string.IsNullOrWhiteSpace(declaration))
                    {
                        continue;
                    }

                    var separator = declaration.IndexOf(':');
                    if (separator <= 0)
                    {
                        continue;
                    }

                    declarations[declaration.Substring(0, separator).Trim()] = declaration.Substring(separator + 1).Trim();
                }
            }

            if (!string.IsNullOrEmpty(strValue))
            {
                declarations[strName] = strValue;
            }
            else
            {
                declarations.Remove(strName);
            }

            if (declarations.Count == 0)
            {
                objControl.Attributes.Remove("style");
                return;
            }

            var builder = new System.Text.StringBuilder();
            foreach (System.Collections.DictionaryEntry declaration in declarations)
            {
                builder.Append(declaration.Key).Append(':').Append(declaration.Value).Append(';');
            }

            objControl.Attributes["style"] = builder.ToString();
        }
    }
}
