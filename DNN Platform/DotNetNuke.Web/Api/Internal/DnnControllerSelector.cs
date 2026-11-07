// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api.Internal
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;

    using DotNetNuke.Services.Localization;
    using DotNetNuke.Web.Common.Internal;

    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.AspNetCore.Mvc.Infrastructure;

    /// <summary>
    /// Resolves the DNN service controller of a request within the controller namespaces of the matched
    /// route. This is the replacement for the legacy <c>DnnHttpControllerSelector</c>
    /// (<c>System.Web.Http.Dispatcher.IHttpControllerSelector</c>), which has no ASP.NET Core equivalent:
    /// Core selects controllers by their short name only, which would make two modules that both ship (for
    /// example) a <c>ServicesController</c> collide. The namespaces supplied to
    /// <see cref="IMapRoute.MapHttpRoute(string,string,string,string[])"/> keep their meaning through this
    /// service, which <see cref="DnnControllerNamespaceMatcherPolicy"/> applies during endpoint selection.
    /// </summary>
    internal sealed class DnnControllerSelector
    {
        private const string ControllerSuffix = "Controller";

        private readonly IActionDescriptorCollectionProvider actionDescriptorCollectionProvider;
        private readonly ConcurrentDictionary<string, string> descriptorCache = new(StringComparer.OrdinalIgnoreCase);
        private int cachedVersion = -1;

        /// <summary>Initializes a new instance of the <see cref="DnnControllerSelector"/> class.</summary>
        /// <param name="actionDescriptorCollectionProvider">The action descriptor collection provider.</param>
        public DnnControllerSelector(IActionDescriptorCollectionProvider actionDescriptorCollectionProvider)
        {
            ArgumentNullException.ThrowIfNull(actionDescriptorCollectionProvider);

            this.actionDescriptorCollectionProvider = actionDescriptorCollectionProvider;
        }

        /// <summary>Selects the controller for the given controller name and route namespaces.</summary>
        /// <param name="controllerName">The <c>controller</c> route value.</param>
        /// <param name="namespaces">The namespaces of the matched route.</param>
        /// <param name="requestUri">The request URI, used in the error messages.</param>
        /// <returns>A <see cref="DnnControllerSelectionResult"/>.</returns>
        public DnnControllerSelectionResult SelectController(string controllerName, IEnumerable<string> namespaces, string requestUri)
        {
            var namespaceList = namespaces?.ToArray();
            if (namespaceList == null || namespaceList.Length == 0 || string.IsNullOrEmpty(controllerName))
            {
                return DnnControllerSelectionResult.Failure(
                    HttpStatusCode.NotFound,
                    "Unable to locate a controller for " + requestUri);
            }

            var cache = this.GetDescriptorCache();
            var matches = new List<string>();
            foreach (var ns in namespaceList)
            {
                var fullName = GetFullName(controllerName, ns);
                if (cache.TryGetValue(fullName, out var controllerTypeFullName))
                {
                    matches.Add(controllerTypeFullName);
                }
            }

            if (matches.Count == 1)
            {
                return DnnControllerSelectionResult.Found(matches[0]);
            }

            // only errors beyond this point
            if (matches.Count == 0)
            {
                return DnnControllerSelectionResult.Failure(
                    HttpStatusCode.NotFound,
                    string.Format(
                        CultureInfo.CurrentCulture,
                        CompositeFormatCache.Get(Localization.GetString("ControllerNotFound", Localization.ExceptionsResourceFile)),
                        requestUri,
                        string.Join(", ", namespaceList)));
            }

            return DnnControllerSelectionResult.Failure(
                HttpStatusCode.Conflict,
                string.Format(
                    CultureInfo.CurrentCulture,
                    CompositeFormatCache.Get(Localization.GetString("AmbiguousController", Localization.ExceptionsResourceFile)),
                    controllerName,
                    string.Join(", ", namespaceList)));
        }

        private static string GetFullName(string controllerName, string ns)
        {
            return $"{ns}.{controllerName}{ControllerSuffix}";
        }

        private ConcurrentDictionary<string, string> GetDescriptorCache()
        {
            var collection = this.actionDescriptorCollectionProvider.ActionDescriptors;
            if (this.cachedVersion == collection.Version)
            {
                return this.descriptorCache;
            }

            this.descriptorCache.Clear();
            foreach (var controllerType in collection.Items
                .OfType<ControllerActionDescriptor>()
                .Select(descriptor => descriptor.ControllerTypeInfo)
                .Where(type => type?.FullName != null))
            {
                this.descriptorCache.TryAdd(controllerType.FullName, controllerType.FullName);
            }

            this.cachedVersion = collection.Version;
            return this.descriptorCache;
        }
    }
}
