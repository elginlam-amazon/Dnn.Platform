// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System.ComponentModel;

    /// <summary>
    /// The appearance of the label of a <see cref="DNNTab"/>.
    /// </summary>
    /// <remarks>
    /// The original implemented <c>System.Web.UI.IStateManager</c> and backed every property with a
    /// <c>StateBag</c>. View state has no .NET 10 equivalent, so the properties are now ordinary
    /// auto-properties with the same defaults the view state getters returned (an empty string, as
    /// <c>Conversions.ToString(Nothing)</c> produced), and the state manager members are kept (as
    /// no-ops) only to preserve the public surface.
    /// </remarks>
    public class DNNTabLabel : IUrlResolutionService
    {
        /// <summary>Gets or sets the text of the label.</summary>
        [Localizable(true)]
        [Category("Appearance")]
        [NotifyParentProperty(true)]
        [DefaultValue("")]
        public virtual string Text { get; set; } = string.Empty;

        /// <summary>Gets or sets the CSS class applied while the tab is selected.</summary>
        [ClientPropertyName("csssel")]
        [ClientProperty]
        public string CssClassSelected { get; set; } = string.Empty;

        /// <summary>Gets or sets the CSS class applied while the pointer is over the tab.</summary>
        [ClientProperty]
        [ClientPropertyName("csshover")]
        public string CssClassHover { get; set; } = string.Empty;

        /// <summary>Gets or sets the CSS class applied to the tab.</summary>
        [ClientPropertyName("css")]
        [ClientProperty]
        public string CssClass { get; set; } = string.Empty;

        /// <summary>Gets or sets the CSS class applied while the tab is disabled.</summary>
        [ClientProperty]
        [ClientPropertyName("cssdisabled")]
        public string CssClassDisabled { get; set; } = string.Empty;

        /// <summary>Gets or sets the URL of the image shown in the label.</summary>
        public string ImageUrl { get; set; } = string.Empty;

        /// <inheritdoc/>
        public string ResolveClientUrl(string url)
        {
            return url;
        }
    }
}
