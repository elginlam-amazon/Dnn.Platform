// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Services.GeneratedImage
{
    using System;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Maps the <see cref="DnnImageHandler"/> onto its endpoint.</summary>
    /// <remarks>
    /// The handler used to be registered in <c>web.config</c> as
    /// <c>&lt;add name="DnnImageHandler" path="DnnImageHandler.ashx" verb="*"
    /// type="DotNetNuke.Services.GeneratedImage.DnnImageHandler, DotNetNuke" /&gt;</c>. Because IIS
    /// matched that mapping on the file name at any folder depth — and DNN emits the URL relative to
    /// the current site root, e.g. <c>/childportal/DnnImageHandler.ashx?mode=profilepic&amp;…</c> —
    /// both an exact root route and a wildcard route are mapped, mirroring what
    /// <see cref="DotNetNuke.UI.WebControls.CaptchaHandler"/> does for the captcha image.
    /// </remarks>
    public static class DnnImageHandlerEndpoint
    {
        /// <summary>The path the generated-image handler is served from, unchanged from the WebForms handler.</summary>
        public const string RouteTemplate = "/DnnImageHandler.ashx";

        /// <summary>The catch-all route that matches <c>DnnImageHandler.ashx</c> at any folder depth.</summary>
        public const string WildcardRouteTemplate = "/{**imageHandlerPath:regex(^.*/DnnImageHandler\\.ashx$)}";

        /// <summary>Maps the generated-image endpoint.</summary>
        /// <param name="endpoints">The endpoint route builder to map onto.</param>
        /// <returns><paramref name="endpoints"/>, for chaining.</returns>
        /// <remarks>
        /// The handler is created from the request's service provider so that its
        /// <c>IApplicationStatusInfo</c> and <c>IPortalAliasService</c> dependencies are injected.
        /// All verbs are mapped, because the <c>web.config</c> entry used <c>verb="*"</c>.
        /// </remarks>
        public static IEndpointRouteBuilder MapDnnImageHandler(this IEndpointRouteBuilder endpoints)
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            endpoints.Map(RouteTemplate, HandleAsync);
            endpoints.Map(WildcardRouteTemplate, HandleAsync);
            return endpoints;
        }

        private static Task HandleAsync(HttpContext context)
        {
            var handler = ActivatorUtilities.CreateInstance<DnnImageHandler>(context.RequestServices);
            return handler.ProcessRequestAsync(context);
        }
    }
}
