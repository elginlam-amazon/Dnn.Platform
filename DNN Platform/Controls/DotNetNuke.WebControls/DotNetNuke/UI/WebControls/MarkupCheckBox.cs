// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    /// <summary>Renders a check box. Replaces <c>System.Web.UI.WebControls.CheckBox</c>.</summary>
    internal sealed class MarkupCheckBox : MarkupControl
    {
        /// <summary>Gets or sets a value indicating whether the box renders as checked.</summary>
        internal bool Checked { get; set; }

        /// <summary>Gets or sets the value rendered as the <c>name</c> attribute.</summary>
        internal string Name { get; set; }

        /// <inheritdoc/>
        protected override string TagName => "input";

        /// <inheritdoc/>
        protected override bool IsSelfClosing => true;

        /// <inheritdoc/>
        protected override void AddAttributesToRender(HtmlMarkupWriter writer)
        {
            writer.AddAttribute("type", "checkbox");
            writer.AddAttribute("name", this.Name ?? this.Id);
            if (this.Checked)
            {
                writer.AddAttribute("checked", "checked");
            }

            base.AddAttributesToRender(writer);
        }
    }
}
