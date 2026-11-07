// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.HttpModules.UrlRewrite
{
    using System;

    using DotNetNuke.Entities.Urls;

    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Chooses the URL rewriter implementation for the current installation.
    /// </summary>
    /// <remarks>
    /// This is the factory that used to live on the <c>UrlRewriteModule</c>
    /// <c>IHttpModule</c>. The module itself is gone — <c>DnnUrlRewriterMiddleware</c>
    /// (<c>app.UseDnnUrlRewriter()</c>) drives rewriting on ASP.NET Core — but the choice
    /// between the basic and the advanced provider is still made here, from the
    /// <c>friendlyUrl</c> provider configured for the portal, and is still what the
    /// <see cref="UrlRewriterBase"/> service registration resolves to.
    /// </remarks>
    public static class UrlRewriterFactory
    {
        /// <summary>Gets an instance of the URL rewriter.</summary>
        /// <param name="serviceProvider">The service provider.</param>
        /// <returns>A <see cref="UrlRewriterBase"/> instance.</returns>
        public static UrlRewriterBase GetUrlRewriterInstance(IServiceProvider serviceProvider)
        {
            var providerToUse = Common.Utilities.Config.GetFriendlyUrlProvider();
            var createAdvancedRewriter = providerToUse == "advanced";
            return createAdvancedRewriter
                ? ActivatorUtilities.GetServiceOrCreateInstance<AdvancedUrlRewriter>(serviceProvider)
                : ActivatorUtilities.GetServiceOrCreateInstance<BasicUrlRewriter>(serviceProvider);
        }
    }
}
