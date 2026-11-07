// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Syndication
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Portals;

    using Microsoft.AspNetCore.Http;

    /// <summary>An HTTP handler for generating an RSS feed.</summary>
    /// <remarks>
    /// This used to derive from an <c>IHttpHandler</c> base registered in <c>web.config</c> for
    /// <c>RSS.aspx</c>. In ASP.NET Core the request is dispatched from an endpoint; see
    /// <c>RssEndpoint.MapDnnRss</c>.
    /// </remarks>
    public class SyndicationHandlerBase : GenericRssHttpHandlerBase
    {
        private int moduleId = Null.NullInteger;

        private int tabId = Null.NullInteger;

        /// <summary>Gets the portal settings.</summary>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public PortalSettings Settings => Globals.GetPortalSettings();

        /// <summary>Gets the tab ID of the request.</summary>
        public int TabId
        {
            get
            {
                if (this.tabId == Null.NullInteger && TryGetQueryValue(this.Request, "tabid", out var rawTabId))
                {
                    if (!int.TryParse(rawTabId, out this.tabId))
                    {
                        this.tabId = Null.NullInteger;
                    }
                }

                return this.tabId;
            }
        }

        /// <summary>Gets the module ID of the request.</summary>
        public int ModuleId
        {
            get
            {
                if (this.moduleId == Null.NullInteger && TryGetQueryValue(this.Request, "moduleid", out var rawModuleId))
                {
                    if (!int.TryParse(rawModuleId, out this.moduleId))
                    {
                        this.moduleId = Null.NullInteger;
                    }
                }

                return this.moduleId;
            }
        }

        /// <summary>Gets the HTTP request.</summary>
        /// <remarks>
        /// Returns the request of the context the handler is processing (<see langword="null"/>
        /// before the handler has been initialized), rather than the old
        /// <c>HttpContext.Current.Request</c>.
        /// </remarks>
        [CLSCompliant(false)]
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public HttpRequest Request => this.Context?.Request;

        /// <summary>Reads a query string value, treating a missing key the same way the old <c>Request.QueryString[key] != null</c> check did.</summary>
        /// <param name="request">The request, which may be <see langword="null"/>.</param>
        /// <param name="key">The query string key.</param>
        /// <param name="value">The raw value, when present.</param>
        /// <returns><see langword="true"/> when the key was present on the query string.</returns>
        private static bool TryGetQueryValue(HttpRequest request, string key, out string value)
        {
            if (request is not null && request.Query.TryGetValue(key, out var values))
            {
                value = values.ToString();
                return true;
            }

            value = null;
            return false;
        }
    }
}
