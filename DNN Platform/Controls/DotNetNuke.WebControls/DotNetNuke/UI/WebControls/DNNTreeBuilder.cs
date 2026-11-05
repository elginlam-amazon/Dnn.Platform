// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections;

    /// <summary>
    /// Resolves the CLR type of a declarative child tag of a tree. In WebForms this derived from
    /// <c>System.Web.UI.ControlBuilder</c>, which the ASPX parser invoked while parsing nested tags.
    /// System.Web.UI has no .NET 10 equivalent, so the class is retained as a plain resolver with
    /// the original <see cref="GetChildControlType"/> behavior preserved verbatim; Blazor resolves
    /// child components from the render tree instead of through a builder.
    /// </summary>
    public class DNNTreeBuilder
    {
        /// <summary>Gets the <c>GetChildControlType</c> operation.</summary>
        /// <param name="tagName">The tag Name.</param>
        /// <param name="attribs">The attribs.</param>
        /// <returns>The result of the operation.</returns>
        public virtual Type GetChildControlType(string tagName, IDictionary attribs)
        {
            return !tagName.EndsWith("TreeNode", StringComparison.OrdinalIgnoreCase) ? null : typeof(TreeNode);
        }
    }
}
