// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Modules.Journal
{
    using Microsoft.AspNetCore.Components;

    /// <summary>A component that displays a journal list.</summary>
    /// <remarks>
    /// <para>
    /// Was <c>JournalList.cs</c>, a <c>System.Web.UI.WebControls.WebControl</c> whose
    /// <c>Render(HtmlTextWriter)</c> override wrote <c>"Hello World"</c>. The markup now lives in
    /// <c>JournalList.razor</c> and produces the same output.
    /// </para>
    /// <para>
    /// The <c>[DefaultProperty("Text")]</c> and <c>[ToolboxData(...)]</c> attributes were designer
    /// metadata for the WebForms markup designer and have no Blazor counterpart, so they are gone.
    /// The four public properties are unchanged apart from becoming component parameters, which is
    /// how markup assigns them now.
    /// </para>
    /// </remarks>
    public partial class JournalList : DotNetNuke.UI.DnnComponentBase
    {
        /// <summary>Gets or sets the portal ID.</summary>
        [Parameter]
        public int PortalId { get; set; }

        /// <summary>Gets or sets the ID of the user whose profile journal is displayed.</summary>
        [Parameter]
        public int ProfileId { get; set; }

        /// <summary>Gets or sets the ID of the social group whose journal is displayed.</summary>
        [Parameter]
        public int SocialGroupId { get; set; }

        /// <summary>Gets or sets the display mode.</summary>
        [Parameter]
        public int DisplayMode { get; set; }
    }
}
