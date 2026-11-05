// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    /// <summary>
    /// Resolves client side URLs on behalf of an object that is not part of a control tree.
    /// </summary>
    /// <remarks>Replaces <c>System.Web.UI.IUrlResolutionService</c>.</remarks>
    public interface IUrlResolutionService
    {
        /// <summary>Resolves the given URL for client side use.</summary>
        /// <param name="url">The URL to resolve.</param>
        /// <returns>The resolved URL.</returns>
        string ResolveClientUrl(string url);
    }
}
