// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api.Internal
{
    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// Determines whether the current request was routed to a DNN services framework endpoint.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the scope gate that keeps the ported services framework pipeline from leaking into the rest of
    /// the application. On .NET Framework the services framework had its OWN pipeline: the request context
    /// handler, the authentication handler chain and the filter provider all lived in
    /// <c>HttpConfiguration</c> (<c>MessageHandlers</c> / <c>Services</c>), so they could only ever observe a
    /// Web API request. ASP.NET Core has a single middleware pipeline and a single filter pipeline shared with
    /// Blazor components, the DotNetNuke.Web.Mvc module host, static files and every other endpoint.
    /// </para>
    /// <para>
    /// Without this gate the ported pieces are silently over-applied: the request-context middleware would
    /// overwrite <c>HttpContext.Items["PortalSettings"]</c> for page requests and could reject one with a
    /// <c>400</c>, and the authentication chain would append <c>WWW-Authenticate: Basic realm="DNNAPI"</c> to
    /// every <c>401</c> in the application, popping a browser credential dialog instead of redirecting to the
    /// DNN login page.
    /// </para>
    /// <para>
    /// The test is endpoint metadata rather than a URL prefix: <c>ServicesRoutingManager.MapRoutes</c> attaches
    /// the <see cref="DnnServiceRoute"/> to every route it maps (including the <c>-old</c>
    /// <c>DesktopModules/{namespace}/API/{controller}/{action}</c> compatibility routes), so the gate covers
    /// exactly the set of routes the legacy <c>HttpConfiguration</c> pipeline covered — no more, no less — and
    /// automatically covers any route a third-party module's <c>IServiceRouteMapper</c> adds.
    /// </para>
    /// </remarks>
    internal static class DnnServiceEndpoint
    {
        /// <summary>Determines whether <paramref name="context"/> was routed to a DNN service endpoint.</summary>
        /// <param name="context">The current HTTP context.</param>
        /// <returns>
        /// <see langword="true"/> when the matched endpoint carries <see cref="DnnServiceRoute"/> metadata;
        /// otherwise <see langword="false"/> (including when routing has not matched an endpoint).
        /// </returns>
        internal static bool IsServiceRequest(HttpContext context)
        {
            return context?.GetEndpoint()?.Metadata.GetMetadata<DnnServiceRoute>() is not null;
        }
    }
}
