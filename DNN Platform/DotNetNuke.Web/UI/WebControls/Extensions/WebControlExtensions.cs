// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.UI.WebControls.Extensions
{
    using System.Linq;

    using DotNetNuke.UI;
    using SixLabors.ImageSharp;

    /// <summary>The layout direction of a repeated control or of an image.</summary>
    /// <remarks>Replaces <c>System.Web.UI.WebControls.Orientation</c>.</remarks>
    public enum DnnOrientation
    {
        /// <summary>Laid out horizontally (wider than tall).</summary>
        Horizontal = 0,

        /// <summary>Laid out vertically (taller than wide).</summary>
        Vertical = 1,
    }

    /// <summary>Extension methods for <see cref="IDnnControl"/> instances.</summary>
    public static class WebControlExtensions
    {
        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        /// <summary>Adds the <paramref name="cssClass"/> to the <paramref name="control"/>.</summary>
        /// <param name="control">The control.</param>
        /// <param name="cssClass">The CSS class to add.</param>
        public static void AddCssClass(this DnnComponentBase control, string cssClass)
        {
            if (control == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(control.CssClass))
            {
                control.CssClass = cssClass;
            }
            else
            {
                var cssClasses = control.CssClass.Split(' ');
                var classExists = cssClasses.Any(@class => @class == cssClass);

                if (!classExists)
                {
                    control.CssClass += " " + cssClass;
                }
            }
        }
        #pragma warning restore BL0005

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        /// <summary>Removes the <paramref name="cssClass"/> from the <paramref name="control"/>.</summary>
        /// <param name="control">The control.</param>
        /// <param name="cssClass">The CSS class.</param>
        public static void RemoveCssClass(this DnnComponentBase control, string cssClass)
        {
            if (control == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(control.CssClass))
            {
                var cssClasses = control.CssClass.Split(' ');
                control.CssClass = string.Join(" ", cssClasses.Where(@class => @class != cssClass).ToArray());
            }
        }
        #pragma warning restore BL0005

        /// <summary>Gets the orientation of the <paramref name="size"/>.</summary>
        /// <param name="size">The size.</param>
        /// <returns>The orientation.</returns>
        /// <remarks>
        /// Takes a <see cref="Size"/> from ImageSharp rather than <c>System.Drawing.Size</c>: System.Drawing
        /// is not supported cross-platform on modern .NET, and <c>InternalServices/ImageHeader</c> — the only
        /// producer of the sizes this is called with — now returns the ImageSharp type. The returned values
        /// are unchanged, so the wire format of <c>FileUploadDto.Orientation</c> is unaffected.
        /// </remarks>
        public static DnnOrientation Orientation(this Size size)
        {
            return size.Width > size.Height ? DnnOrientation.Horizontal : DnnOrientation.Vertical;
        }
    }
}
