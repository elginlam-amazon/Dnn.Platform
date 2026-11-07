// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Client.Providers
{
    using System;
    using System.Collections.Generic;

    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// Implements the <c>$crm_</c> data-item protocol that the client resource loader script uses to
    /// apply newly registered client resources during an asynchronous (partial) post-back.
    /// </summary>
    /// <remarks>
    /// On WebForms this was <c>ScriptManager.RegisterDataItem(control, value)</c> combined with renaming the
    /// placeholder control to <c>$crm_&lt;id&gt;</c>. In ASP.NET Core the data items are collected on the
    /// request and serialized by the rendering path, keeping the exact same <c>$crm_</c> key protocol.
    /// </remarks>
    public static class AsyncPostBackDataItems
    {
        /// <summary>The prefix applied to the placeholder name to build the data item key.</summary>
        public const string DataItemPrefix = "$crm_";

        /// <summary>The <see cref="HttpContext.Items"/> key under which the data items are collected.</summary>
        public const string ItemsKey = "$crm_DataItems";

        /// <summary>Determines whether the current request is an asynchronous (partial) post-back.</summary>
        /// <param name="context">The HTTP context, may be <see langword="null"/>.</param>
        /// <returns><see langword="true"/> when the request is an async post-back.</returns>
        public static bool IsInAsyncPostBack(HttpContext context)
        {
            if (context is null)
            {
                return false;
            }

            // REVIEW: behavior inferred from the WebForms ScriptManager protocol — the MicrosoftAjax
            // client library sends this header (and the ASP.NET Core equivalents send X-Requested-With)
            // for every partial post-back.
            var microsoftAjax = context.Request.Headers["X-MicrosoftAjax"].ToString();
            return !string.IsNullOrEmpty(microsoftAjax)
                && microsoftAjax.Contains("Delta=true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Registers a data item for the given placeholder, mirroring <c>ScriptManager.RegisterDataItem</c>.</summary>
        /// <param name="context">The HTTP context.</param>
        /// <param name="placeHolderName">The name of the placeholder the markup belongs to (without the <see cref="DataItemPrefix"/>).</param>
        /// <param name="markup">The markup to apply on the client.</param>
        public static void RegisterDataItem(HttpContext context, string placeHolderName, string markup)
        {
            if (context is null || string.IsNullOrEmpty(placeHolderName))
            {
                return;
            }

            var items = GetMutableDataItems(context);
            items[DataItemPrefix + placeHolderName] = markup;
        }

        /// <summary>Gets the data items registered during the current request.</summary>
        /// <param name="context">The HTTP context, may be <see langword="null"/>.</param>
        /// <returns>A dictionary of data items keyed by <c>$crm_&lt;placeholder&gt;</c>.</returns>
        public static IReadOnlyDictionary<string, string> GetDataItems(HttpContext context)
        {
            if (context is null)
            {
                return new Dictionary<string, string>(0);
            }

            return GetMutableDataItems(context);
        }

        private static Dictionary<string, string> GetMutableDataItems(HttpContext context)
        {
            if (context.Items.TryGetValue(ItemsKey, out var existing) && existing is Dictionary<string, string> dictionary)
            {
                return dictionary;
            }

            var items = new Dictionary<string, string>(StringComparer.Ordinal);
            context.Items[ItemsKey] = items;
            return items;
        }
    }
}
