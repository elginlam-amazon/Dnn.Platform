// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Globalization;

    using DotNetNuke.Services.Localization;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Web;

    /// <summary>The EnumEditControl control provides a standard UI component for editing enumerated properties.</summary>
    public class EnumEditControl : EditControl
    {
        private readonly Type enumType;

        /// <summary>Initializes a new instance of the <see cref="EnumEditControl"/> class.</summary>
        public EnumEditControl()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="EnumEditControl"/> class.</summary>
        /// <param name="type">The name of the <see cref="Type"/> of the enum.</param>
        public EnumEditControl(string type)
        {
            this.SystemType = type;
            this.enumType = Type.GetType(type);
        }

        /// <summary>Gets or sets stringValue is the value of the control expressed as a String.</summary>
        /// <value>A string representing the Value.</value>
        protected override string StringValue
        {
            get
            {
                var retValue = Convert.ToInt32(this.Value, CultureInfo.InvariantCulture);
                return retValue.ToString(CultureInfo.InvariantCulture);
            }

            set
            {
                int setValue = int.Parse(value, CultureInfo.InvariantCulture);
                this.Value = setValue;
            }
        }

        /// <summary>OnDataChanged runs when the PostbackData has changed.  It raises the <see cref="EditControl.ValueChanged"/> Event.</summary>
        /// <param name="e">The event arguments.</param>
        protected override void OnDataChanged(EventArgs e)
        {
            int intValue = Convert.ToInt32(this.Value, CultureInfo.InvariantCulture);
            int intOldValue = Convert.ToInt32(this.OldValue, CultureInfo.InvariantCulture);

            var args = new PropertyEditorEventArgs(this.Name)
            { Value = Enum.ToObject(this.enumType, intValue), OldValue = Enum.ToObject(this.enumType, intOldValue) };

            this.OnValueChanged(args);
        }

        /// <summary>RenderEditMode renders the Edit mode of the control.</summary>
        /// <param name="builder">The render tree builder to write to.</param>
        protected override void RenderEditMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            int propValue = Convert.ToInt32(this.Value, CultureInfo.InvariantCulture);
            var enumValues = Enum.GetValues(this.enumType);

            // Render the Select Tag
            builder.OpenElement(0, "select");
            this.AddStyleAttributes(builder, 1);
            builder.AddAttribute(5, "name", this.UniqueID);
            builder.AddAttribute(6, "id", this.ClientID);
            builder.AddAttribute(7, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, this.OnInputChanged));

            builder.OpenRegion(8);
            for (int i = 0; i <= enumValues.Length - 1; i++)
            {
                int enumValue = (int)enumValues.GetValue(i);
                string enumName = Enum.GetName(this.enumType, enumValue);
                enumName = Localization.GetString(enumName, this.LocalResourceFile);

                builder.OpenElement(0, "option");

                // Add the Value Attribute
                builder.AddAttribute(1, "value", enumValue.ToString(CultureInfo.InvariantCulture));

                if (enumValue == propValue)
                {
                    // Add the Selected Attribute
                    builder.AddAttribute(2, "selected", "selected");
                }

                builder.AddContent(3, enumName);
                builder.CloseElement();
            }

            builder.CloseRegion();

            // Close Select Tag
            builder.CloseElement();
        }

        /// <summary>RenderViewMode renders the View (readonly) mode of the control.</summary>
        /// <param name="builder">The render tree builder to write to.</param>
        protected override void RenderViewMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            int propValue = Convert.ToInt32(this.Value, CultureInfo.InvariantCulture);
            string enumValue = Enum.Format(this.enumType, propValue, "G");

            builder.OpenElement(0, "span");
            this.AddStyleAttributes(builder, 1);
            builder.AddContent(5, enumValue);
            builder.CloseElement();
        }
    }
}
