// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Mvc.Framework.Modules
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using DotNetNuke.Services.Localization;
    using DotNetNuke.Web.Mvc.Framework.Controllers;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Abstractions;
    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.AspNetCore.Mvc.Infrastructure;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Executes an MVC module action in-process against the current ASP.NET Core request.</summary>
    public class ModuleApplication
    {
        private readonly object @lock = new object();
        private bool initialized;

        /// <summary>Initializes a new instance of the <see cref="ModuleApplication"/> class.</summary>
        public ModuleApplication()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ModuleApplication"/> class.</summary>
        /// <param name="disableMvcResponseHeader">Unused, kept for API compatibility.</param>
        public ModuleApplication(bool disableMvcResponseHeader)
        {
        }

        /// <summary>Gets or sets the default action name.</summary>
        public string DefaultActionName { get; set; }

        /// <summary>Gets or sets the default controller name.</summary>
        public string DefaultControllerName { get; set; }

        /// <summary>Gets or sets the default namespaces used to disambiguate controllers.</summary>
        public string[] DefaultNamespaces { get; set; }

        /// <summary>Gets or sets the module folder path (under DesktopModules/MVC).</summary>
        public string FolderPath { get; set; }

        /// <summary>Gets or sets the module name.</summary>
        public string ModuleName { get; set; }

        /// <summary>Executes the module synchronously.</summary>
        /// <param name="context">The module request context.</param>
        /// <returns>The module request result.</returns>
        public virtual ModuleRequestResult ExecuteRequest(ModuleRequestContext context)
        {
            return this.ExecuteRequestAsync(context, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>Executes the module asynchronously.</summary>
        /// <param name="context">The module request context.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The module request result.</returns>
        public virtual async Task<ModuleRequestResult> ExecuteRequestAsync(ModuleRequestContext context, CancellationToken cancellationToken)
        {
            this.EnsureInitialized();

            var httpContext = context.HttpContext;
            var routeData = context.RouteData ?? new RouteData();

            var controllerName = GetRouteValue(routeData, "controller") ?? this.DefaultControllerName;
            var actionName = GetRouteValue(routeData, "action") ?? this.DefaultActionName;

            var descriptor = this.FindActionDescriptor(httpContext, controllerName, actionName);
            if (descriptor == null)
            {
                throw new InvalidOperationException($"Could not find an action '{actionName}' on controller '{controllerName}'.");
            }

            // Ensure the route values carry controller/action/module context for the invoker.
            routeData.Values["controller"] = controllerName;
            routeData.Values["action"] = actionName;

            var actionContext = new ActionContext(httpContext, routeData, descriptor);

            // Flow the module state to the controller through HttpContext.Items.
            httpContext.Items[ModuleRequestItemKeys.ModuleRequestContext] = context;

            var localResourceFile =
                $"~/DesktopModules/MVC/{context.ModuleContext.Configuration.DesktopModule.FolderName}/{Localization.LocalResourceDirectory}/{controllerName}.resx";

            var moduleState = new DnnModuleState
            {
                DnnPage = context.DnnPage,
                ModuleContext = context.ModuleContext,
                LocalResourceFile = localResourceFile,
                ModuleApplication = this,
            };
            httpContext.Items[typeof(DnnModuleState)] = moduleState;

            var invokerFactory = httpContext.RequestServices.GetRequiredService<IActionInvokerFactory>();

            var originalBody = httpContext.Response.Body;
            using var buffer = new MemoryStream();
            httpContext.Response.Body = buffer;
            try
            {
                var invoker = invokerFactory.CreateInvoker(actionContext);
                if (invoker == null)
                {
                    throw new InvalidOperationException("Could not create an action invoker for the module action.");
                }

                await invoker.InvokeAsync();
            }
            finally
            {
                httpContext.Response.Body = originalBody;
            }

            buffer.Position = 0;
            using var reader = new StreamReader(buffer);
            var renderedOutput = reader.ReadToEnd();

            return new ModuleRequestResult
            {
                ActionContext = actionContext,
                ActionResult = moduleState.ResultOfLastExecute,
                RenderedOutput = renderedOutput,
                ModuleActions = moduleState.ModuleActions,
                ModuleContext = context.ModuleContext,
                ModuleApplication = this,
            };
        }

        /// <summary>Allows subclasses to initialize before execution.</summary>
        protected internal virtual void Init()
        {
        }

        /// <summary>Ensures <see cref="Init"/> has been called exactly once.</summary>
        protected void EnsureInitialized()
        {
            if (this.initialized)
            {
                return;
            }

            lock (this.@lock)
            {
                if (this.initialized)
                {
                    return;
                }

                this.Init();
                this.initialized = true;
            }
        }

        private static string GetRouteValue(RouteData routeData, string key)
        {
            return routeData.Values.TryGetValue(key, out var value) ? value?.ToString() : null;
        }

        private ControllerActionDescriptor FindActionDescriptor(HttpContext httpContext, string controllerName, string actionName)
        {
            var provider = httpContext.RequestServices.GetRequiredService<IActionDescriptorCollectionProvider>();

            var candidates = provider.ActionDescriptors.Items
                .OfType<ControllerActionDescriptor>()
                .Where(d =>
                    string.Equals(d.ControllerName, controllerName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(d.ActionName, actionName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (candidates.Count == 0)
            {
                return null;
            }

            if (candidates.Count > 1 && this.DefaultNamespaces is { Length: > 0 })
            {
                var preferred = candidates
                    .Where(d => this.DefaultNamespaces.Any(ns =>
                        d.ControllerTypeInfo.Namespace != null &&
                        d.ControllerTypeInfo.Namespace.StartsWith(ns, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                if (preferred.Count > 0)
                {
                    return preferred[0];
                }
            }

            return candidates[0];
        }
    }
}
