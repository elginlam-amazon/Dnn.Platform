// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.HttpModules.UrlRewrite
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text.RegularExpressions;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.Portals;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.Entities.Urls;
    using DotNetNuke.Entities.Urls.Config;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Services.EventQueue;
    using DotNetNuke.Services.Localization;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>The basic URL rewriter.</summary>
    internal class BasicUrlRewriter : UrlRewriterBase
    {
        /// <summary>A regular expression matching a tab ID query string parameter.</summary>
        public static readonly Regex TabIdRegex = new Regex("&?tabid=\\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>A regular expression matching a portal ID query string parameter.</summary>
        public static readonly Regex PortalIdRegex = new Regex("&?portalid=\\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly ILogger Logger = DnnLoggingController.GetLogger<BasicUrlRewriter>();

        private readonly IServiceProvider serviceProvider;

        /// <summary>Initializes a new instance of the <see cref="BasicUrlRewriter"/> class.</summary>
        /// <param name="serviceProvider">The service provider.</param>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="portalAliasService">The portal alias service.</param>
        /// <param name="hostSettingsService">The host settings service.</param>
        /// <param name="portalController">The portal controller.</param>
        public BasicUrlRewriter(IServiceProvider serviceProvider, IHostSettings hostSettings, IPortalAliasService portalAliasService, IHostSettingsService hostSettingsService, IPortalController portalController)
            : base(hostSettings, portalAliasService, hostSettingsService, portalController)
        {
            this.serviceProvider = serviceProvider;
        }

        /// <inheritdoc />
        internal override void RewriteUrl(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var request = context.Request;
            var response = context.Response;
            string requestedPath = request.GetUrl().AbsoluteUri;

            if (RewriterUtils.OmitFromRewriteProcessing(request.GetUrl().LocalPath))
            {
                return;
            }

            // 'Carry out first time initialization tasks
            Initialize.Init(context);
            if (!Initialize.ProcessHttpModule(request, false, false))
            {
                return;
            }

            // URL validation
            // check for ".." escape characters commonly used by hackers to traverse the folder tree on the server
            // the application should always use the exact relative location of the resource it is requesting
            var strUrl = request.GetUrl().AbsolutePath;

            // Server.UrlDecode has no ASP.NET Core counterpart; WebUtility.UrlDecode is the
            // equivalent (same decoding rules), applied twice as the original did.
            var strDoubleDecodeUrl = WebUtility.UrlDecode(WebUtility.UrlDecode(request.GetRawUrl())) ?? string.Empty;
            if (Globals.FileEscapingRegex.IsMatch(strUrl) || Globals.FileEscapingRegex.IsMatch(strDoubleDecodeUrl))
            {
                DotNetNuke.Services.Exceptions.Exceptions.ProcessHttpException(request);
            }

            try
            {
                // fix for ASP.NET canonicalization issues http://support.microsoft.com/?kbid=887459
                // Request.PhysicalPath is gone on ASP.NET Core; HttpContextSource.MapPath resolves
                // the request path against IWebHostEnvironment's content/web root, which is the
                // equivalent canonicalization check.
                var requestPath = request.Path.Value ?? string.Empty;
                var physicalPath = HttpContextSource.MapPath(requestPath);
                if (requestPath.Contains('\\') || Path.GetFullPath(physicalPath) != physicalPath)
                {
                    DotNetNuke.Services.Exceptions.Exceptions.ProcessHttpException(request);
                }
            }
            catch (Exception exc)
            {
                // DNN 5479
                // the physical path resolution throws an exception when the path of the request exceeds 248 chars.
                // example to test: http://localhost/dotnetnuke_2/xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx/default.aspx
                Logger.BasicUrlRewriterPhysicalPathTooLong(exc);
            }

            this.RewriteUrl(context, out var domainName);
            if (IsRequestCompleted(context))
            {
                // the helper issued a redirect (previously Response.Redirect(url, endResponse: true))
                return;
            }

            // blank DomainName indicates RewriteUrl couldn't locate a current portal
            // reprocess url for portal alias if auto add is an option
            if (domainName == string.Empty && CanAutoAddPortalAlias())
            {
                domainName = Globals.GetDomainName(request, true);
            }

            // from this point on we are dealing with a "standard" querystring ( ie. http://www.domain.com/default.aspx?tabid=## )
            // if the portal/url was successfully identified
            int tabId = Null.NullInteger;
            int portalId = Null.NullInteger;
            string portalAlias = null;
            IPortalAliasInfo portalAliasInfo = null;
            bool parsingError = false;

            // get TabId from querystring ( this is mandatory for maintaining portal context for child portals )
            var tabIdValue = GetQueryValue(request, "tabid");
            if (!string.IsNullOrEmpty(tabIdValue))
            {
                if (!int.TryParse(tabIdValue, out tabId))
                {
                    tabId = Null.NullInteger;
                    parsingError = true;
                }
            }

            // get PortalId from querystring ( this is used for host menu options as well as child portal navigation )
            var portalIdValue = GetQueryValue(request, "portalid");
            if (!string.IsNullOrEmpty(portalIdValue))
            {
                if (!int.TryParse(portalIdValue, out portalId))
                {
                    portalId = Null.NullInteger;
                    parsingError = true;
                }
            }

            if (parsingError)
            {
                // The tabId or PortalId are incorrectly formatted (potential DOS)
                DotNetNuke.Services.Exceptions.Exceptions.ProcessHttpException(request);
            }

            try
            {
                // alias parameter can be used to switch portals
                var aliasValue = GetQueryValue(request, "alias");
                if (aliasValue != null)
                {
                    // check if the alias is valid
                    string childAlias = aliasValue;
                    if (!Globals.UsePortNumber())
                    {
                        childAlias = childAlias.Replace(":" + request.GetUrl().Port, string.Empty);
                    }

                    if (this.PortalAliasService.GetPortalAlias(childAlias) != null)
                    {
                        // check if the domain name contains the alias
                        if (!childAlias.Contains(domainName, StringComparison.OrdinalIgnoreCase))
                        {
                            // redirect to the url defined in the alias
                            response.Redirect(Globals.GetPortalDomainName(childAlias, request, true));
                            CompleteRequest(context);
                            return;
                        }

                        // the alias is the same as the current domain
                        portalAlias = childAlias;
                    }
                }

                // PortalId identifies a portal when set
                if (portalAlias == null)
                {
                    if (portalId != Null.NullInteger)
                    {
                        portalAlias = this.PortalAliasService.GetPortalAliasByPortal(portalId, domainName);
                    }
                }

                // TabId uniquely identifies a Portal
                if (portalAlias == null)
                {
                    if (tabId != Null.NullInteger)
                    {
                        // get the alias from the tabid, but only if it is for a tab in that domain
                        portalAlias = this.PortalAliasService.GetPortalAliasByTab(tabId, domainName);
                        if (string.IsNullOrEmpty(portalAlias))
                        {
                            // if the TabId is not for the correct domain
                            // see if the correct domain can be found and redirect it
                            portalAliasInfo = this.PortalAliasService.GetPortalAlias(domainName);
                            if (portalAliasInfo != null && !request.GetUrl().LocalPath.EndsWith("/linkclick.aspx", StringComparison.OrdinalIgnoreCase))
                            {
                                if (request.GetUrl().AbsoluteUri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                                {
                                    strUrl = "https://" + portalAliasInfo.HttpAlias.Replace("*.", string.Empty);
                                }
                                else
                                {
                                    strUrl = "http://" + portalAliasInfo.HttpAlias.Replace("*.", string.Empty);
                                }

                                if (!strUrl.Contains(domainName, StringComparison.OrdinalIgnoreCase))
                                {
                                    strUrl += request.GetUrl().PathAndQuery;
                                }

                                response.Redirect(strUrl);
                                CompleteRequest(context);
                                return;
                            }
                        }
                    }
                }

                // else use the domain name
                if (string.IsNullOrEmpty(portalAlias))
                {
                    portalAlias = domainName;
                }

                // using the DomainName above will find that alias that is the domainname portion of the Url
                // ie. dotnetnuke.com will be found even if zzz.dotnetnuke.com was entered on the Url
                portalAliasInfo = this.PortalAliasService.GetPortalAlias(portalAlias);
                if (portalAliasInfo != null)
                {
                    portalId = portalAliasInfo.PortalId;
                }

                // if the portalid is not known
                if (portalId == Null.NullInteger)
                {
                    bool autoAddPortalAlias = CanAutoAddPortalAlias();

                    if (!autoAddPortalAlias && !request.GetUrl().LocalPath.EndsWith(Globals.glbDefaultPage, StringComparison.InvariantCultureIgnoreCase))
                    {
                        // allows requests for aspx pages in custom folder locations to be processed
                        return;
                    }

                    if (autoAddPortalAlias)
                    {
                        this.AutoAddAlias(context);
                        if (IsRequestCompleted(context))
                        {
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 500 Error - Redirect to ErrorPage
                Logger.BasicUrlRewriterRewriteUrlException(ex);

                strUrl = "~/ErrorPage.aspx?status=500&error=" + WebUtility.UrlEncode(ex.Message);

                // Server.Transfer equivalent. Rewriting Request.Path is not enough: no endpoint
                // is mapped for ErrorPage.aspx, so the rewritten request would fall through to an
                // empty 404 and the error page would never render. Redirect to the configured
                // friendly error page instead — the same thing the platform's own unhandled
                // exception handler does — and complete the request here. The HTTP status the
                // original conveyed stays in the `status` query-string parameter, which the error
                // page reads; setting Response.StatusCode here would be dead code because
                // Redirect overwrites it with 302.
                response.Clear();
                response.Redirect(string.Concat(Globals.ResolveUrl(ErrorPage.Path), strUrl.AsSpan(strUrl.IndexOf('?'))));
                CompleteRequest(context);
                return;
            }

            // Add PortalId to the log context for use in any logging that occurs during the request
            this.serviceProvider.GetRequiredService<LogRequestContext>().AddToLogContext("PortalId", portalId);

            if (portalId != -1)
            {
                // load the PortalSettings into current context
                var portalSettings = new PortalSettings(tabId, portalAliasInfo as PortalAliasInfo);
                context.Items.Add(Entities.Portals.PortalController.PortalSettingsContextKey, portalSettings);

                // load PortalSettings and HostSettings dictionaries into current context
                // specifically for use in DotNetNuke.Web.Client, which can't reference DotNetNuke.dll to get settings the normal way
                context.Items.Add("PortalSettingsDictionary", Entities.Portals.PortalController.Instance.GetPortalSettings(portalId));
                context.Items.Add("HostSettingsDictionary", this.HostSettingsService.GetSettingsDictionary());

                // don't redirect if no primary alias is defined
                if (portalSettings.PortalAliasMappingMode == PortalSettings.PortalAliasMapping.Redirect
                    && portalAliasInfo != null && !portalAliasInfo.IsPrimary
                    && !string.IsNullOrWhiteSpace(portalSettings.DefaultPortalAlias))
                {
                    // Permanently Redirect
                    response.StatusCode = 301;

                    var redirectAlias = Globals.AddHTTP(portalSettings.DefaultPortalAlias);
                    var checkAlias = Globals.AddHTTP(portalAliasInfo.HttpAlias);
                    var redirectUrl = string.Concat(redirectAlias, request.GetRawUrl());
                    if (redirectUrl.StartsWith(checkAlias, StringComparison.InvariantCultureIgnoreCase))
                    {
                        redirectUrl = string.Concat(redirectAlias, redirectUrl.Substring(checkAlias.Length));
                    }

                    response.Headers.Append("Location", redirectUrl);
                }

                // manage page URL redirects - that reach here because they bypass the built-in navigation
                // ie Spiders, saved favorites, hand-crafted urls etc
                if (!string.IsNullOrEmpty(portalSettings.ActiveTab.Url) && GetQueryValue(request, "ctl") == null &&
                    GetQueryValue(request, "fileticket") == null)
                {
                    // Target Url
                    string redirectUrl = portalSettings.ActiveTab.FullUrl;
                    if (portalSettings.ActiveTab.PermanentRedirect)
                    {
                        // Permanently Redirect
                        response.StatusCode = 301;
                        response.Headers.Append("Location", redirectUrl);
                    }
                    else
                    {
                        // Normal Redirect
                        response.Redirect(redirectUrl);
                        CompleteRequest(context);
                        return;
                    }
                }

                // manage secure connections
                if (request.GetUrl().AbsolutePath.EndsWith(".aspx", StringComparison.InvariantCultureIgnoreCase))
                {
                    // request is for a standard page
                    strUrl = string.Empty;

                    switch (portalSettings.SSLSetup)
                    {
                        case Abstractions.Security.SiteSslSetup.On:
                            if (!request.IsHttps)
                            {
                                // switch to secure connection
                                strUrl = requestedPath.Replace("http://", "https://");
                            }

                            break;
                        case Abstractions.Security.SiteSslSetup.Advanced:
                            // if site is secure or else page is secure and connection is not secure or else SSL offload is enabled and server value exists
                            if (portalSettings.ActiveTab.IsSecure &&
                                !request.IsHttps &&
                                !UrlUtils.IsSslOffloadEnabled(this.HostSettingsService, request))
                            {
                                // switch to secure connection
                                strUrl = requestedPath.Replace("http://", "https://");
                                strUrl = FormatDomain(strUrl, portalSettings.STDURL, portalSettings.SSLURL);
                            }

                            if (portalSettings.SSLEnforced)
                            {
                                // if page is not secure and connection is secure
                                if (!portalSettings.ActiveTab.IsSecure && request.IsHttps)
                                {
                                    // check if connection has already been forced to secure or else SSL offload is disabled
                                    if (GetQueryValue(request, "ssl") == null)
                                    {
                                        strUrl = requestedPath.Replace("https://", "http://");
                                        strUrl = FormatDomain(strUrl, portalSettings.SSLURL, portalSettings.STDURL);
                                    }
                                }
                            }

                            break;
                    }

                    // if a protocol switch is necessary
                    if (!string.IsNullOrEmpty(strUrl))
                    {
                        if (strUrl.StartsWith("https://", StringComparison.InvariantCultureIgnoreCase))
                        {
                            // redirect to secure connection (permanent, response not ended)
                            response.Redirect(strUrl, true);
                        }
                        else
                        {
                            // when switching to an unsecure page, use a clientside redirector to avoid the browser security warning
                            response.Clear();

                            // add a refresh header to the response
                            response.Headers.Append("Refresh", "0;URL=" + strUrl);

                            // add the clientside javascript redirection script
                            // (synchronous body writes are not allowed on ASP.NET Core, so the HTML is
                            // buffered and flushed by the rewriter middleware)
                            WriteOutput(context, "<html><head><title></title>");
                            var clientRedirectScript = "<!-- <script language=\"javascript\">window.location.replace(\"" + strUrl +
                                           "\")</script> -->";
                            WriteOutput(context, clientRedirectScript);
                            WriteOutput(context, "</head><body></body></html>");

                            // send the response (Response.End equivalent)
                            CompleteRequest(context);
                            return;
                        }
                    }
                }
            }
            else
            {
                // alias does not exist in database
                // and all attempts to find another have failed
                // this should only happen if the HostPortal does not have any aliases
                // 404 Error - Redirect to ErrorPage
                strUrl = "~/ErrorPage.aspx?status=404&error=" + domainName;

                // Server.Transfer equivalent. Rewriting Request.Path is not enough: no endpoint
                // is mapped for ErrorPage.aspx, so the rewritten request would fall through to an
                // empty 404 and the error page would never render. Redirect to the configured
                // friendly error page instead, carrying the original status/error parameters.
                response.Clear();
                response.Redirect(string.Concat(Globals.ResolveUrl(ErrorPage.Path), strUrl.AsSpan(strUrl.IndexOf('?'))));
                CompleteRequest(context);
                return;
            }

            if (context.Items["FirstRequest"] != null)
            {
                context.Items.Remove("FirstRequest");

                // Process any messages in the EventQueue for the Application_Start_FirstRequest event
                EventQueueController.ProcessMessages(this.serviceProvider, "Application_Start_FirstRequest");
            }
        }

        // Note these formerly lived in the 'UrlRewriteModule.cs' class
        private static string FormatDomain(string url, string replaceDomain, string withDomain)
        {
            if (!string.IsNullOrEmpty(replaceDomain) && !string.IsNullOrEmpty(withDomain))
            {
                if (url.Contains(replaceDomain, StringComparison.Ordinal))
                {
                    url = url.Replace(replaceDomain, withDomain);
                }
            }

            return url;
        }

        /// <summary>Gets a query string value, or <see langword="null"/> when the key is absent.</summary>
        /// <param name="request">The request.</param>
        /// <param name="key">The query string key (matched case-insensitively, as <c>Request.QueryString</c> did).</param>
        /// <returns>The value, or <see langword="null"/>.</returns>
        private static string GetQueryValue(HttpRequest request, string key)
            => request.Query.TryGetValue(key, out var values) ? values.ToString() : null;

        /// <summary>Buffers response body content for the rewriter middleware to flush asynchronously.</summary>
        /// <param name="context">The current HTTP context.</param>
        /// <param name="content">The content to append.</param>
        private static void WriteOutput(HttpContext context, string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return;
            }

            context.Items.TryGetValue(AdvancedUrlRewriter.PendingOutputKey, out var existing);
            context.Items[AdvancedUrlRewriter.PendingOutputKey] = (existing as string ?? string.Empty) + content;
        }

        /// <summary>Marks the request as fully handled, so the rest of the pipeline is skipped.</summary>
        /// <param name="context">The current HTTP context.</param>
        private static void CompleteRequest(HttpContext context)
            => context.Items[AdvancedUrlRewriter.RequestCompletedKey] = true;

        /// <summary>Determines whether the request has already been completed (redirect written or response started).</summary>
        /// <param name="context">The current HTTP context.</param>
        /// <returns><see langword="true"/> when no further rewriting should occur.</returns>
        private static bool IsRequestCompleted(HttpContext context)
        {
            if (context.Response.HasStarted)
            {
                return true;
            }

            return context.Items.TryGetValue(AdvancedUrlRewriter.RequestCompletedKey, out var completed) && completed is true;
        }

        private void RewriteUrl(HttpContext context, out string portalAlias)
        {
            var request = context.Request;
            var response = context.Response;
            var requestUrl = request.GetUrl();
            string requestedPath = requestUrl.AbsoluteUri;

            portalAlias = string.Empty;

            // determine portal alias looking for longest possible match
            string myAlias = Globals.GetDomainName(request, true);
            IPortalAliasInfo objPortalAlias;
            do
            {
                objPortalAlias = this.PortalAliasService.GetPortalAlias(myAlias);

                if (objPortalAlias != null)
                {
                    portalAlias = myAlias;
                    break;
                }

                int slashIndex = myAlias.LastIndexOf('/');
                myAlias = slashIndex > 1 ? myAlias.Substring(0, slashIndex) : string.Empty;
            }
            while (myAlias.Length > 0);

            context.Items.Add("UrlRewrite:OriginalUrl", requestUrl.AbsoluteUri);

            // Friendly URLs are exposed externally using the following format
            // http://www.domain.com/tabid/###/mid/###/ctl/xxx/default.aspx
            // and processed internally using the following format
            // http://www.domain.com/default.aspx?tabid=###&mid=###&ctl=xxx
            // The system for accomplishing this is based on an extensible Regex rules definition stored in /SiteUrls.config
            string sendTo = string.Empty;

            // save and remove the querystring as it gets added back on later
            // path parameter specifications will take precedence over querystring parameters
            string strQueryString = string.Empty;
            if (!string.IsNullOrEmpty(requestUrl.Query))
            {
                strQueryString = request.QueryString.Value?.TrimStart('?') ?? string.Empty;
                requestedPath = requestedPath.Replace(requestUrl.Query, string.Empty);
            }

            // get url rewriting rules
            RewriterRuleCollection rules = RewriterConfiguration.GetConfig().Rules;

            // the application path (legacy Request.ApplicationPath) is the path base on ASP.NET Core
            var applicationPath = string.IsNullOrEmpty(request.PathBase.Value) ? "/" : request.PathBase.Value;

            // iterate through list of rules
            int matchIndex = -1;
            for (int ruleIndex = 0; ruleIndex <= rules.Count - 1; ruleIndex++)
            {
                // check for the existence of the LookFor value
                string pattern = "^" +
                                 RewriterUtils.ResolveUrl(applicationPath, rules[ruleIndex].LookFor) +
                                 "$";
                Match objMatch = Regex.Match(requestedPath, pattern, RegexOptions.IgnoreCase);

                // if there is a match
                if (objMatch.Success)
                {
                    // create a new URL using the SendTo regex value
                    sendTo = RewriterUtils.ResolveUrl(
                        applicationPath,
                        Regex.Replace(
                            requestedPath,
                            pattern,
                            rules[ruleIndex].SendTo,
                            RegexOptions.IgnoreCase));

                    string parameters = objMatch.Groups[2].Value;

                    // process the parameters
                    if (parameters.Trim().Length > 0)
                    {
                        // split the value into an array based on "/" ( ie. /tabid/##/ )
                        parameters = parameters.Replace(@"\", "/");
                        string[] splitParameters = parameters.Split('/');

                        // icreate a well formed querystring based on the array of parameters
                        for (int parameterIndex = 0; parameterIndex < splitParameters.Length; parameterIndex++)
                        {
                            // ignore the page name
                            if (
                                !splitParameters[parameterIndex].Contains(
                                    ".aspx",
                                    StringComparison.InvariantCultureIgnoreCase))
                            {
                                // get parameter name
                                string parameterName = splitParameters[parameterIndex].Trim();
                                if (parameterName.Length > 0)
                                {
                                    // add parameter to SendTo if it does not exist already
                                    if (
                                        !sendTo.Contains(
                                            "?" + parameterName + "=",
                                            StringComparison.InvariantCultureIgnoreCase) &&
                                        !sendTo.Contains(
                                            "&" + parameterName + "=",
                                            StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        // get parameter delimiter
                                        string parameterDelimiter = sendTo.Contains('?') ? "&" : "?";
                                        sendTo = sendTo + parameterDelimiter + parameterName;

                                        // get parameter value
                                        string parameterValue = string.Empty;
                                        if (parameterIndex < splitParameters.Length - 1)
                                        {
                                            parameterIndex += 1;
                                            if (!string.IsNullOrEmpty(splitParameters[parameterIndex].Trim()))
                                            {
                                                parameterValue = splitParameters[parameterIndex].Trim();
                                            }
                                        }

                                        // add the parameter value
                                        if (parameterValue.Length > 0)
                                        {
                                            sendTo = sendTo + "=" + parameterValue;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    matchIndex = ruleIndex;
                    break; // exit as soon as it processes the first match
                }
            }

            if (!string.IsNullOrEmpty(strQueryString))
            {
                // add querystring parameters back to SendTo
                string[] parameters = strQueryString.Split('&');

                // iterate through the array of parameters
                for (int parameterIndex = 0; parameterIndex <= parameters.Length - 1; parameterIndex++)
                {
                    // get parameter name
                    string parameterName = parameters[parameterIndex];
                    if (parameterName.Contains('='))
                    {
                        parameterName = parameterName.Substring(0, parameterName.IndexOf('='));
                    }

                    // check if parameter already exists
                    if (!sendTo.Contains("?" + parameterName + "=", StringComparison.InvariantCultureIgnoreCase) &&
                        !sendTo.Contains("&" + parameterName + "=", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // add parameter to SendTo value
                        sendTo = sendTo.Contains('?')
                                     ? sendTo + "&" + parameters[parameterIndex]
                                     : sendTo + "?" + parameters[parameterIndex];
                    }
                }
            }

            // if a match was found to the urlrewrite rules
            if (matchIndex != -1)
            {
                if (rules[matchIndex].SendTo.StartsWith('~'))
                {
                    // rewrite the URL for internal processing
                    RewriterUtils.RewriteUrl(context, sendTo);
                }
                else
                {
                    // it is not possible to rewrite the domain portion of the URL so redirect to the new URL
                    response.Redirect(sendTo);
                    CompleteRequest(context);
                }
            }
            else
            {
                // Try to rewrite by TabPath
                string url;
                if (Globals.UsePortNumber() &&
                    ((requestUrl.Port != 80 && !request.IsHttps) ||
                     (requestUrl.Port != 443 && request.IsHttps)))
                {
                    url = requestUrl.Host + ":" + requestUrl.Port + requestUrl.LocalPath;
                }
                else
                {
                    url = requestUrl.Host + requestUrl.LocalPath;
                }

                if (!string.IsNullOrEmpty(myAlias))
                {
                    if (objPortalAlias != null)
                    {
                        int portalId = objPortalAlias.PortalId;

                        // Identify Tab Name
                        string tabPath = url;
                        if (tabPath.StartsWith(myAlias, StringComparison.OrdinalIgnoreCase))
                        {
                            tabPath = url.Remove(0, myAlias.Length);
                        }

                        // Default Page has been Requested
                        if (string.Equals(tabPath, "/" + Globals.glbDefaultPage, StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }

                        // Start of patch
                        string cultureCode = string.Empty;

                        var dicLocales = LocaleController.Instance.GetLocales(portalId);
                        if (dicLocales.Count > 1)
                        {
                            var splitUrl = requestUrl.ToString().Split('/');

                            foreach (var culturePart in splitUrl)
                            {
                                if (culturePart.Contains('-'))
                                {
                                    foreach (var key in dicLocales)
                                    {
                                        if (key.Key.Equals(culturePart, StringComparison.OrdinalIgnoreCase))
                                        {
                                            cultureCode = key.Value.Code;
                                            tabPath = tabPath.Replace("/" + culturePart, string.Empty);
                                            break;
                                        }
                                    }
                                }
                            }
                        }

                        // Check to see if the tab exists (if localization is enabled, check for the specified culture)
                        int tabId = TabController.GetTabByTabPath(
                            this.HostSettings,
                            portalId,
                            tabPath.Replace("/", "//").Replace(".aspx", string.Empty),
                            cultureCode);

                        // Check to see if neutral culture tab exists
                        if (tabId == Null.NullInteger && cultureCode.Length > 0)
                        {
                            tabId = TabController.GetTabByTabPath(
                                this.HostSettings,
                                portalId,
                                tabPath.Replace("/", "//").Replace(".aspx", string.Empty),
                                string.Empty);
                        }

                        // End of patch
                        if (tabId != Null.NullInteger)
                        {
                            string sendToUrl = "~/" + Globals.glbDefaultPage + "?TabID=" + tabId;
                            if (!cultureCode.Equals(string.Empty, StringComparison.Ordinal))
                            {
                                sendToUrl = sendToUrl + "&language=" + cultureCode;
                            }

                            if (!string.IsNullOrEmpty(requestUrl.Query))
                            {
                                sendToUrl = sendToUrl + "&" + requestUrl.Query.TrimStart('?');
                            }

                            RewriterUtils.RewriteUrl(context, sendToUrl);
                            return;
                        }

                        tabPath = tabPath.ToLowerInvariant();
                        if (tabPath.Contains('?'))
                        {
                            tabPath = tabPath.Substring(0, tabPath.IndexOf('?'));
                        }

                        // Get the Portal
                        PortalInfo portal = Entities.Portals.PortalController.Instance.GetPortal(portalId);
                        string requestQuery = requestUrl.Query;
                        if (!string.IsNullOrEmpty(requestQuery))
                        {
                            requestQuery = TabIdRegex.Replace(requestQuery, string.Empty);
                            requestQuery = PortalIdRegex.Replace(requestQuery, string.Empty);
                            requestQuery = requestQuery.TrimStart('?', '&');
                        }

                        if (tabPath == "/login.aspx")
                        {
                            if (portal.LoginTabId > Null.NullInteger && Globals.ValidateLoginTabID(portal.LoginTabId))
                            {
                                if (!string.IsNullOrEmpty(requestQuery))
                                {
                                    RewriterUtils.RewriteUrl(
                                        context,
                                        $"~/{Globals.glbDefaultPage}?TabID={portal.LoginTabId}&{requestQuery}");
                                }
                                else
                                {
                                    RewriterUtils.RewriteUrl(
                                        context,
                                        $"~/{Globals.glbDefaultPage}?TabID={portal.LoginTabId}");
                                }
                            }
                            else
                            {
                                if (!string.IsNullOrEmpty(requestQuery))
                                {
                                    RewriterUtils.RewriteUrl(
                                        context,
                                        $"~/{Globals.glbDefaultPage}?TabID={portal.HomeTabId}&portalid={portalId}&ctl=login&{requestQuery}");
                                }
                                else
                                {
                                    RewriterUtils.RewriteUrl(
                                        context,
                                        $"~/{Globals.glbDefaultPage}?TabID={portal.HomeTabId}&portalid={portalId}&ctl=login");
                                }
                            }

                            return;
                        }

                        if (tabPath == "/register.aspx")
                        {
                            if (portal.RegisterTabId > Null.NullInteger)
                            {
                                if (!string.IsNullOrEmpty(requestQuery))
                                {
                                    RewriterUtils.RewriteUrl(
                                        context,
                                        $"~/{Globals.glbDefaultPage}?TabID={portal.RegisterTabId}&portalid={portalId}&{requestQuery}");
                                }
                                else
                                {
                                    RewriterUtils.RewriteUrl(
                                        context,
                                        $"~/{Globals.glbDefaultPage}?TabID={portal.RegisterTabId}&portalid={portalId}");
                                }
                            }
                            else
                            {
                                if (!string.IsNullOrEmpty(requestQuery))
                                {
                                    RewriterUtils.RewriteUrl(
                                        context,
                                        $"~/{Globals.glbDefaultPage}?TabID={portal.HomeTabId}&portalid={portalId}&ctl=Register&{requestQuery}");
                                }
                                else
                                {
                                    RewriterUtils.RewriteUrl(
                                        context,
                                        $"~/{Globals.glbDefaultPage}?TabID={portal.HomeTabId}&portalid={portalId}&ctl=Register");
                                }
                            }

                            return;
                        }

                        if (tabPath == "/terms.aspx")
                        {
                            if (!string.IsNullOrEmpty(requestQuery))
                            {
                                RewriterUtils.RewriteUrl(
                                    context,
                                    $"~/{Globals.glbDefaultPage}?TabID={portal.HomeTabId}&portalid={portalId}&ctl=Terms&{requestQuery}");
                            }
                            else
                            {
                                RewriterUtils.RewriteUrl(
                                    context,
                                    $"~/{Globals.glbDefaultPage}?TabID={portal.HomeTabId}&portalid={portalId}&ctl=Terms");
                            }

                            return;
                        }

                        if (tabPath == "/privacy.aspx")
                        {
                            if (!string.IsNullOrEmpty(requestQuery))
                            {
                                RewriterUtils.RewriteUrl(
                                    context,
                                    $"~/{Globals.glbDefaultPage}?TabID={portal.HomeTabId}&portalid={portalId}&ctl=Privacy&{requestQuery}");
                            }
                            else
                            {
                                RewriterUtils.RewriteUrl(
                                    context,
                                    $"~/{Globals.glbDefaultPage}?TabID={portal.HomeTabId}&portalid={portalId}&ctl=Privacy");
                            }

                            return;
                        }

                        tabPath = tabPath.Replace("/", "//");
                        tabPath = tabPath.Replace(".aspx", string.Empty);
                        var objTabs = TabController.Instance.GetTabsByPortal(tabPath.StartsWith("//host", StringComparison.OrdinalIgnoreCase) ? Null.NullInteger : portalId);
                        foreach (var kvp in objTabs)
                        {
                            if (!kvp.Value.IsDeleted && kvp.Value.TabPath.Equals(tabPath, StringComparison.OrdinalIgnoreCase))
                            {
                                if (!string.IsNullOrEmpty(requestUrl.Query))
                                {
                                    RewriterUtils.RewriteUrl(
                                        context,
                                        $"~/{Globals.glbDefaultPage}?TabID={kvp.Value.TabID}&{requestUrl.Query.TrimStart('?')}");
                                }
                                else
                                {
                                    RewriterUtils.RewriteUrl(
                                        context,
                                        $"~/{Globals.glbDefaultPage}?TabID={kvp.Value.TabID}");
                                }

                                return;
                            }
                        }
                    }
                }
            }
        }
    }
}
