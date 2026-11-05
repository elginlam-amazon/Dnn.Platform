// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.Modules.Html5
{
    using System;
    using System.Globalization;

    using DotNetNuke.Common;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Services.Tokens;
    using Microsoft.AspNetCore.Http;

    /// <summary>Replaces tokens related to the current http request.</summary>
    public class RequestPropertyAccess : IPropertyAccess
    {
        private readonly HttpRequest request;

        /// <summary>Initializes a new instance of the <see cref="RequestPropertyAccess"/> class.</summary>
        /// <param name="request">The current http request.</param>
        public RequestPropertyAccess(HttpRequest request)
        {
            this.request = request;
        }

        /// <inheritdoc />
        public virtual CacheLevel Cacheability
        {
            get { return CacheLevel.notCacheable; }
        }

        /// <inheritdoc />
        public string GetProperty(string propertyName, string format, CultureInfo formatProvider, UserInfo accessingUser, Scope accessLevel, ref bool propertyNotFound)
        {
            switch (propertyName.ToLowerInvariant())
            {
                case "querystring":
                    // Was Request.QueryString.ToString(), i.e. the raw query string without the leading '?'.
                    return this.request == null ? string.Empty : this.request.QueryString.ToString().TrimStart('?');
                case "applicationpath":
                    return GetApplicationPath();
                case "relativeapppath":
                    // RelativeAppPath is like ApplicationPath, but will always end with a forward slash (/)
                    var applicationPath = GetApplicationPath();
                    return applicationPath.EndsWith("/", StringComparison.Ordinal)
                        ? applicationPath
                        : $"{applicationPath}/";
            }

            propertyNotFound = true;
            return string.Empty;
        }

        /// <summary>Gets the application's virtual path.</summary>
        /// <returns>The application path.</returns>
        /// <remarks>Replaces <c>HttpRequest.ApplicationPath</c>, which does not exist on ASP.NET Core.</remarks>
        private static string GetApplicationPath()
        {
            var applicationPath = HttpContextSource.ApplicationVirtualPath;
            return string.IsNullOrEmpty(applicationPath) ? "/" : applicationPath;
        }
    }
}
