// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Services.GeneratedImage
{
    using System;
    using System.ComponentModel;
    using System.Globalization;

    /// <summary>A single query-string parameter appended to a <see cref="GeneratedImage"/> URL.</summary>
    /// <remarks>
    /// Was an <c>IDataBindingsAccessor</c> implementation holding a WebForms
    /// <c>DataBindingCollection</c>, neither of which exists outside <c>System.Web.UI</c>. In Blazor
    /// the parent component sets <see cref="Name"/> and <see cref="Value"/> directly, so the
    /// collection is gone; the <see cref="DataBinding"/> event and <see cref="DataBind"/> remain so
    /// that a parameter whose value is computed still has a hook that runs before the URL is built
    /// (<see cref="GeneratedImage"/> raises it from <c>OnParametersSet</c>).
    /// </remarks>
    [Bindable(true)]
    public class ImageParameter
    {
        /// <summary>Raised just before the containing <see cref="GeneratedImage"/> builds its URL.</summary>
        public event EventHandler DataBinding;

        /// <summary>Gets or sets the query-string parameter name.</summary>
        public string Name { get; set; }

        /// <summary>Gets or sets the query-string parameter value.</summary>
        public string Value { get; set; }

        /// <inheritdoc />
        public override string ToString()
        {
            if (string.IsNullOrEmpty(this.Name) && string.IsNullOrEmpty(this.Value))
            {
                return base.ToString();
            }

            return string.Format(CultureInfo.InvariantCulture, "{0} = {1}", this.Name, this.Value);
        }

        /// <summary>Raises <see cref="DataBinding"/>.</summary>
        internal void DataBind()
        {
            this.DataBinding?.Invoke(this, EventArgs.Empty);
        }
    }
}
