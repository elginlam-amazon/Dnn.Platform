// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.Services
{
    /// <summary>Marks a public static method of a page as a page method that can be called from the client (see <see cref="System.Web.UI.PageMethodsHandler"/>).</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class WebMethodAttribute : Attribute
    {
        /// <summary>Gets or sets the description of the method.</summary>
        public string Description { get; set; }

        /// <summary>Gets or sets a value indicating whether the session state is enabled for the method.</summary>
        public bool EnableSession { get; set; }

        /// <summary>Gets or sets the name of the method used in the client proxy.</summary>
        public string MessageName { get; set; }
    }
}
