// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;

    /// <summary>
    /// Marks a property whose value is a URL, so that <see cref="ScriptGenerator"/> resolves it
    /// before marshalling it to the client.
    /// </summary>
    /// <remarks>
    /// Replaces <c>System.Web.UI.UrlPropertyAttribute</c>, which has no .NET 10 equivalent. The
    /// attribute is purely declarative and <see cref="ScriptGenerator"/> looks it up by type, so
    /// re-declaring it here preserves the original behavior exactly.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class UrlPropertyAttribute : Attribute
    {
    }
}
