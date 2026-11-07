// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>Handles an event that carries a grid row.</summary>
    /// <param name="sender">The grid raising the event.</param>
    /// <param name="e">The event arguments.</param>
    [SuppressMessage("Microsoft.Design", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix", Justification = "Breaking change")]
    public delegate void DataGridItemEventHandler(object sender, DataGridItemEventArgs e);

    /// <summary>The arguments of an event that carries a grid row.</summary>
    /// <remarks>Replaces <c>System.Web.UI.WebControls.DataGridItemEventArgs</c>.</remarks>
    public class DataGridItemEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="DataGridItemEventArgs"/> class.</summary>
        /// <param name="item">The row the event is about.</param>
        public DataGridItemEventArgs(DataGridItem item)
        {
            this.Item = item;
        }

        /// <summary>Gets the row the event is about.</summary>
        public DataGridItem Item { get; }
    }
}
