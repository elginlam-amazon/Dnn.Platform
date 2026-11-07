// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    /// <summary>Exposes DNN's navigation nodes as a hierarchical data source.</summary>
    /// <remarks>
    /// Was a <c>System.Web.UI.HierarchicalDataSourceControl</c> carrying an
    /// <c>[AspNetHostingPermission]</c> demand. Code access security is gone on modern .NET, so the
    /// attribute is gone with it; the control is now a <see cref="DnnComponentBase"/> that
    /// implements <see cref="IHierarchicalDataSource"/>, which is the part of the base class DNN
    /// actually used.
    /// </remarks>
    public class NavDataSource : DnnComponentBase, IHierarchicalDataSource
    {
        // Return a strongly typed view for the current data source control.
        private NavDataSourceView view;

        /// <inheritdoc />
        public virtual HierarchicalDataSourceView GetHierarchicalView(string viewPath)
        {
            this.view = new NavDataSourceView(viewPath);
            return this.view;
        }
    }
}
