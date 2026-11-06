//
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
//

namespace DotNetNuke.Tests.UI.WatiN.Utilities;

using System;
using System.Diagnostics;
using System.Runtime.Versioning;

using Microsoft.Web.Administration;

/// <summary>
/// IIS automation used by the UI test fixtures. All members are no-ops on non-Windows hosts so
/// that the assembly builds and loads on Linux CI, where <c>Microsoft.Web.Administration</c>
/// cannot talk to IIS.
/// </summary>
public static class IISManager
{
    /// <summary>Creates (or re-creates) an application under the default web site.</summary>
    /// <param name="appName">The application name, used as the virtual path.</param>
    /// <param name="physicalPath">The physical path of the application.</param>
    public static void CreateIISApplication(string appName, string physicalPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        CreateIISApplicationCore(appName, physicalPath);
    }

    /// <summary>Creates (or re-creates) an application under the default web site in a given application pool.</summary>
    /// <param name="appName">The application name, used as the virtual path.</param>
    /// <param name="physicalPath">The physical path of the application.</param>
    /// <param name="appPool">The name of the application pool to assign.</param>
    public static void CreateIISApplication(string appName, string physicalPath, string appPool)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        CreateIISApplicationCore(appName, physicalPath, appPool);
    }

    /// <summary>Creates (or re-creates) a web site bound to port 80 for the given host header.</summary>
    /// <param name="siteName">The name of the site.</param>
    /// <param name="siteAddress">The host header for the binding.</param>
    /// <param name="physicalPath">The physical path of the site.</param>
    public static void CreateIISWebSite(string siteName, string siteAddress, string physicalPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        CreateIISWebSiteCore(siteName, siteAddress, physicalPath);
    }

    /// <summary>Recycles the application pool hosting the given application.</summary>
    /// <param name="appName">The application name, used as the virtual path.</param>
    public static void RecycleApplicationPool(string appName)
    {
        ////ServerManager iisManager = new ServerManager();
        ////Site defaultSite = iisManager.Sites["Default Web Site"];
        ////Microsoft.Web.Administration.Application app = defaultSite.Applications[String.Format("/{0}", appName)];

        ////if (app != null)
        ////{
        ////    string appPool = app.ApplicationPoolName;
        ////    ApplicationPool pool = iisManager.ApplicationPools[appPool];
        ////    pool.Recycle();
        ////}
    }

    /// <summary>Assigns an application to an application pool.</summary>
    /// <param name="appName">The application name, used as the virtual path.</param>
    /// <param name="appPool">The name of the application pool to assign.</param>
    public static void SetApplicationPool(string appName, string appPool)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        SetApplicationPoolCore(appName, appPool);
    }

    /// <summary>Kills every running IIS worker process.</summary>
    public static void KillW3WP()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        foreach (Process p in Process.GetProcessesByName("w3wp"))
        {
            p.Kill();
        }
    }

    [SupportedOSPlatform("windows")]
    private static void CreateIISApplicationCore(string appName, string physicalPath)
    {
        ServerManager iisManager = new ServerManager();
        Site defaultSite = iisManager.Sites["Default Web Site"];
        Microsoft.Web.Administration.Application app = defaultSite.Applications[string.Format("/{0}", appName)];

        // Remove application if it already exists
        if (app != null)
        {
            defaultSite.Applications.Remove(app);
            iisManager.CommitChanges();
        }

        defaultSite = iisManager.Sites["Default Web Site"];
        defaultSite.Applications.Add(string.Format("/{0}", appName), physicalPath);
        iisManager.CommitChanges();
        iisManager.Dispose();
    }

    [SupportedOSPlatform("windows")]
    private static void CreateIISApplicationCore(string appName, string physicalPath, string appPool)
    {
        ServerManager iisManager = new ServerManager();
        Site defaultSite = iisManager.Sites["Default Web Site"];
        Microsoft.Web.Administration.Application app = defaultSite.Applications[string.Format("/{0}", appName)];

        // Remove application if it already exists
        if (app != null)
        {
            defaultSite.Applications.Remove(app);
            iisManager.CommitChanges();
        }

        defaultSite.Applications.Add(string.Format("/{0}", appName), physicalPath);
        SetApplicationPoolCore(appName, appPool);
        iisManager.CommitChanges();
        iisManager.Dispose();
    }

    [SupportedOSPlatform("windows")]
    private static void CreateIISWebSiteCore(string siteName, string siteAddress, string physicalPath)
    {
        ServerManager iisManager = new ServerManager();
        Site site = iisManager.Sites[siteName];
        if (site != null)
        {
            iisManager.Sites.Remove(iisManager.Sites[siteName]);
            iisManager.CommitChanges();
        }

        iisManager.Sites.Add(siteName, "http", string.Concat("*:80:", siteAddress), physicalPath);
        iisManager.CommitChanges();
        iisManager.Dispose();
    }

    [SupportedOSPlatform("windows")]
    private static void SetApplicationPoolCore(string appName, string appPool)
    {
        ServerManager iisManager = new ServerManager();
        Site defaultSite = iisManager.Sites["Default Web Site"];
        Microsoft.Web.Administration.Application app = defaultSite.Applications[string.Format("/{0}", appName)];

        if (app != null)
        {
            app.ApplicationPoolName = appPool;
            iisManager.CommitChanges();
            iisManager.Dispose();
        }
    }
}
