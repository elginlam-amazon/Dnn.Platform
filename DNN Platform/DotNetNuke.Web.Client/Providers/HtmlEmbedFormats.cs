// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Client.Providers
{
    using System.Text;

    /// <summary>
    /// Pre-parsed <see cref="CompositeFormat"/> instances for the <see cref="HtmlEmbedConstants"/> format
    /// strings, so the repeated per-resource formatting does not re-parse the format on every call.
    /// </summary>
    internal static class HtmlEmbedFormats
    {
        /// <summary>The parsed <see cref="HtmlEmbedConstants.ScriptEmbedWithSource"/> format.</summary>
        internal static readonly CompositeFormat ScriptEmbedWithSource = CompositeFormat.Parse(HtmlEmbedConstants.ScriptEmbedWithSource);

        /// <summary>The parsed <see cref="HtmlEmbedConstants.CssEmbedWithSource"/> format.</summary>
        internal static readonly CompositeFormat CssEmbedWithSource = CompositeFormat.Parse(HtmlEmbedConstants.CssEmbedWithSource);

        /// <summary>The parsed <see cref="HtmlEmbedConstants.ScriptEmbedWithCode"/> format.</summary>
        internal static readonly CompositeFormat ScriptEmbedWithCode = CompositeFormat.Parse(HtmlEmbedConstants.ScriptEmbedWithCode);
    }
}
