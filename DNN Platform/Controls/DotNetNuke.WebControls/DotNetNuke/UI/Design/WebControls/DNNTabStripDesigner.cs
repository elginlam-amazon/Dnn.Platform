// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.Design.WebControls
{
    using System.Globalization;
    using System.IO;

    using DotNetNuke.UI.WebControls;

    /// <summary>
    /// Produces the design-time preview markup of a <see cref="DNNTabStrip"/>.
    /// </summary>
    /// <remarks>
    /// The original derived from <c>System.Web.UI.Design.ControlDesigner</c>, which has no .NET 10
    /// equivalent (the whole <c>System.Design</c> surface was dropped). The class is therefore kept
    /// as a plain helper: the designed control is supplied through <see cref="Component"/> instead
    /// of the base class property, and the <c>MyBase.GetDesignTimeHtml</c> calls — which simply let
    /// the base designer render the control — became direct calls to
    /// <see cref="BaseWebControl.RenderControl(TextWriter)"/>. The two synthetic sample tabs, the
    /// 100x50 pixel content place holder (a WebForms <c>Panel</c> originally, now the same
    /// <c>div</c> produced by a plain <see cref="BaseWebControl"/>) and the save/restore of
    /// <see cref="DNNTabStrip.ForceDownLevel"/> are preserved.
    /// </remarks>
    public class DNNTabStripDesigner
    {
        /// <summary>Gets or sets the tab strip being designed.</summary>
        public DNNTabStrip Component { get; set; }

        /// <summary>Gets the markup shown for the control on the designer surface.</summary>
        /// <returns>The preview markup.</returns>
        public virtual string GetDesignTimeHtml()
        {
            var component = this.Component;
            if (component is null)
            {
                return null;
            }

            var tabs = component.Tabs;
            var forceDownLevel = component.ForceDownLevel;
            component.ForceDownLevel = true;

            string designTimeHtml;
            if (tabs.Count != 0)
            {
                designTimeHtml = Render(component);
            }
            else
            {
                tabs.Add(new DNNTab("Tab 1"));
                tabs.Add(new DNNTab("Tab 2"));

                var child = new BaseWebControl("div");
                child.Style["width"] = "100px";
                child.Style["height"] = "50px";
                tabs[0].Children.Add(child);

                component.SelectedIndex = 0;
                designTimeHtml = Render(component);
                tabs.Clear();
            }

            component.ForceDownLevel = forceDownLevel;
            return designTimeHtml;
        }

        /// <summary>Renders the control the way the removed base designer did.</summary>
        /// <param name="component">The control to render.</param>
        /// <returns>The rendered markup.</returns>
        private static string Render(DNNTabStrip component)
        {
            var writer = new StringWriter(CultureInfo.InvariantCulture);
            component.RenderControl(writer);
            return writer.ToString();
        }
    }
}
