// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Client.Providers
{
    /// <summary>
    /// The HTML formats used to embed client resources into a page.
    /// </summary>
    /// <remarks>
    /// This replaces <c>ClientDependency.Core.HtmlEmbedContants</c> (the original type name contained a
    /// typo in "Constants"). The format strings are reproduced verbatim so that the emitted markup is
    /// byte-for-byte identical to what the legacy pipeline produced.
    /// </remarks>
    public static class HtmlEmbedConstants
    {
        /// <summary>The format used to render a script tag with a source. <c>{0}</c> is the source, <c>{1}</c> the serialized HTML attributes.</summary>
        public const string ScriptEmbedWithSource = "<script src=\"{0}\" {1}></script>";

        /// <summary>The format used to render a link tag with a source. <c>{0}</c> is the href, <c>{1}</c> the serialized HTML attributes.</summary>
        public const string CssEmbedWithSource = "<link href=\"{0}\" {1}/>";

        /// <summary>The format used to render an inline script block. <c>{0}</c> is the script code.</summary>
        public const string ScriptEmbedWithCode = "<script type=\"text/javascript\">{0}</script>";
    }
}
