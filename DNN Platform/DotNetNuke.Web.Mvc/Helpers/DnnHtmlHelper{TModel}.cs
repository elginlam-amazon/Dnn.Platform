// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Helpers
{
    using System;

    using DotNetNuke.Framework;

    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
    using Microsoft.AspNetCore.Routing;

    public class DnnHtmlHelper<TModel> : DnnHtmlHelper
    {
        /// <summary>Initializes a new instance of the <see cref="DnnHtmlHelper{TModel}"/> class.</summary>
        /// <param name="viewContext">The view context (carries the view data in ASP.NET Core).</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IServicesFramework. Scheduled removal in v12.0.0.")]
        public DnnHtmlHelper(ViewContext viewContext)
            : this(viewContext, (IServicesFramework)null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnHtmlHelper{TModel}"/> class.</summary>
        /// <param name="viewContext">The view context (carries the view data in ASP.NET Core).</param>
        /// <param name="servicesFramework">The web API service framework.</param>
        public DnnHtmlHelper(ViewContext viewContext, IServicesFramework servicesFramework)
            : base(HtmlHelperFactory.Create<TModel>(viewContext), null, servicesFramework)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnHtmlHelper{TModel}"/> class.</summary>
        /// <param name="viewContext">The view context (carries the view data in ASP.NET Core).</param>
        /// <param name="routeCollection">The route collection.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IServicesFramework. Scheduled removal in v12.0.0.")]
        public DnnHtmlHelper(ViewContext viewContext, RouteCollection routeCollection)
            : this(viewContext, routeCollection, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnHtmlHelper{TModel}"/> class.</summary>
        /// <param name="viewContext">The view context (carries the view data in ASP.NET Core).</param>
        /// <param name="routeCollection">The route collection.</param>
        /// <param name="servicesFramework">The web API service framework.</param>
        public DnnHtmlHelper(ViewContext viewContext, RouteCollection routeCollection, IServicesFramework servicesFramework)
            : base(HtmlHelperFactory.Create<TModel>(viewContext), routeCollection, servicesFramework)
        {
        }

        /// <summary>Gets the view bag.</summary>
        public new object ViewBag => this.HtmlHelper.ViewBag;

        /// <summary>Gets the strongly typed view data.</summary>
        public new ViewDataDictionary<TModel> ViewData => this.HtmlHelper.ViewData;

        /// <summary>Gets the strongly typed HTML helper.</summary>
        internal new IHtmlHelper<TModel> HtmlHelper => (IHtmlHelper<TModel>)base.HtmlHelper;
    }
}
