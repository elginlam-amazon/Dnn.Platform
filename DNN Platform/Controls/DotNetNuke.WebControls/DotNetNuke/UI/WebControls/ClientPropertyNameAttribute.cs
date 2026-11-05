// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;

    /// <summary>The <see cref="ClientPropertyNameAttribute"/> class.</summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ClientPropertyNameAttribute : Attribute
    {
        private readonly string nameValue = string.Empty;

        /// <summary>Initializes a new instance of the <see cref="ClientPropertyNameAttribute"/> class.</summary>
        /// <param name="name">The name.</param>
        public ClientPropertyNameAttribute(string name)
        {
            this.nameValue = name;
        }

        /// <summary>Gets the name.</summary>
        public string Name
        {
            get { return this.nameValue; }
        }
    }
}
