// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Common
{
    using DotNetNuke.Common;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Provides the ambient <see cref="IAntiForgery"/> instance.</summary>
    public static class AntiForgery
    {
        /// <summary>Gets the <see cref="IAntiForgery"/> implementation registered in the application container.</summary>
        /// <remarks>
        /// The instance is resolved from the host container (the same one the application was built with).
        /// When a request is in flight, resolution goes through the request scope so that scoped
        /// dependencies of the implementation are honored.
        /// </remarks>
        public static IAntiForgery Instance
        {
            get
            {
                var serviceProvider = Globals.GetCurrentServiceProvider();
                var requestServices = serviceProvider.GetService<IHttpContextAccessor>()?.HttpContext?.RequestServices;
                return (requestServices ?? serviceProvider).GetRequiredService<IAntiForgery>();
            }
        }
    }
}
