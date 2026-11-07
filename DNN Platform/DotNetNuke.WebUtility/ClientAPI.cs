// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Utilities
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Reflection;

    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Web.Client.ResourceManager;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Library responsible for interacting with DNN Client API.
    /// </summary>
    /// <remarks>
    /// Originally written by Jon Henning (8/3/2004). The WebForms <c>Control</c> / <c>Page</c> /
    /// <c>WebControl</c> parameters are replaced by the <see cref="IClientApiControl"/> and
    /// <see cref="IClientApiPage"/> abstractions; every emitted client side script is unchanged.
    /// </remarks>
    public partial class ClientAPI
    {
#pragma warning disable CA1707 // Identifiers should not contain underscores - breaking change
#pragma warning disable SA1310 // Field names should not contain underscore - breaking change

        /// <summary>The name of the request field holding the identifier of the control being called back.</summary>
        public const string SCRIPT_CALLBACKID = "__DNNCAPISCI";

        /// <summary>The name of the request field holding the type of the callback.</summary>
        public const string SCRIPT_CALLBACKTYPE = "__DNNCAPISCT";

        /// <summary>The name of the request field holding the argument of the callback.</summary>
        public const string SCRIPT_CALLBACKPARAM = "__DNNCAPISCP";

        /// <summary>The identifier used for the page itself, which normally has no identifier of its own.</summary>
        public const string SCRIPT_CALLBACKPAGEID = "__DNNCAPISCPAGEID";

        /// <summary>The name of the response field holding the status code of a callback.</summary>
        public const string SCRIPT_CALLBACKSTATUSID = "__DNNCAPISCSI";

        /// <summary>The name of the response field holding the status description of a callback.</summary>
        public const string SCRIPT_CALLBACKSTATUSDESCID = "__DNNCAPISCSDI";

        /// <summary>The identifier of the hidden field the client side variables are round-tripped in.</summary>
        public const string DNNVARIABLE_CONTROLID = "__dnnVariable";

#pragma warning restore SA1310
#pragma warning restore CA1707

        /// <summary>The key the parsed client variable dictionary is cached under for the current request.</summary>
        private const string ClientVariableListKey = "CAPIVariableList";

        /// <summary>The key marking that the pre-render serialization handler has been hooked up.</summary>
        private const string PreRenderKey = "CAPIPreRender";

        /// <summary>The key marking that the pre-render event has already passed.</summary>
        private const string PostPreRenderKey = "CAPIPostPreRender";

        /// <summary>Private variable holding location of client side js files. Shared by entire application.</summary>
        private static string scriptPath;

#pragma warning disable CA1034 // Nested types should not be visible - breaking change
#pragma warning disable CA1707 // Identifiers should not contain underscores - breaking change
#pragma warning disable SA1300 // Element should begin with upper-case letter - breaking change

        /// <summary>
        /// Enumerates the client side functionalities a browser may or may not support.
        /// </summary>
        public enum ClientFunctionality
        {
            /// <summary>Dynamic HTML.</summary>
            DHTML = 1,

            /// <summary>Client side XML parsing.</summary>
            XML = 2,

            /// <summary>Client side XSL transformations.</summary>
            XSLT = 4,

            /// <summary>What we would call adaquate positioning support.</summary>
            Positioning = 8,

            /// <summary>Requires the javascript XML parser.</summary>
            XMLJS = 16,

            /// <summary>Client side HTTP requests.</summary>
            XMLHTTP = 32,

            /// <summary>Requires the javascript HTTP request implementation.</summary>
            XMLHTTPJS = 64,

            /// <summary>Supports the single character delimiters used to minimize the payload.</summary>
            SingleCharDelimiters = 128,

            /// <summary>Scripts are referenced as external files instead of being embedded.</summary>
            UseExternalScripts = 256,

            /// <summary>Animation support.</summary>
            Motion = 512,
        }

        /// <summary>
        /// Enumerates each namespace with a separate js file.
        /// </summary>
        public enum ClientNamespaceReferences
        {
            /// <summary>The <c>dnn</c> namespace (dnn.js).</summary>
            dnn = 0,

            /// <summary>The <c>dnn.dom</c> namespace.</summary>
            dnn_dom = 1,

            /// <summary>The <c>dnn.dom.positioning</c> namespace (dnn.dom.positioning.js).</summary>
            dnn_dom_positioning = 2,

            /// <summary>The <c>dnn.xml</c> namespace (dnn.xml.js).</summary>
            dnn_xml = 3,

            /// <summary>The <c>dnn.xmlhttp</c> namespace (dnn.xmlhttp.js).</summary>
            dnn_xmlhttp = 4,

            /// <summary>The <c>dnn.motion</c> namespace (dnn.motion.js).</summary>
            dnn_motion = 5,
        }

#pragma warning restore SA1300
#pragma warning restore CA1707
#pragma warning restore CA1034

#pragma warning disable CA1707 // Identifiers should not contain underscores - breaking change

        /// <summary>Gets the character used for delimiting name from value.</summary>
        [SuppressMessage("Microsoft.Design", "CA1707:IdentifiersShouldNotContainUnderscores", Justification = "Breaking change")]
        public static string COLUMN_DELIMITER
        {
            get
            {
                if (BrowserSupportsFunctionality(ClientFunctionality.SingleCharDelimiters))
                {
                    return "\u0012";
                }
                else
                {
                    return "~|~";
                }
            }
        }

        /// <summary>Gets the character used for delimiting name from value of a custom pair.</summary>
        [SuppressMessage("Microsoft.Design", "CA1707:IdentifiersShouldNotContainUnderscores", Justification = "Breaking change")]
        public static string CUSTOM_COLUMN_DELIMITER
        {
            get
            {
                if (BrowserSupportsFunctionality(ClientFunctionality.SingleCharDelimiters))
                {
                    return "\u0010";
                }
                else
                {
                    return "~.~";
                }
            }
        }

        /// <summary>Gets the character used for delimiting custom name/value pairs.</summary>
        [SuppressMessage("Microsoft.Design", "CA1707:IdentifiersShouldNotContainUnderscores", Justification = "Breaking change")]
        public static string CUSTOM_ROW_DELIMITER
        {
            get
            {
                if (BrowserSupportsFunctionality(ClientFunctionality.SingleCharDelimiters))
                {
                    return "\u000f";
                }
                else
                {
                    return "~,~";
                }
            }
        }

        /// <summary>
        /// Gets the replacement for the quote character.
        /// </summary>
        /// <remarks>
        /// In order to reduce payload, substitute out " with different char, since when put in a
        /// hidden control it uses &amp;quot;.
        /// </remarks>
        [SuppressMessage("Microsoft.Design", "CA1707:IdentifiersShouldNotContainUnderscores", Justification = "Breaking change")]
        public static string QUOTE_REPLACEMENT
        {
            get
            {
                if (BrowserSupportsFunctionality(ClientFunctionality.SingleCharDelimiters))
                {
                    return "\u0013";
                }
                else
                {
                    return "~!~";
                }
            }
        }

        /// <summary>Gets the character used for delimiting name/value pairs.</summary>
        [SuppressMessage("Microsoft.Design", "CA1707:IdentifiersShouldNotContainUnderscores", Justification = "Breaking change")]
        public static string ROW_DELIMITER
        {
            get
            {
                if (BrowserSupportsFunctionality(ClientFunctionality.SingleCharDelimiters))
                {
                    return "\u0011";
                }
                else
                {
                    return "~`~";
                }
            }
        }

#pragma warning restore CA1707

        /// <summary>
        /// Gets or sets the path where the js files are placed.
        /// </summary>
        /// <remarks>
        /// Replaces the <c>Request.ApplicationPath</c> composition of the WebForms implementation
        /// with the request's <see cref="HttpRequest.PathBase"/>, falling back to the configured
        /// <see cref="DnnWebUtilityOptions.ScriptPath"/>. The resulting value is identical for an
        /// application hosted at the root or in a virtual directory.
        /// </remarks>
        public static string ScriptPath
        {
            get
            {
                var script = string.Empty;
                if (!string.IsNullOrEmpty(scriptPath))
                {
                    script = scriptPath;
                }
                else if (!string.IsNullOrEmpty(DnnWebUtilityContext.Options.ScriptPath))
                {
                    script = DnnWebUtilityContext.Options.ScriptPath;
                }
                else
                {
                    var context = DnnWebUtilityContext.Current;
                    if (context != null)
                    {
                        var applicationPath = context.Request.PathBase.HasValue ? context.Request.PathBase.Value : "/";
                        if (applicationPath.EndsWith('/'))
                        {
                            script = applicationPath + "js/";
                        }
                        else
                        {
                            script = applicationPath + "/js/";
                        }
                    }
                }

                return script;
            }

            set
            {
                scriptPath = value;
            }
        }

        /// <summary>Gets a value indicating whether the client scripts are referenced as external files.</summary>
        public static bool UseExternalScripts
        {
            get
            {
                return BrowserSupportsFunctionality(ClientFunctionality.UseExternalScripts);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the Client API is unconditionally enabled.
        /// </summary>
        /// <remarks>
        /// The original VB implementation of <see cref="ClientAPIDisabled"/> short-circuited with
        /// <c>Return False</c> before it ever read the <c>ClientAPI</c> app setting, so the Client
        /// API was always enabled. That behavior is preserved here; removing this short circuit
        /// makes the configured <see cref="DnnWebUtilityOptions.ClientApiDisabled"/> value take effect.
        /// </remarks>
        private static bool ClientApiAlwaysEnabled
        {
            get { return true; }
        }

        /// <summary>Gets the per-request item bag, or <see langword="null"/> when there is no current request.</summary>
        private static IDictionary<object, object> ContextItems
        {
            get { return DnnWebUtilityContext.Current?.Items; }
        }

        /// <summary>
        /// Escapes the backslashes and apostrophes of a string so it can be embedded in a javascript literal.
        /// </summary>
        /// <param name="s">The string to escape.</param>
        /// <returns>The escaped string.</returns>
        public static string EscapeForJavascript(string s)
        {
            return s.Replace("\\", "\\\\").Replace("'", "\\'");
        }

        /// <summary>
        /// Gets the dictionary of the client side variables of the page, deserializing it from the
        /// hidden field on first use and caching it for the duration of the request.
        /// </summary>
        /// <param name="objPage">Current page rendering content.</param>
        /// <returns>The client side variables, never <see langword="null"/>.</returns>
        public static Dictionary<string, string> GetClientVariableList(IClientApiPage objPage)
        {
            var ctlVar = ClientVariableControl(objPage);
            var strValue = string.Empty;
            if (ctlVar != null)
            {
                strValue = ctlVar.Value;
            }

            if (string.IsNullOrEmpty(strValue))
            {
                // using request object in case we are loading before controls have values set
                strValue = GetRequestValue(DNNVARIABLE_CONTROLID);
            }

            if (strValue == null)
            {
                strValue = string.Empty;
            }

            var items = ContextItems;
            Dictionary<string, string> objDict = null;
            if (items != null && items.TryGetValue(ClientVariableListKey, out var cached))
            {
                objDict = cached as Dictionary<string, string>;
            }

            if (objDict == null)
            {
                if (string.IsNullOrEmpty(strValue) == false)
                {
                    try
                    {
                        // fix serialization issues with invalid json objects
                        if (strValue.StartsWith('`'))
                        {
                            strValue = strValue.Substring(1).Replace("`", "\"");
                        }

                        objDict = MSAJAX.Deserialize<Dictionary<string, string>>(strValue);
                    }
                    catch
                    {
                        // ignore error
                    }
                }

                if (objDict == null)
                {
                    objDict = new Dictionary<string, string>();
                }

                if (items != null)
                {
                    items[ClientVariableListKey] = objDict;
                }
            }

            return objDict;
        }

        /// <summary>
        /// Serializes the client side variables back into the page's hidden field.
        /// </summary>
        /// <param name="objPage">Current page rendering content.</param>
        /// <param name="objDict">The client side variables to serialize.</param>
        public static void SerializeClientVariableDictionary(IClientApiPage objPage, Dictionary<string, string> objDict)
        {
            var ctlVar = ClientVariableControl(objPage);
            ctlVar.Value = MSAJAX.Serialize(objDict);

            // minimize payload by using ` for ", which serializes to &quot;
            if (ctlVar.Value.Contains('`') == false)
            {
                // prefix the value with ` to denote that we escaped it (it was safe)
                ctlVar.Value = "`" + ctlVar.Value.Replace("\"", "`");
            }
        }

        /// <summary>
        /// Common way to handle confirmation prompts on client.
        /// </summary>
        /// <param name="objButton">Button to trap click event.</param>
        /// <param name="strText">Text to display in confirmation.</param>
        public static void AddButtonConfirm(IClientApiControl objButton, string strText)
        {
            objButton.Attributes["onClick"] = "javascript:return confirm('" + GetSafeJSString(strText) + "');";
        }

        /// <summary>
        /// Determines of browser currently requesting page adaquately supports passed un client-side functionality.
        /// </summary>
        /// <param name="eFunctionality">Desired Functionality.</param>
        /// <returns>True when browser supports it.</returns>
        public static bool BrowserSupportsFunctionality(ClientFunctionality eFunctionality)
        {
            try
            {
                var context = DnnWebUtilityContext.Current;
                if (context == null)
                {
                    return true;
                }

                var blnSupports = false;

                if (ClientAPIDisabled() == false)
                {
                    var objCaps = BrowserCaps.GetBrowserCaps();
                    if (objCaps != null)
                    {
                        var objRequest = context.Request;
                        var strUserAgent = objRequest.Headers.UserAgent.ToString();
                        if (!string.IsNullOrEmpty(strUserAgent))
                        {
                            // First check whether we have checked this browser before
                            if (objCaps.FunctionalityDictionary.ContainsKey(strUserAgent) == false)
                            {
                                // REVIEW: the browser name and version used to come from the WebForms
                                // Request.Browser capabilities, which do not exist on ASP.NET Core;
                                // they are now derived from the user agent itself.
                                GetBrowserNameAndVersion(strUserAgent, out var strBrowser, out var dblVersion);
                                var iBitValue = 0;
                                FunctionalityInfo objFuncInfo;

                                // loop through all functionalities for this UserAgent and determine the bitvalue
                                foreach (var eFunc in Enum.GetValues<ClientFunctionality>())
                                {
                                    objFuncInfo = objCaps.Functionality[eFunc];
                                    if (objFuncInfo != null && objFuncInfo.HasMatch(strUserAgent, strBrowser, dblVersion))
                                    {
                                        iBitValue += (int)eFunc;
                                    }
                                }

                                objCaps.FunctionalityDictionary[strUserAgent] = iBitValue;
                            }

                            blnSupports = ((int)objCaps.FunctionalityDictionary[strUserAgent] & (int)eFunctionality) != 0;
                        }
                    }
                }

                return blnSupports;
            }
            catch
            {
                // bad user agent (CAP-7321)
            }

            return false;
        }

        /// <summary>Gets the client side script that performs a callback to the given control.</summary>
        /// <param name="objControl">The control handling the callback.</param>
        /// <param name="strArgument">The argument to pass to the server.</param>
        /// <param name="strClientCallBack">The client side function receiving the response.</param>
        /// <param name="strContext">The client side context passed through to the callbacks.</param>
        /// <param name="srtClientErrorCallBack">The client side function receiving failures.</param>
        /// <returns>The client side callback script, or an empty string when the browser does not support callbacks.</returns>
        public static string GetCallbackEventReference(IClientApiControl objControl, string strArgument, string strClientCallBack, string strContext, string srtClientErrorCallBack)
        {
            return GetCallbackEventReference(objControl, strArgument, strClientCallBack, strContext, srtClientErrorCallBack, null, string.Empty);
        }

        /// <summary>Gets the client side script that performs a callback to the given control.</summary>
        /// <param name="objControl">The control handling the callback.</param>
        /// <param name="strArgument">The argument to pass to the server.</param>
        /// <param name="strClientCallBack">The client side function receiving the response.</param>
        /// <param name="strContext">The client side context passed through to the callbacks.</param>
        /// <param name="srtClientErrorCallBack">The client side function receiving failures.</param>
        /// <param name="eCallbackType">The type of the callback.</param>
        /// <returns>The client side callback script, or an empty string when the browser does not support callbacks.</returns>
        public static string GetCallbackEventReference(IClientApiControl objControl, string strArgument, string strClientCallBack, string strContext, string srtClientErrorCallBack, ClientAPICallBackResponse.CallBackTypeCode eCallbackType)
        {
            return GetCallbackEventReference(objControl, strArgument, strClientCallBack, strContext, srtClientErrorCallBack, null, null, eCallbackType);
        }

        /// <summary>Gets the client side script that performs a callback to the given control.</summary>
        /// <param name="objControl">The control handling the callback.</param>
        /// <param name="strArgument">The argument to pass to the server.</param>
        /// <param name="strClientCallBack">The client side function receiving the response.</param>
        /// <param name="strContext">The client side context passed through to the callbacks.</param>
        /// <param name="srtClientErrorCallBack">The client side function receiving failures.</param>
        /// <param name="objPostChildrenOf">The control whose children are posted along with the callback.</param>
        /// <returns>The client side callback script, or an empty string when the browser does not support callbacks.</returns>
        public static string GetCallbackEventReference(IClientApiControl objControl, string strArgument, string strClientCallBack, string strContext, string srtClientErrorCallBack, IClientApiControl objPostChildrenOf)
        {
            return GetCallbackEventReference(objControl, strArgument, strClientCallBack, strContext, srtClientErrorCallBack, null, objPostChildrenOf.ClientId, ClientAPICallBackResponse.CallBackTypeCode.Simple);
        }

        /// <summary>Gets the client side script that performs a callback to the given control.</summary>
        /// <param name="objControl">The control handling the callback.</param>
        /// <param name="strArgument">The argument to pass to the server.</param>
        /// <param name="strClientCallBack">The client side function receiving the response.</param>
        /// <param name="strContext">The client side context passed through to the callbacks.</param>
        /// <param name="srtClientErrorCallBack">The client side function receiving failures.</param>
        /// <param name="strClientStatusCallBack">The client side function receiving status updates.</param>
        /// <returns>The client side callback script, or an empty string when the browser does not support callbacks.</returns>
        public static string GetCallbackEventReference(IClientApiControl objControl, string strArgument, string strClientCallBack, string strContext, string srtClientErrorCallBack, string strClientStatusCallBack)
        {
            return GetCallbackEventReference(objControl, strArgument, strClientCallBack, strContext, srtClientErrorCallBack, strClientStatusCallBack, null, ClientAPICallBackResponse.CallBackTypeCode.Simple);
        }

        /// <summary>Gets the client side script that performs a callback to the given control.</summary>
        /// <param name="objControl">The control handling the callback.</param>
        /// <param name="strArgument">The argument to pass to the server.</param>
        /// <param name="strClientCallBack">The client side function receiving the response.</param>
        /// <param name="strContext">The client side context passed through to the callbacks.</param>
        /// <param name="srtClientErrorCallBack">The client side function receiving failures.</param>
        /// <param name="strClientStatusCallBack">The client side function receiving status updates.</param>
        /// <param name="eCallbackType">The type of the callback.</param>
        /// <returns>The client side callback script, or an empty string when the browser does not support callbacks.</returns>
        public static string GetCallbackEventReference(IClientApiControl objControl, string strArgument, string strClientCallBack, string strContext, string srtClientErrorCallBack, string strClientStatusCallBack, ClientAPICallBackResponse.CallBackTypeCode eCallbackType)
        {
            return GetCallbackEventReference(objControl, strArgument, strClientCallBack, strContext, srtClientErrorCallBack, strClientStatusCallBack, null, eCallbackType);
        }

        /// <summary>Gets the client side script that performs a callback to the given control.</summary>
        /// <param name="objControl">The control handling the callback.</param>
        /// <param name="strArgument">The argument to pass to the server.</param>
        /// <param name="strClientCallBack">The client side function receiving the response.</param>
        /// <param name="strContext">The client side context passed through to the callbacks.</param>
        /// <param name="srtClientErrorCallBack">The client side function receiving failures.</param>
        /// <param name="strClientStatusCallBack">The client side function receiving status updates.</param>
        /// <param name="objPostChildrenOf">The control whose children are posted along with the callback.</param>
        /// <returns>The client side callback script, or an empty string when the browser does not support callbacks.</returns>
        public static string GetCallbackEventReference(IClientApiControl objControl, string strArgument, string strClientCallBack, string strContext, string srtClientErrorCallBack, string strClientStatusCallBack, IClientApiControl objPostChildrenOf)
        {
            return GetCallbackEventReference(objControl, strArgument, strClientCallBack, strContext, srtClientErrorCallBack, strClientStatusCallBack, objPostChildrenOf.ClientId, ClientAPICallBackResponse.CallBackTypeCode.Simple);
        }

        /// <summary>Gets the client side script that performs a callback to the given control.</summary>
        /// <param name="objControl">The control handling the callback.</param>
        /// <param name="strArgument">The argument to pass to the server.</param>
        /// <param name="strClientCallBack">The client side function receiving the response.</param>
        /// <param name="strContext">The client side context passed through to the callbacks.</param>
        /// <param name="srtClientErrorCallBack">The client side function receiving failures.</param>
        /// <param name="strClientStatusCallBack">The client side function receiving status updates.</param>
        /// <param name="strPostChildrenOfId">The client side identifier of the element whose children are posted along with the callback.</param>
        /// <returns>The client side callback script, or an empty string when the browser does not support callbacks.</returns>
        public static string GetCallbackEventReference(IClientApiControl objControl, string strArgument, string strClientCallBack, string strContext, string srtClientErrorCallBack, string strClientStatusCallBack, string strPostChildrenOfId)
        {
            return GetCallbackEventReference(objControl, strArgument, strClientCallBack, strContext, srtClientErrorCallBack, strClientStatusCallBack, strPostChildrenOfId, ClientAPICallBackResponse.CallBackTypeCode.Simple);
        }

        /// <summary>Gets the client side script that performs a callback to the given control.</summary>
        /// <param name="objControl">The control handling the callback.</param>
        /// <param name="strArgument">The argument to pass to the server.</param>
        /// <param name="strClientCallBack">The client side function receiving the response.</param>
        /// <param name="strContext">The client side context passed through to the callbacks.</param>
        /// <param name="srtClientErrorCallBack">The client side function receiving failures.</param>
        /// <param name="strClientStatusCallBack">The client side function receiving status updates.</param>
        /// <param name="strPostChildrenOfId">The client side identifier of the element whose children are posted along with the callback.</param>
        /// <param name="eCallbackType">The type of the callback.</param>
        /// <returns>The client side callback script, or an empty string when the browser does not support callbacks.</returns>
        public static string GetCallbackEventReference(IClientApiControl objControl, string strArgument, string strClientCallBack, string strContext, string srtClientErrorCallBack, string strClientStatusCallBack, string strPostChildrenOfId, ClientAPICallBackResponse.CallBackTypeCode eCallbackType)
        {
            var strCallbackType = ((int)eCallbackType).ToString(CultureInfo.InvariantCulture);
            if (strArgument == null)
            {
                strArgument = "null";
            }

            if (strContext == null)
            {
                strContext = "null";
            }

            if (srtClientErrorCallBack == null)
            {
                srtClientErrorCallBack = "null";
            }

            if (strClientStatusCallBack == null)
            {
                strClientStatusCallBack = "null";
            }

            if (string.IsNullOrEmpty(strPostChildrenOfId))
            {
                strPostChildrenOfId = "null";
            }
            else if (strPostChildrenOfId.StartsWith('\'') == false)
            {
                strPostChildrenOfId = "'" + strPostChildrenOfId + "'";
            }

            var strControlID = objControl.Id;
            if (BrowserSupportsFunctionality(ClientFunctionality.XMLHTTP) && BrowserSupportsFunctionality(ClientFunctionality.XML))
            {
                RegisterClientReference(objControl.Page, ClientNamespaceReferences.dnn_xml);
                RegisterClientReference(objControl.Page, ClientNamespaceReferences.dnn_xmlhttp);

                if (objControl is IClientApiPage && string.IsNullOrEmpty(strControlID))
                {
                    // page doesn't usually have an ID so we need to make one up
                    strControlID = SCRIPT_CALLBACKPAGEID;
                }

                if (objControl is IClientApiPage == false)
                {
                    // ID is not unique (obviously)
                    strControlID = strControlID + " " + objControl.ClientId;
                }

                return string.Format(CultureInfo.InvariantCulture, "dnn.xmlhttp.doCallBack('{0}',{1},{2},{3},{4},{5},{6},{7},{8});", strControlID, strArgument, strClientCallBack, strContext, srtClientErrorCallBack, strClientStatusCallBack, "null", strPostChildrenOfId, strCallbackType);
            }
            else
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Retrieves DNN Client Variable value.
        /// </summary>
        /// <param name="objPage">Current page rendering content.</param>
        /// <param name="strVar">Variable name to retrieve value for.</param>
        /// <returns>Value of variable.</returns>
        public static string GetClientVariable(IClientApiPage objPage, string strVar)
        {
            var strPair = GetClientVariableNameValuePair(objPage, strVar);
            if (strPair.IndexOf(COLUMN_DELIMITER, StringComparison.Ordinal) > -1)
            {
                return strPair.Split(new[] { COLUMN_DELIMITER }, StringSplitOptions.None)[1];
            }
            else
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Retrieves DNN Client Variable value.
        /// </summary>
        /// <param name="objPage">Current page rendering content.</param>
        /// <param name="strVar">Variable name to retrieve value for.</param>
        /// <param name="strDefaultValue">Default value if variable not found.</param>
        /// <returns>Value of variable.</returns>
        public static string GetClientVariable(IClientApiPage objPage, string strVar, string strDefaultValue)
        {
            var strPair = GetClientVariableNameValuePair(objPage, strVar);
            if (strPair.IndexOf(COLUMN_DELIMITER, StringComparison.Ordinal) > -1)
            {
                return strPair.Split(new[] { COLUMN_DELIMITER }, StringSplitOptions.None)[1];
            }
            else
            {
                return strDefaultValue;
            }
        }

        /// <summary>
        /// Escapes string to be safely used in client side javascript.
        /// </summary>
        /// <param name="strString">String to escape.</param>
        /// <returns>Escaped string.</returns>
        /// <remarks>Currently this only escapes out quotes and apostrophes.</remarks>
        public static string GetSafeJSString(string strString)
        {
            if (string.IsNullOrEmpty(strString))
            {
                return string.Empty;
            }

            return global::System.Web.HttpUtility.JavaScriptStringEncode(strString);
        }

        /// <summary>
        /// Determines if DNNVariable control is present in page's control collection.
        /// </summary>
        /// <param name="objParent">The control whose page is inspected.</param>
        /// <returns><see langword="true"/> when the hidden field still needs to be added.</returns>
        public static bool NeedsDNNVariable(IClientApiControl objParent)
        {
            return GetDNNVariableControl(objParent) == null;
        }

        /// <summary>
        /// Responsible for registering client side js libraries and its dependecies.
        /// </summary>
        /// <param name="objPage">Current page rendering content.</param>
        /// <param name="eRef">Enumerator of library to reference.</param>
        public static void RegisterClientReference(IClientApiPage objPage, ClientNamespaceReferences eRef)
        {
            var controller = GetClientResourcesController(objPage);
            switch (eRef)
            {
                case ClientNamespaceReferences.dnn:
                    if (!IsClientScriptBlockRegistered(objPage, "dnn.js"))
                    {
                        RegisterClientScriptBlock(objPage, "dnn.js", "<script src=\"" + ClientAPI.ScriptPath + "dnn.js\"></script>");
                        if (BrowserSupportsFunctionality(ClientFunctionality.SingleCharDelimiters) == false)
                        {
                            // SingleCharDelimiters Off!!!
                            RegisterClientVariable(objPage, "__scdoff", "1", true);
                        }

                        // allow scripts to be dynamically loaded when use external false
                        if (ClientAPI.UseExternalScripts == false)
                        {
                            ClientAPI.RegisterEmbeddedResource(objPage, "dnn.scripts.js", typeof(ClientAPI));
                        }
                    }

                    break;
                case ClientNamespaceReferences.dnn_dom:
                    RegisterClientReference(objPage, ClientNamespaceReferences.dnn);
                    break;
                case ClientNamespaceReferences.dnn_dom_positioning:
                    RegisterClientReference(objPage, ClientNamespaceReferences.dnn);
                    controller.CreateScript(ScriptPath + "dnn.dom.positioning.js").SetPriority(FileOrder.Js.DnnDomPositioning).Register();
                    break;

                case ClientNamespaceReferences.dnn_xml:
                    RegisterClientReference(objPage, ClientNamespaceReferences.dnn);
                    controller.CreateScript(ScriptPath + "dnn.xml.js").SetPriority(FileOrder.Js.DnnXml).Register();

                    if (BrowserSupportsFunctionality(ClientFunctionality.XMLJS))
                    {
                        controller.CreateScript(ScriptPath + "dnn.xml.jsparser.js").SetPriority(FileOrder.Js.DnnXmlJsParser).Register();
                    }

                    break;
                case ClientNamespaceReferences.dnn_xmlhttp:
                    RegisterClientReference(objPage, ClientNamespaceReferences.dnn);
                    controller.CreateScript(ScriptPath + "dnn.xmlhttp.js").SetPriority(FileOrder.Js.DnnXmlHttp).Register();

                    if (BrowserSupportsFunctionality(ClientFunctionality.XMLHTTPJS))
                    {
                        controller.CreateScript(ScriptPath + "dnn.xmlhttp.jsxmlhttprequest.js").SetPriority(FileOrder.Js.DnnXmlHttpJsXmlHttpRequest).Register();
                    }

                    break;
                case ClientNamespaceReferences.dnn_motion:
                    RegisterClientReference(objPage, ClientNamespaceReferences.dnn_dom_positioning);
                    controller.CreateScript(ScriptPath + "dnn.motion.js").Register();
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Registers a client side variable (name/value) pair.
        /// </summary>
        /// <param name="objPage">Current page rendering content.</param>
        /// <param name="strVar">Variable name.</param>
        /// <param name="strValue">Value.</param>
        /// <param name="blnOverwrite">Determins if a replace or append is applied when variable already exists.</param>
        public static void RegisterClientVariable(IClientApiPage objPage, string strVar, string strValue, bool blnOverwrite)
        {
            // only add once
            var objDict = GetClientVariableList(objPage);
            if (objDict.TryAdd(strVar, strValue) == false)
            {
                if (blnOverwrite)
                {
                    objDict[strVar] = strValue;
                }
                else
                {
                    // appending value
                    objDict[strVar] += strValue;
                }
            }

            var items = ContextItems;

            // instead of serializing each time, do it once
            if (items != null && items.ContainsKey(PreRenderKey) == false)
            {
                GetDNNVariableControl(objPage).PreRender += CAPIPreRender;
                items[PreRenderKey] = true;
            }

            // if we are past prerender event then we need to keep serializing
            if (items != null && items.ContainsKey(PostPreRenderKey))
            {
                SerializeClientVariableDictionary(objPage, objDict);
            }
        }

        /// <summary>
        /// Responsible for inputting the hidden field necessary for the ClientAPI to pass variables back in forth.
        /// </summary>
        /// <param name="objParent">The control whose page the hidden field belongs to.</param>
        /// <returns>The hidden field, or <see langword="null"/> when the page has no form to add it to.</returns>
        public static IClientApiHiddenInput RegisterDNNVariableControl(IClientApiControl objParent)
        {
            var variableHiddenInput = GetDNNVariableControl(objParent);

            if (variableHiddenInput != null)
            {
                return variableHiddenInput;
            }
            else
            {
                var oForm = FindForm(objParent);
                if (oForm != null)
                {
                    var nonNamingHiddenInput = new NonNamingHiddenInput();
                    nonNamingHiddenInput.Id = DNNVARIABLE_CONTROLID;
                    oForm.Children.Add(nonNamingHiddenInput);
                    return nonNamingHiddenInput;
                }
            }

            return null;
        }

        /// <summary>
        /// Traps client side keydown event looking for passed in key press (ASCII) and hooks it up
        /// with server side postback handler.
        /// </summary>
        /// <param name="objControl">Control that should trap the keydown.</param>
        /// <param name="objPostbackControl">Server-side control that has its onclick event handled server-side.</param>
        /// <param name="intKeyAscii">ASCII value of key to trap.</param>
        public static void RegisterKeyCapture(IClientApiControl objControl, IClientApiControl objPostbackControl, int intKeyAscii)
        {
            Globals.SetAttribute(objControl, "onkeydown", GetKeyDownHandler(intKeyAscii, GetPostBackClientHyperlink(objPostbackControl, string.Empty)));
        }

        /// <summary>
        /// Traps client side keydown event looking for passed in key press (ASCII) and hooks it up
        /// with client-side javascript.
        /// </summary>
        /// <param name="objControl">Control that should trap the keydown.</param>
        /// <param name="strJavascript">Javascript to execute when event fires.</param>
        /// <param name="intKeyAscii">ASCII value of key to trap.</param>
        public static void RegisterKeyCapture(IClientApiControl objControl, string strJavascript, int intKeyAscii)
        {
            Globals.SetAttribute(objControl, "onkeydown", GetKeyDownHandler(intKeyAscii, strJavascript));
        }

        /// <summary>
        /// Allows a listener to be associated to a client side post back.
        /// </summary>
        /// <param name="objParent">The current control on the page or the page itself. Depending on
        /// where the page is in its lifecycle it may not be possible to add a control directly to
        /// the page object, therefore we will use the current control being rendered to append the
        /// postback control.</param>
        /// <param name="strEventName">Name of the event to sync. If a page contains more than a
        /// single client side event only the events associated with the passed in name will be raised.</param>
        /// <param name="objDelegate">Server side handler of the event.</param>
        /// <param name="blnMultipleHandlers">Boolean flag to determine if multiple event handlers
        /// can be associated to an event.</param>
        public static void RegisterPostBackEventHandler(IClientApiControl objParent, string strEventName, ClientAPIPostBackControl.PostBackEvent objDelegate, bool blnMultipleHandlers)
        {
            const string CLIENTAPI_POSTBACKCTL_ID = "ClientAPIPostBackCtl";
            var objCtl = Globals.FindControlRecursive(objParent.Page, CLIENTAPI_POSTBACKCTL_ID) as ClientAPIPostBackControl;
            if (objCtl == null)
            {
                objCtl = new ClientAPIPostBackControl(objParent.Page, strEventName, objDelegate);
                objCtl.Id = CLIENTAPI_POSTBACKCTL_ID;
                objParent.Children.Add(objCtl);
                ClientAPI.RegisterClientVariable(objParent.Page, "__dnn_postBack", GetPostBackClientHyperlink(objCtl, "[DATA]"), true);
            }
            else if (blnMultipleHandlers)
            {
                objCtl.AddEventHandler(strEventName, objDelegate);
            }
        }

        /// <summary>
        /// Registers a button inside a table for the ability to perform client-side reordering.
        /// </summary>
        /// <param name="objButton">Button responsible for moving the row up or down.</param>
        /// <param name="objPage">Page the table belongs to. Can't just use objButton.Page because
        /// inside ItemCreated event of grid the button has no page yet.</param>
        /// <param name="blnUp">Determines if the button is responsible for moving the row up or down.</param>
        /// <param name="strKey">Unique key for the table/grid to be used to obtain the new order on
        /// postback. Needed when calling GetClientSideReOrder.</param>
        public static void EnableClientSideReorder(IClientApiControl objButton, IClientApiPage objPage, bool blnUp, string strKey)
        {
            if (BrowserSupportsFunctionality(ClientFunctionality.DHTML))
            {
                RegisterClientReference(objPage, ClientNamespaceReferences.dnn_dom);
                var controller = GetClientResourcesController(objPage);
                controller.CreateScript(ScriptPath + "dnn.util.tablereorder.js").Register();

                // the VB conversion of a Boolean to an Integer yields -1 for True, which the
                // client side script relies on, so the emitted script is kept identical.
                AddAttribute(objButton, "onclick", "if (dnn.util.tableReorderMove(this," + (blnUp ? "-1" : "0") + ",'" + strKey + "')) return false;");
                var objParent = objButton.Parent;
                while (objParent != null)
                {
                    if (objParent is IClientApiTableRow)
                    {
                        // mark row as one that we care about, it will be numbered correctly on client
                        AddAttribute(objParent, "origidx", "-1");
                    }

                    objParent = objParent.Parent;
                }
            }
        }

        /// <summary>
        /// Retrieves an array of the new order for the rows.
        /// </summary>
        /// <param name="strKey">Unique key for the table/grid to be used to obtain the new order on
        /// postback. Needed when calling GetClientSideReOrder.</param>
        /// <param name="objPage">Page the table belongs to. Can't just use objButton.Page because
        /// inside ItemCreated event of grid the button has no page yet.</param>
        /// <returns>The new order of the rows.</returns>
        public static string[] GetClientSideReorder(string strKey, IClientApiPage objPage)
        {
            if (!string.IsNullOrEmpty(ClientAPI.GetClientVariable(objPage, strKey)))
            {
                return ClientAPI.GetClientVariable(objPage, strKey).Split(',');
            }
            else
            {
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Determines whether the Client API is disabled.
        /// </summary>
        /// <returns><see langword="true"/> when the Client API is disabled.</returns>
        /// <remarks>
        /// Replaces the <c>ClientAPI</c> app setting lookup with
        /// <see cref="DnnWebUtilityOptions.ClientApiDisabled"/>. See
        /// <see cref="ClientApiAlwaysEnabled"/> for the short circuit the original code applied.
        /// </remarks>
        public static bool ClientAPIDisabled()
        {
            if (ClientApiAlwaysEnabled)
            {
                return false;
            }

            return DnnWebUtilityContext.Options.ClientApiDisabled;
        }

        /// <summary>Gets the client side script that posts the page back on behalf of a control.</summary>
        /// <param name="objPage">The page being rendered.</param>
        /// <param name="objControl">The control raising the post back.</param>
        /// <param name="arg">The post back argument.</param>
        /// <returns>The client side post back script.</returns>
        public static string GetPostBackClientEvent(IClientApiPage objPage, IClientApiControl objControl, string arg)
        {
            return objPage.GetPostBackEventReference(objControl, arg);
        }

        /// <summary>Gets the <c>javascript:</c> hyperlink that posts the page back on behalf of a control.</summary>
        /// <param name="objControl">The control raising the post back.</param>
        /// <param name="strArgument">The post back argument.</param>
        /// <returns>The client side post back hyperlink.</returns>
        public static string GetPostBackClientHyperlink(IClientApiControl objControl, string strArgument)
        {
            return "javascript:" + GetPostBackEventReference(objControl, strArgument);
        }

        /// <summary>Gets the client side script that posts the page back on behalf of a control.</summary>
        /// <param name="objControl">The control raising the post back.</param>
        /// <param name="strArgument">The post back argument.</param>
        /// <returns>The client side post back script.</returns>
        public static string GetPostBackEventReference(IClientApiControl objControl, string strArgument)
        {
            return objControl.Page.GetPostBackEventReference(objControl, strArgument);
        }

        /// <summary>Determines whether a client script block has already been registered on the page.</summary>
        /// <param name="objPage">The page being rendered.</param>
        /// <param name="key">The registration key.</param>
        /// <returns><see langword="true"/> when the script block is already registered.</returns>
        public static bool IsClientScriptBlockRegistered(IClientApiPage objPage, string key)
        {
            return objPage.IsClientScriptBlockRegistered(objPage.GetType(), key);
        }

        /// <summary>Registers a client script block on the page.</summary>
        /// <param name="objPage">The page being rendered.</param>
        /// <param name="key">The registration key.</param>
        /// <param name="strScript">The script (or script tag) to emit.</param>
        public static void RegisterClientScriptBlock(IClientApiPage objPage, string key, string strScript)
        {
            if (Globals.IsEmbeddedScript(key) == false)
            {
                objPage.RegisterClientScriptBlock(objPage.GetType(), key, strScript);
            }
            else
            {
                RegisterClientScriptBlock(objPage, key);
            }
        }

        /// <summary>Registers a script that runs once the page has loaded.</summary>
        /// <param name="objPage">The page being rendered.</param>
        /// <param name="key">The registration key.</param>
        /// <param name="script">The script to run.</param>
        public static void RegisterStartUpScript(IClientApiPage objPage, string key, string script)
        {
            MSAJAX.RegisterStartupScript(objPage, key, script);
        }

        /// <summary>Registers one of the library's own scripts, either as an external file or from the assembly.</summary>
        /// <param name="objPage">The page being rendered.</param>
        /// <param name="key">The file name of the script.</param>
        public static void RegisterClientScriptBlock(IClientApiPage objPage, string key)
        {
            if (UseExternalScripts)
            {
                MSAJAX.RegisterClientScript(objPage, ClientAPI.ScriptPath + key);
            }
            else
            {
                MSAJAX.RegisterClientScript(objPage, key, "DotNetNuke.WebUtility");

                // client-side won't be able to get this info from dnn.js location, since its embedded.
                // so we need to use where it would have gone instead
                RegisterClientVariable(objPage, "__sp", ScriptPath, true);
            }
        }

        /// <summary>
        /// Builds a strongly typed list of the given type out of a list of deserialized dictionaries.
        /// </summary>
        /// <param name="theType">The type of the items of the resulting list.</param>
        /// <param name="theList">The deserialized items.</param>
        /// <returns>A <c>List&lt;theType&gt;</c> holding the converted items.</returns>
        public static IList GetListFromType(Type theType, IList theList)
        {
            var listOf = typeof(List<>);
            var argType = listOf.MakeGenericType(theType);

            var instance = (IList)Activator.CreateInstance(argType);
            foreach (Dictionary<string, object> listitem in theList)
            {
                if (listitem != null)
                {
                    instance.Add(ConvertDictionaryToObject(listitem, theType));
                }
            }

            return instance;
        }

        /// <summary>
        /// Creates an instance of the given type and fills its writable properties from a deserialized dictionary.
        /// </summary>
        /// <param name="dict">The deserialized property values.</param>
        /// <param name="theType">The type to create.</param>
        /// <returns>The created and filled instance.</returns>
        public static object ConvertDictionaryToObject(Dictionary<string, object> dict, Type theType)
        {
            var item = Activator.CreateInstance(theType);
            PropertyInfo pi;
            foreach (var pair in dict)
            {
                pi = theType.GetProperty(pair.Key);
                if (pi != null && pi.CanWrite && pair.Value != null)
                {
                    theType.InvokeMember(pair.Key, BindingFlags.SetProperty, null, item, new object[] { pair.Value }, CultureInfo.InvariantCulture);
                }
            }

            return item;
        }

        /// <summary>
        /// Publishes the URL an embedded script of the given assembly is served from as a client side variable.
        /// </summary>
        /// <param name="thePage">The page being rendered.</param>
        /// <param name="fileName">The file name of the embedded script, for instance <c>dnn.scripts.js</c>.</param>
        /// <param name="assemblyType">A type from the assembly holding the embedded script.</param>
        /// <remarks>
        /// On ASP.NET Core the embedded scripts are served by the manifest embedded file provider at
        /// <c>/js/&lt;name&gt;.js</c>, which is the URL
        /// <see cref="IClientApiPage.GetWebResourceUrl(Type, string)"/> returns; the client side
        /// variable name (<c>&lt;name&gt;.resx</c>) is unchanged.
        /// </remarks>
        public static void RegisterEmbeddedResource(IClientApiPage thePage, string fileName, Type assemblyType)
        {
            RegisterClientVariable(thePage, fileName + ".resx", thePage.GetWebResourceUrl(assemblyType, fileName), true);
        }

        /// <summary>Gets the client resource controller of the current request.</summary>
        /// <param name="page">The page being rendered.</param>
        /// <returns>The client resource controller.</returns>
        internal static IClientResourceController GetClientResourcesController(IClientApiPage page)
        {
            var serviceProvider = GetCurrentServiceProvider(page.HttpContext);
            return serviceProvider.GetRequiredService<IClientResourceController>();
        }

        /// <summary>Gets the service provider of the given request.</summary>
        /// <param name="context">The context of the request.</param>
        /// <returns>The service provider of the request.</returns>
        /// <remarks>
        /// Replaces the <c>HttpContextBase</c> overload; the scope stored in the context's items is
        /// still honored so that hosts which create their own scope keep working, falling back to
        /// <see cref="HttpContext.RequestServices"/>.
        /// </remarks>
        internal static IServiceProvider GetCurrentServiceProvider(HttpContext context)
        {
            return GetScope(context.Items)?.ServiceProvider ?? context.RequestServices;
        }

        /// <summary>Gets the service scope stored in the given request's items, if any.</summary>
        /// <param name="httpContextItems">The items of the current request.</param>
        /// <returns>The scope, or <see langword="null"/> when the request has none.</returns>
        internal static IServiceScope GetScope(IDictionary<object, object> httpContextItems)
        {
            return httpContextItems.TryGetValue(typeof(IServiceScope), out var scope) ? scope as IServiceScope : null;
        }

        /// <summary>
        /// Finds the <c>__dnnVariable</c> control on the page, if not found it attempts to add its own.
        /// </summary>
        /// <param name="objPage">Current page rendering content.</param>
        /// <returns>The hidden field holding the client side variables.</returns>
        private static IClientApiHiddenInput ClientVariableControl(IClientApiPage objPage)
        {
            return RegisterDNNVariableControl(objPage);
        }

        /// <summary>Loop up parent controls to find form.</summary>
        /// <param name="oCtl">The control to start from.</param>
        /// <returns>The form of the control's page.</returns>
        private static IClientApiControl FindForm(IClientApiControl oCtl)
        {
            return oCtl.Page.Form;
        }

        /// <summary>Returns the <c>__dnnVariable</c> control if present.</summary>
        /// <param name="objParent">The control whose page is searched.</param>
        /// <returns>The hidden field, or <see langword="null"/> when it is not on the page.</returns>
        private static IClientApiHiddenInput GetDNNVariableControl(IClientApiControl objParent)
        {
            return Globals.FindControlRecursive(objParent.Page, DNNVARIABLE_CONTROLID) as IClientApiHiddenInput;
        }

        /// <summary>Gets a value posted or queried for the current request.</summary>
        /// <param name="key">The name of the field.</param>
        /// <returns>The value, or <see langword="null"/> when it was not supplied.</returns>
        /// <remarks>Replaces the WebForms <c>Request(key)</c> indexer.</remarks>
        private static string GetRequestValue(string key)
        {
            var context = DnnWebUtilityContext.Current;
            if (context == null)
            {
                return null;
            }

            if (context.Request.HasFormContentType)
            {
                var formValues = context.Request.Form[key];
                if (formValues.Count > 0)
                {
                    return formValues.ToString();
                }
            }

            var queryValues = context.Request.Query[key];
            return queryValues.Count > 0 ? queryValues.ToString() : null;
        }

        /// <summary>
        /// Parses DNN Variable control contents and returns out the delimited name/value pair.
        /// </summary>
        /// <param name="objPage">Current page rendering content.</param>
        /// <param name="strVar">Name to retrieve.</param>
        /// <returns>Delimited name/value pair string.</returns>
        private static string GetClientVariableNameValuePair(IClientApiPage objPage, string strVar)
        {
            var objDict = GetClientVariableList(objPage);

            if (objDict.TryGetValue(strVar, out var value))
            {
                return strVar + COLUMN_DELIMITER + value;
            }

            return string.Empty;
        }

        /// <summary>
        /// Returns javascript to call dnncore.js key handler logic.
        /// </summary>
        /// <param name="intKeyAscii">ASCII value to trap.</param>
        /// <param name="strJavascript">Javascript to execute.</param>
        /// <returns>Javascript to handle key press.</returns>
        private static string GetKeyDownHandler(int intKeyAscii, string strJavascript)
        {
            return "return __dnn_KeyDown('" + intKeyAscii.ToString(CultureInfo.InvariantCulture) + "', '" + strJavascript.Replace("'", "%27") + "', event);";
        }

        /// <summary>Adds an attribute to a control.</summary>
        /// <param name="objControl">The control to add the attribute to.</param>
        /// <param name="strName">The name of the attribute.</param>
        /// <param name="strValue">The value of the attribute.</param>
        private static void AddAttribute(IClientApiControl objControl, string strName, string strValue)
        {
            objControl.Attributes[strName] = strValue;
        }

        /// <summary>Serializes the client side variables once, right before the page renders.</summary>
        /// <param name="sender">The hidden field raising the event.</param>
        /// <param name="args">The arguments of the event.</param>
        private static void CAPIPreRender(object sender, EventArgs args)
        {
            var ctl = (IClientApiControl)sender;
            var objDict = GetClientVariableList(ctl.Page);
            SerializeClientVariableDictionary(ctl.Page, objDict);
            var items = ContextItems;
            if (items != null)
            {
                items[PostPreRenderKey] = true;
            }
        }

        /// <summary>
        /// Derives the browser name and version from a user agent string.
        /// </summary>
        /// <param name="userAgent">The user agent of the request.</param>
        /// <param name="browser">The name of the browser, matching the names used in <c>ClientAPICaps.config</c>.</param>
        /// <param name="version">The version of the browser, as major.minor.</param>
        /// <remarks>
        /// REVIEW: behavior inferred from the WebForms <c>Request.Browser</c> capabilities, which do
        /// not exist on ASP.NET Core. The names are the ones the capabilities file matches on.
        /// </remarks>
        private static void GetBrowserNameAndVersion(string userAgent, out string browser, out double version)
        {
            string token;
            if (userAgent.Contains("Edg/", StringComparison.Ordinal))
            {
                browser = "Edge";
                token = "Edg/";
            }
            else if (userAgent.Contains("OPR/", StringComparison.Ordinal))
            {
                browser = "Opera";
                token = "OPR/";
            }
            else if (userAgent.Contains("Opera", StringComparison.Ordinal))
            {
                browser = "Opera";
                token = "Version/";
            }
            else if (userAgent.Contains("Firefox/", StringComparison.Ordinal))
            {
                browser = "FireFox";
                token = "Firefox/";
            }
            else if (userAgent.Contains("MSIE ", StringComparison.Ordinal))
            {
                browser = "IE";
                token = "MSIE ";
            }
            else if (userAgent.Contains("Trident/", StringComparison.Ordinal))
            {
                browser = "IE";
                token = "rv:";
            }
            else if (userAgent.Contains("Chrome/", StringComparison.Ordinal))
            {
                browser = "Chrome";
                token = "Chrome/";
            }
            else if (userAgent.Contains("Safari/", StringComparison.Ordinal))
            {
                browser = "Safari";
                token = "Version/";
            }
            else if (userAgent.Contains("Netscape", StringComparison.Ordinal))
            {
                browser = "Netscape";
                token = "Netscape/";
            }
            else if (userAgent.Contains("Gecko/", StringComparison.Ordinal))
            {
                browser = "Gecko";
                token = "rv:";
            }
            else
            {
                browser = "Unknown";
                token = null;
            }

            version = GetVersionAfterToken(userAgent, token);
        }

        /// <summary>Reads the version number following a token of a user agent string.</summary>
        /// <param name="userAgent">The user agent of the request.</param>
        /// <param name="token">The token the version follows, or <see langword="null"/>.</param>
        /// <returns>The version as major.minor, or 0 when no version could be read.</returns>
        private static double GetVersionAfterToken(string userAgent, string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return 0;
            }

            var index = userAgent.IndexOf(token, StringComparison.Ordinal);
            if (index == -1)
            {
                return 0;
            }

            index += token.Length;
            var major = string.Empty;
            var minor = string.Empty;
            var inMinor = false;
            for (var i = index; i < userAgent.Length; i++)
            {
                var c = userAgent[i];
                if (c >= '0' && c <= '9')
                {
                    if (inMinor)
                    {
                        minor += c;
                    }
                    else
                    {
                        major += c;
                    }
                }
                else if (c == '.' && !inMinor)
                {
                    inMinor = true;
                }
                else
                {
                    break;
                }
            }

            if (major.Length == 0)
            {
                return 0;
            }

            var text = minor.Length > 0 ? major + "." + minor : major;
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        }
    }
}
