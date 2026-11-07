// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.Skins.Controls
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.UI.Skins;

    using Microsoft.AspNetCore.Components;

    /// <summary>A skin object which requests the inclusion of jQuery on the page.</summary>
    /// <remarks>
    /// <para>
    /// Was <c>jQuery.ascx</c> + <c>jQuery.ascx.cs</c>. This is a client-resource REGISTRATION, not
    /// visible markup: the control never rendered anything, it only recorded requests for the
    /// jQuery libraries, which the page emits in one <c>RenderDependencies</c> pass with the
    /// <c>FileOrder</c> priority each library definition declares. No inline <c>&lt;script&gt;</c>
    /// is written (a Blazor component cannot emit one).
    /// </para>
    /// <para>
    /// It is a component class rather than a <c>.razor</c> file because a Razor component file name
    /// may not start with a lowercase character (RZ10011), and the type name <c>jQuery</c> is
    /// public API that the skin-object registry resolves by name.
    /// </para>
    /// </remarks>
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Breaking Change")]

    // ReSharper disable once InconsistentNaming
    public class jQuery : SkinObjectBase
    {
        /// <summary>Gets or sets a value indicating whether the DNN jQuery plugins are requested.</summary>
        [Parameter]
        public bool DnnjQueryPlugins { get; set; }

        /// <summary>Gets or sets a value indicating whether the hoverIntent plugin is requested.</summary>
        [Parameter]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Breaking Change")]

        // ReSharper disable once InconsistentNaming
        public bool jQueryHoverIntent { get; set; }

        /// <summary>Gets or sets a value indicating whether jQuery UI is requested.</summary>
        [Parameter]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Breaking Change")]

        // ReSharper disable once InconsistentNaming
        public bool jQueryUI { get; set; }

        /// <summary>Gets or sets the JavaScript library helper.</summary>
        [Inject]
        public IJavaScriptLibraryHelper JsLibraries { get; set; }

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            // Registration order is behaviour: jQuery, then migrate, then the optional add-ons.
            this.JsLibraries.RequestRegistration(CommonJs.jQuery);
            this.JsLibraries.RequestRegistration(CommonJs.jQueryMigrate);

            if (this.jQueryUI)
            {
                this.JsLibraries.RequestRegistration(CommonJs.jQueryUI);
            }

            if (this.DnnjQueryPlugins)
            {
                this.JsLibraries.RequestRegistration(CommonJs.DnnPlugins);
            }

            if (this.jQueryHoverIntent)
            {
                this.JsLibraries.RequestRegistration(CommonJs.HoverIntent);
            }
        }
    }
}
