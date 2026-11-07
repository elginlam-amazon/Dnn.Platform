// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Modules.Journal.Controls
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Modules.Journal.Components;

    using Microsoft.AspNetCore.Components;

    /// <summary>A component that renders a journal list.</summary>
    /// <remarks>
    /// <para>
    /// Was <c>JournalListControl.cs</c>, a <c>System.Web.UI.WebControls.WebControl</c>. The
    /// <c>Render(HtmlTextWriter)</c> override is now <c>JournalListControl.razor</c>; the public
    /// surface (<see cref="ModuleId"/>, <see cref="PageSize"/>, <see cref="ProfileId"/>,
    /// <see cref="SocialGroupId"/>, <see cref="CurrentIndex"/>, <see cref="Enabled"/>,
    /// <see cref="portalSettings"/>, <see cref="userInfo"/>, <see cref="JournalId"/>) is unchanged.
    /// </para>
    /// <para>
    /// The designer-only <c>[DefaultProperty]</c>/<c>[ToolboxData]</c> attributes and the
    /// <c>[Browsable]</c>/<c>[DesignerSerializationVisibility]</c> metadata that supported the
    /// WebForms markup designer were dropped; they have no Blazor equivalent.
    /// </para>
    /// </remarks>
    public partial class JournalListControl : DotNetNuke.UI.DnnComponentBase
    {
        /// <summary>The portal settings captured when this component initialized.</summary>
        private PortalSettings capturedPortalSettings;

        /// <summary>The viewing user captured when this component initialized.</summary>
        private UserInfo capturedUserInfo;

        /// <summary>Gets the portal settings of the request this control was created for.</summary>
        /// <remarks>
        /// Reads from the request <see cref="DotNetNuke.UI.DnnComponentBase"/> captured when this
        /// component initialized, falling back to <see cref="PortalSettings.Current"/>. The
        /// WebForms control read the ambient <see cref="PortalSettings.Current"/>, which is only
        /// populated while an HTTP request is on the stack; on an interactive re-render there is
        /// no such request, so the captured one is what keeps this non-null.
        /// </remarks>
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Breaking Change")]

        // ReSharper disable once InconsistentNaming
        public PortalSettings portalSettings => this.capturedPortalSettings ?? PortalSettings.Current;

        /// <summary>Gets the user viewing the journal.</summary>
        /// <remarks>
        /// Captured at initialization for the same reason as <see cref="portalSettings"/>:
        /// <c>UserController.Instance.GetCurrentUserInfo()</c> resolves against the ambient
        /// request and would return an anonymous user on an interactive re-render.
        /// </remarks>
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Breaking Change")]

        // ReSharper disable once InconsistentNaming
        public UserInfo userInfo => this.capturedUserInfo ?? UserController.Instance.GetCurrentUserInfo();

        /// <summary>Gets the ID of the single journal item requested through the <c>jid</c> query string parameter.</summary>
        /// <remarks>
        /// Replaces the two <c>HttpContext.Current.Request.QueryString["jid"]</c> reads with the
        /// request <see cref="DotNetNuke.UI.DnnComponentBase"/> captured at initialization (which
        /// falls back to <see cref="HttpContextSource.Current"/>); the
        /// <see cref="Null.NullInteger"/> fallback when there is no request or no <c>jid</c> is
        /// unchanged.
        /// </remarks>
        public int JournalId
        {
            get
            {
                var request = this.Request;
                if (request != null)
                {
                    var jid = request.Query["jid"].ToString();
                    if (!string.IsNullOrEmpty(jid))
                    {
                        return Convert.ToInt32(jid);
                    }
                }

                return Null.NullInteger;
            }
        }

        /// <summary>Gets or sets the ID of the user whose profile journal is rendered.</summary>
        [Parameter]
        public int ProfileId { get; set; }

        /// <summary>Gets or sets the ID of the module the journal belongs to.</summary>
        [Parameter]
        public int ModuleId { get; set; }

        /// <summary>Gets or sets the ID of the social group whose journal is rendered.</summary>
        [Parameter]
        public int SocialGroupId { get; set; }

        /// <summary>Gets or sets the number of journal items rendered per page.</summary>
        [Parameter]
        public int PageSize { get; set; }

        /// <summary>Gets or sets the index of the first journal item rendered.</summary>
        [Parameter]
        public int CurrentIndex { get; set; }

        /// <summary>Gets or sets a value indicating whether this control renders its journal list.</summary>
        /// <remarks>Replaces <c>WebControl.Enabled</c>, which the <c>Render</c> override tested.</remarks>
        [Parameter]
        public bool Enabled { get; set; }

        /// <summary>Gets the journal list HTML produced by <see cref="JournalParser"/>.</summary>
        /// <remarks>
        /// This is the body of the original <c>Render</c>, including the negative
        /// <see cref="CurrentIndex"/> clamp; the HTML is emitted verbatim, as
        /// <c>output.Write(jp.GetList(...))</c> did.
        /// </remarks>
        protected MarkupString ListMarkup
        {
            get
            {
                if (this.CurrentIndex < 0)
                {
                    this.CurrentIndex = 0;
                }

                var jp = new JournalParser(this.portalSettings, this.ModuleId, this.ProfileId, this.SocialGroupId, this.userInfo)
                {
                    JournalId = this.JournalId,
                };

                return (MarkupString)jp.GetList(this.CurrentIndex, this.PageSize);
            }
        }

        /// <summary>Captures the request-scoped context this control renders against.</summary>
        /// <param name="e">The event arguments.</param>
        /// <remarks>
        /// The WebForms control resolved <see cref="PortalSettings.Current"/> and the current user
        /// inside <c>Render</c>, which was always inside the HTTP request. A Razor component can
        /// re-render outside any request, so both are captured once here, while the request this
        /// component was created for is still ambient.
        /// </remarks>
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            this.capturedPortalSettings = PortalSettings.Current;
            this.capturedUserInfo = UserController.Instance.GetCurrentUserInfo();
        }
    }
}
