// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc
{
    using System;

    using Microsoft.AspNetCore.Builder;

    /// <summary>Extension methods to add the DNN MVC module host middleware to the request pipeline.</summary>
    public static class MvcHttpModuleExtensions
    {
        /// <summary>Adds the DNN MVC module host initialization middleware (the former <c>MvcHttpModule</c>).</summary>
        /// <param name="app">The application builder.</param>
        /// <returns>The <paramref name="app"/>, for chaining.</returns>
        public static IApplicationBuilder UseDnnMvcModuleHost(this IApplicationBuilder app)
        {
            ArgumentNullException.ThrowIfNull(app);

            return app.UseMiddleware<MvcHttpModule>();
        }
    }
}
