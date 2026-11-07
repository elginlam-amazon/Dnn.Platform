// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Services.Install
{
    using Microsoft.AspNetCore.Components;

    /// <summary>Holds information to build the Security tab during upgrade.</summary>
    public class SecurityTabResult
    {
        /// <summary>Gets or sets a value indicating whether the Upgrade Now button will be enabled or not by default.</summary>
        public bool CanProceed { get; set; }

        /// <summary>Gets or sets the content to render in the Security tab.</summary>
        /// <remarks>
        /// Was <c>string View</c>: a blob of HTML that <c>RenderControls(...)</c> produced by
        /// rendering a WebForms control tree into an <c>HtmlTextWriter</c>, which the client script
        /// then injected with <c>$('#upgradeSecurityView').html(result.Item3.View)</c>. On ASP.NET
        /// Core the natural - and safe - representation of "a piece of UI built in code" is a
        /// <see cref="RenderFragment"/>: it is composed from real markup, participates in the
        /// render tree and is HTML-encoded by the framework rather than being string-concatenated
        /// and injected.
        /// </remarks>
        public RenderFragment View { get; set; }
    }
}
