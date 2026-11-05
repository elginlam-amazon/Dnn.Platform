// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections;
    using System.Globalization;
    using System.Text;
    using System.Text.RegularExpressions;

    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.UI.Utilities;
    using DotNetNuke.Web.Client;

    /// <summary>
    /// Static helpers shared by the controls of this library.
    /// </summary>
    /// <remarks>
    /// The original was a VB <c>StandardModule</c> (a module), which C# expresses as a static class.
    /// Its ambient-request uses (the WebForms <c>HttpContext</c> static) are replaced by
    /// <see cref="DnnWebUtilityContext.Current"/>, which resolves the request through the
    /// registered <c>IHttpContextAccessor</c>, and its <c>System.Web.UI.Page</c> parameters by the
    /// <see cref="IClientApiPage"/> abstraction of the migrated <c>DotNetNuke.WebUtility</c>. The
    /// helpers that do not actually need a request (<see cref="GetNodeJS"/>,
    /// <see cref="StripUrlPaths(string, UrlFormatType)"/>, <see cref="ToJSON"/>) take plain
    /// parameters instead, which is what the Blazor components call.
    /// </remarks>
    public static class WebControls
    {
        /// <summary>The registration key of the client side submit component.</summary>
        private const string SubmitComponentKey = "dnn.controls.submitComp";

        /// <summary>The base URL the scripts of this library are served from.</summary>
        /// <remarks>
        /// The RCL static web asset path that replaced the embedded script resources of the
        /// WebForms library.
        /// </remarks>
        private const string StaticScriptPath = "_content/DotNetNuke.WebControls/js/";

        /// <summary>
        /// The script that runs the client side submit component when the page's form is submitted.
        /// </summary>
        /// <remarks>
        /// REVIEW: behavior inferred from usage — verify. Replaces
        /// <c>ClientScript.RegisterOnSubmitStatement</c>, which has no ASP.NET Core equivalent, with
        /// an explicit submit listener on the page's form that calls the very same function.
        /// </remarks>
        private const string SubmitComponentScript =
            "if (document.forms.length > 0) { document.forms[0].addEventListener('submit', " +
            "function () { dnn.controls.submitComp.onsubmit(); }); }";

        /// <summary>The user agent fragments that identify a crawler.</summary>
        /// <remarks>
        /// REVIEW: behavior inferred from usage — verify. The original read the WebForms
        /// <c>Request.Browser.Crawler</c> capability, which does not exist on ASP.NET Core, so the
        /// detection is done on the user agent instead.
        /// </remarks>
        private static readonly string[] CrawlerAgents =
        {
            "bot", "crawler", "spider", "slurp", "search", "archiver", "facebookexternalhit",
            "mediapartners-google", "yandex", "baidu", "duckduckbot", "ia_archiver",
        };

        /// <summary>
        /// Gets the URL one of this library's scripts is served from.
        /// </summary>
        /// <param name="key">The file name of the script.</param>
        /// <returns>The URL of the script.</returns>
        /// <remarks>
        /// The scripts used to be embedded assembly resources served through
        /// the client resource manager; they are now static web assets of this Razor Class
        /// Library, served from <c>_content/DotNetNuke.WebControls/js/</c>. A host that overrode
        /// <c>ClientAPI.ScriptPath</c> still wins, so an existing deployment layout keeps working.
        /// </remarks>
        public static string GetScriptUrl(string key)
        {
            var path = ClientAPI.ScriptPath;
            return string.IsNullOrEmpty(path) ? StaticScriptPath + key : path + key;
        }

        /// <summary>
        /// Gets a value indicating whether the code runs outside of a request.
        /// </summary>
        /// <returns><see langword="true"/> when there is no current request.</returns>
        /// <remarks>
        /// The original tested whether the ambient WebForms request context was <c>Nothing</c>,
        /// which was true both at design time and outside of a request; the replacement keeps that
        /// exact meaning, resolving the request through <c>IHttpContextAccessor</c>.
        /// </remarks>
        public static bool IsDesignMode()
        {
            return DnnWebUtilityContext.Current is null;
        }

        /// <summary>
        /// Gets the client side script that a node's click should run.
        /// </summary>
        /// <param name="navControl">The control owning the node.</param>
        /// <param name="node">The node being clicked.</param>
        /// <param name="defaultJS">The script to fall back to when the node has none.</param>
        /// <param name="target">The frame to navigate, when navigating.</param>
        /// <param name="noneDoesPostBack">Whether a node without a click action posts back.</param>
        /// <returns>The client side script of the click.</returns>
        public static string GetNodeJS(IClientApiControl navControl, DNNNode node, string defaultJS, string target, bool noneDoesPostBack)
        {
            ArgumentNullException.ThrowIfNull(node);

            var expression = string.Empty;
            if (!string.IsNullOrEmpty(node.JSFunction))
            {
                expression = FixJs(node.JSFunction);
            }
            else if (!string.IsNullOrEmpty(defaultJS))
            {
                expression = FixJs(defaultJS);
            }

            switch (node.ClickAction)
            {
                case eClickAction.PostBack:
                case eClickAction.Expand:
                case eClickAction.None:
                    if (node.ClickAction != eClickAction.None || noneDoesPostBack)
                    {
                        if (expression.Length > 0)
                        {
                            expression = "if (eval(\"" + expression.Replace("\"", "\"\"", StringComparison.Ordinal) + "\") != false) ";
                        }

                        expression += ClientAPI.GetPostBackEventReference(navControl, node.ID + ClientAPI.COLUMN_DELIMITER + "Click");
                    }

                    break;

                case eClickAction.Navigate:
                    if (expression.Length > 0)
                    {
                        expression = "if (eval(\"" + expression.Replace("\"", "\"\"", StringComparison.Ordinal) + "\") != false) ";
                    }

                    if (!string.IsNullOrEmpty(target))
                    {
                        expression = string.Concat(expression, "window.frames.", target, ".location.href='", node.NavigateURL, "'; void(0);");
                    }
                    else if (!string.IsNullOrEmpty(expression))
                    {
                        expression = expression + "window.location.href='" + node.NavigateURL + "';";
                    }

                    break;

                default:
                    break;
            }

            return expression;
        }

        /// <summary>Registers one of this library's embedded scripts with the page.</summary>
        /// <param name="thePage">The page to register the script with.</param>
        /// <param name="key">The file name of the script.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.3. Please use overload with IClientResourceController. Scheduled removal in v12.0.0.")]
        public static void RegisterClientScriptBlock(IClientApiPage thePage, string key)
        {
            if (ClientAPI.UseExternalScripts)
            {
                thePage?.RegisterClientScriptInclude(key, GetScriptUrl(key));
            }
            else
            {
                MSAJAX.RegisterClientScript(thePage, key, "DotNetNuke.WebControls");
            }
        }

        /// <summary>Registers one of this library's embedded scripts with the page.</summary>
        /// <param name="clientResourceController">The controller registering the resource.</param>
        /// <param name="thePage">The page to register the script with.</param>
        /// <param name="key">The file name of the script.</param>
        public static void RegisterClientScriptBlock(IClientResourceController clientResourceController, IClientApiPage thePage, string key)
        {
            ArgumentNullException.ThrowIfNull(clientResourceController);

            if (ClientAPI.UseExternalScripts)
            {
                clientResourceController.CreateScript(GetScriptUrl(key)).Register();
            }
            else
            {
                MSAJAX.RegisterClientScript(thePage, key, "DotNetNuke.WebControls");
            }
        }

        /// <summary>Registers one of this library's embedded scripts with the page.</summary>
        /// <param name="thePage">The page to register the script with.</param>
        /// <param name="key">The file name of the script.</param>
        /// <param name="priority">The load order of the script.</param>
        [CLSCompliant(false)]
        [Obsolete("Deprecated in DotNetNuke 10.2.3. Please use overload with IClientResourceController. Scheduled removal in v12.0.0.")]
        public static void RegisterClientScriptBlock(IClientApiPage thePage, string key, DotNetNuke.Web.Client.FileOrder.Js priority)
        {
            if (ClientAPI.UseExternalScripts)
            {
                thePage?.RegisterClientScriptInclude(key, GetScriptUrl(key));
            }
            else
            {
                MSAJAX.RegisterClientScript(thePage, key, "DotNetNuke.WebControls");
            }
        }

        /// <summary>Registers one of this library's embedded scripts with the page.</summary>
        /// <param name="clientResourceController">The controller registering the resource.</param>
        /// <param name="thePage">The page to register the script with.</param>
        /// <param name="key">The file name of the script.</param>
        /// <param name="priority">The load order of the script.</param>
        [CLSCompliant(false)]
        public static void RegisterClientScriptBlock(IClientResourceController clientResourceController, IClientApiPage thePage, string key, DotNetNuke.Web.Client.FileOrder.Js priority)
        {
            ArgumentNullException.ThrowIfNull(clientResourceController);

            if (ClientAPI.UseExternalScripts)
            {
                // IScriptResource exposes the load order as the Priority property; the original
                // SetPriority fluent helper does not exist on the abstraction.
                var script = clientResourceController.CreateScript(GetScriptUrl(key));
                script.Priority = (int)priority;
                script.Register();
            }
            else
            {
                MSAJAX.RegisterClientScript(thePage, key, "DotNetNuke.WebControls");
            }
        }

        /// <summary>Makes sure the client side submit component runs when the page's form is submitted.</summary>
        /// <param name="thePage">The page to register the component with.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.3. Please use overload with IClientResourceController. Scheduled removal in v12.0.0.")]
        public static void RegisterSubmitComponent(IClientApiPage thePage)
        {
            ArgumentNullException.ThrowIfNull(thePage);

            if (!thePage.IsClientScriptBlockRegistered(typeof(WebControls), SubmitComponentKey))
            {
                RegisterClientScriptBlock(thePage, "dnn.controls.js", DotNetNuke.Web.Client.FileOrder.Js.DnnControlsLabelEdit);
                thePage.RegisterStartupScript(typeof(WebControls), SubmitComponentKey, SubmitComponentScript, true);
            }
        }

        /// <summary>Makes sure the client side submit component runs when the page's form is submitted.</summary>
        /// <param name="clientResourceController">The controller registering the resource.</param>
        /// <param name="thePage">The page to register the component with.</param>
        public static void RegisterSubmitComponent(IClientResourceController clientResourceController, IClientApiPage thePage)
        {
            ArgumentNullException.ThrowIfNull(thePage);

            if (!thePage.IsClientScriptBlockRegistered(typeof(WebControls), SubmitComponentKey))
            {
                RegisterClientScriptBlock(clientResourceController, thePage, "dnn.controls.js", DotNetNuke.Web.Client.FileOrder.Js.DnnControlsLabelEdit);
                thePage.RegisterStartupScript(typeof(WebControls), SubmitComponentKey, SubmitComponentScript, true);
            }
        }

        /// <summary>
        /// Removes the server part of the URLs found in the <c>src</c> and <c>href</c> attributes of
        /// the given markup, without needing a request.
        /// </summary>
        /// <param name="text">The markup to rewrite.</param>
        /// <param name="type">How much of the URL to strip.</param>
        /// <returns>The markup with the URLs stripped.</returns>
        /// <remarks>
        /// The overload the Blazor components call: over a SignalR circuit there is no request to
        /// read the absolute URL from, so the scheme and authority are matched by pattern instead
        /// of by comparing against the current request's URL. <see cref="UrlFormatType.Relative"/>
        /// needs the requested path to shorten against and therefore keeps behaving like
        /// <see cref="UrlFormatType.AbsoluteWithoutServer"/> here, which is the closest result the
        /// original produced without a request.
        /// </remarks>
        public static string StripUrlPaths(string text, UrlFormatType type)
        {
            if (type == UrlFormatType.None || string.IsNullOrEmpty(text))
            {
                return text;
            }

            const string pattern = "(?i:(?<=(?:src|href)=(?:\"|'))[a-z][a-z0-9+.-]*://[^/'\"]+(?=[^'|\"]*?(?:\"|')))";
            return Regex.Replace(text, pattern, string.Empty);
        }

        /// <summary>
        /// Removes the server part of the URLs found in the <c>src</c> and <c>href</c> attributes of
        /// the given markup.
        /// </summary>
        /// <param name="text">The markup to rewrite.</param>
        /// <param name="type">How much of the URL to strip.</param>
        /// <param name="thePage">The page the markup belongs to.</param>
        /// <returns>The markup with the URLs stripped.</returns>
        public static string StripUrlPaths(string text, UrlFormatType type, IClientApiPage thePage)
        {
            var request = thePage?.HttpContext?.Request;
            if (request is null)
            {
                return text;
            }

            var absoluteUri = $"{request.Scheme}://{request.Host}{request.PathBase}{request.Path}{request.QueryString}";
            var pathAndQuery = $"{request.PathBase}{request.Path}{request.QueryString}";
            var requestPath = $"{request.PathBase}{request.Path}";
            var prefix = string.Empty;

            switch (type)
            {
                case UrlFormatType.AbsoluteWithoutServer:
                    prefix = absoluteUri.Substring(0, absoluteUri.Length - pathAndQuery.Length);
                    break;

                case UrlFormatType.Relative:
                    var fileName = requestPath.Substring(requestPath.LastIndexOf('/') + 1);
                    var fileNameIndex = absoluteUri.IndexOf(fileName, StringComparison.Ordinal);
                    prefix = fileNameIndex < 0 ? string.Empty : absoluteUri.Substring(0, fileNameIndex);
                    break;

                default:
                    break;
            }

            prefix = Regex.Escape(prefix);
            var pattern = $"(?i:(?<=(?:src|href)=(?:\"|')){prefix}(?=[^'|\"]*?(?:\"|')))";
            return Regex.Replace(text, pattern, string.Empty);
        }

        /// <summary>Serializes a hash table into the JSON object literal the client scripts expect.</summary>
        /// <param name="hash">The values to serialize.</param>
        /// <returns>The JSON object literal.</returns>
        public static string ToJSON(Hashtable hash)
        {
            ArgumentNullException.ThrowIfNull(hash);

            var builder = new StringBuilder();
            builder.Append('{');
            foreach (var key in hash.Keys)
            {
                var name = Convert.ToString(key, CultureInfo.InvariantCulture);
                if (builder.Length > 1)
                {
                    builder.Append(',');
                }

                // The original had an `If Not True Then ... Else ...` whose dead branch assigned the
                // raw value; only the quoted, escaped form was ever produced.
                var value = "'" + ClientAPI.EscapeForJavascript(Convert.ToString(hash[key], CultureInfo.InvariantCulture)) + "'";
                builder.Append(name).Append(':').Append(value);
            }

            builder.Append('}');
            return builder.ToString();
        }

        /// <summary>
        /// Gets a value indicating whether the current request comes from a crawler.
        /// </summary>
        /// <returns><see langword="true"/> when the user agent identifies a crawler.</returns>
        /// <remarks>
        /// REVIEW: behavior inferred from usage — verify. Replaces the WebForms
        /// <c>Request.Browser.Crawler</c> capability, which has no ASP.NET Core equivalent.
        /// </remarks>
        internal static bool IsCrawlerRequest()
        {
            var context = DnnWebUtilityContext.Current;
            if (context is null)
            {
                return false;
            }

            var userAgent = context.Request.Headers.UserAgent.ToString();
            if (string.IsNullOrEmpty(userAgent))
            {
                return false;
            }

            foreach (var agent in CrawlerAgents)
            {
                if (userAgent.Contains(agent, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Appends the statement terminator the client scripts expect.</summary>
        /// <param name="js">The script fragment.</param>
        /// <returns>The script fragment, terminated.</returns>
        private static string FixJs(string js)
        {
            return string.IsNullOrEmpty(js) || js.EndsWith(';') ? js : js + ";";
        }
    }
}
