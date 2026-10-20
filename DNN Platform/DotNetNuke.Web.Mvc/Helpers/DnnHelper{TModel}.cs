// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// ReSharper disable ConvertPropertyToExpressionBody
namespace DotNetNuke.Web.Mvc.Helpers
{
    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Abstractions.Logging;

    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;

    /// <summary>A strongly typed DNN helper.</summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    public class DnnHelper<TModel> : DnnHelper
    {
        /// <summary>Initializes a new instance of the <see cref="DnnHelper{TModel}"/> class.</summary>
        /// <param name="clientResourceController">The client resource controller.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="htmlHelper">The strongly typed HTML helper to wrap.</param>
        public DnnHelper(IClientResourceController clientResourceController, IApplicationStatusInfo appStatus, IEventLogger eventLogger, IHtmlHelper<TModel> htmlHelper)
            : base(clientResourceController, appStatus, eventLogger, htmlHelper)
        {
        }

        /// <summary>Gets the strongly typed view data.</summary>
        public new ViewDataDictionary<TModel> ViewData => ((IHtmlHelper<TModel>)this.HtmlHelper).ViewData;
    }
}
