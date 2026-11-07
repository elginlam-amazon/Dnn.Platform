// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System.Collections;

    /// <summary>A node of a hierarchy, which may have children.</summary>
    /// <remarks>Replaces <c>System.Web.UI.IHierarchyData</c>, which lives in the WebForms assembly.</remarks>
    public interface IHierarchyData
    {
        /// <summary>Gets a value indicating whether this node has any child nodes.</summary>
        bool HasChildren { get; }

        /// <summary>Gets the hierarchical path of this node.</summary>
        string Path { get; }

        /// <summary>Gets the object this node represents.</summary>
        object Item { get; }

        /// <summary>Gets the name of the type of object <see cref="Item"/> holds.</summary>
        string Type { get; }

        /// <summary>Gets the children of this node.</summary>
        /// <returns>The children, which may be empty.</returns>
        IHierarchicalEnumerable GetChildren();

        /// <summary>Gets the parent of this node.</summary>
        /// <returns>The parent, or <see langword="null"/> when this node is a root.</returns>
        IHierarchyData GetParent();
    }

    /// <summary>An enumerable of objects that each map onto an <see cref="IHierarchyData"/>.</summary>
    /// <remarks>Replaces <c>System.Web.UI.IHierarchicalEnumerable</c>.</remarks>
    public interface IHierarchicalEnumerable : IEnumerable
    {
        /// <summary>Gets the hierarchy node of an enumerated item.</summary>
        /// <param name="enumeratedItem">The item, as yielded by the enumerator.</param>
        /// <returns>The hierarchy node of the item.</returns>
        IHierarchyData GetHierarchyData(object enumeratedItem);
    }

    /// <summary>A hierarchy node that carries the data a navigation control renders.</summary>
    /// <remarks>Replaces <c>System.Web.UI.INavigateUIData</c>.</remarks>
    public interface INavigateUIData
    {
        /// <summary>Gets the display name of the node.</summary>
        string Name { get; }

        /// <summary>Gets the value of the node, which uniquely identifies it.</summary>
        string Value { get; }

        /// <summary>Gets the URL the node navigates to.</summary>
        string NavigateUrl { get; }

        /// <summary>Gets the description (tool tip) of the node.</summary>
        string Description { get; }
    }

    /// <summary>A source of hierarchical data.</summary>
    /// <remarks>Replaces <c>System.Web.UI.IHierarchicalDataSource</c>.</remarks>
    public interface IHierarchicalDataSource
    {
        /// <summary>Gets the view of this source rooted at the given path.</summary>
        /// <param name="viewPath">The path of the node the view is rooted at, which may be empty.</param>
        /// <returns>The view.</returns>
        HierarchicalDataSourceView GetHierarchicalView(string viewPath);
    }
}
