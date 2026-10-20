// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.UI
{
    using System.ComponentModel;

    /// <summary>
    /// Replacement for the System.Web.Extensions <c>UpdateProgress</c> on .NET 10.
    /// </summary>
    /// <remarks>Asynchronous postbacks are not available on ASP.NET Core, so the progress template is never displayed.</remarks>
    [ParseChildren(true)]
    [PersistChildren(false)]
    public class UpdateProgress : Control
    {
        /// <summary>Gets or sets the ID of the associated <see cref="UpdatePanel"/>.</summary>
        public string AssociatedUpdatePanelID { get; set; }

        /// <summary>Gets or sets the display delay in milliseconds.</summary>
        [DefaultValue(500)]
        public int DisplayAfter { get; set; } = 500;

        /// <summary>Gets or sets a value indicating whether the progress is rendered dynamically.</summary>
        [DefaultValue(true)]
        public bool DynamicLayout { get; set; } = true;

        /// <summary>Gets or sets the progress template.</summary>
        [Browsable(false)]
        [PersistenceMode(PersistenceMode.InnerProperty)]
        [TemplateInstance(TemplateInstance.Single)]
        public ITemplate ProgressTemplate { get; set; }

        /// <inheritdoc />
        protected override void Render(HtmlTextWriter writer)
        {
            // The progress template is only shown during asynchronous postbacks, which do not exist on .NET 10.
        }
    }
}
