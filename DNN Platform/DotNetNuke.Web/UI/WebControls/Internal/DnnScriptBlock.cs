// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.UI.WebControls.Internal
{
    using System;
    using System.Threading.Tasks;

    using DotNetNuke.UI;

    using Microsoft.AspNetCore.Components;
    using Microsoft.JSInterop;

    /// <summary>This control is only for internal use, please don't reference it in any other place as it may be removed in the future.</summary>
    /// <remarks>
    /// This was a <c>Control</c> that rendered its children — an inline <c>script</c> block — either
    /// straight into the page or, during an MS AJAX partial postback, through
    /// <c>ScriptManager.RegisterClientScriptBlock</c> so that the script still ran after the update
    /// panel refreshed. Neither the script manager nor an inline <c>script</c> element is allowed on
    /// a Blazor page (the browser never executes markup a component adds to the DOM), so the script
    /// text is registered and evaluated once per key through JS interop — which covers both of the
    /// original cases with the same single code path.
    /// </remarks>
    public class DnnScriptBlock : DnnComponentBase
    {
        private bool registered;

        /// <summary>Gets or sets the script to run.</summary>
        /// <remarks>
        /// Replaces the inline content of the control; it was declared as the control's children in
        /// markup and is assigned as text here.
        /// </remarks>
        [Parameter]
        public string Script { get; set; }

        /// <summary>Gets or sets the JavaScript runtime the script is registered with.</summary>
        [Inject]
        protected IJSRuntime JsRuntime { get; set; }

        /// <summary>Gets the key the script is registered under.</summary>
        /// <remarks>Replaces the <c>UniqueID</c> the script manager registration used as its key.</remarks>
        protected string RegistrationKey =>
            string.IsNullOrEmpty(this.UniqueId) ? this.GetType().FullName : this.UniqueId;

        /// <inheritdoc />
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            if (this.registered || this.JsRuntime == null || string.IsNullOrWhiteSpace(this.Script))
            {
                return;
            }

            this.registered = true;
            await this.JsRuntime.InvokeVoidAsync("dnn.registerScriptBlock", this.RegistrationKey, this.Script);
        }

        /// <inheritdoc />
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            // the script is handed to the browser through JS interop, so nothing is rendered; any
            // declared child content is still rendered so that the control can wrap markup.
            ArgumentNullException.ThrowIfNull(builder);

            if (!this.Visible || this.ChildContent == null)
            {
                return;
            }

            builder.AddContent(0, this.ChildContent);
        }
    }
}
