// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Website.Components
{
    /// <summary>
    /// One keyed client script registration of a page — what the WebForms
    /// <c>Page.ClientScript.RegisterClientScriptBlock</c> / <c>RegisterStartupScript</c> pair kept
    /// on the page until it rendered.
    /// </summary>
    /// <param name="Key">The registration key the script was registered under.</param>
    /// <param name="Script">The script (or complete markup) to emit.</param>
    /// <param name="AddScriptTags">Whether the script still has to be wrapped in a <c>script</c> element.</param>
    public sealed record ClientScriptRegistration(string Key, string Script, bool AddScriptTags = false);
}
