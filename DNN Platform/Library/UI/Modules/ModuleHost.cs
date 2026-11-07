// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Host;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Tabs.TabVersions;
    using DotNetNuke.Framework;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Security;
    using DotNetNuke.Security.Permissions;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.Log.EventLog;
    using DotNetNuke.Services.ModuleCache;
    using DotNetNuke.Services.Personalization;
    using DotNetNuke.Web.Client;
    using DotNetNuke.Web.Client.ClientResourceManagement;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Web;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    using Globals = DotNetNuke.Common.Globals;

    /// <summary>ModuleHost hosts a Module Control (or its cached Content).</summary>
    /// <remarks>
    /// <para>
    /// Was a <c>System.Web.UI.WebControls.Panel</c> that added the loaded module control to its
    /// <c>Controls</c> collection. It is now a DNN skinning component that renders a
    /// <c>&lt;div&gt;</c> containing:
    /// </para>
    /// <list type="number">
    /// <item><description>the module message placeholder (<c>Skin.AddModuleMessage</c> targets it),</description></item>
    /// <item><description>the module control itself, rendered as a real Blazor component so that its lifecycle runs.</description></item>
    /// </list>
    /// <para>
    /// The module control is created by the <see cref="IModuleControlPipeline"/> and appended to
    /// <see cref="DnnComponentBase.Controls"/>; <see cref="DnnComponentBase.RenderControl"/> renders
    /// it by its component type, carrying over the Blazor parameters the pipeline assigned. Output
    /// caching, which used to be done by rendering the control into an <c>HtmlTextWriter</c>, is now
    /// done with <see cref="HtmlRenderer"/> after the first render.
    /// </para>
    /// </remarks>
    public sealed partial class ModuleHost : DnnComponentBase
    {
        private const string DefaultCssProvider = "DnnPageHeaderProvider";
        private const string DefaultJsProvider = "DnnBodyProvider";

        private static readonly ILogger Logger = DnnLoggingController.GetLogger<ModuleHost>();

        private static readonly Regex CdfMatchRegex = new Regex(
            @"<\!--CDF\((?<type>JAVASCRIPT|CSS|JS-LIBRARY)\|(?<path>.+?)(\|(?<provider>.+?)\|(?<priority>\d+?))?\)-->",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly char[] ArgsSeparator = [',',];

        private readonly ModuleInfo moduleConfiguration;
        private readonly IModuleControlPipeline moduleControlPipeline = Globals.GetCurrentServiceProvider().GetRequiredService<IModuleControlPipeline>();
        private IComponent control;
        private bool isCached;
        private bool useUpdatePanel;
        private bool cacheWritten;

        /// <summary>Initializes a new instance of the <see cref="ModuleHost"/> class using the ModuleConfiguration for the Module.</summary>
        /// <param name="moduleConfiguration">The module info.</param>
        /// <param name="skin">The skin for the page.</param>
        /// <param name="container">The container for the module.</param>
        public ModuleHost(ModuleInfo moduleConfiguration, Skins.Skin skin, Containers.Container container)
        {
            this.ID = "ModuleContent";
            this.Container = container;
            this.moduleConfiguration = moduleConfiguration;
            this.Skin = skin;
        }

        /// <summary>Gets the attached ModuleControl.</summary>
        /// <returns>An IModuleControl.</returns>
        public IModuleControl ModuleControl
        {
            get
            {
                // Make sure the Control tree has been created
                this.EnsureChildControls();
                return this.control as IModuleControl;
            }
        }

        /// <summary>Gets the current Portal Settings.</summary>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public PortalSettings PortalSettings => PortalSettings.Current;

        /// <summary>Gets the container this module is rendered in.</summary>
        public Containers.Container Container { get; private set; }

        /// <summary>Gets the skin of the page this module is rendered on.</summary>
        public Skins.Skin Skin { get; private set; }

        /// <inheritdoc />
        protected override string TagName => "div";

        /// <summary>Gets a flag that indicates whether the Module is in View Mode.</summary>
        /// <param name="moduleInfo">The module information.</param>
        /// <param name="settings">The portal settings.</param>
        /// <returns>A Boolean.</returns>
        internal static bool IsViewMode(ModuleInfo moduleInfo, PortalSettings settings)
        {
            bool viewMode;

            if (ModulePermissionController.HasModuleAccess(SecurityAccessLevel.ViewPermissions, Null.NullString, moduleInfo))
            {
                viewMode = false;
            }
            else
            {
                viewMode = !ModulePermissionController.HasModuleAccess(SecurityAccessLevel.Edit, Null.NullString, moduleInfo);
            }

            return viewMode || Personalization.GetUserMode() == PortalSettings.Mode.View;
        }

        /// <summary>CreateChildControls builds the control tree.</summary>
        protected override void CreateChildControls()
        {
            this.Controls.Clear();

            // Load Module Control (or cached control)
            this.LoadModuleControl();

            // Optionally Inject AJAX Update Panel
            if (this.ModuleControl != null)
            {
                // if module is dynamically loaded and AJAX is installed and the control supports partial rendering (defined in ModuleControls table )
                if (!this.isCached && this.moduleConfiguration.ModuleControl.SupportsPartialRendering && AJAX.IsInstalled())
                {
                    this.LoadUpdatePanel();
                }
                else
                {
                    // inject a message placeholder for common module messaging - UI.Skins.Skin.AddModuleMessage
                    InjectMessageControl(this);

                    this.InjectVersionToTheModuleIfSupported();

                    // inject the module into the panel
                    this.InjectModuleContent(this.control);
                }
            }
        }

        /// <inheritdoc />
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);

            if (Host.EnableCustomModuleCssClass)
            {
                string moduleName = this.ModuleControl?.ModuleContext?.Configuration?.DesktopModule?.ModuleName;
                if (moduleName != null)
                {
                    moduleName = Globals.CleanName(moduleName);
                }

                this.Attributes["class"] = $"DNNModuleContent Mod{moduleName}C";
            }
        }

        /// <inheritdoc />
        protected override void RenderContents(RenderTreeBuilder builder, ref int sequence)
        {
            this.EnsureChildControls();

            if (!this.useUpdatePanel)
            {
                base.RenderContents(builder, ref sequence);
                return;
            }

            // Was: the module (and its message placeholder) wrapped in an MS AJAX UpdatePanel plus an
            // UpdateProgress. Blazor diffs and re-renders on its own, so AJAX.WrapUpdatePanelControl
            // supplies the equivalent markup (dnnUpdatePanel + dnnUpdateProgress).
            RenderFragment content = contentBuilder =>
            {
                var contentSequence = 0;
                this.RenderChildControls(contentBuilder, ref contentSequence);
            };

            builder.AddContent(sequence++, AJAX.WrapUpdatePanelControl(content, true));
        }

        /// <inheritdoc />
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            if (!firstRender)
            {
                return;
            }

            await this.SaveModuleOutputToCacheAsync();
        }

        private static void InjectMessageControl(DnnComponentBase container)
        {
            // inject a message placeholder for common module messaging - UI.Skins.Skin.AddModuleMessage
            var messagePlaceholder = new DnnPlaceHolder { ID = "MessagePlaceHolder", Visible = false };
            container.Controls.Add(messagePlaceholder);
        }

        private static bool IsVersionRequest() => TabVersionUtils.TryGetUrlVersion(out _);

        private void InjectVersionToTheModuleIfSupported()
        {
            if (this.control is not IVersionableControl versionableControl)
            {
                return;
            }

            if (this.moduleConfiguration.ModuleVersion != Null.NullInteger)
            {
                versionableControl.SetModuleVersion(this.moduleConfiguration.ModuleVersion);
            }
        }

        private void InjectModuleContent(IComponent content)
        {
            if (content is Utilities.IClientApiControl treeControl)
            {
                this.Controls.Add(treeControl);
                return;
            }

            // A module control that is not part of the DNN component tree (a plain Blazor component,
            // e.g. a PortalModuleBase) is still rendered by its component type; it is wrapped so the
            // ordered child collection can carry it.
            this.Controls.Add(new ComponentHolder(content));
        }

        /// <summary>Gets a flag that indicates whether the Module Content should be displayed.</summary>
        /// <returns>A Boolean.</returns>
        private bool DisplayContent()
        {
            // module content visibility options
            var content = Personalization.GetUserMode() != PortalSettings.Mode.Layout;
            var contentQueryValue = this.Request?.GetValue("content");
            if (contentQueryValue != null)
            {
                switch (contentQueryValue.ToLowerInvariant())
                {
                    case "1":
                    case "true":
                        content = true;
                        break;
                    case "0":
                    case "false":
                        content = false;
                        break;
                }
            }

            if (Globals.IsAdminControl())
            {
                content = true;
            }

            return content;
        }

        /// <summary>LoadModuleControl loads the ModuleControl (PortalModuleBase).</summary>
        private void LoadModuleControl()
        {
            try
            {
                if (this.DisplayContent())
                {
                    // if the module supports caching and caching is enabled for the instance and the user does not have Edit rights or is currently in View mode
                    if (this.SupportsCaching() && IsViewMode(this.moduleConfiguration, this.PortalSettings) && !IsVersionRequest())
                    {
                        // attempt to load the cached content
                        this.isCached = this.TryLoadCached();
                    }

                    if (!this.isCached)
                    {
                        // load the control dynamically
                        this.control = this.moduleControlPipeline.LoadModuleControl(this, this.moduleConfiguration);
                    }
                }
                else
                {
                    // content placeholder
                    this.control = this.moduleControlPipeline.CreateModuleControl(this.moduleConfiguration);
                }

                // check for IMC
                this.Skin?.Communicator.LoadCommunicator(this.control);

                // add module settings
                if (this.control is IModuleControl loadedModuleControl)
                {
                    loadedModuleControl.ModuleContext.Configuration = this.moduleConfiguration;
                }
            }
            catch (Exception exc)
            {
                Logger.ModuleHostLoadModuleControlException(exc);

                // add module settings
                this.control = this.moduleControlPipeline.CreateModuleControl(this.moduleConfiguration);
                if (this.control is IModuleControl fallbackModuleControl)
                {
                    fallbackModuleControl.ModuleContext.Configuration = this.moduleConfiguration;
                }

                if (TabPermissionController.CanAdminPage())
                {
                    // only display the error to page administrators
                    Exceptions.ProcessModuleLoadException((IComponent)this.control, exc);
                }
                else
                {
                    // Otherwise just log the fact that an exception occurred
                    new ExceptionLogController().AddLog(exc);
                }
            }

            // Was: this.control.ViewStateMode = ViewStateMode.Enabled. There is no view state on
            // ASP.NET Core; component state lives in the component instance for the circuit's lifetime.
        }

        /// <summary>LoadUpdatePanel optionally loads an AJAX Update Panel.</summary>
        private void LoadUpdatePanel()
        {
            // register AJAX
            AJAX.RegisterScriptManager();

            // Was: enable Partial Rendering on the page's ScriptManager. ASP.NET Core has no MS AJAX
            // partial postbacks; GetScriptManager only reports whether AJAX is registered.
            AJAX.GetScriptManager(HttpContextSource.Current);

            // inject a message placeholder for common module messaging - UI.Skins.Skin.AddModuleMessage
            InjectMessageControl(this);

            this.InjectVersionToTheModuleIfSupported();

            // inject module into update panel content template
            this.InjectModuleContent(this.control);

            this.useUpdatePanel = true;
        }

        /// <summary>Gets a flag that indicates whether the Module Instance supports Caching.</summary>
        /// <returns>A Boolean.</returns>
        private bool SupportsCaching()
        {
            return this.moduleConfiguration.CacheTime > 0;
        }

        /// <summary>Trys to load previously cached Module Content.</summary>
        /// <returns>A Boolean that indicates whether the cahed content was loaded.</returns>
        private bool TryLoadCached()
        {
            bool success = false;
            string cachedContent = string.Empty;
            try
            {
                var cache = ModuleCachingProvider.Instance(this.moduleConfiguration.GetEffectiveCacheMethod());
                var varyBy = new SortedDictionary<string, string> { { "locale", System.Threading.Thread.CurrentThread.CurrentUICulture.ToString() } };

                string cacheKey = cache.GenerateCacheKey(this.moduleConfiguration.TabModuleID, varyBy);
                byte[] cachedBytes = ModuleCachingProvider.Instance(this.moduleConfiguration.GetEffectiveCacheMethod()).GetModule(this.moduleConfiguration.TabModuleID, cacheKey);

                if (cachedBytes != null && cachedBytes.Length > 0)
                {
                    cachedContent = Encoding.UTF8.GetString(cachedBytes);
                    success = true;
                }
            }
            catch (Exception ex)
            {
                cachedContent = string.Empty;
                Exceptions.LogException(ex);
                success = false;
            }

            if (success)
            {
                this.RestoreCachedClientResourceRegistrations(cachedContent);
                this.control = this.moduleControlPipeline.CreateCachedControl(cachedContent, this.moduleConfiguration);
                this.InjectModuleContent(this.control);
            }

            return success;
        }

        /// <summary>
        /// Renders the module's output a second time, into a string, and stores it in the module
        /// output cache.
        /// </summary>
        /// <returns>A task that completes when the output has been cached.</returns>
        /// <remarks>
        /// Replaces the WebForms <c>RenderContents</c> branch that rendered the control into a
        /// <c>StringWriter</c> before writing it to the response. A Blazor component cannot be
        /// rendered synchronously while it is being rendered, so the markup is produced with
        /// <see cref="HtmlRenderer"/> once the first render has completed.
        /// </remarks>
        private async Task SaveModuleOutputToCacheAsync()
        {
            if (this.cacheWritten
                || this.isCached
                || this.control == null
                || !this.SupportsCaching()
                || !IsViewMode(this.moduleConfiguration, this.PortalSettings)
                || Globals.IsAdminControl()
                || IsVersionRequest())
            {
                return;
            }

            var request = this.Request;
            if (request != null && BrowserCapabilities.IsCrawler(request))
            {
                return;
            }

            this.cacheWritten = true;

            try
            {
                var serviceProvider = Globals.GetCurrentServiceProvider();
                var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
                var parameters = ParameterView.FromDictionary(DnnComponentParameters.Capture(this.control));

                string cachedOutput;
                await using (var htmlRenderer = new HtmlRenderer(serviceProvider, loggerFactory))
                {
                    cachedOutput = await htmlRenderer.Dispatcher.InvokeAsync(async () =>
                    {
                        var output = await htmlRenderer.RenderComponentAsync(this.control.GetType(), parameters);
                        return output.ToHtmlString();
                    });
                }

                if (string.IsNullOrEmpty(cachedOutput))
                {
                    return;
                }

                // Save content to cache
                var moduleContent = Encoding.UTF8.GetBytes(cachedOutput);
                var cache = ModuleCachingProvider.Instance(this.moduleConfiguration.GetEffectiveCacheMethod());

                var varyBy = new SortedDictionary<string, string> { { "locale", System.Threading.Thread.CurrentThread.CurrentUICulture.ToString() } };

                var cacheKey = cache.GenerateCacheKey(this.moduleConfiguration.TabModuleID, varyBy);
                cache.SetModule(this.moduleConfiguration.TabModuleID, cacheKey, new TimeSpan(0, 0, this.moduleConfiguration.CacheTime), moduleContent);
            }
            catch (Exception ex)
            {
                // a failure to populate the output cache must never break the page
                Exceptions.LogException(ex);
            }
        }

        /// <summary>
        /// Restores client resource registrations from the <paramref name="cachedContent"/>.
        /// These are registrations that originated from <c>DnnJsInclude</c>, <c>DnnCssInclude</c>, and <c>JavaScriptLibraryInclude</c> controls.
        /// </summary>
        /// <param name="cachedContent">The HTML text of the cached module.</param>
        private void RestoreCachedClientResourceRegistrations(string cachedContent)
        {
            // parse the registered CDF from content
            var matches = CdfMatchRegex.Matches(cachedContent);
            if (matches.Count == 0)
            {
                return;
            }

            var context = HttpContextSource.Current;

            foreach (Match match in matches)
            {
                cachedContent = cachedContent.Replace(match.Value, string.Empty);
                var dependencyType = match.Groups["type"].Value.ToUpperInvariant();
                var filePath = match.Groups["path"].Value;
                var forceProvider = string.Empty;
                var priority = Null.NullInteger;

                if (match.Groups["provider"].Success)
                {
                    forceProvider = match.Groups["provider"].Value;
                }

                if (match.Groups["priority"].Success)
                {
                    priority = Convert.ToInt32(match.Groups["priority"].Value, CultureInfo.InvariantCulture);
                }

                switch (dependencyType)
                {
                    case "JAVASCRIPT":
                        if (string.IsNullOrEmpty(forceProvider))
                        {
                            forceProvider = DefaultJsProvider;
                        }

                        if (priority == Null.NullInteger)
                        {
                            priority = (int)FileOrder.Js.DefaultPriority;
                        }

                        ClientResourceManager.RegisterScript(context, filePath, priority, forceProvider);
                        break;
                    case "CSS":
                        if (string.IsNullOrEmpty(forceProvider))
                        {
                            forceProvider = DefaultCssProvider;
                        }

                        if (priority == Null.NullInteger)
                        {
                            priority = (int)FileOrder.Css.DefaultPriority;
                        }

                        ClientResourceManager.RegisterStyleSheet(context, filePath, priority, forceProvider);
                        break;
                    case "JS-LIBRARY":
                        var args = filePath.Split(ArgsSeparator, StringSplitOptions.None);
                        if (string.IsNullOrEmpty(args[1]))
                        {
                            JavaScript.RequestRegistration(args[0]);
                        }
                        else if (string.IsNullOrEmpty(args[2]))
                        {
                            JavaScript.RequestRegistration(args[0], new Version(args[1]));
                        }
                        else
                        {
                            JavaScript.RequestRegistration(args[0], new Version(args[1]), (SpecificVersion)Enum.Parse(typeof(SpecificVersion), args[2]));
                        }

                        break;
                }
            }
        }

        /// <summary>
        /// Carries a Blazor component that is not part of the DNN component tree inside an ordered
        /// child collection, rendering it as a component of its own type.
        /// </summary>
        private sealed class ComponentHolder : DnnComponentBase
        {
            private readonly IComponent hostedComponent;

            /// <summary>Initializes a new instance of the <see cref="ComponentHolder"/> class.</summary>
            /// <param name="hostedComponent">The component to render.</param>
            internal ComponentHolder(IComponent hostedComponent)
            {
                this.hostedComponent = hostedComponent;
            }

            /// <summary>Gets the component this holder renders.</summary>
            internal IComponent HostedComponent => this.hostedComponent;

            /// <inheritdoc />
            protected override void RenderContents(RenderTreeBuilder builder, ref int sequence)
            {
                if (this.hostedComponent == null)
                {
                    return;
                }

                builder.OpenRegion(sequence++);
                builder.OpenComponent(0, this.hostedComponent.GetType());
                var parameters = DnnComponentParameters.Capture(this.hostedComponent);
                if (parameters.Count > 0)
                {
                    builder.AddMultipleAttributes(1, parameters);
                }

                builder.CloseComponent();
                builder.CloseRegion();
            }
        }
    }
}
