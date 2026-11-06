// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// ReSharper disable ConvertPropertyToExpressionBody
namespace DotNetNuke.Web.Mvc.Helpers
{
    using System;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Abstractions.Logging;

    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;

    public class DnnHelper<TModel> : DnnHelper
    {
        /// <summary>Initializes a new instance of the <see cref="DnnHelper{TModel}"/> class.</summary>
        /// <param name="clientResourceController">The client resource controller.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="viewContext">The view context (carries the view data in ASP.NET Core).</param>
        [ActivatorUtilitiesConstructor]
        public DnnHelper(IClientResourceController clientResourceController, IApplicationStatusInfo appStatus, IEventLogger eventLogger, ViewContext viewContext)
            : base(clientResourceController, appStatus, eventLogger, HtmlHelperFactory.Create<TModel>(viewContext), null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnHelper{TModel}"/> class.</summary>
        /// <param name="viewContext">The view context (carries the view data in ASP.NET Core).</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IClientResourceController. Scheduled removal in v12.0.0.")]
        public DnnHelper(ViewContext viewContext)
            : base(null, null, null, HtmlHelperFactory.Create<TModel>(viewContext), null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnHelper{TModel}"/> class.</summary>
        /// <param name="viewContext">The view context (carries the view data in ASP.NET Core).</param>
        /// <param name="routeCollection">The route collection.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IClientResourceController. Scheduled removal in v12.0.0.")]
        public DnnHelper(ViewContext viewContext, RouteCollection routeCollection)
            : base(null, null, null, HtmlHelperFactory.Create<TModel>(viewContext), routeCollection)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnHelper{TModel}"/> class.</summary>
        /// <param name="clientResourceController">The client resource controller.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="viewContext">The view context (carries the view data in ASP.NET Core).</param>
        /// <param name="routeCollection">The route collection.</param>
        public DnnHelper(IClientResourceController clientResourceController, IApplicationStatusInfo appStatus, IEventLogger eventLogger, ViewContext viewContext, RouteCollection routeCollection)
            : base(clientResourceController, appStatus, eventLogger, HtmlHelperFactory.Create<TModel>(viewContext), routeCollection)
        {
        }

        /// <summary>Gets the strongly typed view data.</summary>
        public new ViewDataDictionary<TModel> ViewData => this.HtmlHelper.ViewData;

        /// <summary>Gets the strongly typed HTML helper.</summary>
        internal new IHtmlHelper<TModel> HtmlHelper => (IHtmlHelper<TModel>)base.HtmlHelper;
    }
}
