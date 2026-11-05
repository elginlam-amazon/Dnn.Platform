// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections.Specialized;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Web;

    /// <summary>
    /// The TrueFalseEditControl control provides a standard UI component for editing
    /// true/false (boolean) properties.
    /// </summary>
    public class CheckEditControl : TrueFalseEditControl
    {
        /// <summary>Initializes a new instance of the <see cref="CheckEditControl"/> class.</summary>
        public CheckEditControl()
        {
            this.SystemType = "System.Boolean";
        }

        /// <inheritdoc />
        public override bool LoadPostData(string postDataKey, NameValueCollection postCollection)
        {
            ArgumentNullException.ThrowIfNull(postCollection);

            string postedValue = postCollection[postDataKey];
            bool boolValue = false;
            if (!(postedValue == null || postedValue == string.Empty))
            {
                boolValue = true;
            }

            if (!this.BooleanValue.Equals(boolValue))
            {
                this.Value = boolValue;
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        protected override void RenderEditMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.OpenElement(0, "input");
            builder.AddAttribute(1, "type", "checkbox");
            if (this.BooleanValue)
            {
                builder.AddAttribute(2, "checked", "checked");
                builder.AddAttribute(3, "value", "1");
            }
            else
            {
                builder.AddAttribute(3, "value", string.Empty);
            }

            builder.AddAttribute(4, "name", this.UniqueID);
            builder.AddAttribute(5, "id", this.ClientID);
            builder.AddAttribute(6, "aria-label", this.Name);
            builder.AddAttribute(7, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, this.OnCheckedChanged));
            builder.CloseElement();
        }

        /// <inheritdoc />
        protected override void RenderViewMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.OpenElement(0, "input");
            builder.AddAttribute(1, "type", "checkbox");
            if (this.BooleanValue)
            {
                builder.AddAttribute(2, "checked", "checked");
            }

            builder.AddAttribute(3, "disabled", "disabled");
            builder.AddAttribute(4, "aria-label", this.Name);
            builder.CloseElement();
        }

        /// <summary>Handles a change of the checkbox.</summary>
        /// <param name="args">The change event arguments.</param>
        /// <remarks>
        /// A checkbox only posted a value when it was checked, which is what the
        /// <c>onclick="this.value = this.checked ? '1' : '';"</c> handler of the Web Forms version
        /// arranged for. Blazor reports <c>checked</c> for a checkbox, so the boolean is mapped back
        /// onto the same <c>"1"</c>/empty value that <see cref="LoadPostData"/> expects.
        /// </remarks>
        private void OnCheckedChanged(ChangeEventArgs args)
        {
            var isChecked = args.Value is bool boolValue
                ? boolValue
                : !string.IsNullOrEmpty(args.Value?.ToString());
            this.SetPostValue(string.Empty, isChecked ? "1" : string.Empty);
            this.CommitPostData();
        }
    }
}
