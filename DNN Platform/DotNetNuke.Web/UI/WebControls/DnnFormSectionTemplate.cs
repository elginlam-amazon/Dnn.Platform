// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using System;
    using System.Collections.Generic;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Common;
    using DotNetNuke.UI;

    using Microsoft.Extensions.DependencyInjection;

    /// <summary>A form section template.</summary>
    /// <remarks>
    /// This implemented <c>System.Web.UI.ITemplate</c>, whose single member instantiated the
    /// section's items into a container control. There is no <c>ITemplate</c> on ASP.NET Core, so
    /// <see cref="InstantiateIn(DnnComponentBase)"/> keeps the same contract against the Blazor
    /// control tree, and the work it does — handing the items to
    /// <see cref="DnnFormEditor.SetUpItems"/> — is unchanged.
    /// </remarks>
    internal class DnnFormSectionTemplate
    {
        private readonly IHostSettings hostSettings;

        /// <summary>Initializes a new instance of the <see cref="DnnFormSectionTemplate"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IHostSettings. Scheduled removal in v12.0.0.")]
        public DnnFormSectionTemplate()
            : this(null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnFormSectionTemplate"/> class.</summary>
        /// <param name="hostSettings">The host settings.</param>
        public DnnFormSectionTemplate(IHostSettings hostSettings)
        {
            this.hostSettings = hostSettings ?? Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettings>();
            this.Items = [];
        }

        /// <summary>Gets the items.</summary>
        public List<DnnFormItemBase> Items { get; private set; }

        /// <summary>Gets or sets the path to the local resource file.</summary>
        public string LocalResourceFile { get; set; }

        /// <summary>Builds this section's items into the given container.</summary>
        /// <param name="container">The container to add the items to.</param>
        public void InstantiateIn(DnnComponentBase container)
        {
            if (container != null)
            {
                DnnFormEditor.SetUpItems(this.hostSettings, this.Items, container, false);
            }
        }
    }
}
