// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using System;

    using DotNetNuke.UI;

    using Microsoft.AspNetCore.Components;

    /// <summary>A time zone combobox control.</summary>
    /// <remarks>
    /// This was a <c>DropDownList</c> that bound itself to <c>TimeZoneInfo.GetSystemTimeZones()</c>
    /// in <c>OnInit</c>. It derives from <see cref="DnnListControl"/> now — the ASP.NET Core
    /// replacement of <c>ListControl</c> — and binds the very same source, with the same
    /// text/value fields. <see cref="InitialValue"/> lets a host (the time zone edit control)
    /// preselect a zone declaratively, which replaces the <c>DataBind(value)</c> call the Web Forms
    /// host made in its pre-render stage.
    /// </remarks>
    public class DnnTimeZoneComboBox : DnnListControl
    {
        /// <summary>Gets or sets the identifier of the time zone to preselect.</summary>
        [Parameter]
        public string InitialValue { get; set; }

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            this.DataTextField = "DisplayName";
            this.DataValueField = "Id";

            this.DataSource = TimeZoneInfo.GetSystemTimeZones();
            this.DataBind();

            this.ApplyInitialValue();
        }

        /// <inheritdoc />
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            this.ApplyInitialValue();
        }

        private void ApplyInitialValue()
        {
            if (string.IsNullOrEmpty(this.InitialValue) || this.Items.Count == 0)
            {
                return;
            }

            var item = this.Items.FindByValue(this.InitialValue);
            if (item != null && !item.Selected)
            {
                this.ClearSelection();
                item.Selected = true;
            }
        }
    }
}
