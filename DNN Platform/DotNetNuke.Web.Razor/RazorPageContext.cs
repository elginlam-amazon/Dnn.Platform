// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Razor
{
    using DotNetNuke.Internal.SourceGenerators;

    using Microsoft.AspNetCore.Http;

    /// <summary>The rendering context of a <see cref="DotNetNukeWebPage"/>.</summary>
    /// <remarks>
    /// Replaces <c>System.Web.WebPages.WebPageContext</c>, which has no successor on ASP.NET Core.
    /// It carries the same three pieces of state the <c>WebPageContext(httpContext, page, model)</c>
    /// constructor carried, with <c>System.Web.HttpContextBase</c> replaced by the ASP.NET Core
    /// <see cref="Microsoft.AspNetCore.Http.HttpContext"/>.
    /// </remarks>
    [DnnDeprecated(9, 3, 2, "Use Razor Pages instead")]
    public partial class RazorPageContext
    {
        /// <summary>Initializes a new instance of the <see cref="RazorPageContext"/> class.</summary>
        /// <param name="httpContext">The context of the request the page renders for.</param>
        /// <param name="page">The page being rendered.</param>
        /// <param name="model">The model handed to the page, or <see langword="null"/>.</param>
        public RazorPageContext(HttpContext httpContext, DotNetNukeWebPage page, object model)
        {
            this.HttpContext = httpContext;
            this.Page = page;
            this.Model = model;
        }

        /// <summary>Gets the context of the request the page renders for.</summary>
        public HttpContext HttpContext { get; }

        /// <summary>Gets the page being rendered.</summary>
        public DotNetNukeWebPage Page { get; }

        /// <summary>Gets the model handed to the page.</summary>
        public object Model { get; }
    }
}
