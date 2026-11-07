// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Utilities
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;
    using System.Reflection;

    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// The Client API callback pipeline: detecting a callback request, dispatching it to the control
    /// (or control method) it targets and writing the response back to the client.
    /// </summary>
    public partial class ClientAPI
    {
        /// <summary>Determines whether the current request is a Client API callback.</summary>
        /// <param name="objPage">The page handling the request.</param>
        /// <returns><see langword="true"/> when the request is a Client API callback.</returns>
        public static bool IsInCallback(IClientApiPage objPage)
        {
            ArgumentNullException.ThrowIfNull(objPage);

            return string.IsNullOrEmpty(objPage.GetRequestValue(SCRIPT_CALLBACKID)) == false
                && objPage.HttpContext is not null
                && objPage.HttpContext.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Determines whether the given request is a Client API callback.</summary>
        /// <param name="context">The request context to inspect.</param>
        /// <returns><see langword="true"/> when the request is a Client API callback.</returns>
        /// <remarks>
        /// Used by the <c>UseDnnWebUtility</c> middleware so that a callback can be recognized
        /// before a page object exists for the request.
        /// </remarks>
        public static bool IsInCallback(HttpContext context)
        {
            if (context is null)
            {
                return false;
            }

            return string.IsNullOrEmpty(GetRequestValue(context, SCRIPT_CALLBACKID)) == false
                && context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Handles the Client API callback of the current request, if there is one.</summary>
        /// <param name="objPage">The page handling the request.</param>
        public static void HandleClientAPICallbackEvent(IClientApiPage objPage)
        {
            HandleClientAPICallbackEvent(objPage, GetCallbackType(objPage));
        }

        /// <summary>Handles the Client API callback of the current request as the given type of callback.</summary>
        /// <param name="objPage">The page handling the request.</param>
        /// <param name="eType">The type of the callback.</param>
        public static void HandleClientAPICallbackEvent(IClientApiPage objPage, ClientAPICallBackResponse.CallBackTypeCode eType)
        {
            if (IsInCallback(objPage))
            {
                switch (eType)
                {
                    case ClientAPICallBackResponse.CallBackTypeCode.Simple:
                    case ClientAPICallBackResponse.CallBackTypeCode.CallbackMethod:
                        var arrIDs = objPage.GetRequestValue(SCRIPT_CALLBACKID).Split(' ');
                        var strControlID = arrIDs[0];
                        var strClientID = string.Empty;
                        if (arrIDs.Length > 1)
                        {
                            strClientID = arrIDs[1];
                        }

                        var strParam = WebUtility.UrlDecode(objPage.GetRequestValue(SCRIPT_CALLBACKPARAM) ?? string.Empty);
                        IClientApiControl objControl;
                        IClientAPICallbackEventHandler objInterface;
                        var objResponse = new ClientAPICallBackResponse(objPage, ClientAPICallBackResponse.CallBackTypeCode.Simple);

                        try
                        {
                            // clear response stream
                            ClearResponse(objPage);
                            if (strControlID == SCRIPT_CALLBACKPAGEID)
                            {
                                objControl = objPage;
                            }
                            else
                            {
                                objControl = Globals.FindControlRecursive(objPage, strControlID, strClientID);
                            }

                            if (objControl is not null)
                            {
                                if (eType == ClientAPICallBackResponse.CallBackTypeCode.CallbackMethod)
                                {
                                    try
                                    {
                                        objResponse.Response = ExecuteControlMethod(objControl, strParam);
                                        objResponse.StatusCode = ClientAPICallBackResponse.CallBackResponseStatusCode.OK;
                                    }
                                    catch (Exception ex)
                                    {
                                        objResponse.StatusCode = ClientAPICallBackResponse.CallBackResponseStatusCode.GenericFailure;
                                        objResponse.StatusDesc = ex.Message;
                                    }
                                }
                                else
                                {
                                    if (ReferenceEquals(objControl, objPage.Form))
                                    {
                                        // form doesn't implement interface, so use page instead
                                        objInterface = objPage as IClientAPICallbackEventHandler;
                                    }
                                    else
                                    {
                                        objInterface = objControl as IClientAPICallbackEventHandler;
                                    }

                                    if (objInterface is not null)
                                    {
                                        try
                                        {
                                            objResponse.Response = objInterface.RaiseClientAPICallbackEvent(strParam);
                                            objResponse.StatusCode = ClientAPICallBackResponse.CallBackResponseStatusCode.OK;
                                        }
                                        catch (Exception ex)
                                        {
                                            objResponse.StatusCode = ClientAPICallBackResponse.CallBackResponseStatusCode.GenericFailure;
                                            objResponse.StatusDesc = ex.Message;
                                        }
                                    }
                                    else
                                    {
                                        // The original performed a hard cast here, so a control that
                                        // did not implement the interface threw InvalidCastException
                                        // and was reported by the outer handler as a generic failure.
                                        // That behavior is preserved so client scripts that branch on
                                        // the status code see the same value as before.
                                        objResponse.StatusCode = ClientAPICallBackResponse.CallBackResponseStatusCode.GenericFailure;
                                        objResponse.StatusDesc = "Generic failure";
                                    }
                                }
                            }
                            else
                            {
                                objResponse.StatusCode = ClientAPICallBackResponse.CallBackResponseStatusCode.ControlNotFound;
                                objResponse.StatusDesc = "Control Not Found";
                            }
                        }
                        catch (Exception)
                        {
                            objResponse.StatusCode = ClientAPICallBackResponse.CallBackResponseStatusCode.GenericFailure;
                            objResponse.StatusDesc = "Generic failure";
                        }
                        finally
                        {
                            objResponse.Write();
                            EndResponse(objPage);
                        }

                        break;
                    case ClientAPICallBackResponse.CallBackTypeCode.ProcessPage:
                    case ClientAPICallBackResponse.CallBackTypeCode.ProcessPageCallbackMethod:
                        objPage.SetRenderMethodDelegate(CallbackRenderMethod);
                        break;
                }
            }
        }

        /// <summary>
        /// Registers the client side plumbing a control needs so that its methods marked with
        /// <see cref="ControlMethodAttribute"/> can be invoked from the client.
        /// </summary>
        /// <param name="callbackControl">The control exposing the methods.</param>
        /// <returns><see langword="true"/> when the browser supports the callbacks.</returns>
        public static bool RegisterControlMethods(IClientApiControl callbackControl)
        {
            return RegisterControlMethods(callbackControl, string.Empty);
        }

        /// <summary>
        /// Registers the client side plumbing a control needs so that its methods marked with
        /// <see cref="ControlMethodAttribute"/> can be invoked from the client.
        /// </summary>
        /// <param name="callbackControl">The control exposing the methods.</param>
        /// <param name="friendlyID">The friendly identifier the client addresses the control by.</param>
        /// <returns><see langword="true"/> when the browser supports the callbacks.</returns>
        public static bool RegisterControlMethods(IClientApiControl callbackControl, string friendlyID)
        {
            ArgumentNullException.ThrowIfNull(callbackControl);

            string name;
            var ret = true;

            var classAttr = (ControlMethodClassAttribute)Attribute.GetCustomAttribute(callbackControl.GetType(), typeof(ControlMethodClassAttribute));

            if (classAttr is not null)
            {
                if (string.IsNullOrEmpty(classAttr.FriendlyNamespace))
                {
                    name = callbackControl.GetType().FullName;
                }
                else
                {
                    name = classAttr.FriendlyNamespace;
                }

                var format = "{0}.{1}={2} ";
                if (string.IsNullOrEmpty(friendlyID))
                {
                    format = "{0}={2} ";
                }

                ClientAPI.RegisterClientVariable(callbackControl.Page, "__dnncbm", string.Format(CultureInfo.InvariantCulture, format, name, friendlyID, callbackControl.UniqueId), false);

                if (BrowserSupportsFunctionality(ClientFunctionality.XMLHTTP) && BrowserSupportsFunctionality(ClientFunctionality.XML))
                {
                    DotNetNuke.UI.Utilities.ClientAPI.RegisterClientReference(callbackControl.Page, DotNetNuke.UI.Utilities.ClientAPI.ClientNamespaceReferences.dnn_xml);
                    DotNetNuke.UI.Utilities.ClientAPI.RegisterClientReference(callbackControl.Page, DotNetNuke.UI.Utilities.ClientAPI.ClientNamespaceReferences.dnn_xmlhttp);
                }
                else
                {
                    ret = false;
                }
            }
            else
            {
                throw new InvalidOperationException("Control does not have CallbackMethodAttribute");
            }

            return ret;
        }

        /// <summary>
        /// Invokes the method of the given control the client asked for, passing it the deserialized
        /// arguments and serializing its result back.
        /// </summary>
        /// <param name="callbackControl">The control exposing the method.</param>
        /// <param name="callbackArgument">The JSON describing the method and its arguments.</param>
        /// <returns>The JSON holding the result of the method.</returns>
        public static string ExecuteControlMethod(IClientApiControl callbackControl, string callbackArgument)
        {
            ArgumentNullException.ThrowIfNull(callbackControl);

            var controlType = callbackControl.GetType();
            var callInfo = MSAJAX.DeserializeObject(callbackArgument) as Dictionary<string, object>;
            var methodName = Convert.ToString(callInfo["method"], CultureInfo.InvariantCulture);
            var args = callInfo["args"] as Dictionary<string, object>;
            var mi = controlType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);

            if (mi is null)
            {
                throw new InvalidOperationException($"Class: {controlType.FullName} does not have the method: {methodName}");
            }

            var methodParams = mi.GetParameters();

            // only allow methods with attribute to be called
            var methAttr = (ControlMethodAttribute)Attribute.GetCustomAttribute(mi, typeof(ControlMethodAttribute));
            if (methAttr is null || args is null || args.Count != methodParams.Length)
            {
                throw new InvalidOperationException($"Class: {controlType.FullName} does not have the method: {methodName}");
            }

            var targetArgs = new object[args.Count];
            for (var i = 0; i <= methodParams.Length - 1; i++)
            {
                var paramName = methodParams[i].Name;
                var paramType = methodParams[i].ParameterType;
                if (args.TryGetValue(paramName, out var arg))
                {
                    if (paramType.IsGenericType)
                    {
                        if (arg is IList list)
                        {
                            var genTypes = paramType.GetGenericArguments();
                            targetArgs[i] = GetListFromType(genTypes[0], list);
                        }
                        else
                        {
                            targetArgs[i] = arg;
                        }
                    }
                    else if (paramType.IsClass && arg is Dictionary<string, object> dict)
                    {
                        targetArgs[i] = ConvertDictionaryToObject(dict, paramType);
                    }
                    else
                    {
                        targetArgs[i] = Convert.ChangeType(arg, methodParams[i].ParameterType, CultureInfo.InvariantCulture);
                    }
                }
            }

            var result = mi.Invoke(callbackControl, targetArgs);

            var resultInfo = new Dictionary<string, object>();
            resultInfo["result"] = result;
            return MSAJAX.Serialize(resultInfo);
        }

        /// <summary>
        /// The render method installed on the page for the callback types that need the whole page
        /// lifecycle to run before the callback is raised.
        /// </summary>
        /// <param name="output">The writer the page would have rendered into.</param>
        /// <param name="container">The page being rendered.</param>
        private static void CallbackRenderMethod(IClientApiHtmlWriter output, IClientApiControl container)
        {
            var objPage = (IClientApiPage)container;
            var eType = GetCallbackType(objPage);
            if (eType == ClientAPICallBackResponse.CallBackTypeCode.ProcessPage)
            {
                eType = ClientAPICallBackResponse.CallBackTypeCode.Simple;
            }
            else
            {
                eType = ClientAPICallBackResponse.CallBackTypeCode.CallbackMethod;
            }

            HandleClientAPICallbackEvent(objPage, eType);
        }

        /// <summary>Gets the type of callback the client asked for.</summary>
        /// <param name="objPage">The page handling the request.</param>
        /// <returns>The type of the callback, defaulting to a simple callback.</returns>
        private static ClientAPICallBackResponse.CallBackTypeCode GetCallbackType(IClientApiPage objPage)
        {
            var eType = ClientAPICallBackResponse.CallBackTypeCode.Simple;
            var strType = objPage.GetRequestValue(SCRIPT_CALLBACKTYPE);
            if (string.IsNullOrEmpty(strType) == false)
            {
                eType = (ClientAPICallBackResponse.CallBackTypeCode)Convert.ToInt32(strType, CultureInfo.InvariantCulture);
            }

            return eType;
        }

        /// <summary>Reads a posted or queried value straight from the given request context.</summary>
        /// <param name="context">The request context to read from.</param>
        /// <param name="key">The name of the value.</param>
        /// <returns>The value, or <see langword="null"/> when it was not supplied.</returns>
        private static string GetRequestValue(HttpContext context, string key)
        {
            if (context.Request.HasFormContentType && context.Request.Form.TryGetValue(key, out var formValue))
            {
                return formValue;
            }

            return context.Request.Query.TryGetValue(key, out var queryValue) ? queryValue.ToString() : null;
        }

        /// <summary>Discards everything buffered for the response so far, mirroring <c>Response.Clear</c>.</summary>
        /// <param name="objPage">The page handling the request.</param>
        private static void ClearResponse(IClientApiPage objPage)
        {
            var response = objPage.HttpContext?.Response;
            if (response is not null && response.HasStarted == false)
            {
                response.Headers.Clear();
                response.ContentLength = null;
            }
        }

        /// <summary>Completes the response, mirroring <c>Response.End</c>.</summary>
        /// <param name="objPage">The page handling the request.</param>
        private static void EndResponse(IClientApiPage objPage)
        {
            var response = objPage.HttpContext?.Response;
            if (response is not null)
            {
                response.CompleteAsync().GetAwaiter().GetResult();
            }
        }
    }
}
