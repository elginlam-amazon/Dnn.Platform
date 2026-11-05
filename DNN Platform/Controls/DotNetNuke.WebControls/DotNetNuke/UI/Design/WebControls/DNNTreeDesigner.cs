// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.Design.WebControls
{
    /// <summary>
    /// Produces the design-time preview markup of a <c>DNNTree</c>.
    /// </summary>
    /// <remarks>
    /// The original derived from <c>System.Web.UI.Design.ControlDesigner</c>, which has no .NET 10
    /// equivalent (the whole <c>System.Design</c> surface was dropped). The class is therefore kept
    /// as a plain helper. The original returned a fixed place holder string (and fell back to
    /// <c>GetErrorDesignTimeHtml</c> if that ever threw, which it could not), so the emitted HTML is
    /// unchanged.
    /// </remarks>
    public class DNNTreeDesigner
    {
        /// <summary>Gets or sets the tree being designed.</summary>
        public DotNetNuke.UI.WebControls.DnnTree Component { get; set; }

        /// <summary>Gets the markup shown for the control on the designer surface.</summary>
        /// <returns>The preview markup.</returns>
        public virtual string GetDesignTimeHtml()
        {
            return "<Div>This is a PlaceHolder.<br>Additional design-time support will be <br>added in future versions</Div>";
        }
    }
}
