// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.UI.WebControls
{
    /// <summary>Provides data for the <see cref="CustomValidator.ServerValidate"/> event.</summary>
    public class ServerValidateEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="ServerValidateEventArgs"/> class.</summary>
        /// <param name="value">The value to validate.</param>
        /// <param name="isValid">The initial validation result.</param>
        public ServerValidateEventArgs(string value, bool isValid)
        {
            this.Value = value;
            this.IsValid = isValid;
        }

        /// <summary>Gets the value to validate.</summary>
        public string Value { get; }

        /// <summary>Gets or sets a value indicating whether the value is valid.</summary>
        public bool IsValid { get; set; }
    }
}
