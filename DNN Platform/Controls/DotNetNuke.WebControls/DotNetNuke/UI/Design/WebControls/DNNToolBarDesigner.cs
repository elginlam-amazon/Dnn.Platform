// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.Design.WebControls
{
    using System.Globalization;
    using System.IO;

    using DotNetNuke.UI.WebControls;

    /// <summary>
    /// Produces the design-time preview markup of a <see cref="DNNToolBar"/>.
    /// </summary>
    /// <remarks>
    /// The original derived from <c>System.Web.UI.Design.ControlDesigner</c>, which has no .NET 10
    /// equivalent (the whole <c>System.Design</c> surface was dropped). The class is therefore kept
    /// as a plain helper: the designed control is supplied through <see cref="Component"/> instead
    /// of the base class property, and the preview markup is produced by this project's markup
    /// primitives rather than by rendering WebForms <c>Label</c> controls. The emitted HTML is
    /// unchanged, including the three inline style declarations the original added last.
    /// </remarks>
    public class DNNToolBarDesigner
    {
        /// <summary>Gets or sets the tool bar being designed.</summary>
        public DNNToolBar Component { get; set; }

        /// <summary>Gets the markup shown for the control on the designer surface.</summary>
        /// <returns>The preview markup.</returns>
        public virtual string GetDesignTimeHtml()
        {
            var component = this.Component;
            if (component is null)
            {
                return null;
            }

            var writer = new StringWriter(CultureInfo.InvariantCulture);
            var container = new MarkupLabel();
            if (!string.IsNullOrEmpty(component.CssClass))
            {
                container.CssClass = component.CssClass;
            }

            foreach (DNNToolBarButton button in component.Buttons)
            {
                var child = new MarkupLabel();
                if (!string.IsNullOrEmpty(button.CssClass))
                {
                    child.CssClass = button.CssClass;
                }

                if (!string.IsNullOrEmpty(button.Text))
                {
                    child.Text = button.Text;
                }

                container.Controls.Add(child);
            }

            container.Style["position"] = string.Empty;
            container.Style["top"] = "0px";
            container.Style["left"] = "0px";
            container.RenderControl(writer);
            return writer.ToString();
        }
    }
}
