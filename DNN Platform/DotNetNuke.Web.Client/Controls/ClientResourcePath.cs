// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Client.ClientResourceManagement
{
    /// <summary>Defines the path to a client resource.</summary>
    /// <remarks>
    /// This is a plain POCO. The WebForms data-binding surface (<c>BindingContainer</c>, the
    /// <c>DataBinding</c> event and <c>DataBind()</c>) existed only so the designer could bind the
    /// <c>&lt;Paths&gt;</c> inner property; the Tag Helper binds the collection directly instead
    /// (see <see cref="ClientResourceLoaderTagHelper.Paths"/>).
    /// </remarks>
    public class ClientResourcePath
    {
        /// <summary>Gets or sets the name.</summary>
        public string Name { get; set; }

        /// <summary>Gets or sets the path.</summary>
        public string Path { get; set; }

        /// <summary>Gets or sets the parent.</summary>
        public ClientResourceLoaderTagHelper Parent { get; set; }
    }
}
