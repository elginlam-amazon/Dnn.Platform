// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>The font of a grid row or cell.</summary>
    /// <remarks>
    /// Replaces <c>System.Web.UI.WebControls.FontInfo</c>. <see cref="Size"/> is a CSS length
    /// instead of a <c>FontUnit</c>, which lives in the WebForms assembly.
    /// </remarks>
    public class GridFontStyle
    {
        private readonly List<string> names = new List<string>();

        /// <summary>Gets or sets the font families, in order of preference.</summary>
        [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly", Justification = "The WebForms FontInfo.Names this replaces was assignable, and DNN assigns it.")]
        public IList<string> Names
        {
            get => this.names;

            set
            {
                this.names.Clear();
                if (value != null)
                {
                    this.names.AddRange(value);
                }
            }
        }

        /// <summary>Gets or sets the font size as a CSS length, for example <c>10pt</c>.</summary>
        public string Size { get; set; } = string.Empty;

        /// <summary>Gets or sets a value indicating whether the font is bold.</summary>
        public bool Bold { get; set; }

        /// <summary>Gets or sets a value indicating whether the font is italic.</summary>
        public bool Italic { get; set; }
    }
}
