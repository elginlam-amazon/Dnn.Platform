// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Globalization;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Web;

    /// <summary>
    /// The MultiLineTextEditControl control provides a standard UI component for editing
    /// string/text properties.
    /// </summary>
    public class MultiLineTextEditControl : TextEditControl
    {
        /// <inheritdoc />
        protected override void RenderEditMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            string propValue = Convert.ToString(this.Value, CultureInfo.InvariantCulture);

            builder.OpenElement(0, "textarea");
            this.AddStyleAttributes(builder, 1);
            builder.AddAttribute(5, "name", this.UniqueID);
            builder.AddAttribute(6, "id", this.ClientID);
            builder.AddAttribute(7, "aria-label", "editor");
            builder.AddAttribute(8, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, this.OnInputChanged));
            builder.AddContent(9, propValue);
            builder.CloseElement();
        }
    }
}
