// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System.Globalization;
    using System.IO;

    /// <summary>
    /// Produces the design-time preview markup of a <see cref="DNNMenu"/>.
    /// </summary>
    /// <remarks>
    /// The original derived from <c>System.Web.UI.Design.ControlDesigner</c>, which has no .NET 10
    /// equivalent (the whole <c>System.Design</c> surface was dropped). The class is therefore kept
    /// as a plain helper: the designed control is supplied through <see cref="Component"/> instead
    /// of the base class property, and the preview markup is produced by this project's
    /// <see cref="MarkupControl"/> primitives rather than by rendering a WebForms <c>Label</c>.
    /// The emitted HTML is unchanged.
    /// </remarks>
    public class DNNMenuDesigner
    {
        /// <summary>Gets or sets the menu being designed.</summary>
        public DNNMenu Component { get; set; }

        /// <summary>Gets a value indicating whether the designer surface may resize the control.</summary>
        public virtual bool AllowResize
        {
            get { return false; }
        }

        /// <summary>Gets the markup shown for the control on the designer surface.</summary>
        /// <returns>The preview markup, or <see langword="null"/> when the control has no identifier.</returns>
        public virtual string GetDesignTimeHtml()
        {
            var component = this.Component;
            if (component is null || component.ID is null || component.ID.Length <= 0)
            {
                return null;
            }

            var writer = new StringWriter(CultureInfo.InvariantCulture);
            var label = new MarkupLabel
            {
                CssClass = component.MenuBarCssClass + " " + component.DefaultNodeCssClass,
                Text = component.ID,
            };

            if (component.Orientation == Orientation.Horizontal)
            {
                label.Style["width"] = "100%";
            }
            else
            {
                label.Style["height"] = "500px";
            }

            label.RenderControl(writer);
            return writer.ToString();
        }
    }
}
