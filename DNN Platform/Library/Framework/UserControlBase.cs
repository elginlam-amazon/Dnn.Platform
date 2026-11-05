// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Framework
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Portals;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Html;
    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// The UserControlBase class defines a custom base class inherited by all
    /// user controls within the Portal.
    /// </summary>
    /// <remarks>
    /// This was a <c>System.Web.UI.UserControl</c>. On ASP.NET Core the platform renders with
    /// Blazor, so the base type is <see cref="ComponentBase"/>; the public surface
    /// (<see cref="IsHostMenu"/>, <see cref="PortalSettings"/>,
    /// <see cref="JavaScriptStringEncode(string)"/>) is unchanged apart from
    /// <c>IHtmlString</c> becoming <see cref="IHtmlContent"/>.
    /// </remarks>
    public class UserControlBase : ComponentBase
    {
        private HttpRequest capturedRequest;

        public bool IsHostMenu
            => Globals.IsHostTab(this.PortalSettings.ActiveTab.TabID);

        /// <summary>
        /// Gets the request that produced the page this control is rendered on, or
        /// <see langword="null"/> when there is no request in flight.
        /// </summary>
        /// <remarks>
        /// Replaces <c>UserControl.Request</c> (which was <c>Page.Request</c>). The context is
        /// captured during <see cref="OnInitialized"/>, i.e. during the initial (static) render,
        /// because <c>HttpContext</c> is <see langword="null"/> for the rest of a Blazor Server
        /// circuit. Read the values you need from it in <c>OnInitialized</c> and keep them in
        /// fields; do not read it from an <c>@onclick</c> handler.
        /// </remarks>
        public HttpRequest Request => this.capturedRequest ?? HttpContextSource.Current?.Request;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public PortalSettings PortalSettings
            => PortalSettings.Current;

        /// <inheritdoc cref="HtmlUtils.JavaScriptStringEncode(string)"/>
        public static IHtmlContent JavaScriptStringEncode(string value)
            => HtmlUtils.JavaScriptStringEncode(value);

        /// <inheritdoc cref="HtmlUtils.JavaScriptStringEncode(string,bool)"/>
        public static IHtmlContent JavaScriptStringEncode(string value, bool addDoubleQuotes)
            => HtmlUtils.JavaScriptStringEncode(value, addDoubleQuotes);

        /// <inheritdoc />
        /// <remarks>
        /// Captures the current request while one is still in flight (see <see cref="Request"/>) and
        /// then raises <see cref="OnInit(EventArgs)"/>, which is the hook derived controls override.
        /// </remarks>
        protected override void OnInitialized()
        {
            base.OnInitialized();
            this.capturedRequest ??= HttpContextSource.Current?.Request;
            this.OnInit(EventArgs.Empty);
        }

        /// <inheritdoc />
        /// <remarks>Raises <see cref="OnLoad(EventArgs)"/>, which is the hook derived controls override.</remarks>
        protected override void OnParametersSet()
        {
            base.OnParametersSet();
            this.OnLoad(EventArgs.Empty);
        }

        /// <inheritdoc />
        /// <remarks>Raises <see cref="OnPreRender(EventArgs)"/> after the first render completes.</remarks>
        protected override void OnAfterRender(bool firstRender)
        {
            base.OnAfterRender(firstRender);
            if (firstRender)
            {
                this.OnPreRender(EventArgs.Empty);
            }
        }

        /// <summary>Called during the component's initialization phase.</summary>
        /// <param name="e">The event arguments (always <see cref="EventArgs.Empty"/>).</param>
        /// <remarks>
        /// This is the same overridable hook controls used when they derived from
        /// <c>System.Web.UI.UserControl</c> (the WebForms <c>Init</c> phase); it is now invoked from
        /// <see cref="OnInitialized"/>. Request-derived values must be read here (or in
        /// <see cref="OnLoad(EventArgs)"/>) and captured in fields, because <c>HttpContext</c> is
        /// <see langword="null"/> for the rest of a Blazor Server circuit.
        /// </remarks>
        protected virtual void OnInit(EventArgs e)
        {
        }

        /// <summary>Called when the component's parameters have been set.</summary>
        /// <param name="e">The event arguments (always <see cref="EventArgs.Empty"/>).</param>
        /// <remarks>
        /// Replaces the WebForms <c>Load</c> phase (<c>Page_Load</c> / <c>OnLoad</c>); it is now
        /// invoked from <see cref="OnParametersSet"/>.
        /// </remarks>
        protected virtual void OnLoad(EventArgs e)
        {
        }

        /// <summary>Called once the component has rendered for the first time.</summary>
        /// <param name="e">The event arguments (always <see cref="EventArgs.Empty"/>).</param>
        /// <remarks>
        /// Replaces the WebForms <c>PreRender</c> phase. In Blazor the markup is produced by the
        /// renderer rather than written by the control, so the closest honest equivalent is
        /// "after the first render", which is also the only point at which JS interop is legal.
        /// </remarks>
        protected virtual void OnPreRender(EventArgs e)
        {
        }
    }
}
