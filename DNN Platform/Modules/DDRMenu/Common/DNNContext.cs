// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.DDRMenu.DNNCommon
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Common;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.UI.Modules;
    using DotNetNuke.UI.Utilities;

    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Provides DNN context for the DDR Menu.</summary>
    /// <remarks>
    /// The ambient slot was <c>HttpContext.Current.Items[DataName]</c>. On ASP.NET Core there is no
    /// ambient request static and <see cref="HttpContextSource.Current"/> is <see langword="null"/>
    /// during Blazor Server interactive (SignalR) rendering, so the slot is an
    /// <see cref="AsyncLocal{T}"/>. The <c>HttpContext.Items</c> slot is still written whenever a
    /// request is in flight, so that cross-component reads during a request behave exactly as
    /// before.
    /// </remarks>
    public class DNNContext : IDisposable
    {
        private static readonly AsyncLocal<DNNContext> AmbientContext = new AsyncLocal<DNNContext>();

        private static string moduleName;
        private static string moduleFolder;
        private static string dataName;

        private readonly DNNContext savedContext;
        private readonly string moduleClientId;
        private bool isDisposed;
        private IClientApiPage page;
        private PortalSettings portalSettings;
        private TabInfo activeTab;
        private string skinPath;

        /// <summary>Initializes a new instance of the <see cref="DNNContext"/> class.</summary>
        /// <param name="hostControl">The control that hosts the menu.</param>
        public DNNContext(IClientApiControl hostControl)
        {
            this.HostControl = hostControl;
            this.moduleClientId = hostControl?.ClientId;

            this.savedContext = Current;
            Current = this;
        }

        /// <summary>Initializes a new instance of the <see cref="DNNContext"/> class.</summary>
        /// <param name="moduleControl">The module control (a Blazor component) that hosts the menu.</param>
        /// <remarks>
        /// Module controls derive from <see cref="Entities.Modules.PortalModuleBase"/>, which is a
        /// Blazor component and therefore not an <see cref="IClientApiControl"/>. Such a host has no
        /// control tree, so <see cref="HostControl"/> stays <see langword="null"/> and
        /// <see cref="ClientId"/> comes from the module control itself.
        /// </remarks>
        public DNNContext(IModuleControl moduleControl)
        {
            this.ModuleControl = moduleControl;
            this.HostControl = moduleControl?.Control as IClientApiControl;
            this.moduleClientId = this.HostControl?.ClientId ?? (moduleControl as ModuleBase)?.ClientId;

            this.savedContext = Current;
            Current = this;
        }

        /// <summary>Gets the module name.</summary>
        public static string ModuleName => moduleName ??= GetModuleNameFromAssembly();

        /// <summary>Gets the module folder.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.4. Please use GetModuleFolder(IHostSettings). Scheduled removal in v12.0.0.")]
        public static string ModuleFolder => GetModuleFolder(DotNetNuke.Common.Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettings>());

        /// <summary>Gets the current Dnn context.</summary>
        public static DNNContext Current
        {
            get
            {
                var items = CurrentItems;
                if (items != null && items.TryGetValue(DataName, out var stored))
                {
                    return (DNNContext)stored;
                }

                return AmbientContext.Value;
            }

            private set
            {
                AmbientContext.Value = value;

                var items = CurrentItems;
                if (items != null)
                {
                    if (value == null)
                    {
                        items.Remove(DataName);
                    }
                    else
                    {
                        items[DataName] = value;
                    }
                }
            }
        }

        /// <summary>Gets a reference to the page.</summary>
        public IClientApiPage Page => this.page ??= this.HostControl?.Page;

        /// <summary>Gets the current portal settings.</summary>
        public PortalSettings PortalSettings => this.portalSettings ??= (PortalSettings)PortalController.Instance.GetCurrentSettings();

        /// <summary>Gets the currently active tab (page).</summary>
        public TabInfo ActiveTab => this.activeTab ??= this.PortalSettings.ActiveTab;

        /// <summary>Gets the path to the skin (theme).</summary>
        public string SkinPath => this.skinPath ??= this.ActiveTab.SkinPath;

        /// <summary>Gets the host control, or <see langword="null"/> when the menu is hosted by a module control.</summary>
        public IClientApiControl HostControl { get; private set; }

        /// <summary>Gets the module control hosting the menu, or <see langword="null"/> when the host is a plain control.</summary>
        public IModuleControl ModuleControl { get; private set; }

        /// <summary>Gets the client side identifier of the host, which templates render as the menu's element id.</summary>
        public string ClientId => this.moduleClientId;

        private static IDictionary<object, object> CurrentItems => HttpContextSource.Current?.Items;

        private static string DataName => dataName ??= "DDRMenu.DNNContext." + ModuleName;

        /// <summary>Gets the module folder.</summary>
        /// <param name="hostSettings">The host settings.</param>
        /// <returns>The virtual rooted path.</returns>
        public static string GetModuleFolder(IHostSettings hostSettings)
        {
            var desktopModule = DesktopModuleController.GetDesktopModuleByModuleName(hostSettings, ModuleName, PortalSettings.Current.PortalId);
            return moduleFolder ??= $"~/DesktopModules/{desktopModule.FolderName}/";
        }

        /// <summary>Converts a url into one that is usable on the requesting Client.</summary>
        /// <param name="relativeUrl">The relative url.</param>
        /// <returns>The converted url.</returns>
        public string ResolveUrl(string relativeUrl)
        {
            // Control.ResolveUrl() made an application relative url client usable; the component
            // tree equivalent is Globals.ResolveUrl, which uses the application's virtual path.
            return DotNetNuke.Common.Globals.ResolveUrl(relativeUrl);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>Disposes this instance resources.</summary>
        /// <param name="disposing">A value indicating if the current instance is disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (this.isDisposed)
            {
                return;
            }

            if (disposing)
            {
                // free managed resources.
            }

            Current = this.savedContext;
            this.isDisposed = true;
        }

        private static string GetModuleNameFromAssembly()
        {
            // Assembly.CodeBase throws on .NET (single file / no code base), and the assembly simple
            // name is the same value Path.GetFileNameWithoutExtension(CodeBase) produced.
            var moduleFullName = Assembly.GetExecutingAssembly().GetName().Name;

            return moduleFullName.Substring(moduleFullName.LastIndexOf('.') + 1);
        }
    }
}
