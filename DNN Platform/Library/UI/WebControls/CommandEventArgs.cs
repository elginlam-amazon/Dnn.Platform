// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>Handles a command raised by a DNN web control.</summary>
    /// <param name="sender">The control raising the command.</param>
    /// <param name="e">The event arguments.</param>
    [SuppressMessage("Microsoft.Design", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix", Justification = "Breaking change")]
    public delegate void CommandEventHandler(object sender, CommandEventArgs e);

    /// <summary>The arguments of a command raised by a DNN web control.</summary>
    /// <remarks>Replaces <c>System.Web.UI.WebControls.CommandEventArgs</c>.</remarks>
    public class CommandEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="CommandEventArgs"/> class.</summary>
        /// <param name="commandName">The name of the command.</param>
        /// <param name="commandArgument">The argument of the command.</param>
        public CommandEventArgs(string commandName, object commandArgument)
        {
            this.CommandName = commandName;
            this.CommandArgument = commandArgument;
        }

        /// <summary>Gets the name of the command.</summary>
        public string CommandName { get; }

        /// <summary>Gets the argument of the command.</summary>
        public object CommandArgument { get; }
    }
}
