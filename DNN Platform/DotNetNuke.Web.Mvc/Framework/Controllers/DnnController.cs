// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework.Controllers
{
    using System.Web.UI;

    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Modules.Actions;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI.Modules;
    using DotNetNuke.Web.Mvc.Framework.ActionResults;
    using DotNetNuke.Web.Mvc.Framework.Modules;
    using DotNetNuke.Web.Mvc.Helpers;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.AspNetCore.Routing;

    /// <summary>Base class for DNN MVC module controllers.</summary>
    public abstract class DnnController : Controller, IDnnController
    {
        /// <summary>Gets the active module for the current request.</summary>
        public ModuleInfo ActiveModule => this.ModuleContext?.Configuration;

        /// <summary>Gets the active page (tab) for the current request.</summary>
        public TabInfo ActivePage => this.PortalSettings?.ActiveTab;

        /// <summary>Gets the current portal settings.</summary>
        public PortalSettings PortalSettings => this.ModuleContext?.PortalSettings;

        /// <inheritdoc />
        public IActionResult ResultOfLastExecute { get; private set; }

        /// <summary>Gets the current user.</summary>
        public new UserInfo User => this.PortalSettings?.UserInfo;

        /// <inheritdoc />
        public Page DnnPage { get; set; }

        /// <inheritdoc />
        public new DnnUrlHelper Url { get; set; }

        /// <inheritdoc />
        public string LocalResourceFile { get; set; }

        /// <inheritdoc />
        public ModuleActionCollection ModuleActions { get; set; }

        /// <inheritdoc />
        public ModuleInstanceContext ModuleContext { get; set; }

        /// <inheritdoc />
        public bool ValidateRequest { get; set; }

        /// <inheritdoc />
        public string LocalizeString(string key)
        {
            return Localization.GetString(key, this.LocalResourceFile);
        }

        /// <inheritdoc />
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);

            if (context.HttpContext.Items.TryGetValue(typeof(DnnModuleState), out var stateObj) && stateObj is DnnModuleState state)
            {
                this.DnnPage = state.DnnPage;
                this.ModuleContext = state.ModuleContext;
                this.LocalResourceFile = state.LocalResourceFile;
                this.ModuleActions ??= state.ModuleActions;
            }

            this.Url ??= new DnnUrlHelper(this);
            context.HttpContext.Items[typeof(IDnnController)] = this;
        }

        /// <inheritdoc />
        public override void OnActionExecuted(ActionExecutedContext context)
        {
            base.OnActionExecuted(context);
            if (context.Result != null)
            {
                this.ResultOfLastExecute = context.Result;
            }

            if (context.HttpContext.Items.TryGetValue(typeof(DnnModuleState), out var stateObj) && stateObj is DnnModuleState state)
            {
                state.ModuleActions = this.ModuleActions;
                state.ResultOfLastExecute = this.ResultOfLastExecute;
            }
        }

        /// <inheritdoc />
        public override RedirectToActionResult RedirectToAction(string actionName, string controllerName, object routeValues)
        {
            var values = routeValues as RouteValueDictionary ?? new RouteValueDictionary(routeValues);
            this.ResultOfLastExecute = new DnnRedirecttoRouteResult(actionName, controllerName, string.Empty, values, false, this.Url);
            return base.RedirectToAction(actionName, controllerName, routeValues);
        }

        /// <inheritdoc />
        public override ViewResult View()
        {
            return this.CaptureView(base.View());
        }

        /// <inheritdoc />
        public override ViewResult View(object model)
        {
            return this.CaptureView(base.View(model));
        }

        /// <inheritdoc />
        public override ViewResult View(string viewName, object model)
        {
            return this.CaptureView(base.View(viewName, model));
        }

        /// <inheritdoc />
        public override PartialViewResult PartialView(string viewName, object model)
        {
            var result = new DnnPartialViewResult
            {
                ViewName = viewName,
                ViewData = this.ViewData,
                TempData = this.TempData,
            };
            if (model != null)
            {
                this.ViewData.Model = model;
            }

            this.ResultOfLastExecute = result;
            return result;
        }

        /// <summary>Redirects to the module's default route.</summary>
        /// <returns>A redirect result.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Breaking change")]
        protected internal RedirectToRouteResult RedirectToDefaultRoute()
        {
            return new RedirectToRouteResult(string.Empty, new RouteValueDictionary());
        }

        private ViewResult CaptureView(ViewResult source)
        {
            var result = new DnnViewResult
            {
                ViewName = source.ViewName,
                ViewData = source.ViewData,
                TempData = source.TempData,
                ViewEngine = source.ViewEngine,
                ContentType = source.ContentType,
                StatusCode = source.StatusCode,
            };

            this.ResultOfLastExecute = result;
            return result;
        }
    }
}
