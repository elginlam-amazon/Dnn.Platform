// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.ExtensionPoints
{
    using System;
    using System.Collections.Generic;
    using System.Composition;
    using System.IO;
    using System.Linq;

    using DotNetNuke.Common;
    using DotNetNuke.ExtensionPoints.Filters;

    public class ExtensionPointManager
    {
        private static readonly object SyncRoot = new object();
        private static readonly Lazy<SafeDirectoryCatalog> Catalog = new Lazy<SafeDirectoryCatalog>(InitializeCatalog, true);

// These are private [ImportMany] members. They were fields; MEF 2's ImportManyAttribute is not
        // valid on a field (only on a property or parameter), so they are now private properties with
        // the same semantics. They are initialized to empty so that a manager whose imports were
        // never satisfied enumerates empty rather than throwing a NullReferenceException.
        [ImportMany]
        private IEnumerable<Lazy<IScriptItemExtensionPoint, IExtensionPointData>> Scripts { get; set; } = Array.Empty<Lazy<IScriptItemExtensionPoint, IExtensionPointData>>();

        [ImportMany]
        private IEnumerable<Lazy<IEditPageTabExtensionPoint, IExtensionPointData>> EditPageTabExtensionPoint { get; set; } = Array.Empty<Lazy<IEditPageTabExtensionPoint, IExtensionPointData>>();

        [ImportMany]
        private IEnumerable<Lazy<IToolBarButtonExtensionPoint, IExtensionPointData>> ToolbarButtonExtensionPoints { get; set; } = Array.Empty<Lazy<IToolBarButtonExtensionPoint, IExtensionPointData>>();

        [ImportMany]
        private IEnumerable<Lazy<IEditPagePanelExtensionPoint, IExtensionPointData>> EditPagePanelExtensionPoints { get; set; } = Array.Empty<Lazy<IEditPagePanelExtensionPoint, IExtensionPointData>>();

        [ImportMany]
        private IEnumerable<Lazy<IContextMenuItemExtensionPoint, IExtensionPointData>> CtxMenuItemExtensionPoints { get; set; } = Array.Empty<Lazy<IContextMenuItemExtensionPoint, IExtensionPointData>>();

        [ImportMany]
        private IEnumerable<Lazy<IUserControlExtensionPoint, IExtensionPointData>> UserControlExtensionPoints { get; set; } = Array.Empty<Lazy<IUserControlExtensionPoint, IExtensionPointData>>();

        [ImportMany]
        private IEnumerable<Lazy<IMenuItemExtensionPoint, IExtensionPointData>> MenuItems { get; set; } = Array.Empty<Lazy<IMenuItemExtensionPoint, IExtensionPointData>>();

        [ImportMany]
        private IEnumerable<Lazy<IGridColumnExtensionPoint, IExtensionPointData>> GridColumns { get; set; } = Array.Empty<Lazy<IGridColumnExtensionPoint, IExtensionPointData>>();

        /// <summary>Initializes a new instance of the <see cref="ExtensionPointManager"/> class.</summary>
        public ExtensionPointManager()
        {
            ComposeParts(this);
        }

        /// <summary>Satisfies the <c>[ImportMany]</c> members of the given objects from the discovered extensions.</summary>
        /// <param name="attributeParts">The objects whose imports should be satisfied.</param>
        /// <remarks>
        /// The signature and the attribute contract are unchanged; only the engine behind them is.
        /// See <see cref="ExportDiscovery"/> for how <c>[Export]</c>/<c>[ExportMetadata]</c> are read
        /// now that MEF 1's <c>CompositionContainer</c> no longer exists.
        /// </remarks>
        public static void ComposeParts(params object[] attributeParts)
        {
            if (attributeParts is null)
            {
                return;
            }

            lock (SyncRoot)
            {
                var parts = Catalog.Value.Parts.ToList();
                foreach (var attributePart in attributeParts)
                {
                    if (attributePart != null)
                    {
                        ExportDiscovery.Satisfy(attributePart, parts);
                    }
                }
            }
        }

        public IEnumerable<IEditPageTabExtensionPoint> GetEditPageTabExtensionPoints(string module)
        {
            return this.GetEditPageTabExtensionPoints(module, null);
        }

        public IEnumerable<IEditPageTabExtensionPoint> GetEditPageTabExtensionPoints(string module, string group)
        {
            return from e in this.EditPageTabExtensionPoint
                   where e.Metadata.Module == module
                        && (string.IsNullOrEmpty(@group) || e.Metadata.Group == @group)
                   orderby e.Value.Order
                   select e.Value;
        }

        public IEnumerable<IToolBarButtonExtensionPoint> GetToolBarButtonExtensionPoints(string module)
        {
            return this.GetToolBarButtonExtensionPoints(module, null);
        }

        public IEnumerable<IToolBarButtonExtensionPoint> GetToolBarButtonExtensionPoints(string module, string group)
        {
            return this.GetToolBarButtonExtensionPoints(module, group, new NoFilter());
        }

        public IEnumerable<IToolBarButtonExtensionPoint> GetToolBarButtonExtensionPoints(string module, string group, IExtensionPointFilter filter)
        {
            return from e in this.ToolbarButtonExtensionPoints
                   where FilterCondition(e.Metadata, module, @group) && filter.Condition(e.Metadata)
                   orderby e.Value.Order
                   select e.Value;
        }

        public IToolBarButtonExtensionPoint GetToolBarButtonExtensionPointFirstByPriority(string module, string name)
        {
            return (from e in this.ToolbarButtonExtensionPoints
                    where e.Metadata.Module == module && e.Metadata.Name == name
                    orderby e.Metadata.Priority
                    select e.Value).FirstOrDefault();
        }

        public IEnumerable<IScriptItemExtensionPoint> GetScriptItemExtensionPoints(string module)
        {
            return this.GetScriptItemExtensionPoints(module, null);
        }

        public IEnumerable<IScriptItemExtensionPoint> GetScriptItemExtensionPoints(string module, string group)
        {
            return from e in this.Scripts
                   where e.Metadata.Module == module
                       && (string.IsNullOrEmpty(@group) || e.Metadata.Group == @group)
                   orderby e.Value.Order
                   select e.Value;
        }

        public IEnumerable<IEditPagePanelExtensionPoint> GetEditPagePanelExtensionPoints(string module)
        {
            return this.GetEditPagePanelExtensionPoints(module, null);
        }

        public IEnumerable<IEditPagePanelExtensionPoint> GetEditPagePanelExtensionPoints(string module, string group)
        {
            return from e in this.EditPagePanelExtensionPoints
                   where e.Metadata.Module == module
                        && (string.IsNullOrEmpty(@group) || e.Metadata.Group == @group)
                   orderby e.Value.Order
                   select e.Value;
        }

        public IEditPagePanelExtensionPoint GetEditPagePanelExtensionPointFirstByPriority(string module, string name)
        {
            return (from e in this.EditPagePanelExtensionPoints
                    where e.Metadata.Module == module && e.Metadata.Name == name
                    orderby e.Metadata.Priority
                    select e.Value).FirstOrDefault();
        }

        public IEnumerable<IContextMenuItemExtensionPoint> GetContextMenuItemExtensionPoints(string module)
        {
            return this.GetContextMenuItemExtensionPoints(module, null);
        }

        public IEnumerable<IContextMenuItemExtensionPoint> GetContextMenuItemExtensionPoints(string module, string group)
        {
            return from e in this.CtxMenuItemExtensionPoints
                   where e.Metadata.Module == module
                        && (string.IsNullOrEmpty(@group) || e.Metadata.Group == @group)
                   orderby e.Value.Order
                   select e.Value;
        }

        public IUserControlExtensionPoint GetUserControlExtensionPointFirstByPriority(string module, string name)
        {
            return (from e in this.UserControlExtensionPoints
                    where e.Metadata.Module == module && e.Metadata.Name == name
                    orderby e.Metadata.Priority
                    select e.Value).FirstOrDefault();
        }

        public IEnumerable<IUserControlExtensionPoint> GetUserControlExtensionPoints(string module, string group)
        {
            return from e in this.UserControlExtensionPoints
                   where e.Metadata.Module == module && e.Metadata.Group == @group
                   orderby e.Value.Order
                   select e.Value;
        }

        public IEnumerable<IMenuItemExtensionPoint> GetMenuItemExtensionPoints(string module)
        {
            return this.GetMenuItemExtensionPoints(module, null);
        }

        public IEnumerable<IMenuItemExtensionPoint> GetMenuItemExtensionPoints(string module, string group)
        {
            return this.GetMenuItemExtensionPoints(module, group, new NoFilter());
        }

        public IEnumerable<IMenuItemExtensionPoint> GetMenuItemExtensionPoints(string module, string group, IExtensionPointFilter filter)
        {
            return from e in this.MenuItems
                   where FilterCondition(e.Metadata, module, @group) && filter.Condition(e.Metadata)
                   orderby e.Value.Order
                   select e.Value;
        }

        public IEnumerable<IGridColumnExtensionPoint> GetGridColumnExtensionPoints(string module)
        {
            return this.GetGridColumnExtensionPoints(module, null);
        }

        public IEnumerable<IGridColumnExtensionPoint> GetGridColumnExtensionPoints(string module, string group)
        {
            return this.GetGridColumnExtensionPoints(module, group, new NoFilter());
        }

        public IEnumerable<IGridColumnExtensionPoint> GetGridColumnExtensionPoints(string module, string group, IExtensionPointFilter filter)
        {
            return from e in this.GridColumns
                   where FilterCondition(e.Metadata, module, @group) && filter.Condition(e.Metadata)
                   orderby e.Value.Order
                   select e.Value;
        }

        /// <summary>Gets the directory that is scanned for extensions.</summary>
        /// <returns>The probing directory.</returns>
        /// <remarks>
        /// On .NET Framework the application's assemblies lived in <c>&lt;web root&gt;/bin</c>. On
        /// modern .NET they live next to the host executable, so that folder is used whenever the
        /// legacy <c>bin</c> folder is not present.
        /// </remarks>
        private static string GetProbingDirectory()
        {
            try
            {
                var legacyBin = Path.Combine(Globals.ApplicationMapPath, "bin");
                if (Directory.Exists(legacyBin))
                {
                    return legacyBin;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or NullReferenceException or InvalidOperationException)
            {
                // The application path is not available (e.g. outside of a hosted process);
                // fall through to the assembly location below.
            }

            return AppContext.BaseDirectory;
        }

        private static SafeDirectoryCatalog InitializeCatalog()
        {
            // Any failure here is rethrown rather than swallowed: an empty catalog compiles and runs
            // perfectly while removing every third-party button, tab, menu item and grid column from
            // the UI, which is far harder to diagnose than a startup exception.
            return new SafeDirectoryCatalog(GetProbingDirectory());
        }

        private static bool FilterCondition(IExtensionPointData data, string module, string group)
        {
            return data.Module == module && (string.IsNullOrEmpty(@group) || data.Group == @group);
        }
    }
}
