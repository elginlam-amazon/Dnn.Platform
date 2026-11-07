// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI
{
    using System;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI.ControlPanels;
    using DotNetNuke.UI.Modules;
    using DotNetNuke.UI.Utilities;

    public class UIUtilities
    {
        public static ModuleInfo GetSlaveModule(int tabId)
        {
            var key = GetControlKey();
            var moduleId = GetModuleId(key);

            ModuleInfo slaveModule = GetSlaveModule(moduleId, key, tabId);
            if (slaveModule != null)
            {
                var moduleControl = ModuleControlController.GetModuleControlByControlKey(key, slaveModule.ModuleDefID) ??
                                    ModuleControlController.GetModuleControlByControlKey(key, Null.NullInteger);
                if (moduleControl != null)
                {
                    slaveModule.ModuleControlId = moduleControl.ModuleControlID;
                }
            }

            return slaveModule;
        }

        public static bool IsLegacyUI(int moduleId, string key, int portalId)
        {
            var request = HttpContextSource.Current?.Request;
            var isLegacyUi = true;
            var currentPage = TabController.CurrentPage;
            var settings = PortalController.Instance.GetCurrentSettings();
            if (settings != null)
            {
                isLegacyUi = !(settings.EnablePopUps && !BrowserCapabilities.IsCrawler(request) && BrowserCapabilities.GetEcmaScriptVersion(request) >= new Version(1, 0));

                if (!isLegacyUi && !string.IsNullOrEmpty(key))
                {
                    var slaveModule = GetSlaveModule(moduleId, key, currentPage.TabID);
                    if (slaveModule != null)
                    {
                        var moduleControl = ModuleControlController.GetModuleControlByControlKey(key, slaveModule.ModuleDefID) ??
                                            ModuleControlController.GetModuleControlByControlKey(key, Null.NullInteger);
                        if (moduleControl != null)
                        {
                            isLegacyUi = !moduleControl.SupportsPopUps;
                        }
                    }
                }
            }

            return isLegacyUi;
        }

        public static bool IsLegacyUI(int portalId)
        {
            var key = GetControlKey();
            var moduleId = GetModuleId(key);

            return IsLegacyUI(moduleId, key, portalId);
        }

        internal static string GetControlKey()
        {
            var queryString = HttpContextSource.CurrentOrThrow.Request.GetQueryString();

            string key = string.Empty;
            if (queryString["ctl"] != null)
            {
                key = queryString["ctl"];
            }

            return key;
        }

        internal static int GetModuleId(string key)
        {
            var queryString = HttpContextSource.CurrentOrThrow.Request.GetQueryString();

            int moduleId = -1;
            if (queryString["mid"] != null)
            {
                if (!int.TryParse(queryString["mid"], out moduleId))
                {
                    moduleId = -1;
                }
            }

            if (queryString["moduleid"] != null && (key.Equals("module", StringComparison.OrdinalIgnoreCase) || key.Equals("help", StringComparison.OrdinalIgnoreCase)))
            {
                if (!int.TryParse(queryString["moduleid"], out moduleId))
                {
                    moduleId = -1;
                }
            }

            return moduleId;
        }

        internal static string GetRenderMode()
        {
            var queryString = HttpContextSource.CurrentOrThrow.Request.GetQueryString();

            var renderMode = string.Empty;
            if (queryString["render"] != null)
            {
                renderMode = queryString["render"];
            }

            return renderMode;
        }

        internal static ModuleInfo GetSlaveModule(int moduleId, string key, int tabId)
        {
            var queryString = HttpContextSource.CurrentOrThrow.Request.GetQueryString();

            ModuleInfo slaveModule = null;
            if (moduleId != -1)
            {
                ModuleInfo module = ModuleController.Instance.GetModule(moduleId, tabId, false);
                if (module != null)
                {
                    slaveModule = module.Clone();
                }
            }

            if (slaveModule == null)
            {
                slaveModule = new ModuleInfo { ModuleID = moduleId, ModuleDefID = -1, TabID = tabId, InheritViewPermissions = true };
            }

            if (queryString["moduleid"] != null && (key.Equals("module", StringComparison.OrdinalIgnoreCase) || key.Equals("help", StringComparison.OrdinalIgnoreCase)))
            {
                slaveModule.ModuleDefID = -1;
            }

            if (queryString["dnnprintmode"] != "true")
            {
                slaveModule.ModuleTitle = string.Empty;
            }

            slaveModule.Header = string.Empty;
            slaveModule.Footer = string.Empty;
            slaveModule.StartDate = DateTime.MinValue;
            slaveModule.EndDate = DateTime.MaxValue;
            slaveModule.Visibility = VisibilityState.None;
            slaveModule.Color = string.Empty;
            slaveModule.Border = string.Empty;
            slaveModule.DisplayTitle = true;
            slaveModule.DisplayPrint = false;
            slaveModule.DisplaySyndicate = false;

            return slaveModule;
        }

        /// <summary>Gets the path to the local resource file associated to the control.</summary>
        /// <param name="ctrl">The control.</param>
        /// <returns>The path to the resource file.</returns>
        /// <remarks>
        /// The <c>ctrl</c> parameter was a <c>System.Web.UI.Control</c>; it is now the DNN
        /// component tree node (<see cref="IClientApiControl"/>). The <c>UserControl</c> and
        /// <c>Page</c> branches, which guessed the <c>.ascx.resx</c>/<c>.aspx.resx</c> file from
        /// <c>TemplateSourceDirectory</c>, now use <see cref="DnnComponentBase.TemplateSourceDirectory"/>,
        /// which is derived from the markup source the control was loaded from.
        /// </remarks>
        internal static string GetLocalResourceFile(IClientApiControl ctrl)
        {
            string resourceFileName = Null.NullString;

            while (ctrl != null)
            {
                if (ctrl is IModuleControl moduleControl)
                {
                    resourceFileName = moduleControl.LocalResourceFile;
                    break;
                }

                if (ctrl is ControlPanelBase controlPanel)
                {
                    resourceFileName = controlPanel.LocalResourceFile;
                    break;
                }

                if (ctrl is DnnComponentBase component && !string.IsNullOrEmpty(component.ControlSrc))
                {
                    var typeName = component.GetType().BaseType?.Name ?? component.GetType().Name;
                    var candidate = $"{component.TemplateSourceDirectory}/{Localization.LocalResourceDirectory}/{typeName}.ascx.resx";
                    resourceFileName = candidate;
                    if (System.IO.File.Exists(HttpContextSource.MapPath(candidate)))
                    {
                        break;
                    }
                }

                ctrl = ctrl.Parent;
            }

            return resourceFileName;
        }
    }
}
