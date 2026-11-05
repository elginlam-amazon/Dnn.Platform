// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Installer.Packages.WebControls
{
    using System;
    using System.Collections.Generic;

    using DotNetNuke.Common.Utilities;
    using DotNetNuke.UI.WebControls;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Web;

    using DNNLocalization = DotNetNuke.Services.Localization.Localization;

    /// <summary>
    /// The PackageTypeEditControl control provides a standard UI component for editing
    /// package types.
    /// </summary>
    /// <remarks>
    /// Migrated from a WebForms <c>TextEditControl</c> (which rendered through an
    /// <c>HtmlTextWriter</c> and carried a <c>[ToolboxData]</c> designer attribute, neither of
    /// which exists on modern .NET) to the Blazor <c>EditControl</c> component contract: the edit
    /// mode now writes into a <see cref="RenderTreeBuilder"/>. The rendered markup — a
    /// <c>&lt;select&gt;</c> with a "not specified" option followed by one option per extension
    /// package type, with the current value selected — is unchanged.
    /// </remarks>
    public class PackageTypeEditControl : TextEditControl
    {
        /// <summary>RenderEditMode renders the Edit mode of the control.</summary>
        /// <param name="builder">The render tree builder to write to.</param>
        protected override void RenderEditMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            IList<PackageType> packageTypes = PackageController.Instance.GetExtensionPackageTypes();

            // Render the Select Tag
            builder.OpenElement(0, "select");
            this.AddStyleAttributes(builder, 1);
            builder.AddAttribute(5, "name", this.UniqueID);
            builder.AddAttribute(6, "id", this.ClientID);
            builder.AddAttribute(7, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, this.OnInputChanged));

            // Add the Not Specified Option
            builder.OpenElement(8, "option");
            builder.AddAttribute(9, "value", Null.NullString);
            if (this.StringValue == Null.NullString)
            {
                builder.AddAttribute(10, "selected", "selected");
            }

            builder.AddContent(11, "<" + DNNLocalization.GetString("Not_Specified", DNNLocalization.SharedResourceFile) + ">");
            builder.CloseElement();

            var sequence = 12;
            foreach (PackageType type in packageTypes)
            {
                builder.OpenElement(sequence++, "option");

                // Add the Value Attribute
                builder.AddAttribute(sequence++, "value", type.PackageType);

                if (type.PackageType == this.StringValue)
                {
                    // Add the Selected Attribute
                    builder.AddAttribute(sequence++, "selected", "selected");
                }

                builder.AddContent(sequence++, type.PackageType);
                builder.CloseElement();
            }

            // Close Select Tag
            builder.CloseElement();
        }
    }
}
