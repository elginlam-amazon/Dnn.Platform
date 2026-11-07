// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvp
{
    using System.Threading.Tasks;

    using DotNetNuke.Internal.SourceGenerators;

    using Microsoft.AspNetCore.Http;

    /// <summary>The view of a Model-View-Presenter request handler (formerly an <c>.ashx</c>).</summary>
    /// <remarks>
    /// Replaces the WebFormsMvp view contract of an <c>IHttpHandler</c>. The handler is no longer an
    /// <c>IHttpHandler</c> registered in <c>web.config</c>; it is an endpoint, mapped by
    /// <see cref="MvpEndpointRouteBuilderExtensions.MapDnnMvpEndpoints"/> at the path declared with
    /// <see cref="MvpHandlerRouteAttribute"/>, and it writes to the response directly as it always did.
    /// </remarks>
    [DnnDeprecated(9, 2, 0, "Replace WebFormsMvp and DotNetNuke.Web.Mvp with MVC or SPA patterns instead")]
    public partial interface IHttpHandlerView : IMvpView
    {
        /// <summary>Handles a request routed to this view.</summary>
        /// <param name="context">The context of the request.</param>
        /// <returns>A task which completes once the response has been written.</returns>
        /// <remarks>Replaces <c>IHttpHandler.ProcessRequest(HttpContext)</c>.</remarks>
        Task HandleRequestAsync(HttpContext context);
    }
}
