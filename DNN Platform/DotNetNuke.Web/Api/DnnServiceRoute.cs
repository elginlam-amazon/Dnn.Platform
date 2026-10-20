// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api
{
    using System.Collections.Generic;

    using Microsoft.AspNetCore.Routing;

    /// <summary>A services framework route registered by an <see cref="IServiceRouteMapper"/> (mapped to an ASP.NET Core endpoint).</summary>
    public class DnnServiceRoute
    {
        /// <summary>Initializes a new instance of the <see cref="DnnServiceRoute"/> class.</summary>
        /// <param name="name">The unique name of the route.</param>
        /// <param name="url">The URL pattern of the route.</param>
        /// <param name="defaults">The default values.</param>
        /// <param name="constraints">The constraints.</param>
        /// <param name="namespaces">The namespaces of the controllers handled by the route.</param>
        public DnnServiceRoute(string name, string url, object defaults, object constraints, IReadOnlyList<string> namespaces)
        {
            this.Name = name;
            this.Url = url;
            this.Defaults = new RouteValueDictionary(defaults);
            this.Constraints = new RouteValueDictionary(constraints);
            this.Namespaces = namespaces ?? [];
            this.DataTokens = new RouteValueDictionary();
        }

        /// <summary>Gets the unique name of the route.</summary>
        public string Name { get; }

        /// <summary>Gets the URL pattern of the route.</summary>
        public string Url { get; }

        /// <summary>Gets the default values.</summary>
        public RouteValueDictionary Defaults { get; }

        /// <summary>Gets the constraints.</summary>
        public RouteValueDictionary Constraints { get; }

        /// <summary>Gets the namespaces of the controllers handled by the route.</summary>
        public IReadOnlyList<string> Namespaces { get; private set; }

        /// <summary>Gets the data tokens of the route.</summary>
        public RouteValueDictionary DataTokens { get; }

        /// <summary>Sets the namespaces of the controllers handled by the route.</summary>
        /// <param name="namespaces">The namespaces.</param>
        internal void SetNamespaces(IReadOnlyList<string> namespaces)
        {
            this.Namespaces = namespaces ?? [];
        }
    }
}
