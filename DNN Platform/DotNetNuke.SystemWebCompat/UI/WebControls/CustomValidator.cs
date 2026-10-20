// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.UI.WebControls
{
    using System.ComponentModel;

    /// <summary>Performs a user-defined validation of an input control.</summary>
    [DefaultEvent("ServerValidate")]
    [ToolboxData("<{0}:CustomValidator runat=\"server\" ErrorMessage=\"CustomValidator\"></{0}:CustomValidator>")]
    public class CustomValidator : BaseValidator
    {
        private static readonly object EventServerValidate = new object();

        /// <summary>Occurs when the validation is performed on the server.</summary>
        public event ServerValidateEventHandler ServerValidate
        {
            add => this.Events.AddHandler(EventServerValidate, value);
            remove => this.Events.RemoveHandler(EventServerValidate, value);
        }

        /// <summary>Gets or sets the name of the client side validation function.</summary>
        [DefaultValue("")]
        public string ClientValidationFunction
        {
            get => (string)this.ViewState[nameof(this.ClientValidationFunction)] ?? string.Empty;
            set => this.ViewState[nameof(this.ClientValidationFunction)] = value;
        }

        /// <summary>Gets or sets a value indicating whether empty text is validated.</summary>
        [DefaultValue(false)]
        public bool ValidateEmptyText
        {
            get => (bool?)this.ViewState[nameof(this.ValidateEmptyText)] ?? false;
            set => this.ViewState[nameof(this.ValidateEmptyText)] = value;
        }

        /// <inheritdoc />
        protected override void AddAttributesToRender(HtmlTextWriter writer)
        {
            base.AddAttributesToRender(writer);
            if (this.RenderUplevel && writer != null)
            {
                writer.AddAttribute("data-val-evaluationfunction", "CustomValidatorEvaluateIsValid", false);
                if (this.ClientValidationFunction.Length > 0)
                {
                    writer.AddAttribute("data-val-clientvalidationfunction", this.ClientValidationFunction);
                }

                if (this.ValidateEmptyText)
                {
                    writer.AddAttribute("data-val-validateemptytext", "true", false);
                }
            }
        }

        /// <inheritdoc />
        protected override bool ControlPropertiesValid()
        {
            var controlToValidate = this.ControlToValidate;
            if (controlToValidate.Length > 0)
            {
                this.CheckControlValidationProperty(controlToValidate, nameof(this.ControlToValidate));
            }

            return true;
        }

        /// <inheritdoc />
        protected override bool EvaluateIsValid()
        {
            var value = string.Empty;
            var controlToValidate = this.ControlToValidate;
            if (controlToValidate.Length > 0)
            {
                value = this.GetControlValidationValue(controlToValidate);
                if (string.IsNullOrWhiteSpace(value) && !this.ValidateEmptyText)
                {
                    return true;
                }
            }

            return this.OnServerValidate(value);
        }

        /// <summary>Raises the <see cref="ServerValidate"/> event.</summary>
        /// <param name="value">The value to validate.</param>
        /// <returns>A value indicating whether the value is valid.</returns>
        protected virtual bool OnServerValidate(string value)
        {
            var handler = (ServerValidateEventHandler)this.Events[EventServerValidate];
            var args = new ServerValidateEventArgs(value, true);
            handler?.Invoke(this, args);
            return args.IsValid;
        }
    }
}
