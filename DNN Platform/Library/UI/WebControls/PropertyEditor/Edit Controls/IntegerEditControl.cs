// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Globalization;

    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Instrumentation;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Web;
    using Microsoft.Extensions.Logging;

    /// <summary>The IntegerEditControl control provides a standard UI component for editing integer properties.</summary>
    public class IntegerEditControl : EditControl
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<IntegerEditControl>();

        /// <summary>
        /// Initializes a new instance of the <see cref="IntegerEditControl"/> class.
        /// Constructs an IntegerEditControl.
        /// </summary>
        public IntegerEditControl()
        {
            this.SystemType = "System.Int32";
        }

        /// <summary>Gets integerValue returns the Integer representation of the Value.</summary>
        /// <value>An integer representing the Value.</value>
        protected int IntegerValue
        {
            get
            {
                int intValue = Null.NullInteger;
                try
                {
                    // Try and cast the value to an Integer
                    if (this.Value != null)
                    {
                        if (!int.TryParse(this.Value.ToString(), out intValue))
                        {
                            intValue = Null.NullInteger;
                        }
                    }
                }
                catch (Exception exc)
                {
                    Logger.IntegerEditControlIntegerValueException(exc);
                }

                return intValue;
            }
        }

        /// <summary>Gets oldIntegerValue returns the Integer representation of the OldValue.</summary>
        /// <value>An integer representing the OldValue.</value>
        protected int OldIntegerValue
        {
            get
            {
                int intValue = Null.NullInteger;
                try
                {
                    // Try and cast the value to an Integer
                    if (!int.TryParse(this.OldValue.ToString(), out intValue))
                    {
                        intValue = Null.NullInteger;
                    }
                }
                catch (Exception exc)
                {
                    Logger.IntegerEditControlOldIntegerValueException(exc);
                }

                return intValue;
            }
        }

        /// <summary>Gets or sets stringValue is the value of the control expressed as a String.</summary>
        /// <value>A string representing the Value.</value>
        protected override string StringValue
        {
            get
            {
                return this.IntegerValue.ToString(CultureInfo.InvariantCulture);
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
            var args = new PropertyEditorEventArgs(this.Name);
            args.Value = this.IntegerValue;
            args.OldValue = this.OldIntegerValue;
            args.StringValue = this.StringValue;
            this.OnValueChanged(args);
        }

        /// <summary>RenderEditMode renders the Edit mode of the control.</summary>
        /// <param name="builder">The render tree builder to write to.</param>
        protected override void RenderEditMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.OpenElement(0, "input");
            this.AddStyleAttributes(builder, 1);
            builder.AddAttribute(5, "type", "text");
            builder.AddAttribute(6, "size", "5");
            builder.AddAttribute(7, "value", this.StringValue);
            builder.AddAttribute(8, "name", this.UniqueID);
            builder.AddAttribute(9, "id", this.ClientID);
            builder.AddAttribute(10, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, this.OnInputChanged));
            builder.CloseElement();
        }
    }
}
