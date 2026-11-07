// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using System.Collections.Generic;

    using DotNetNuke.UI;

    using Microsoft.AspNetCore.Components;

    /// <summary>A form tab control.</summary>
    /// <remarks>
    /// This is a declarative configuration container — <see cref="DnnFormEditor"/> reads its
    /// <see cref="Items"/>/<see cref="Sections"/> and builds the markup. It was a
    /// <c>WebControl, INamingContainer</c>; the collections are unchanged.
    /// </remarks>
    public class DnnFormTab : DnnComponentBase
    {
        /// <summary>Initializes a new instance of the <see cref="DnnFormTab"/> class.</summary>
        public DnnFormTab()
        {
            this.Sections = new List<DnnFormSection>();
            this.Items = new List<DnnFormItemBase>();
        }

        /// <summary>Gets or sets a value indicating whether to include expand all.</summary>
        [Parameter]
        public bool IncludeExpandAll { get; set; }

        /// <summary>Gets the list of items.</summary>
        public List<DnnFormItemBase> Items { get; private set; }

        /// <summary>Gets the sections.</summary>
        public List<DnnFormSection> Sections { get; private set; }

        /// <summary>Gets or sets the resource key.</summary>
        [Parameter]
        public string ResourceKey { get; set; }

        /// <summary>Gets or sets the options of the "expand all" link of this tab.</summary>
        internal DnnFormExpandAllOptions ExpandAll { get; set; }
    }
}
