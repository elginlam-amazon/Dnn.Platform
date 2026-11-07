// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework.Controllers
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;

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

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.AspNetCore.Mvc.ViewEngines;
    using Microsoft.AspNetCore.Routing;

    /// <summary>The base class of DNN MVC module controllers.</summary>
    /// <remarks>Was a <c>System.Web.Mvc.Controller</c>, now a <see cref="Controller"/> of ASP.NET Core MVC.</remarks>
    public abstract class DnnController : Controller, IDnnController
    {
        /// <summary>Initializes a new instance of the <see cref="DnnController"/> class.</summary>
        protected DnnController()
        {
        }

        /// <summary>Gets the module of this request.</summary>
        public ModuleInfo ActiveModule
        {
            get { return (this.ModuleContext == null) ? null : this.ModuleContext.Configuration; }
        }

        /// <summary>Gets the page (tab) of this request.</summary>
        public TabInfo ActivePage
        {
            get { return (this.PortalSettings == null) ? null : this.PortalSettings.ActiveTab; }
        }

        /// <summary>Gets the portal settings of this request.</summary>
        public PortalSettings PortalSettings
        {
            get { return (this.ModuleContext == null) ? null : this.ModuleContext.PortalSettings; }
        }

        /// <inheritdoc />
        public IActionResult ResultOfLastExecute
            => ResultCapturingActionInvoker.GetCurrent(this.HttpContext)?.ResultOfLastInvoke;

        /// <summary>Gets the DNN user of this request.</summary>
        public new UserInfo User
        {
            get { return (this.PortalSettings == null) ? null : this.PortalSettings.UserInfo; }
        }

        /// <inheritdoc />
        public IComponent DnnPage { get; set; }

        /// <inheritdoc />
        public new DnnUrlHelper Url { get; set; }

        /// <inheritdoc />
        public string LocalResourceFile { get; set; }

        /// <inheritdoc />
        public ModuleActionCollection ModuleActions { get; set; }

        /// <inheritdoc />
        public ModuleInstanceContext ModuleContext { get; set; }

        /// <summary>Gets or sets a value indicating whether the request should be validated.</summary>
        /// <remarks>
        /// MVC 5 exposed this through <c>Controller.ValidateRequest</c> (the ASP.NET request validation).
        /// ASP.NET Core has no request validation to switch off, so the flag is kept on the DNN controller:
        /// the module host sets it to <see langword="false"/> and module code can still read it.
        /// </remarks>
        public bool ValidateRequest { get; set; } = true;

        /// <inheritdoc />
        public IReadOnlyList<IViewEngine> ViewEngines { get; set; }

        /// <inheritdoc />
        public string LocalizeString(string key)
        {
            return Localization.GetString(key, this.LocalResourceFile);
        }

        /// <inheritdoc />
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);

            // Replaces the legacy Controller.Initialize(RequestContext) override: ASP.NET Core has no
            // initialize hook, the controller context is only available once the action is executing.
            this.Initialize((ActionContext)context ?? this.ControllerContext);
        }

        /// <inheritdoc />
        public override PartialViewResult PartialView(string viewName, object model)
        {
            if (model != null)
            {
                this.ViewData.Model = model;
            }

            return new DnnPartialViewResult
            {
                ViewName = viewName,
                ViewData = this.ViewData,
                TempData = this.TempData,
                ViewEngines = this.ViewEngines,
            };
        }

        /// <summary>Redirects to the default DNN route.</summary>
        /// <returns>A redirect result.</returns>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        protected internal RedirectToRouteResult RedirectToDefaultRoute()
        {
            return new DnnRedirecttoRouteResult(string.Empty, string.Empty, string.Empty, null, false);
        }

        /// <summary>Redirects to the given action of the given controller.</summary>
        /// <param name="actionName">The action name.</param>
        /// <param name="controllerName">The controller name.</param>
        /// <param name="routeValues">The route values.</param>
        /// <returns>A redirect result.</returns>
        protected internal RedirectToRouteResult RedirectToAction(string actionName, string controllerName, RouteValueDictionary routeValues)
        {
            return new DnnRedirecttoRouteResult(actionName, controllerName, string.Empty, routeValues, false, this.Url);
        }

        /// <summary>Renders the given view.</summary>
        /// <param name="view">The view to render.</param>
        /// <param name="model">The model, may be <see langword="null"/>.</param>
        /// <returns>A view result.</returns>
        protected internal ViewResult View(IView view, object model)
        {
            if (model != null)
            {
                this.ViewData.Model = model;
            }

            return new DnnViewResult
            {
                View = view,
                ViewData = this.ViewData,
                TempData = this.TempData,
                ViewEngines = this.ViewEngines,
            };
        }

        /// <summary>Renders the given view with the given master page (layout).</summary>
        /// <param name="viewName">The view name.</param>
        /// <param name="masterName">The master page (layout) name.</param>
        /// <param name="model">The model, may be <see langword="null"/>.</param>
        /// <returns>A view result.</returns>
        protected internal ViewResult View(string viewName, string masterName, object model)
        {
            if (model != null)
            {
                this.ViewData.Model = model;
            }

            return new DnnViewResult
            {
                ViewName = viewName,
                MasterName = masterName,
                ViewData = this.ViewData,
                TempData = this.TempData,
                ViewEngines = this.ViewEngines,
            };
        }

        /// <summary>Initializes the DNN specific members of the controller for the executing action.</summary>
        /// <param name="actionContext">The context of the executing action.</param>
        protected virtual void Initialize(ActionContext actionContext)
        {
            this.Url = new DnnUrlHelper(actionContext, this);
        }
    }
}
