// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Client.ClientResourceManagement
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using System.Text.Encodings.Web;
    using System.Text.Json;

    using DotNetNuke.Web.Client.Providers;

    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// Emits the client resource management async-postback loader script and its <c>$crm_</c> payload.
    /// </summary>
    /// <remarks>
    /// The legacy <see cref="ClientResourceLoaderTagHelper"/> and <see cref="DnnResourcesTagHelper"/>
    /// controls each emitted an identical ~70 line inline script through
    /// <c>Page.ClientScript.RegisterStartupScript</c>. The script now lives in a single static asset
    /// (<c>wwwroot/js/dnn.clientresourceloader.js</c>, staged to the site's <c>/js</c> folder) and both
    /// Tag Helpers emit a reference to that one asset instead of duplicating the script.
    /// </remarks>
    public static class ClientResourceLoaderScript
    {
        /// <summary>The <see cref="HttpContext.Items"/> key used to make sure the loader is emitted only once per request.</summary>
        public const string EmittedItemsKey = "$crm_LoaderScriptEmitted";

        /// <summary>The default URL of the loader script asset.</summary>
        public const string DefaultScriptPath = "~/js/dnn.clientresourceloader.js";

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        /// <summary>Gets a value indicating whether the WebForms-era async post-back handler is enabled for this request.</summary>
        /// <param name="context">The HTTP context, may be <see langword="null"/>.</param>
        /// <returns><see langword="true"/> when <c>ClientResourceManager.EnableAsyncPostBackHandler</c> was called.</returns>
        public static bool IsAsyncPostBackHandlerEnabled(HttpContext context)
        {
            return context is not null && context.Items.ContainsKey("AsyncPostBackHandlerEnabled");
        }

        /// <summary>Renders the loader script reference along with the <c>$crm_</c> data items of this request.</summary>
        /// <param name="context">The HTTP context, may be <see langword="null"/>.</param>
        /// <param name="scriptPath">The URL of the loader asset.</param>
        /// <param name="applicationPath">The application path used to resolve a <c>~/</c> rooted <paramref name="scriptPath"/>.</param>
        /// <returns>The markup to emit, or <see cref="string.Empty"/> when it has already been emitted for this request.</returns>
        public static string Render(HttpContext context, string scriptPath, string applicationPath)
        {
            if (context is not null)
            {
                if (context.Items.ContainsKey(EmittedItemsKey))
                {
                    return string.Empty;
                }

                context.Items[EmittedItemsKey] = true;
            }

            var markup = new StringBuilder();

            var dataItems = AsyncPostBackDataItems.GetDataItems(context);
            if (dataItems.Count > 0)
            {
                // The loader script scrapes these for <script src> / <link href> tags, exactly as it
                // scraped args.get_dataItems() in the WebForms PageRequestManager pageLoading event.
                var payload = JsonSerializer.Serialize(new Dictionary<string, string>(dataItems), JsonOptions);
                markup.AppendFormat(
                    CultureInfo.InvariantCulture,
                    HtmlEmbedFormats.ScriptEmbedWithCode,
                    $"window.dnnCrmDataItems = Object.assign(window.dnnCrmDataItems || {{}}, {payload});");
            }

            markup.AppendFormat(
                CultureInfo.InvariantCulture,
                HtmlEmbedFormats.ScriptEmbedWithSource,
                ResolveUrl(scriptPath, applicationPath),
                string.Empty);

            return markup.ToString();
        }

        private static string ResolveUrl(string scriptPath, string applicationPath)
        {
            if (string.IsNullOrEmpty(scriptPath))
            {
                scriptPath = DefaultScriptPath;
            }

            if (!scriptPath.StartsWith("~/", System.StringComparison.Ordinal))
            {
                return scriptPath;
            }

            var root = string.IsNullOrEmpty(applicationPath) ? string.Empty : applicationPath.TrimEnd('/');
            return string.Concat(root, scriptPath.AsSpan(1));
        }
    }
}
