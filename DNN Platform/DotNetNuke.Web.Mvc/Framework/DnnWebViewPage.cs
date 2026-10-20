// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework
{
    using DotNetNuke.Common;
    using DotNetNuke.Web.Mvc.Helpers;

    using Microsoft.AspNetCore.Mvc.Razor;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Base class for DNN MVC module Razor views.</summary>
    public abstract class DnnWebViewPage : RazorPage<dynamic>
    {
        private DnnHelper<object> dnn;
        private DnnHtmlHelper<object> html;
        private DnnUrlHelper url;

        /// <summary>Gets the DNN helper.</summary>
        public DnnHelper<object> Dnn => this.dnn ??= this.CreateDnnHelper();

        /// <summary>Gets the DNN HTML helper.</summary>
        public DnnHtmlHelper<object> Html => this.html ??= this.CreateHtmlHelper();

        /// <summary>Gets the DNN URL helper.</summary>
        public DnnUrlHelper Url => this.url ??= new DnnUrlHelper(this.ViewContext);

        private IHtmlHelper<object> ContextualizedHtmlHelper()
        {
            var htmlHelper = this.Context.RequestServices.GetRequiredService<IHtmlHelper<object>>();
            if (htmlHelper is IViewContextAware contextAware)
            {
                contextAware.Contextualize(this.ViewContext);
            }

            return htmlHelper;
        }

        private DnnHtmlHelper<object> CreateHtmlHelper()
        {
            return new DnnHtmlHelper<object>(this.ContextualizedHtmlHelper(), null);
        }

        private DnnHelper<object> CreateDnnHelper()
        {
            return ActivatorUtilities.CreateInstance<DnnHelper<object>>(
                Globals.GetCurrentServiceProvider(),
                this.ContextualizedHtmlHelper());
        }
    }
}
