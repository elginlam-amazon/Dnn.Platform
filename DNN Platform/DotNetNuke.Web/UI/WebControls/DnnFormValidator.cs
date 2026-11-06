// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using System;
    using System.Text.RegularExpressions;

    using DotNetNuke.UI;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>The kind of rule a <see cref="DnnFormValidator"/> enforces.</summary>
    public enum DnnFormValidatorType
    {
        /// <summary>The value must not be empty.</summary>
        Required = 0,

        /// <summary>The value must match a regular expression.</summary>
        RegularExpression = 1,
    }

    /// <summary>How a <see cref="DnnFormValidator"/> occupies space when it is valid.</summary>
    /// <remarks>Replaces <c>System.Web.UI.WebControls.ValidatorDisplay</c>.</remarks>
    public enum DnnValidatorDisplay
    {
        /// <summary>No markup is rendered while the value is valid.</summary>
        Dynamic = 0,

        /// <summary>An empty, space-occupying element is rendered while the value is valid.</summary>
        Static = 1,

        /// <summary>Nothing is ever rendered inline.</summary>
        None = 2,
    }

    /// <summary>
    /// Validates the value of a <see cref="DnnFormItemBase"/> and renders its validation message.
    /// </summary>
    /// <remarks>
    /// Replaces the <c>RequiredFieldValidator</c> and <c>RegularExpressionValidator</c> instances
    /// that <see cref="DnnFormItemBase"/> added to its control tree. The rules are unchanged: a
    /// required value must be non-empty and a regular expression must match, but — exactly as with
    /// <c>BaseValidator</c> — an empty value always passes the regular expression check because the
    /// required validator is what rejects it.
    /// </remarks>
    public class DnnFormValidator : DnnComponentBase, DotNetNuke.UI.WebControls.IValidator
    {
        private readonly Func<string> valueAccessor;

        /// <summary>Initializes a new instance of the <see cref="DnnFormValidator"/> class.</summary>
        /// <param name="validatorType">The kind of rule to enforce.</param>
        /// <param name="valueAccessor">Reads the value being validated.</param>
        public DnnFormValidator(DnnFormValidatorType validatorType, Func<string> valueAccessor)
        {
            this.ValidatorType = validatorType;
            this.valueAccessor = valueAccessor;
            this.IsValid = true;
            this.CssClass = "dnnFormMessage dnnFormError";
        }

        /// <summary>Gets the kind of rule this validator enforces.</summary>
        public DnnFormValidatorType ValidatorType { get; }

        /// <summary>Gets or sets the identifier of the control being validated.</summary>
        [Parameter]
        public string ControlToValidate { get; set; }

        /// <summary>Gets or sets the regular expression the value must match.</summary>
        [Parameter]
        public string ValidationExpression { get; set; }

        /// <summary>Gets or sets the message rendered when the value is not valid.</summary>
        [Parameter]
        public string ErrorMessage { get; set; }

        /// <summary>Gets or sets how this validator occupies space while the value is valid.</summary>
        [Parameter]
        public DnnValidatorDisplay Display { get; set; } = DnnValidatorDisplay.Dynamic;

        /// <inheritdoc />
        public bool IsValid { get; private set; }

        /// <inheritdoc />
        public void Validate()
        {
            var value = this.valueAccessor?.Invoke();
            switch (this.ValidatorType)
            {
                case DnnFormValidatorType.Required:
                    this.IsValid = !string.IsNullOrEmpty(value);
                    break;
                case DnnFormValidatorType.RegularExpression:
                    this.IsValid = string.IsNullOrEmpty(value)
                        || string.IsNullOrEmpty(this.ValidationExpression)
                        || Regex.IsMatch(value, this.ValidationExpression, RegexOptions.None, TimeSpan.FromSeconds(2));
                    break;
                default:
                    this.IsValid = true;
                    break;
            }
        }

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            if (!this.Visible || this.Display == DnnValidatorDisplay.None)
            {
                return;
            }

            this.Validate();
            if (this.IsValid && this.Display == DnnValidatorDisplay.Dynamic)
            {
                return;
            }

            builder.OpenElement(0, "span");
            if (!string.IsNullOrEmpty(this.ID))
            {
                builder.AddAttribute(1, "id", this.ClientId);
            }

            if (!string.IsNullOrEmpty(this.CssClass))
            {
                builder.AddAttribute(2, "class", this.CssClass);
            }

            var inlineStyle = this.BuildStyleAttribute();
            if (!string.IsNullOrEmpty(inlineStyle))
            {
                builder.AddAttribute(3, "style", inlineStyle);
            }

            if (!this.IsValid)
            {
                builder.AddContent(4, this.ErrorMessage);
            }

            builder.CloseElement();
        }
    }
}
