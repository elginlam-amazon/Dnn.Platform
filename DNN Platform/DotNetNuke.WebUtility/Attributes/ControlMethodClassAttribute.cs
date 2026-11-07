// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Utilities
{
    using System;

    /// <summary>
    /// Marks a control as exposing client callable methods and optionally gives the client side
    /// namespace the methods are published under.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class ControlMethodClassAttribute : Attribute
    {
        private string friendlyNamespace = string.Empty;

        /// <summary>Initializes a new instance of the <see cref="ControlMethodClassAttribute"/> class.</summary>
        public ControlMethodClassAttribute()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ControlMethodClassAttribute"/> class.</summary>
        /// <param name="friendlyNamespace">The client side namespace the control's methods are published under.</param>
        public ControlMethodClassAttribute(string friendlyNamespace)
        {
            this.friendlyNamespace = friendlyNamespace;
        }

        /// <summary>
        /// Gets or sets the client side namespace the control's methods are published under.
        /// </summary>
        /// <remarks>When empty the full name of the control's type is used.</remarks>
        public string FriendlyNamespace
        {
            get => this.friendlyNamespace;
            set => this.friendlyNamespace = value;
        }
    }
}
