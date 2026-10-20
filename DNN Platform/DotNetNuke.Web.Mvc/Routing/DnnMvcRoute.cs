// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Routing
{
    using Microsoft.AspNetCore.Routing;

    /// <summary>Describes a DNN MVC route collected during route registration and later mapped to an ASP.NET Core endpoint.</summary>
    public class DnnMvcRoute
    {
        /// <summary>Gets or sets the unique route name.</summary>
        public string Name { get; set; }

        /// <summary>Gets or sets the route URL pattern.</summary>
        public string Url { get; set; }

        /// <summary>Gets or sets the route default values.</summary>
        public RouteValueDictionary Defaults { get; set; }

        /// <summary>Gets or sets the route constraints.</summary>
        public RouteValueDictionary Constraints { get; set; }

        /// <summary>Gets or sets the namespaces searched for controllers for this route.</summary>
        public string[] Namespaces { get; set; }
    }
}
