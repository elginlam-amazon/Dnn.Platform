// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using DotNetNuke.UI;

    /// <summary>An empty control that hosts the content of a template.</summary>
    /// <remarks>
    /// This was a <c>WebControl, INamingContainer</c> rendering a <c>div</c>; it is the container an
    /// <c>ITemplate</c> was instantiated into. On ASP.NET Core a template is a
    /// <see cref="Microsoft.AspNetCore.Components.RenderFragment"/>, which this component renders
    /// through the inherited <c>ChildContent</c> parameter, inside the same <c>div</c>.
    /// </remarks>
    public class DnnFormEmptyTemplate : DnnComponentBase
    {
        /// <inheritdoc />
        protected override string TagName => "div";
    }
}
