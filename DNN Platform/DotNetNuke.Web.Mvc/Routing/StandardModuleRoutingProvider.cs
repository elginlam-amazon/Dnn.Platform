// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Routing
{
    using System.Collections.Generic;
    using System.Linq;

    using DotNetNuke.Abstractions;
    using DotNetNuke.Common;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.UI.Modules;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>The standard DNN module routing provider.</summary>
    public class StandardModuleRoutingProvider : ModuleRoutingProvider
    {
        private const string ExcludedQueryStringParams = "tabid,mid,ctl,language,popup,action,controller";
        private const string ExcludedRouteValues = "mid,ctl,popup";

        /// <summary>Initializes a new instance of the <see cref="StandardModuleRoutingProvider"/> class.</summary>
        public StandardModuleRoutingProvider()
        {
            this.NavigationManager = Globals.GetCurrentServiceProvider().GetRequiredService<INavigationManager>();
        }

        /// <summary>Gets the navigation manager.</summary>
        protected INavigationManager NavigationManager { get; }

        /// <inheritdoc />
        public override string GenerateUrl(RouteValueDictionary routeValues, ModuleInstanceContext moduleContext)
        {
            string controlKey = routeValues.ContainsKey("ctl") ? (string)routeValues["ctl"] : string.Empty;

            var additionalParams = (from routeValue in routeValues
                                    where !ExcludedRouteValues.Split(',').ToList().Contains(routeValue.Key.ToLowerInvariant())
                                    select routeValue.Key + "=" + routeValue.Value)
                                    .ToList();

            string url;
            if (string.IsNullOrEmpty(controlKey))
            {
                additionalParams.Insert(0, "moduleId=" + moduleContext.Configuration.ModuleID);
                url = this.NavigationManager.NavigateURL(string.Empty, additionalParams.ToArray());
            }
            else
            {
                url = moduleContext.EditUrl(string.Empty, string.Empty, controlKey, additionalParams.ToArray());
            }

            return url;
        }

        /// <inheritdoc />
        public override RouteData GetRouteData(HttpContext httpContext, ModuleControlInfo moduleControl)
        {
            var segments = moduleControl.ControlSrc.Replace(".mvc", string.Empty).Split('/');
            string routeNamespace = string.Empty;
            string routeControllerName;
            string routeActionName;
            if (segments.Length == 4)
            {
                routeNamespace = segments[0];
                routeControllerName = segments[1];
                routeActionName = segments[3];
            }
            else if (segments.Length == 3)
            {
                routeNamespace = segments[0];
                routeControllerName = segments[1];
                routeActionName = segments[2];
            }
            else
            {
                routeControllerName = segments[0];
                routeActionName = segments[1];
            }

            var query = httpContext?.Request.Query;
            var actionName = GetQueryValue(query, "action", routeActionName);
            var controllerName = GetQueryValue(query, "controller", routeControllerName);

            var routeData = new RouteData();
            routeData.Values.Add("controller", controllerName);
            routeData.Values.Add("action", actionName);

            if (query != null)
            {
                foreach (var param in query.Keys)
                {
                    if (!ExcludedQueryStringParams.Split(',').ToList().Contains(param.ToLowerInvariant()))
                    {
                        routeData.Values.Add(param, query[param].ToString());
                    }
                }
            }

            if (!string.IsNullOrEmpty(routeNamespace))
            {
                routeData.DataTokens.Add("namespaces", new[] { routeNamespace });
            }

            return routeData;
        }

        private static string GetQueryValue(IQueryCollection query, string key, string fallback)
        {
            if (query != null && query.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
            {
                return value;
            }

            return fallback;
        }
    }
}
