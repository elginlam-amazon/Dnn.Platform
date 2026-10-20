// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Mvc
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Web.UI;

    using DotNetNuke.Abstractions.Modules;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.ComponentModel;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Modules.Actions;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.UI.Modules;
    using DotNetNuke.Web.Mvc.Common;
    using DotNetNuke.Web.Mvc.Framework.Modules;
    using DotNetNuke.Web.Mvc.Routing;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>WebForms control for hosting an MVC module control.</summary>
    public class MvcHostControl : ModuleControlBase, IActionable
    {
        /// <summary>Initializes a new instance of the <see cref="MvcHostControl"/> class.</summary>
        public MvcHostControl()
        {
            this.ControlKey = string.Empty;
        }

        /// <summary>Initializes a new instance of the <see cref="MvcHostControl"/> class.</summary>
        /// <param name="controlKey">The module control key.</param>
        public MvcHostControl(string controlKey)
        {
            this.ControlKey = controlKey;
        }

        /// <inheritdoc />
        public ModuleActionCollection ModuleActions { get; protected set; }

        /// <summary>Gets or sets the module request result.</summary>
        protected ModuleRequestResult Result { get; set; }

        /// <summary>Gets or sets the module control key.</summary>
        protected string ControlKey { get; set; }

        /// <summary>Gets or sets a value indicating whether the module controller should execute immediately.</summary>
        protected bool ExecuteModuleImmediately { get; set; } = true;

        /// <summary>Gets the module execution engine.</summary>
        /// <returns>The module execution engine.</returns>
        protected static IModuleExecutionEngine GetModuleExecutionEngine()
        {
            var moduleExecutionEngine = ComponentFactory.GetComponent<IModuleExecutionEngine>();

            if (moduleExecutionEngine == null)
            {
                moduleExecutionEngine = new ModuleExecutionEngine();
                ComponentFactory.RegisterComponentInstance<IModuleExecutionEngine>(moduleExecutionEngine);
            }

            return moduleExecutionEngine;
        }

        /// <summary>Renders the module result to an HTML string.</summary>
        /// <param name="moduleResult">The module result.</param>
        /// <returns>The rendered HTML.</returns>
        protected static string RenderModule(ModuleRequestResult moduleResult)
        {
            using var writer = new StringWriter(CultureInfo.CurrentCulture);
            var moduleExecutionEngine = GetModuleExecutionEngine();
            moduleExecutionEngine.ExecuteModuleResult(moduleResult, writer);
            return writer.ToString();
        }

        /// <summary>Gets the current ASP.NET Core HTTP context.</summary>
        /// <returns>The current HTTP context.</returns>
        protected static HttpContext GetHttpContext()
        {
            return System.Web.HttpContext.Current;
        }

        /// <summary>Runs and renders the MVC action.</summary>
        protected void ExecuteModule()
        {
            try
            {
                var httpContext = GetHttpContext();

                var moduleExecutionEngine = GetModuleExecutionEngine();

                this.Result = moduleExecutionEngine.ExecuteModule(this.GetModuleRequestContext(httpContext));

                this.ModuleActions = this.LoadActions(this.Result);

                httpContext.SetModuleRequestResult(this.Result);
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            this.OnInitInternal(e);
        }

        /// <summary>Internal OnInit handler.</summary>
        /// <param name="e">The event args.</param>
        protected virtual void OnInitInternal(EventArgs e)
        {
            if (this.ExecuteModuleImmediately)
            {
                this.ExecuteModule();
            }
        }

        /// <inheritdoc />
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
            this.OnPreRenderInternal(e);
        }

        /// <summary>Internal OnPreRender handler.</summary>
        /// <param name="e">The event args.</param>
        protected virtual void OnPreRenderInternal(EventArgs e)
        {
            try
            {
                if (this.Result == null)
                {
                    return;
                }

                var mvcString = RenderModule(this.Result);
                if (!string.IsNullOrEmpty(mvcString))
                {
                    this.Controls.Add(new LiteralControl(mvcString));
                }
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <summary>Builds the module request context.</summary>
        /// <param name="httpContext">The HTTP context.</param>
        /// <returns>The module request context.</returns>
        protected ModuleRequestContext GetModuleRequestContext(HttpContext httpContext)
        {
            var module = this.ModuleContext.Configuration;

            var desktopModule = DesktopModuleControllerAdapter.Instance.GetDesktopModule(module.DesktopModuleID, module.PortalID);
            var defaultControl = ModuleControlControllerAdapter.Instance.GetModuleControlByControlKey(string.Empty, module.ModuleDefID);

            var defaultRouteData = ModuleRoutingProvider.Instance().GetRouteData(null, defaultControl);

            var moduleApplication = GetModuleApplication(
                httpContext.RequestServices.GetRequiredService<IBusinessControllerProvider>(),
                desktopModule,
                defaultRouteData);

            RouteData routeData;

            var queryString = httpContext.Request.Query;

            if (string.IsNullOrEmpty(this.ControlKey))
            {
                this.ControlKey = queryString.TryGetValue("ctl", out var ctl) ? ctl.ToString() : string.Empty;
            }

            var moduleId = Null.NullInteger;
            if (queryString.TryGetValue("moduleid", out var moduleIdValue))
            {
                if (!int.TryParse(moduleIdValue, out moduleId))
                {
                    moduleId = Null.NullInteger;
                }
            }

            if (moduleId != this.ModuleContext.ModuleId && string.IsNullOrEmpty(this.ControlKey))
            {
                routeData = defaultRouteData;
            }
            else
            {
                var control = ModuleControlControllerAdapter.Instance.GetModuleControlByControlKey(this.ControlKey, module.ModuleDefID);
                routeData = ModuleRoutingProvider.Instance().GetRouteData(httpContext, control);
            }

            return new ModuleRequestContext
            {
                DnnPage = this.Page,
                HttpContext = httpContext,
                ModuleContext = this.ModuleContext,
                ModuleApplication = moduleApplication,
                RouteData = routeData,
            };
        }

        /// <summary>Loads the module actions produced by the module result.</summary>
        /// <param name="requestResult">The module request result.</param>
        /// <returns>The module action collection.</returns>
        protected ModuleActionCollection LoadActions(ModuleRequestResult requestResult)
        {
            var actions = new ModuleActionCollection();

            if (requestResult?.ModuleActions != null)
            {
                foreach (ModuleAction action in requestResult.ModuleActions)
                {
                    action.ID = this.ModuleContext.GetNextActionID();
                    actions.Add(action);
                }
            }

            return actions;
        }

        private static ModuleApplication GetModuleApplication(
            IBusinessControllerProvider businessControllerProvider,
            DesktopModuleInfo desktopModule,
            RouteData defaultRouteData)
        {
            var moduleApplication = businessControllerProvider.GetInstance<ModuleApplication>(desktopModule);
            if (moduleApplication != null)
            {
                defaultRouteData.Values["controller"] = moduleApplication.DefaultControllerName;
                defaultRouteData.Values["action"] = moduleApplication.DefaultActionName;
                defaultRouteData.DataTokens["namespaces"] = moduleApplication.DefaultNamespaces;
                return moduleApplication;
            }

            var defaultControllerName = (string)defaultRouteData.Values["controller"];
            var defaultActionName = (string)defaultRouteData.Values["action"];
            var defaultNamespaces = defaultRouteData.DataTokens.TryGetValue("namespaces", out var ns) ? (string[])ns : null;

            return new ModuleApplication
            {
                DefaultActionName = defaultActionName,
                DefaultControllerName = defaultControllerName,
                DefaultNamespaces = defaultNamespaces,
                ModuleName = desktopModule.ModuleName,
                FolderPath = desktopModule.FolderName,
            };
        }
    }
}
