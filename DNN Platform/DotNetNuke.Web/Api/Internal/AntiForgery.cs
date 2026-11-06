// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

#nullable enable
namespace DotNetNuke.Web.Api.Internal
{
    using System;

    using DotNetNuke.Framework;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>The <see cref="IAntiForgery"/> service locator.</summary>
    /// <remarks>
    /// <c>AntiForgery.Instance</c> stays the public accessor, but the implementation is no longer
    /// <c>new</c>-ed up: <see cref="AntiForgeryImpl"/> is backed by the ASP.NET Core
    /// <c>Microsoft.AspNetCore.Antiforgery.IAntiforgery</c> service and is therefore resolved from the
    /// container — through the request scope when a request is in flight, so that scoped dependencies are
    /// honored. This matches the <c>DotNetNuke.Web.Mvc</c> carry-forward.
    /// </remarks>
    public class AntiForgery : ServiceLocator<IAntiForgery, AntiForgery>
    {
        /// <inheritdoc />
        protected override Func<IAntiForgery> GetFactory()
        {
            return () =>
            {
                var serviceProvider = DotNetNuke.Common.Globals.GetCurrentServiceProvider();
                var requestServices = serviceProvider.GetService<IHttpContextAccessor>()?.HttpContext?.RequestServices;
                return (requestServices ?? serviceProvider).GetRequiredService<IAntiForgery>();
            };
        }
    }
}
