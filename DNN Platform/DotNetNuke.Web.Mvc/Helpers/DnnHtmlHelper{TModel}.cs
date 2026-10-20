// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Helpers
{
    using DotNetNuke.Framework;

    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;

    /// <summary>A strongly typed DNN wrapper over the ASP.NET Core <see cref="IHtmlHelper{TModel}"/>.</summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    public class DnnHtmlHelper<TModel> : DnnHtmlHelper
    {
        /// <summary>Initializes a new instance of the <see cref="DnnHtmlHelper{TModel}"/> class.</summary>
        /// <param name="htmlHelper">The strongly typed HTML helper to wrap.</param>
        /// <param name="servicesFramework">The web API service framework.</param>
        public DnnHtmlHelper(IHtmlHelper<TModel> htmlHelper, IServicesFramework servicesFramework)
            : base(htmlHelper, servicesFramework)
        {
        }

        /// <summary>Gets the strongly typed view data.</summary>
        public new ViewDataDictionary<TModel> ViewData => this.HtmlHelper.ViewData;

        /// <summary>Gets the wrapped strongly typed HTML helper.</summary>
        internal new IHtmlHelper<TModel> HtmlHelper => (IHtmlHelper<TModel>)base.HtmlHelper;
    }
}
