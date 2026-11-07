// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Mvc.Framework.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using DotNetNuke.Common;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Web.Mvc.Framework.Controllers;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Abstractions;
    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.AspNetCore.Mvc.Infrastructure;
    using Microsoft.AspNetCore.Mvc.ViewEngines;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;

    /// <summary>Hosts a single DNN MVC module application (its controllers and views) inside a DNN page.</summary>
    public class ModuleApplication
    {
        /// <summary>The <see cref="HttpContext.Items"/> key holding the folder path of the module currently executing.</summary>
        internal const string ModuleFolderPathItemKey = "DotNetNuke.Web.Mvc.ModuleFolderPath";

        /// <summary>The <see cref="HttpContext.Items"/> key holding the module controller currently executing.</summary>
        internal const string CurrentControllerItemKey = "DotNetNuke.Web.Mvc.CurrentDnnController";

        internal static readonly string MvcVersion = GetMvcVersionString();

        protected const string ControllerMasterFormat = "~/DesktopModules/MVC/{0}/Views/{{1}}/{{0}}.cshtml";
        protected const string SharedMasterFormat = "~/DesktopModules/MVC/{0}/Views/Shared/{{0}}.cshtml";
        protected const string ControllerViewFormat = "~/DesktopModules/MVC/{0}/Views/{{1}}/{{0}}.cshtml";
        protected const string SharedViewFormat = "~/DesktopModules/MVC/{0}/Views/Shared/{{0}}.cshtml";
        protected const string ControllerPartialFormat = "~/DesktopModules/MVC/{0}/Views/{{1}}/{{0}}.cshtml";
        protected const string SharedPartialFormat = "~/DesktopModules/MVC/{0}/Views/Shared/{{0}}.cshtml";
        private const string MvcVersionHeaderName = "X-AspNetMvc-Version";

        private static readonly CompositeFormat ControllerMasterCompositeFormat = CompositeFormat.Parse(ControllerMasterFormat);
        private static readonly CompositeFormat SharedMasterCompositeFormat = CompositeFormat.Parse(SharedMasterFormat);
        private static readonly CompositeFormat ControllerViewCompositeFormat = CompositeFormat.Parse(ControllerViewFormat);
        private static readonly CompositeFormat SharedViewCompositeFormat = CompositeFormat.Parse(SharedViewFormat);
        private static readonly CompositeFormat ControllerPartialCompositeFormat = CompositeFormat.Parse(ControllerPartialFormat);
        private static readonly CompositeFormat SharedPartialCompositeFormat = CompositeFormat.Parse(SharedPartialFormat);

        private readonly object @lock = new object();

        private bool initialized;

        /// <summary>Initializes a new instance of the <see cref="ModuleApplication"/> class.</summary>
        public ModuleApplication()
            : this(null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ModuleApplication"/> class.</summary>
        /// <param name="disableMvcResponseHeader">Unused, the header suppression is a global switch (<see cref="DnnMvcHandler.DisableMvcResponseHeader"/>).</param>
        public ModuleApplication(bool disableMvcResponseHeader)
            : this(null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ModuleApplication"/> class.</summary>
        /// <param name="actionContext">The action context of the current request.</param>
        /// <param name="disableMvcResponseHeader">Unused, the header suppression is a global switch (<see cref="DnnMvcHandler.DisableMvcResponseHeader"/>).</param>
        public ModuleApplication(ActionContext actionContext, bool disableMvcResponseHeader)
            : this(actionContext)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ModuleApplication"/> class.</summary>
        /// <param name="actionContext">The action context of the current request. Replaces the legacy <c>System.Web.Routing.RequestContext</c>.</param>
        public ModuleApplication(ActionContext actionContext)
        {
            this.ActionContext = actionContext;

            // ReSharper disable once DoNotCallOverridableMethodsInConstructor
            this.ControllerFactory = Globals.GetCurrentServiceProvider().GetRequiredService<IControllerFactory>();
            this.ViewEngines = new List<IViewEngine>();
        }

        /// <summary>Gets the action context of the current request.</summary>
        public ActionContext ActionContext { get; private set; }

        /// <summary>Gets or sets the controller factory used to construct the module's controllers.</summary>
        public virtual IControllerFactory ControllerFactory { get; set; }

        public string DefaultActionName { get; set; }

        public string DefaultControllerName { get; set; }

        public string[] DefaultNamespaces { get; set; }

        public string FolderPath { get; set; }

        public string ModuleName { get; set; }

        /// <summary>Gets or sets the view engines used to resolve this module's views.</summary>
        public IList<IViewEngine> ViewEngines { get; set; }

        /// <summary>Gets the master (layout) location formats of this module.</summary>
        public IReadOnlyList<string> MasterLocationFormats { get; private set; } = [];

        /// <summary>Gets the view location formats of this module.</summary>
        public IReadOnlyList<string> ViewLocationFormats { get; private set; } = [];

        /// <summary>Gets the partial view location formats of this module.</summary>
        public IReadOnlyList<string> PartialViewLocationFormats { get; private set; } = [];

        /// <summary>Gets the view location formats of a module folder, as contributed by <see cref="ModuleViewLocationExpander"/>.</summary>
        /// <param name="folderPath">The module folder path (e.g. <c>MyModule</c>).</param>
        /// <param name="isMainPage"><see langword="true"/> when a main page (view) is being located, <see langword="false"/> for a partial.</param>
        /// <returns>The view location formats.</returns>
        public static IEnumerable<string> GetModuleViewLocationFormats(string folderPath, bool isMainPage)
        {
            var prefix = NormalizeFolderPath(folderPath);
            if (string.IsNullOrEmpty(prefix))
            {
                yield break;
            }

            yield return string.Format(CultureInfo.InvariantCulture, ControllerViewCompositeFormat, prefix);
            yield return string.Format(CultureInfo.InvariantCulture, SharedViewCompositeFormat, prefix);

            if (!isMainPage)
            {
                yield return string.Format(CultureInfo.InvariantCulture, ControllerPartialCompositeFormat, prefix);
                yield return string.Format(CultureInfo.InvariantCulture, SharedPartialCompositeFormat, prefix);
            }
        }

        /// <summary>Gets the folder path of the module currently executing on the given request.</summary>
        /// <param name="httpContext">The current HTTP context.</param>
        /// <returns>The module folder path, or <see langword="null"/> when no module is executing.</returns>
        public static string GetCurrentModuleFolderPath(HttpContext httpContext)
        {
            return httpContext is not null && httpContext.Items.TryGetValue(ModuleFolderPathItemKey, out var folderPath)
                ? folderPath as string
                : null;
        }

        /// <summary>Gets the module controller currently executing on the given request.</summary>
        /// <param name="httpContext">The current HTTP context.</param>
        /// <returns>The module controller, or <see langword="null"/> when no module controller is executing.</returns>
        public static IDnnController GetCurrentDnnController(HttpContext httpContext)
        {
            return httpContext is not null && httpContext.Items.TryGetValue(CurrentControllerItemKey, out var controller)
                ? controller as IDnnController
                : null;
        }

        /// <summary>Executes the module request.</summary>
        /// <param name="context">The module request context.</param>
        /// <returns>The captured module request result.</returns>
        /// <remarks>Controller execution is asynchronous in ASP.NET Core, hence the <see cref="Task{TResult}"/>.</remarks>
        public virtual Task<ModuleRequestResult> ExecuteRequest(ModuleRequestContext context)
        {
            return this.ExecuteRequestAsync(context, CancellationToken.None);
        }

        /// <summary>Executes the module request.</summary>
        /// <param name="context">The module request context.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The captured module request result.</returns>
        public virtual async Task<ModuleRequestResult> ExecuteRequestAsync(ModuleRequestContext context, CancellationToken cancellationToken)
        {
            Requires.NotNull(nameof(context), context);

            this.EnsureInitialized();

            var httpContext = context.HttpContext ?? this.ActionContext?.HttpContext
                ?? throw new InvalidOperationException("A module request requires an HttpContext.");
            var routeData = context.RouteData ?? this.ActionContext?.RouteData ?? new RouteData();

            // Note: ASP.NET Core has no System.Web request-validation / dynamic-validation feature
            // (Microsoft.Web.Infrastructure.DynamicValidationHelper.ValidationUtility was removed), so the
            // former 'enable dynamic request validation on the current HttpContext' step has no equivalent.
            // Request input validation is handled by model binding / validation attributes instead.
            this.AddVersionHeader(httpContext);
            RemoveOptionalRoutingParameters(routeData);

            var controllerName = GetRouteValue(routeData, "controller") ?? this.DefaultControllerName;
            if (string.IsNullOrEmpty(controllerName))
            {
                throw new InvalidOperationException("The RouteData must contain an item named 'controller' with a non-empty string value.");
            }

            var services = httpContext.RequestServices ?? Globals.GetCurrentServiceProvider();
            var actionName = GetRouteValue(routeData, "action") ?? this.DefaultActionName;
            var descriptor = this.SelectActionDescriptor(services, controllerName, actionName)
                ?? throw new InvalidOperationException($"Could Not Construct Controller: no action '{actionName}' was found on controller '{controllerName}'.");

            var actionContext = new ActionContext(httpContext, routeData, descriptor);
            this.ActionContext = actionContext;

            // Construct the controller using the ControllerFactory to validate that it can be constructed
            // and that it supports IDnnController. We can't support plain controllers: we need to retrieve
            // the ActionResult without executing it, which only IDnnController exposes.
            var controllerContext = new ControllerContext(actionContext);
            var controller = this.ControllerFactory.CreateController(controllerContext);
            try
            {
                if (controller is not IDnnController)
                {
                    throw new InvalidOperationException("Could Not Construct Controller");
                }
            }
            finally
            {
                this.ControllerFactory.ReleaseController(controllerContext, controller);
            }

            // Execute the controller through the MVC filter/action pipeline and capture the result.
            // The capturing filter configures the controller instance the pipeline creates and prevents
            // the produced result from being executed, so the module host can render it into the page.
            var capturingFilter = new ResultCapturingActionInvoker
            {
                ConfigureController = instance => this.ConfigureController(instance, context, controllerName, httpContext),
            };

            httpContext.Items[ModuleFolderPathItemKey] = NormalizeFolderPath(this.FolderPath);
            capturingFilter.AttachTo(httpContext);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var invoker = services.GetRequiredService<IActionInvokerFactory>().CreateInvoker(actionContext)
                    ?? throw new InvalidOperationException($"Could not create an action invoker for controller '{controllerName}'.");
                await invoker.InvokeAsync();
            }
            finally
            {
                ResultCapturingActionInvoker.DetachFrom(httpContext);
            }

            var moduleController = capturingFilter.Controller
                ?? throw new InvalidOperationException("Could Not Construct Controller");

            // Return the final result.
            return new ModuleRequestResult
            {
                ActionResult = capturingFilter.ResultOfLastInvoke ?? moduleController.ResultOfLastExecute,
                ActionContext = moduleController.ControllerContext ?? actionContext,
                ModuleActions = moduleController.ModuleActions,
                ModuleContext = context.ModuleContext,
                ModuleApplication = this,
            };
        }

        protected internal virtual void Init()
        {
            var prefix = NormalizeFolderPath(this.FolderPath);
            string[] masterFormats =
            {
                string.Format(CultureInfo.InvariantCulture, ControllerMasterCompositeFormat, prefix),
                string.Format(CultureInfo.InvariantCulture, SharedMasterCompositeFormat, prefix),
            };
            string[] viewFormats =
            {
                string.Format(CultureInfo.InvariantCulture, ControllerViewCompositeFormat, prefix),
                string.Format(CultureInfo.InvariantCulture, SharedViewCompositeFormat, prefix),
                string.Format(CultureInfo.InvariantCulture, ControllerPartialCompositeFormat, prefix),
                string.Format(CultureInfo.InvariantCulture, SharedPartialCompositeFormat, prefix),
            };

            this.MasterLocationFormats = masterFormats;
            this.ViewLocationFormats = viewFormats;
            this.PartialViewLocationFormats = viewFormats;

            // In ASP.NET Core these location formats are contributed to the single Razor view engine by
            // ModuleViewLocationExpander (registered through RazorViewEngineOptions.ViewLocationExpanders),
            // so no per-module RazorViewEngine instance is created here. The module's view engines are the
            // engines configured in DI, minus the delegating engine itself (which would recurse back here).
            var services = this.ActionContext?.HttpContext?.RequestServices ?? Globals.GetCurrentServiceProvider();
            var configuredEngines = services?.GetService<IOptions<MvcViewOptions>>()?.Value?.ViewEngines;
            this.ViewEngines = configuredEngines is null
                ? new List<IViewEngine>()
                : [.. configuredEngines.Where(engine => engine is not ModuleDelegatingViewEngine)];
        }

        protected internal virtual void AddVersionHeader(HttpContext httpContext)
        {
            if (!DnnMvcHandler.DisableMvcResponseHeader && httpContext is not null && !httpContext.Response.HasStarted)
            {
                httpContext.Response.Headers[MvcVersionHeaderName] = MvcVersion;
            }
        }

        protected static string NormalizeFolderPath(string path)
        {
            // Remove leading and trailing slashes
            return !string.IsNullOrEmpty(path) ? path.Trim('/') : path;
        }

        protected void EnsureInitialized()
        {
            // Double-check lock to wait for initialization
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

        private static string GetMvcVersionString()
        {
            return new AssemblyName(typeof(Controller).Assembly.FullName).Version.ToString(2);
        }

        private static string GetRouteValue(RouteData routeData, string key)
        {
            if (routeData.Values.TryGetValue(key, out var value))
            {
                var text = value as string ?? value?.ToString();
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }

            return null;
        }

        private static void RemoveOptionalRoutingParameters(RouteData routeData)
        {
            // Note: System.Web.Mvc.UrlParameter.Optional has no ASP.NET Core equivalent — Core route
            // values are simply absent when they were not supplied. The equivalent clean-up is therefore to
            // drop the placeholder (null valued) entries that legacy module routes still add.
            var optionalKeys = routeData.Values
                .Where(entry => entry.Value is null)
                .Select(entry => entry.Key)
                .ToList();
            foreach (var key in optionalKeys)
            {
                routeData.Values.Remove(key);
            }
        }

        private static bool IsInNamespace(string candidateNamespace, string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                return false;
            }

            if (pattern.EndsWith(".*", StringComparison.Ordinal))
            {
                var prefix = pattern[..^1];
                return candidateNamespace is not null
                    && (candidateNamespace.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(candidateNamespace, pattern[..^2], StringComparison.OrdinalIgnoreCase));
            }

            return string.Equals(candidateNamespace, pattern, StringComparison.OrdinalIgnoreCase);
        }

        private ControllerActionDescriptor SelectActionDescriptor(IServiceProvider services, string controllerName, string actionName)
        {
            var actionDescriptors = services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items;
            var candidates = actionDescriptors
                .OfType<ControllerActionDescriptor>()
                .Where(d => string.Equals(d.ControllerName, controllerName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // The legacy code passed DefaultNamespaces to the controller factory to scope the lookup to the
            // module's own controller namespaces; the equivalent here is filtering the candidate actions.
            if (this.DefaultNamespaces is { Length: > 0 })
            {
                var scoped = candidates
                    .Where(d => this.DefaultNamespaces.Any(ns => IsInNamespace(d.ControllerTypeInfo?.Namespace, ns)))
                    .ToList();
                if (scoped.Count > 0)
                {
                    candidates = scoped;
                }
            }

            var dnnControllers = candidates
                .Where(d => d.ControllerTypeInfo is not null && typeof(IDnnController).IsAssignableFrom(d.ControllerTypeInfo))
                .ToList();
            if (dnnControllers.Count > 0)
            {
                candidates = dnnControllers;
            }

            return candidates.FirstOrDefault(d => string.Equals(d.ActionName, actionName, StringComparison.OrdinalIgnoreCase))
                ?? candidates.FirstOrDefault(d => string.Equals(d.ActionName, this.DefaultActionName, StringComparison.OrdinalIgnoreCase))
                ?? candidates.FirstOrDefault();
        }

        private void ConfigureController(object controllerInstance, ModuleRequestContext context, string controllerName, HttpContext httpContext)
        {
            if (controllerInstance is not IDnnController moduleController)
            {
                // We can't support plain controller implementations: we need to retrieve the ActionResult
                // without executing it, which only IDnnController exposes.
                throw new InvalidOperationException("Could Not Construct Controller");
            }

            moduleController.ValidateRequest = false;

            moduleController.DnnPage = context.DnnPage;

            moduleController.ModuleContext = context.ModuleContext;

            moduleController.LocalResourceFile =
                $"~/DesktopModules/MVC/{context.ModuleContext.Configuration.DesktopModule.FolderName}/{Localization.LocalResourceDirectory}/{controllerName}.resx";

            moduleController.ViewEngines = this.ViewEngines as IReadOnlyList<IViewEngine> ?? [.. this.ViewEngines];

            httpContext.Items[CurrentControllerItemKey] = moduleController;
        }
    }
}
