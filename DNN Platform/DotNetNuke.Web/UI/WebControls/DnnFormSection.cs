// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using System.Collections.Generic;

    using DotNetNuke.UI;

    using Microsoft.AspNetCore.Components;

    /// <summary>A form section control.</summary>
    /// <remarks>
    /// This is a declarative configuration container — <see cref="DnnFormEditor"/> reads its
    /// <see cref="Items"/> and builds the markup. It was a <c>WebControl, INamingContainer</c>.
    /// </remarks>
    public class DnnFormSection : DnnComponentBase
    {
        /// <summary>Initializes a new instance of the <see cref="DnnFormSection"/> class.</summary>
        public DnnFormSection()
        {
            this.Items = new List<DnnFormItemBase>();
        }

        /// <summary>Gets or sets a value indicating whether the section is expanded.</summary>
        [Parameter]
        public bool Expanded { get; set; }

        /// <summary>Gets the items.</summary>
        public List<DnnFormItemBase> Items { get; private set; }

        /// <summary>Gets or sets the resource key.</summary>
        [Parameter]
        public string ResourceKey { get; set; }
    }
}
