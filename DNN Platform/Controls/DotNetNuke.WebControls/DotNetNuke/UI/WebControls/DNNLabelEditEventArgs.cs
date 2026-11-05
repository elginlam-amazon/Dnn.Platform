// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;

    /// <summary>The <see cref="DNNLabelEditEventArgs"/> class.</summary>
    public class DNNLabelEditEventArgs : EventArgs
    {
        private readonly string textField;
        private string returnedText;

        /// <summary>Initializes a new instance of the <see cref="DNNLabelEditEventArgs"/> class.</summary>
        /// <param name="text">The text.</param>
        public DNNLabelEditEventArgs(string text)
        {
            this.textField = text;
            this.returnedText = text;
        }

        /// <summary>Gets the text.</summary>
        public string Text
        {
            get { return this.textField; }
        }

        /// <summary>Gets or sets returned Text.</summary>
        public string ReturnedText
        {
            get { return this.returnedText; }
            set { this.returnedText = value; }
        }
    }
}
