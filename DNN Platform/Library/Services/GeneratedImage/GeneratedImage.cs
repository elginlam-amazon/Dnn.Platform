// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Services.GeneratedImage
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>
    /// Renders an <c>&lt;img&gt;</c> whose <c>src</c> points at the generated-image endpoint, with
    /// the supplied <see cref="Parameters"/> appended as query-string values.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Was a <c>System.Web.UI.WebControls.Image</c> subclass. It is now a Blazor component:
    /// <c>OnPreRender</c> (which assigned <c>ImageUrl</c>) became
    /// <see cref="OnParametersSet"/>, and <c>OnDataBinding</c> — which pushed the binding container
    /// into each <see cref="ImageParameter"/> and called <c>DataBind()</c> — became a pass over
    /// <see cref="Parameters"/> raising <see cref="ImageParameter.DataBind"/> so that a parameter
    /// which computes its value still gets the chance to do so before the URL is built.
    /// </para>
    /// <para>
    /// The URL-building logic, including the <c>__timestamp</c> cache buster, is unchanged.
    /// </para>
    /// </remarks>
    public class GeneratedImage : ComponentBase
    {
        private const string TimestampField = "__timestamp";

        private string timestamp;
        private string imageHandlerUrl;

        /// <summary>Initializes a new instance of the <see cref="GeneratedImage"/> class.</summary>
        public GeneratedImage()
        {
            this.Parameters = new List<ImageParameter>();
        }

        /// <summary>Gets the query-string parameters appended to <see cref="ImageHandlerUrl"/>.</summary>
        [Parameter]
        public List<ImageParameter> Parameters { get; set; }

        /// <summary>Gets or sets the URL of the image handler, e.g. <c>~/DnnImageHandler.ashx</c>.</summary>
        [Parameter]
        public string ImageHandlerUrl
        {
            get
            {
                return this.imageHandlerUrl ?? string.Empty;
            }

            set
            {
                this.imageHandlerUrl = value;
            }
        }

        /// <summary>Gets or sets a cache-busting timestamp appended to the generated URL.</summary>
        [Parameter]
        public string Timestamp
        {
            get
            {
                return this.timestamp ?? string.Empty;
            }

            set
            {
                this.timestamp = value;
            }
        }

        /// <summary>Gets or sets the alternate text, rendered as the <c>alt</c> attribute.</summary>
        [Parameter]
        public string AlternateText { get; set; }

        /// <summary>Gets or sets the CSS class rendered on the <c>&lt;img&gt;</c>.</summary>
        [Parameter]
        public string CssClass { get; set; }

        /// <summary>Gets or sets any additional attributes to render on the <c>&lt;img&gt;</c>.</summary>
        [Parameter(CaptureUnmatchedValues = true)]
        public IReadOnlyDictionary<string, object> AdditionalAttributes { get; set; }

        /// <summary>Gets the URL that was built for the image, i.e. the rendered <c>src</c>.</summary>
        public string ImageUrl { get; private set; }

        /// <inheritdoc />
        protected override void OnParametersSet()
        {
            base.OnParametersSet();

            // Replaces OnDataBinding: give every parameter the chance to compute its value.
            if (this.Parameters != null)
            {
                foreach (var parameter in this.Parameters)
                {
                    parameter.DataBind();
                }
            }

            // Replaces OnPreRender's this.ImageUrl = this.BuildImageUrl().
            this.ImageUrl = this.BuildImageUrl();
        }

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.OpenElement(0, "img");
            if (this.AdditionalAttributes != null)
            {
                builder.AddMultipleAttributes(1, this.AdditionalAttributes);
            }

            if (!string.IsNullOrEmpty(this.CssClass))
            {
                builder.AddAttribute(2, "class", this.CssClass);
            }

            builder.AddAttribute(3, "alt", this.AlternateText ?? string.Empty);
            builder.AddAttribute(4, "src", this.ImageUrl);
            builder.CloseElement();
        }

        private static void AddQueryStringParameter(StringBuilder stringBuilder, bool paramAlreadyAdded, string name, string value)
        {
            stringBuilder.Append(paramAlreadyAdded ? '&' : '?');

            // Replaces HttpUtility.UrlEncode, which lives in System.Web.
            stringBuilder.Append(WebUtility.UrlEncode(name));
            stringBuilder.Append('=');
            stringBuilder.Append(WebUtility.UrlEncode(value));
        }

        private string BuildImageUrl()
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.Append(this.ImageHandlerUrl);

            var paramAlreadyAdded = false;

            if (this.Parameters != null)
            {
                foreach (var parameter in this.Parameters)
                {
                    AddQueryStringParameter(stringBuilder, paramAlreadyAdded, parameter.Name, parameter.Value);
                    paramAlreadyAdded = true;
                }
            }

            string timeStamp = this.Timestamp?.Trim();
            if (!string.IsNullOrEmpty(timeStamp))
            {
                AddQueryStringParameter(stringBuilder, paramAlreadyAdded, TimestampField, timeStamp);
            }

            return stringBuilder.ToString();
        }
    }
}
