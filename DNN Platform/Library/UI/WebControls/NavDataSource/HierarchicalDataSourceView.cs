// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    /// <summary>A view over an <see cref="IHierarchicalDataSource"/>, rooted at one node.</summary>
    /// <remarks>Replaces <c>System.Web.UI.HierarchicalDataSourceView</c>.</remarks>
    public abstract class HierarchicalDataSourceView
    {
        /// <summary>Gets the nodes of this view.</summary>
        /// <returns>The nodes.</returns>
        public abstract IHierarchicalEnumerable Select();
    }
}
