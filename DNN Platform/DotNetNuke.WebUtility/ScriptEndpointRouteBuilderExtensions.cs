// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.FileProviders;

    /// <summary>
    /// Serves the client side scripts this assembly embeds.
    /// </summary>
    /// <remarks>
    /// The original assembly exposed the scripts through <c>System.Web.UI.WebResource</c> assembly
    /// attributes, which the WebForms <c>WebResource.axd</c> handler served. On ASP.NET Core the
    /// scripts are read out of the embedded files manifest of this assembly and served at
    /// <c>/js/&lt;name&gt;.js</c>, which is the path the <c>ClientAPI.ScriptPath</c> based
    /// registrations point at, so every previously reachable script stays reachable.
    /// </remarks>
    public static class ScriptEndpointRouteBuilderExtensions
    {
        /// <summary>The content type the scripts were served with by the WebResource handler.</summary>
        public const string ScriptContentType = "application/x-javascript";

        private static readonly string[] ScriptNames = new[]
        {
            "dnn.js",
            "dnn.motion.js",
            "dnn.dom.positioning.js",
            "dnn.diagnostics.js",
            "dnn.scripts.js",
            "dnn.util.tablereorder.js",
            "dnn.xml.js",
            "dnn.xml.jsparser.js",
            "dnn.xmlhttp.js",
            "dnn.xmlhttp.jsxmlhttprequest.js",
            "MicrosoftAjax.js",
            "MicrosoftAjaxWebForms.js",
        };

        private static readonly Lazy<IFileProvider> EmbeddedScripts =
            new Lazy<IFileProvider>(() => new ManifestEmbeddedFileProvider(typeof(ClientAPI).Assembly));

        /// <summary>Gets the names of the scripts this assembly embeds and serves.</summary>
        public static IEnumerable<string> EmbeddedScriptNames => ScriptNames;

        /// <summary>Gets the file provider the embedded scripts are read from.</summary>
        public static IFileProvider ScriptFileProvider => EmbeddedScripts.Value;

        /// <summary>
        /// Maps an endpoint for every script this assembly embeds, serving each of them at
        /// <c>/js/&lt;name&gt;.js</c>.
        /// </summary>
        /// <param name="endpoints">The endpoint route builder of the host.</param>
        /// <returns>The <paramref name="endpoints"/>, for chaining.</returns>
        public static IEndpointRouteBuilder MapDnnWebUtilityScripts(this IEndpointRouteBuilder endpoints)
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            foreach (var scriptName in ScriptNames)
            {
                var name = scriptName;
                endpoints.MapGet("/js/" + name, context => WriteScriptAsync(context, name));
            }

            return endpoints;
        }

        /// <summary>Writes the given embedded script to the response.</summary>
        /// <param name="context">The request being answered.</param>
        /// <param name="name">The file name of the script.</param>
        /// <returns>A task that completes once the script has been written.</returns>
        private static async Task WriteScriptAsync(HttpContext context, string name)
        {
            var file = ScriptFileProvider.GetFileInfo("js/" + name);
            if (file.Exists == false)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.ContentType = ScriptContentType;
            context.Response.ContentLength = file.Length;
            using (var stream = file.CreateReadStream())
            {
                await stream.CopyToAsync(context.Response.Body).ConfigureAwait(false);
            }
        }
    }
}
