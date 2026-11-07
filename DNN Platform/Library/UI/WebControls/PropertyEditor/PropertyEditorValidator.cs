// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Globalization;
    using System.Text.RegularExpressions;

    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>Validates the value of an <see cref="EditControl"/>.</summary>
    /// <remarks>Replaces <c>System.Web.UI.IValidator</c>, which does not exist on ASP.NET Core.</remarks>
    public interface IValidator
    {
        /// <summary>Gets a value indicating whether the validated value is valid.</summary>
        bool IsValid { get; }

        /// <summary>Gets or sets the message displayed when the value is not valid.</summary>
        string ErrorMessage { get; set; }

        /// <summary>Evaluates the validated value and updates <see cref="IsValid"/>.</summary>
        void Validate();
    }

    /// <summary>The kind of rule a <see cref="PropertyEditorValidator"/> enforces.</summary>
    public enum PropertyEditorValidatorType
    {
        /// <summary>The value must not be empty.</summary>
        Required = 0,

        /// <summary>The value must match a regular expression.</summary>
        RegularExpression = 1,
    }

    /// <summary>
    /// Validates the value of an <see cref="EditControl"/> and renders the validation message.
    /// </summary>
    /// <remarks>
    /// Replaces the <c>RequiredFieldValidator</c> and <c>RegularExpressionValidator</c> instances that
    /// <see cref="FieldEditorControl"/> created. The rules are unchanged: a required field must have a
    /// non-empty value, and a regular expression must match — but, exactly as with
    /// <c>BaseValidator</c>, an empty value always passes a regular expression check (the
    /// <c>RequiredFieldValidator</c> is what rejects it). The message is rendered dynamically, which
    /// is what <c>ValidatorDisplay.Dynamic</c> did.
    /// </remarks>
    public class PropertyEditorValidator : IValidator
    {
        private readonly Func<string> valueAccessor;

        /// <summary>Initializes a new instance of the <see cref="PropertyEditorValidator"/> class.</summary>
        /// <param name="validatorType">The kind of rule to enforce.</param>
        /// <param name="valueAccessor">Reads the current value of the control being validated.</param>
        public PropertyEditorValidator(PropertyEditorValidatorType validatorType, Func<string> valueAccessor)
        {
            this.ValidatorType = validatorType;
            this.valueAccessor = valueAccessor;
            this.IsValid = true;
            this.CssClass = "dnnFormMessage dnnFormError";
        }

        /// <summary>Gets the kind of rule this validator enforces.</summary>
        public PropertyEditorValidatorType ValidatorType { get; }

        /// <summary>Gets or sets the identifier of this validator.</summary>
        public string ID { get; set; }

        /// <summary>Gets or sets the identifier of the control being validated.</summary>
        public string ControlToValidate { get; set; }

        /// <summary>Gets or sets the regular expression to match, for a
        /// <see cref="PropertyEditorValidatorType.RegularExpression"/> validator.</summary>
        public string ValidationExpression { get; set; }

        /// <summary>Gets or sets the message displayed when the value is not valid.</summary>
        public string ErrorMessage { get; set; }

        /// <summary>Gets or sets the localization key applied to the rendered message.</summary>
        public string ResourceKey { get; set; }

        /// <summary>Gets or sets the css class applied to the rendered message.</summary>
        public string CssClass { get; set; }

        /// <summary>Gets or sets the width of the rendered message.</summary>
        public Unit Width { get; set; }

        /// <summary>Gets or sets the style applied to the rendered message.</summary>
        public Style ControlStyle { get; set; } = new Style();

        /// <summary>Gets a value indicating whether the validated value is valid.</summary>
        public bool IsValid { get; private set; }

        /// <inheritdoc />
        public void Validate()
        {
            var value = this.valueAccessor?.Invoke();
            switch (this.ValidatorType)
            {
                case PropertyEditorValidatorType.Required:
                    this.IsValid = !string.IsNullOrEmpty(value);
                    break;
                case PropertyEditorValidatorType.RegularExpression:
                    // As with BaseValidator, an empty value is left to the required validator.
                    this.IsValid = string.IsNullOrEmpty(value)
                                   || string.IsNullOrEmpty(this.ValidationExpression)
                                   || Regex.IsMatch(value, this.ValidationExpression, RegexOptions.None, TimeSpan.FromSeconds(2));
                    break;
                default:
                    this.IsValid = true;
                    break;
            }
        }

        /// <summary>Renders the validation message when the value is not valid.</summary>
        /// <param name="builder">The render tree builder to write to.</param>
        /// <param name="sequence">The render tree sequence number to use.</param>
        public void RenderContent(RenderTreeBuilder builder, int sequence)
        {
            ArgumentNullException.ThrowIfNull(builder);

            this.Validate();
            if (this.IsValid)
            {
                return;
            }

            builder.OpenElement(sequence, "span");
            builder.AddAttribute(sequence + 1, "id", this.ID);
            if (!string.IsNullOrEmpty(this.CssClass))
            {
                builder.AddAttribute(sequence + 2, "class", this.CssClass);
            }

            var inline = this.ControlStyle?.GetInlineStyle();
            if (!this.Width.IsEmpty)
            {
                inline = $"width:{this.Width.ToString(CultureInfo.InvariantCulture)};{inline}";
            }

            if (!string.IsNullOrEmpty(inline))
            {
                builder.AddAttribute(sequence + 3, "style", inline);
            }

            if (!string.IsNullOrEmpty(this.ResourceKey))
            {
                // PageBase localizes and then strips the resourcekey attribute, exactly as the
                // Web Forms render-time control tree walk used to.
                builder.AddAttribute(sequence + 4, "resourcekey", this.ResourceKey);
            }

            builder.AddContent(sequence + 5, this.ErrorMessage);
            builder.CloseElement();
        }
    }
}
