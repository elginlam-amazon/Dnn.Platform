// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.UI.WebControls
{
    using System;

    using DotNetNuke.Common.Utilities;
    using DotNetNuke.UI;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>A hidden field with a typed value.</summary>
    /// <typeparam name="T">The type of value.</typeparam>
    /// <remarks>
    /// This was a <c>HiddenField</c> that round-tripped its JSON payload through view state and
    /// <c>LoadPostData</c>. On ASP.NET Core it is a <see cref="DnnComponentBase"/> that renders an
    /// <c>input type="hidden"</c>; the client scripts still write into that input and raise its
    /// <c>change</c> event, which is now handled server side by <see cref="OnValueInput"/> — this
    /// replaces <c>LoadPostData</c> and raises <see cref="ValueChanged"/> exactly as before.
    /// </remarks>
    public class DnnGenericHiddenField<T> : DnnComponentBase
        where T : class, new()
    {
        private T typedValue;
        private string serializedValue = string.Empty;
        private bool isValueSerialized;

        /// <summary>Occurs when the value of this field changed.</summary>
        public event EventHandler ValueChanged;

        /// <summary>Gets the typed value or a new instance of <typeparamref name="T"/> if it hasn't been set.</summary>
        public T TypedValueOrDefault
        {
            get
            {
                return this.TypedValue ?? (this.TypedValue = new T());
            }
        }

        /// <summary>Gets a value indicating whether the hidden field has a value.</summary>
        public bool HasValue
        {
            get { return this.typedValue != null; }
        }

        /// <summary>Gets or sets the typed value.</summary>
        public T TypedValue
        {
            get
            {
                return this.typedValue;
            }

            set
            {
                this.typedValue = value;
                this.isValueSerialized = false;
            }
        }

        // BL0007: the parameter keeps the property accessors of the original Web Forms control, which forward to the child control or to view state.
        #pragma warning disable BL0007
        /// <summary>Gets or sets the serialized (JSON) value of this field.</summary>
        /// <remarks>Replaces <c>HiddenField.Value</c>.</remarks>
        [Parameter]
        public string Value
        {
            get
            {
                this.EnsureValue();
                return this.serializedValue;
            }

            set
            {
                this.serializedValue = value;
                this.isValueSerialized = true;
                this.SetTypedValue();
            }
        }
        #pragma warning restore BL0007

        /// <summary>Gets or sets a callback raised in addition to the <see cref="ValueChanged"/> event.</summary>
        [Parameter]
        public EventCallback<string> ValueChangedCallback { get; set; }

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            if (!this.Visible)
            {
                return;
            }

            this.EnsureValue();

            builder.OpenElement(0, "input");
            builder.AddAttribute(1, "type", "hidden");
            if (!string.IsNullOrEmpty(this.ID))
            {
                builder.AddAttribute(2, "id", this.ClientId);
                builder.AddAttribute(3, "name", this.UniqueId);
            }

            builder.AddAttribute(4, "value", this.serializedValue);
            builder.AddAttribute(5, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, this.OnValueInput));
            builder.CloseElement();
        }

        /// <summary>Handles the value written into this field by the client scripts.</summary>
        /// <param name="args">The change event arguments.</param>
        /// <remarks>Replaces <c>HiddenField.LoadPostData</c>.</remarks>
        protected virtual void OnValueInput(ChangeEventArgs args)
        {
            ArgumentNullException.ThrowIfNull(args);

            var posted = args.Value?.ToString() ?? string.Empty;
            if (string.Equals(posted, this.serializedValue, StringComparison.Ordinal))
            {
                return;
            }

            this.serializedValue = posted;
            this.isValueSerialized = true;
            this.SetTypedValue();

            this.ValueChanged?.Invoke(this, EventArgs.Empty);
            if (this.ValueChangedCallback.HasDelegate)
            {
                this.ValueChangedCallback.InvokeAsync(posted);
            }
        }

        private void SetTypedValue()
        {
            this.typedValue = string.IsNullOrEmpty(this.serializedValue) ? null : Json.Deserialize<T>(this.serializedValue);
        }

        private void EnsureValue()
        {
            if (!this.isValueSerialized)
            {
                this.serializedValue = this.typedValue == null ? string.Empty : Json.Serialize(this.typedValue);
                this.isValueSerialized = true;
            }
        }
    }
}
