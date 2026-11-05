// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System.Diagnostics.CodeAnalysis;

    /// <summary>Handles a command raised from a grid cell.</summary>
    /// <param name="sender">The grid raising the event.</param>
    /// <param name="e">The event arguments.</param>
    [SuppressMessage("Microsoft.Design", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix", Justification = "Breaking change")]
    public delegate void DataGridCommandEventHandler(object sender, DataGridCommandEventArgs e);

    /// <summary>The arguments of a command raised from a grid cell.</summary>
    /// <remarks>
    /// Replaces <c>System.Web.UI.WebControls.DataGridCommandEventArgs</c>. The WebForms command
    /// bubbled up the control tree from the button inside the cell; the cell render fragments now
    /// raise it on the grid directly through <see cref="DNNDataGrid.RaiseItemCommand"/>.
    /// </remarks>
    public class DataGridCommandEventArgs : DataGridItemEventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="DataGridCommandEventArgs"/> class.</summary>
        /// <param name="item">The row the command was raised from.</param>
        /// <param name="commandSource">The object that raised the command.</param>
        /// <param name="commandName">The name of the command.</param>
        /// <param name="commandArgument">The argument of the command.</param>
        public DataGridCommandEventArgs(DataGridItem item, object commandSource, string commandName, object commandArgument)
            : base(item)
        {
            this.CommandSource = commandSource;
            this.CommandName = commandName;
            this.CommandArgument = commandArgument;
        }

        /// <summary>Gets the object that raised the command.</summary>
        public object CommandSource { get; }

        /// <summary>Gets the name of the command.</summary>
        public string CommandName { get; }

        /// <summary>Gets the argument of the command.</summary>
        public object CommandArgument { get; }
    }
}
