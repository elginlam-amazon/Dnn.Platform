// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Framework
{
    using DotNetNuke.Instrumentation;
    using DotNetNuke.UI.Utilities;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Resolves the <see cref="IClientApiPage"/> of the current request, which is the ASP.NET Core
    /// seam that replaces <c>System.Web.UI.Page</c> for everything that registers client-side state
    /// (client variables, client script blocks, hidden fields).
    /// </summary>
    /// <remarks>
    /// The host registers a scoped <see cref="IClientApiPage"/> (see
    /// <c>DotNetNuke.UI.Utilities.ServiceCollectionExtensions.UseDnnWebUtility</c>). When it is absent
    /// the client-side state cannot be written anywhere, which is a host-wiring bug, so it is logged
    /// rather than silently ignored.
    /// </remarks>
    internal static class ClientApiPageAccessor
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger(typeof(ClientApiPageAccessor));

        /// <summary>Gets the client API page of the given request.</summary>
        /// <param name="context">The HTTP context of the current request.</param>
        /// <param name="caller">The member asking, used in the diagnostic message.</param>
        /// <returns>The page, or <see langword="null"/> when the host registered none.</returns>
        internal static IClientApiPage GetPage(HttpContext context, string caller)
        {
            var page = context?.RequestServices?.GetService<IClientApiPage>();
            if (page == null)
            {
                Logger.LogWarning(
                    "{Caller} could not register client-side page state: no IClientApiPage is registered for this request. Register one in the host (see UseDnnWebUtility) or the client variables and script blocks DNN's client code reads will be missing.",
                    caller);
            }

            return page;
        }
    }
}
