// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Common.Utilities
{
    using SixLabors.ImageSharp;

    /// <summary>
    /// Describes whether something is laid out horizontally or vertically. Replaces
    /// <c>System.Web.UI.WebControls.Orientation</c>, which only exists in WebForms.
    /// </summary>
    /// <remarks>
    /// The member names are identical to the WebForms enumeration, so call sites that only used
    /// <c>Orientation.Horizontal</c>/<c>Orientation.Vertical</c> keep compiling once they import
    /// <c>DotNetNuke.Common.Utilities</c> instead of <c>System.Web.UI.WebControls</c>.
    /// </remarks>
    public enum Orientation
    {
        /// <summary>The item is laid out horizontally.</summary>
        Horizontal = 0,

        /// <summary>The item is laid out vertically.</summary>
        Vertical = 1,
    }

    /// <summary>Extension methods for <see cref="Size"/>.</summary>
    public static class SizeExtensions
    {
        /// <summary>Determines whether a size is landscape (horizontal) or portrait (vertical).</summary>
        /// <param name="size">The size, now an ImageSharp <see cref="Size"/> rather than a <c>System.Drawing.Size</c>.</param>
        /// <returns><see cref="Orientation.Horizontal"/> when the size is wider than it is tall, otherwise <see cref="Orientation.Vertical"/>.</returns>
        public static Orientation Orientation(this Size size)
        {
            return size.Width > size.Height
                       ? Utilities.Orientation.Horizontal
                       : Utilities.Orientation.Vertical;
        }
    }
}
