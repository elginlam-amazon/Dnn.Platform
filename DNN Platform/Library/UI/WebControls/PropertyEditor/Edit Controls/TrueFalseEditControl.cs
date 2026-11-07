// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Globalization;

    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Services.Localization;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Web;
    using Microsoft.Extensions.Logging;

    /// <summary>The TrueFalseEditControl control provides a standard UI component for editing true/false (boolean) properties.</summary>
    public class TrueFalseEditControl : EditControl
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<TrueFalseEditControl>();

        /// <summary>
        /// Initializes a new instance of the <see cref="TrueFalseEditControl"/> class.
        /// Constructs a TrueFalseEditControl.
        /// </summary>
        public TrueFalseEditControl()
        {
            this.SystemType = "System.Boolean";
        }

        /// <summary>Gets a value indicating whether booleanValue returns the Boolean representation of the Value.</summary>
        /// <value>A Boolean representing the Value.</value>
        protected bool BooleanValue
        {
            get
            {
                bool boolValue = Null.NullBoolean;
                try
                {
                    // Try and cast the value to a Boolean
                    boolValue = Convert.ToBoolean(this.Value, CultureInfo.InvariantCulture);
                }
                catch (Exception exc)
                {
                    Logger.TrueFalseEditControlBooleanValueException(exc);
                }

                return boolValue;
            }
        }

        /// <summary>Gets a value indicating whether oldBooleanValue returns the Boolean representation of the OldValue.</summary>
        /// <value>A Boolean representing the OldValue.</value>
        protected bool OldBooleanValue
        {
            get
            {
                bool boolValue = Null.NullBoolean;
                try
                {
                    // Try and cast the value to a Boolean
                    boolValue = Convert.ToBoolean(this.OldValue, CultureInfo.InvariantCulture);
                }
                catch (Exception exc)
                {
                    Logger.TrueFalseEditControlOldBooleanValueException(exc);
                }

                return boolValue;
            }
        }

        /// <summary>Gets or sets stringValue is the value of the control expressed as a String.</summary>
        /// <value>A string representing the Value.</value>
        protected override string StringValue
        {
            get
            {
                return this.BooleanValue.ToString();
            }

            set
            {
                bool setValue = bool.Parse(value);
                this.Value = setValue;
            }
        }

        /// <summary>OnDataChanged runs when the PostbackData has changed.  It raises the <see cref="EditControl.ValueChanged"/> Event.</summary>
        /// <param name="e">The event arguments.</param>
        protected override void OnDataChanged(EventArgs e)
        {
            var args = new PropertyEditorEventArgs(this.Name);
            args.Value = this.BooleanValue;
            args.OldValue = this.OldBooleanValue;
            args.StringValue = this.StringValue;
            this.OnValueChanged(args);
        }

        /// <summary>RenderEditMode renders the Edit mode of the control.</summary>
        /// <param name="builder">The render tree builder to write to.</param>
        protected override void RenderEditMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            this.RenderOption(builder, 0, "True", this.BooleanValue, Localization.GetString("True", Localization.SharedResourceFile));
            this.RenderOption(builder, 20, "False", !this.BooleanValue, Localization.GetString("False", Localization.SharedResourceFile));
        }

        private void RenderOption(RenderTreeBuilder builder, int sequence, string optionValue, bool isChecked, string optionText)
        {
            builder.OpenElement(sequence, "input");
            builder.AddAttribute(sequence + 1, "type", "radio");
            if (isChecked)
            {
                builder.AddAttribute(sequence + 2, "checked", "checked");
            }

            builder.AddAttribute(sequence + 3, "value", optionValue);
            builder.AddAttribute(sequence + 4, "name", this.UniqueID);
            builder.AddAttribute(sequence + 5, "aria-label", this.Name);
            builder.AddAttribute(sequence + 6, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, this.OnInputChanged));
            builder.CloseElement();

            builder.OpenElement(sequence + 10, "span");
            this.AddStyleAttributes(builder, sequence + 11);
            builder.AddContent(sequence + 15, optionText);
            builder.CloseElement();
        }
    }
}
