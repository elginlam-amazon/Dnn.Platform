// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.ExtensionPoints
{
    using System.Diagnostics.CodeAnalysis;

    using DotNetNuke.UI.WebControls;

    public interface IGridColumnExtensionPoint : IExtensionPoint
    {
        int ColumnAt { get; }

        string UniqueName { get; }

        string DataField { get; }

        string HeaderText { get; }

        /// <summary>Gets the width of the column header.</summary>
        /// <remarks>
        /// The type is now <see cref="DotNetNuke.UI.WebControls.Unit"/>, DNN's own replacement for
        /// <c>System.Web.UI.WebControls.Unit</c>, which does not exist on ASP.NET Core.
        /// </remarks>
        Unit HeaderStyleWidth { get; }

        [SuppressMessage("Microsoft.Naming", "CA1716:IdentifiersShouldNotMatchKeywords", Justification = "Breaking change")]
        bool ReadOnly { get; }

        bool Reorderable { get; }

        string SortExpression { get; }
    }
}
