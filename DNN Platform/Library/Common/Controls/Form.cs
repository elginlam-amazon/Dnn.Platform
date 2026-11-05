// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Common.Controls
{
    using System.Collections.Generic;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>
    /// A form component whose <c>action</c> attribute is the raw (non-rewritten) request URL
    /// instead of the rewritten URL.
    /// </summary>
    /// <remarks>
    /// This is the Blazor replacement for the legacy WebForms <c>HtmlForm</c>-derived control of
    /// the same name. The original overrode <c>RenderAttributes</c> to rewrite the emitted
    /// <c>action</c> attribute to <c>Request.RawUrl</c> and the <c>id</c> attribute to the
    /// control's <c>ClientID</c>; here both attributes are simply rendered with those values.
    /// The raw URL is captured during initialization, because <c>HttpContext</c> is not available
    /// once the component is rendering interactively over SignalR.
    /// </remarks>
    public class Form : ComponentBase
    {
        private string rawUrl;

        /// <summary>Gets or sets the element id. Rendered as the form's <c>id</c> attribute when set.</summary>
        [Parameter]
        public string ID { get; set; }

        /// <summary>Gets or sets the content rendered inside the form.</summary>
        [Parameter]
        public RenderFragment ChildContent { get; set; }

        /// <summary>Gets or sets additional attributes to render on the form element.</summary>
        [Parameter(CaptureUnmatchedValues = true)]
        public IReadOnlyDictionary<string, object> AdditionalAttributes { get; set; }

        /// <summary>Gets the client id of the rendered form element.</summary>
        public string ClientID => this.ID;

        /// <summary>Gets the raw (non-rewritten) URL used for the form's action attribute.</summary>
        protected string RawUrl => this.rawUrl;

        /// <inheritdoc />
        protected override void OnInitialized()
        {
            base.OnInitialized();

            // Capture during the initial (static) render — HttpContext is null afterwards.
            var request = HttpContextSource.Current?.Request;
            if (request != null)
            {
                this.rawUrl = request.PathBase + request.Path + request.QueryString;
            }
        }

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "form");

            if (this.AdditionalAttributes != null)
            {
                builder.AddMultipleAttributes(1, this.AdditionalAttributes);
            }

            builder.AddAttribute(2, "method", "post");

            if (!string.IsNullOrEmpty(this.rawUrl))
            {
                // Blazor HTML-encodes attribute values, matching the original HttpUtility.HtmlEncode call.
                builder.AddAttribute(3, "action", this.rawUrl);
            }

            if (this.ID != null)
            {
                builder.AddAttribute(4, "id", this.ClientID);
            }

            if (this.ChildContent != null)
            {
                builder.AddContent(5, this.ChildContent);
            }

            builder.CloseElement();
        }
    }
}
