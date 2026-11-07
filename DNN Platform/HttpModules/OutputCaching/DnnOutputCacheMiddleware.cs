// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.HttpModules.OutputCaching
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.Portals;
    using DotNetNuke.Common;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.Services.Installer.Blocker;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Services.OutputCache;
    using DotNetNuke.UI;

    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// Manages the per-tab output cache for a request. This is the ASP.NET Core replacement for
    /// the legacy <c>OutputCacheModule</c> <c>IHttpModule</c>.
    /// </summary>
    /// <remarks>
    /// The code before <c>await next</c> is the former <c>ResolveRequestCache</c> handler (cache
    /// lookup, short-circuit on a hit, response capture on a miss) and the code after it is the
    /// former <c>UpdateRequestCache</c> handler (persisting the captured response).
    /// </remarks>
    public class DnnOutputCacheMiddleware
    {
        /// <summary>
        /// The <see cref="HttpContext.Items"/> key (and response header name) that signals that the
        /// response was served from the output cache. The compression module reads this item.
        /// </summary>
        public const string OutputCacheItemKey = "DNNOutputCache";

        private const string ContextKeyResponseFilter = "OutputCache:ResponseFilter";
        private const string ContextKeyTabId = "OutputCache:TabId";
        private const string ContextKeyTabOutputCacheProvider = "OutputCache:TabOutputCacheProvider";

        private readonly RequestDelegate next;

        /// <summary>Initializes a new instance of the <see cref="DnnOutputCacheMiddleware"/> class.</summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        public DnnOutputCacheMiddleware(RequestDelegate next)
        {
            this.next = next ?? throw new ArgumentNullException(nameof(next));
        }

        private enum IncludeExcludeType
        {
            IncludeByDefault = 0,
            ExcludeByDefault = 1,
        }

        /// <summary>Serves the request from the tab output cache, or captures the response into it.</summary>
        /// <param name="context">The current request.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var originalBody = context.Response.Body;
            OutputCacheResponseFilter responseFilter = null;
            try
            {
                if (!IsCacheableRequest(context))
                {
                    await this.next(context).ConfigureAwait(false);
                    return;
                }

                // (b) the portal/tab the rewriter resolved, plus that tab's cache settings.
                var portalSettings = context.Items["PortalSettings"] as PortalSettings;
                var tabId = portalSettings?.ActiveTab?.TabID;
                if (tabId == null)
                {
                    await this.next(context).ConfigureAwait(false);
                    return;
                }

                Hashtable tabSettings = TabController.Instance.GetTabSettings(tabId.Value);
                if (tabSettings["CacheProvider"] == null || string.IsNullOrEmpty(tabSettings["CacheProvider"].ToString()))
                {
                    await this.next(context).ConfigureAwait(false);
                    return;
                }

                var tabOutputCacheProvider = tabSettings["CacheProvider"].ToString();
                context.Items[ContextKeyTabOutputCacheProvider] = tabOutputCacheProvider;

                var cacheKey = BuildCacheKey(context, portalSettings, tabId.Value, tabSettings, tabOutputCacheProvider, out var maxCachedVariationsForTab);

                // (d) cache hit: write the cached response and short-circuit the pipeline.
                // The content type is set BEFORE the cached body is written: once a provider
                // flushes, the response has started and header writes throw. The legacy module
                // emitted "{ContentType}; charset={Charset}", which on ASP.NET Core means the
                // charset has to be part of the value we write.
                var cachedContentType = context.Response.Headers.ContentType.ToString();
                context.Response.ContentType = string.IsNullOrEmpty(cachedContentType)
                    ? "text/html; charset=utf-8"
                    : cachedContentType.Contains("charset=", StringComparison.OrdinalIgnoreCase)
                        ? cachedContentType
                        : cachedContentType + "; charset=utf-8";

                if (OutputCachingProvider.Instance(tabOutputCacheProvider).StreamOutput(tabId.Value, cacheKey, context))
                {
                    // This is to give a site owner the ability to visually verify that a page was
                    // rendered via the output cache.
                    context.Response.Headers[OutputCacheItemKey] = "true";

                    // Also add it to the Context, because we need to know if output caching is
                    // active in the compression module.
                    context.Items[OutputCacheItemKey] = "true";

                    // Replaces the legacy Response.End(): do not invoke the rest of the pipeline.
                    return;
                }

                context.Items[ContextKeyTabId] = tabId;

                // (e) cache miss: capture the response into the provider's filter stream.
                if (tabSettings["CacheDuration"] != null &&
                    !string.IsNullOrEmpty(tabSettings["CacheDuration"].ToString()) &&
                    Convert.ToInt32(tabSettings["CacheDuration"].ToString(), CultureInfo.InvariantCulture) > 0)
                {
                    var seconds = Convert.ToInt32(tabSettings["CacheDuration"].ToString(), CultureInfo.InvariantCulture);
                    var duration = new TimeSpan(0, 0, seconds);

                    responseFilter = OutputCachingProvider.Instance(context.Items[ContextKeyTabOutputCacheProvider].ToString()).GetResponseFilter(
                        Convert.ToInt32(context.Items[ContextKeyTabId], CultureInfo.InvariantCulture),
                        maxCachedVariationsForTab,
                        originalBody,
                        cacheKey,
                        duration);
                    context.Items[ContextKeyResponseFilter] = responseFilter;

                    // Replaces `Response.Filter = responseFilter`: everything the rest of the
                    // pipeline writes goes both to the real body and to the capture stream.
                    responseFilter.AttachTo(context);
                    context.Response.Body = responseFilter;
                }

                await this.next(context).ConfigureAwait(false);

                // (f) the former UpdateRequestCache handler.
                var isRedirect = context.Response.StatusCode == (int)HttpStatusCode.Redirect;
                if (!BrowserCapabilities.IsCrawler(context.Request) && !isRedirect)
                {
                    if (context.Items[ContextKeyResponseFilter] is OutputCacheResponseFilter filter)
                    {
                        await filter.FlushAsync(context.RequestAborted).ConfigureAwait(false);
                        filter.StopFiltering(Convert.ToInt32(context.Items[ContextKeyTabId], CultureInfo.InvariantCulture), false);
                    }
                }
            }
            finally
            {
                // (g) the response body must be restored on every path or the request hangs.
                responseFilter?.Detach();
                context.Response.Body = originalBody;
            }
        }

        /// <summary>(a) The eligibility gate of the legacy <c>OnResolveRequestCache</c>.</summary>
        /// <param name="context">The current request.</param>
        /// <returns><see langword="true"/> when the request may be served from, or stored in, the output cache.</returns>
        private static bool IsCacheableRequest(HttpContext context)
        {
            // On ASP.NET Core the content type is not set until the endpoint runs, while classic
            // ASP.NET defaulted Response.ContentType to "text/html"; an unset content type is
            // therefore treated the same way the legacy module treated the default.
            var contentType = context.Response.ContentType;
            var isHtml = string.IsNullOrEmpty(contentType) ||
                         contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase);
            if (!isHtml)
            {
                return false;
            }

            if (context.User?.Identity?.IsAuthenticated == true)
            {
                return false;
            }

            if (BrowserCapabilities.IsCrawler(context.Request))
            {
                return false;
            }

            if (InstallBlocker.Instance.IsInstallInProgress())
            {
                return false;
            }

            if (HttpMethods.IsPost(context.Request.Method))
            {
                return false;
            }

            var localPath = context.Request.Path.HasValue ? context.Request.Path.Value : string.Empty;
            return localPath.EndsWith(Globals.glbDefaultPage, StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// (c) Builds the vary-by key collections from the tab's cache settings and the request
        /// query string, then (d) generates the provider cache key for them.
        /// </summary>
        /// <param name="context">The current request.</param>
        /// <param name="portalSettings">The current portal settings.</param>
        /// <param name="tabId">The active tab ID.</param>
        /// <param name="tabSettings">The active tab's settings.</param>
        /// <param name="tabOutputCacheProvider">The tab's output cache provider name.</param>
        /// <param name="maxCachedVariationsForTab">The maximum number of cached variations allowed for this tab.</param>
        /// <returns>The provider cache key for this request.</returns>
        private static string BuildCacheKey(
            HttpContext context,
            PortalSettings portalSettings,
            int tabId,
            Hashtable tabSettings,
            string tabOutputCacheProvider,
            out int maxCachedVariationsForTab)
        {
            var portalId = portalSettings.PortalId;
            var locale = Localization.GetPageLocale((IPortalSettings)portalSettings).Name;

            var includeExclude = IncludeExcludeType.ExcludeByDefault;
            if (tabSettings["CacheIncludeExclude"] != null && !string.IsNullOrEmpty(tabSettings["CacheIncludeExclude"].ToString()))
            {
                if (tabSettings["CacheIncludeExclude"].ToString() == "0")
                {
                    includeExclude = IncludeExcludeType.ExcludeByDefault;
                }
                else
                {
                    includeExclude = IncludeExcludeType.IncludeByDefault;
                }
            }

            maxCachedVariationsForTab = 250; // by default, prevent DOS attacks
            if (tabSettings["MaxVaryByCount"] != null && !string.IsNullOrEmpty(tabSettings["MaxVaryByCount"].ToString()))
            {
                maxCachedVariationsForTab = Convert.ToInt32(tabSettings["MaxVaryByCount"].ToString(), CultureInfo.InvariantCulture);
            }

            var includeVaryByKeys = new StringCollection
            {
                "ctl",
                "returnurl",
                "tabid",
                "portalid",
                "locale",
                "alias",
            };

            // make sure to always add keys in lowercase only
            if (includeExclude == IncludeExcludeType.ExcludeByDefault)
            {
                var includeVaryByKeysSettings = string.Empty;
                if (tabSettings["IncludeVaryBy"] != null)
                {
                    includeVaryByKeysSettings = tabSettings["IncludeVaryBy"].ToString();
                }

                if (!string.IsNullOrEmpty(includeVaryByKeysSettings))
                {
                    if (includeVaryByKeysSettings.Contains(','))
                    {
                        var keys = includeVaryByKeysSettings.Split(',');
                        foreach (var key in keys)
                        {
                            includeVaryByKeys.Add(key.Trim().ToLowerInvariant());
                        }
                    }
                    else
                    {
                        includeVaryByKeys.Add(includeVaryByKeysSettings.Trim().ToLowerInvariant());
                    }
                }
            }

            var excludeVaryByKeys = new StringCollection();
            if (includeExclude == IncludeExcludeType.IncludeByDefault)
            {
                var excludeVaryByKeysSettings = string.Empty;
                if (tabSettings["ExcludeVaryBy"] != null)
                {
                    excludeVaryByKeysSettings = tabSettings["ExcludeVaryBy"].ToString();
                }

                if (!string.IsNullOrEmpty(excludeVaryByKeysSettings))
                {
                    if (excludeVaryByKeysSettings.Contains(','))
                    {
                        var keys = excludeVaryByKeysSettings.Split(',');
                        foreach (var key in keys)
                        {
                            excludeVaryByKeys.Add(key.Trim().ToLowerInvariant());
                        }
                    }
                    else
                    {
                        excludeVaryByKeys.Add(excludeVaryByKeysSettings.Trim().ToLowerInvariant());
                    }
                }
            }

            var varyBy = new SortedDictionary<string, string>();

            foreach (var queryStringEntry in context.Request.Query)
            {
                if (queryStringEntry.Key != null && queryStringEntry.Value.Count > 0)
                {
                    var varyKey = queryStringEntry.Key.ToLowerInvariant();
                    varyBy[varyKey] = queryStringEntry.Value.ToString();

                    if (includeExclude == IncludeExcludeType.IncludeByDefault && !includeVaryByKeys.Contains(varyKey))
                    {
                        includeVaryByKeys.Add(varyKey);
                    }
                }
            }

            if (!varyBy.ContainsKey("portalid"))
            {
                varyBy.Add("portalid", portalId.ToString(CultureInfo.InvariantCulture));
            }

            if (!varyBy.ContainsKey("tabid"))
            {
                varyBy.Add("tabid", tabId.ToString(CultureInfo.InvariantCulture));
            }

            if (!varyBy.ContainsKey("locale"))
            {
                varyBy.Add("locale", locale);
            }

            if (!varyBy.ContainsKey("alias"))
            {
                varyBy.Add("alias", ((IPortalAliasInfo)portalSettings.PortalAlias).HttpAlias);
            }

            return OutputCachingProvider.Instance(tabOutputCacheProvider)
                .GenerateCacheKey(tabId, includeVaryByKeys, excludeVaryByKeys, varyBy);
        }
    }
}
