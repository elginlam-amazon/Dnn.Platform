// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Utilities
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Net;
    using System.Text;

    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// The response of a Client API callback, which knows how to write itself back to the client in
    /// the transport the client used to make the callback.
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Justification = "Breaking change")]
    public class ClientAPICallBackResponse
    {
        /// <summary>The payload sent back to the client.</summary>
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        public string Response = string.Empty;

        /// <summary>The status of the callback.</summary>
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        public CallBackResponseStatusCode StatusCode;

        /// <summary>The description of the status of the callback.</summary>
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        public string StatusDesc = string.Empty;

        /// <summary>The type of the callback this is the response of.</summary>
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        public CallBackTypeCode CallBackType;

        private readonly IClientApiPage objPage;

        /// <summary>Initializes a new instance of the <see cref="ClientAPICallBackResponse"/> class.</summary>
        /// <param name="objPage">The page handling the callback.</param>
        /// <param name="eCallBackType">The type of the callback.</param>
        public ClientAPICallBackResponse(IClientApiPage objPage, CallBackTypeCode eCallBackType)
        {
            this.objPage = objPage;
            this.CallBackType = eCallBackType;
        }

        /// <summary>The status of a Client API callback.</summary>
        public enum CallBackResponseStatusCode
        {
            /// <summary>The callback succeeded.</summary>
            OK = 200,

            /// <summary>The callback failed.</summary>
            GenericFailure = 400,

            /// <summary>The control the callback targeted was not found.</summary>
            ControlNotFound = 404,

            /// <summary>The control the callback targeted does not implement the callback interface.</summary>
            InterfaceNotSupported = 501,
        }

        /// <summary>The kind of a Client API callback.</summary>
        public enum CallBackTypeCode
        {
            /// <summary>A callback raised directly against the target control.</summary>
            Simple = 0,

            /// <summary>A callback that runs the full page lifecycle before being raised.</summary>
            ProcessPage = 1,

            /// <summary>A callback invoking a method of the target control.</summary>
            CallbackMethod = 2,

            /// <summary>A callback that runs the full page lifecycle before invoking a method of the target control.</summary>
            ProcessPageCallbackMethod = 3,
        }

        /// <summary>The transport a Client API callback was made over.</summary>
        public enum TransportTypeCode
        {
            /// <summary>The callback was made with an XML HTTP request.</summary>
            XMLHTTP = 0,

            /// <summary>The callback was made by posting a hidden iframe.</summary>
            IFRAMEPost = 1,
        }

        /// <summary>Gets the transport the client used to make the callback.</summary>
        public TransportTypeCode TransportType
        {
            get
            {
                if (string.IsNullOrEmpty(this.ContextId) == false)
                {
                    return TransportTypeCode.IFRAMEPost;
                }
                else
                {
                    return TransportTypeCode.XMLHTTP;
                }
            }
        }

        /// <summary>Gets the client side context identifier posted along with an iframe callback.</summary>
        /// <remarks>
        /// The original read <c>Request.Form("ctx")</c> only. Reading the query string too would let a
        /// callback URL that happens to carry a <c>ctx</c> parameter be answered with the IFRAME HTML
        /// document instead of the XMLHTTP payload, so the lookup stays form-only.
        /// </remarks>
        private string ContextId
        {
            get
            {
                var request = this.objPage?.HttpContext?.Request;
                if (request is null || request.HasFormContentType == false)
                {
                    return null;
                }

                return request.Form["ctx"];
            }
        }

        /// <summary>Writes this response back to the client over the transport the callback used.</summary>
        public void Write()
        {
            var response = this.objPage?.HttpContext?.Response;
            if (response is null)
            {
                return;
            }

            switch (this.TransportType)
            {
                case TransportTypeCode.IFRAMEPost:
                    // if context passed in then we are using IFRAME Implementation
                    var strContextID = this.ContextId;
                    if (double.TryParse(strContextID, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                    {
                        var builder = new StringBuilder();
                        builder.Append("<html><head></head><body onload=\"window.parent.dnn.xmlhttp.requests['" + strContextID + "'].complete(window.parent.dnn.dom.getById('txt', document).value);\"><form>");
                        builder.Append("<input type=\"hidden\" id=\"" + ClientAPI.SCRIPT_CALLBACKSTATUSID + "\" value=\"" + ((int)this.StatusCode).ToString(CultureInfo.InvariantCulture) + "\">");
                        builder.Append("<input type=\"hidden\" id=\"" + ClientAPI.SCRIPT_CALLBACKSTATUSDESCID + "\" value=\"" + this.StatusDesc + "\">");
                        builder.Append("<textarea id=\"txt\">");
                        builder.Append(WebUtility.HtmlEncode(MSAJAX.Serialize(new { d = this.Response })));
                        builder.Append("</textarea></body></html>");

                        WriteText(response, builder.ToString());
                    }

                    break;
                case TransportTypeCode.XMLHTTP:
                    if (response.HasStarted == false)
                    {
                        response.Headers.Append(ClientAPI.SCRIPT_CALLBACKSTATUSID, ((int)this.StatusCode).ToString(CultureInfo.InvariantCulture));
                        response.Headers.Append(ClientAPI.SCRIPT_CALLBACKSTATUSDESCID, this.StatusDesc);
                    }

                    // don't serialize straight html
                    WriteText(response, MSAJAX.Serialize(new { d = this.Response }));
                    break;
            }
        }

        /// <summary>Writes the given text to the given response.</summary>
        /// <param name="response">The response to write to.</param>
        /// <param name="text">The text to write.</param>
        private static void WriteText(HttpResponse response, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            response.Body.Write(bytes, 0, bytes.Length);
            response.Body.Flush();
        }
    }
}
