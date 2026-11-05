// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>Handles the server side validation of a value.</summary>
    /// <param name="source">The control raising the event.</param>
    /// <param name="args">The event arguments.</param>
    [SuppressMessage("Microsoft.Design", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix", Justification = "Breaking change")]
    public delegate void ServerValidateEventHandler(object source, ServerValidateEventArgs args);

    /// <summary>The arguments of a server side validation.</summary>
    /// <remarks>Replaces <c>System.Web.UI.WebControls.ServerValidateEventArgs</c>.</remarks>
    public class ServerValidateEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="ServerValidateEventArgs"/> class.</summary>
        /// <param name="value">The value that was validated.</param>
        /// <param name="isValid">Whether the value is valid.</param>
        public ServerValidateEventArgs(string value, bool isValid)
        {
            this.Value = value;
            this.IsValid = isValid;
        }

        /// <summary>Gets the value that was validated.</summary>
        public string Value { get; }

        /// <summary>Gets or sets a value indicating whether the value is valid.</summary>
        public bool IsValid { get; set; }
    }
}
