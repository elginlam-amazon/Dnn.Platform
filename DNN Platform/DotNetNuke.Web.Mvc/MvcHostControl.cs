// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Mvc
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.Modules;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Modules.Actions;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.UI.Modules;
    using DotNetNuke.Web.Mvc.Common;
    using DotNetNuke.Web.Mvc.Extensions;
    using DotNetNuke.Web.Mvc.Framework.Modules;
    using DotNetNuke.Web.Mvc.Routing;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    using RouteData = Microsoft.AspNetCore.Routing.RouteData;

    /// <summary>Blazor component that hosts an MVC module control.</summary>
    /// <remarks>
    /// Was a WebForms control: the module was executed in <c>OnInit</c> and its rendered output was
    /// appended to the control tree as a <c>LiteralControl</c> in <c>OnPreRender</c>. On ASP.NET Core
    /// the module is executed (asynchronously) while the component initializes and the rendered
    /// output is emitted as markup content by <see cref="RenderContents"/>.
    /// </remarks>
    public class MvcHostControl : ModuleControlBase, IActionable
    {
        private string renderedOutput;

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

        /// <summary>Gets or sets the accessor used to reach the current <see cref="HttpContext"/>.</summary>
        /// <remarks>
        /// Replaces the legacy <c>HttpContext.Current</c> static. In a Blazor Server circuit the
        /// accessor is only valid during the initial (pre-)rendering request, which is exactly when
        /// the module is executed; it is only used as a fallback for the request captured by
        /// <see cref="DotNetNuke.UI.DnnComponentBase.Request"/>.
        /// </remarks>
        [Inject]
        protected IHttpContextAccessor HttpContextAccessor { get; set; }

        /// <summary>Gets or sets the result of the module execution, or <see langword="null"/> when the module has not run (yet).</summary>
        protected ModuleRequestResult Result { get; set; }

        /// <summary>Gets or sets the module control key.</summary>
        protected string ControlKey { get; set; }

        /// <summary>Gets or sets a value indicating whether the module controller should execute immediately (i.e. while the component initializes rather than from <see cref="ISettingsControl.LoadSettings"/>).</summary>
        protected bool ExecuteModuleImmediately { get; set; } = true;

        /// <summary>Gets the <see cref="HttpContext"/> of the request this control was created for.</summary>
        /// <remarks>
        /// The request is captured by the component base while it initializes, because
        /// <c>HttpContext</c> is <see langword="null"/> once the Blazor circuit is interactive.
        /// </remarks>
        protected HttpContext CurrentHttpContext =>
            this.Request?.HttpContext ?? this.HttpContextAccessor?.HttpContext;

        /// <summary>Gets the module execution engine, resolving it from the container when needed.</summary>
        /// <param name="services">The services of the current request, or <see langword="null"/> to use the application's provider.</param>
        /// <returns>The <see cref="IModuleExecutionEngine"/> to use.</returns>
        protected static IModuleExecutionEngine GetModuleExecutionEngine(IServiceProvider services = null)
        {
            return ModuleExecutionExtensions.GetModuleExecutionEngine(services);
        }

        /// <summary>Renders the result of a module execution to markup.</summary>
        /// <param name="moduleResult">The result of the module execution.</param>
        /// <returns>The rendered markup of the module.</returns>
        protected static async Task<string> RenderModuleAsync(ModuleRequestResult moduleResult)
        {
            using var writer = new StringWriter(CultureInfo.CurrentCulture);
            var moduleExecutionEngine = GetModuleExecutionEngine(moduleResult?.ActionContext?.HttpContext?.RequestServices);

            await moduleExecutionEngine.ExecuteModuleResultAsync(moduleResult, writer);

            return writer.ToString();
        }

        /// <summary>Runs the MVC action and renders its output into this component.</summary>
        /// <param name="cancellationToken">A token to cancel the module execution.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        protected async Task ExecuteModuleAsync(CancellationToken cancellationToken)
        {
            try
            {
                var httpContext = this.CurrentHttpContext;
                if (httpContext == null)
                {
                    throw new InvalidOperationException(
                        $"The MVC module control '{this.ControlName}' can only execute while an HTTP request is in flight (the request is captured during the initial render).");
                }

                var moduleExecutionEngine = GetModuleExecutionEngine(httpContext.RequestServices);

                this.Result = await moduleExecutionEngine.ExecuteModuleAsync(this.GetModuleRequestContext(httpContext), cancellationToken);

                this.ModuleActions = this.LoadActions(this.Result);

                httpContext.SetModuleRequestResult(this.Result);

                await this.RenderResultAsync();
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <summary>Renders <see cref="Result"/> into the markup emitted by this component.</summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <remarks>Replaces appending a <c>LiteralControl</c> to the control tree in <c>OnPreRender</c>.</remarks>
        protected async Task RenderResultAsync()
        {
            if (this.Result == null)
            {
                return;
            }

            var mvcString = await RenderModuleAsync(this.Result);
            if (!string.IsNullOrEmpty(mvcString))
            {
                this.renderedOutput = mvcString;
                this.NotifyTreeChanged();
            }
        }

        /// <summary>Runs work to completion that is asynchronous internally, but was requested through a synchronous API.</summary>
        /// <param name="operation">The work to perform.</param>
        /// <remarks>
        /// <see cref="ISettingsControl.LoadSettings"/> and <see cref="ISettingsControl.UpdateSettings"/>
        /// are a synchronous contract: the caller persists the module settings and redirects as soon
        /// as the call returns, so the work MUST be finished before this method returns. Deferring it
        /// to a later render (e.g. from <c>OnAfterRenderAsync</c>) would mean the settings are never
        /// saved for callers that do not render the component again. The operation is started on the
        /// current thread — during the initial (pre-)render, which is the only point at which a module
        /// control executes, ASP.NET Core installs no synchronization context, so waiting for the
        /// continuation cannot deadlock. Failures are reported through
        /// <c>Exceptions.ProcessModuleLoadException</c> exactly as the
        /// asynchronous path does, rather than faulting the render.
        /// </remarks>
        protected void RunOperation(Func<CancellationToken, Task> operation)
        {
            ArgumentNullException.ThrowIfNull(operation);

            try
            {
                operation(CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <inheritdoc />
        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();

            if (this.ExecuteModuleImmediately)
            {
                await this.ExecuteModuleAsync(CancellationToken.None);
            }
        }

        /// <inheritdoc />
        protected override void RenderContents(RenderTreeBuilder builder, ref int sequence)
        {
            if (!string.IsNullOrEmpty(this.renderedOutput))
            {
                // Literal source-order sequence number (ASP0006). This control renders no wrapper
                // element (TagName is null), so the base class enters RenderContents with sequence 0
                // and the markup keeps the very same position in the render tree as before. The ref
                // counter is advanced afterwards for the base implementation's own content.
                builder.AddMarkupContent(0, this.renderedOutput);
                sequence++;
            }

            base.RenderContents(builder, ref sequence);
        }

        /// <summary>Builds the context the module is executed with.</summary>
        /// <param name="httpContext">The context of the current request.</param>
        /// <returns>A <see cref="ModuleRequestContext"/>.</returns>
        protected ModuleRequestContext GetModuleRequestContext(HttpContext httpContext)
        {
            var module = this.ModuleContext.Configuration;

            // TODO DesktopModuleControllerAdapter usage is temporary in order to make method testable
            var desktopModule = DesktopModuleControllerAdapter.Instance.GetDesktopModule(module.DesktopModuleID, module.PortalID);
            var defaultControl = ModuleControlControllerAdapter.Instance.GetModuleControlByControlKey(string.Empty, module.ModuleDefID);

            var defaultRouteData = ModuleRoutingProvider.Instance().GetRouteData(null, defaultControl);

            var moduleApplication = GetModuleApplication(
                httpContext.GetScope().ServiceProvider.GetRequiredService<IBusinessControllerProvider>(),
                desktopModule,
                defaultRouteData);

            RouteData routeData;

            var query = httpContext.Request.Query;

            if (string.IsNullOrEmpty(this.ControlKey))
            {
                this.ControlKey = query["ctl"].ToString() ?? string.Empty;
            }

            var moduleId = Null.NullInteger;
            if (query.ContainsKey("moduleid"))
            {
                if (!int.TryParse(query["moduleid"].ToString(), out moduleId))
                {
                    moduleId = Null.NullInteger;
                }
            }

            if (moduleId != this.ModuleContext.ModuleId && string.IsNullOrEmpty(this.ControlKey))
            {
                // Set default routeData for module that is not the "selected" module
                routeData = defaultRouteData;
            }
            else
            {
                var control = ModuleControlControllerAdapter.Instance.GetModuleControlByControlKey(this.ControlKey, module.ModuleDefID);
                routeData = ModuleRoutingProvider.Instance().GetRouteData(httpContext, control);
            }

            var moduleRequestContext = new ModuleRequestContext
            {
                DnnPage = this.Page as IComponent,
                HttpContext = httpContext,
                ModuleContext = this.ModuleContext,
                ModuleApplication = moduleApplication,
                RouteData = routeData,
            };

            return moduleRequestContext;
        }

        /// <summary>Converts the module actions returned by the MVC action into DNN module actions.</summary>
        /// <param name="requestResult">The result of the module execution.</param>
        /// <returns>The module actions.</returns>
        protected ModuleActionCollection LoadActions(ModuleRequestResult requestResult)
        {
            var actions = new ModuleActionCollection();

            if (requestResult.ModuleActions != null)
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
            // Check if the MVC Module overrides the base ModuleApplication class.
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
            var defaultNamespaces = (string[])defaultRouteData.DataTokens["namespaces"];

            return new ModuleApplication
            {
                DefaultActionName = defaultControllerName,
                DefaultControllerName = defaultActionName,
                DefaultNamespaces = defaultNamespaces,
                ModuleName = desktopModule.ModuleName,
                FolderPath = desktopModule.FolderName,
            };
        }
    }
}
