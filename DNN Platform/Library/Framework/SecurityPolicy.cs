// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Framework
{
    using System;
    using System.Net;
    using System.Security;
    using System.Security.Permissions;
    using System.Web;

    using DotNetNuke.Internal.SourceGenerators;

    /// <summary>Provides the ability to get information about CLR permissions.</summary>
    public partial class SecurityPolicy
    {
        public const string ReflectionPermission = "ReflectionPermission";
        public const string WebPermission = "WebPermission";
        public const string AspNetHostingPermission = "AspNetHostingPermission";
        public const string UnManagedCodePermission = "UnManagedCodePermission";
        private static bool initialized;
        private static bool reflectionPermission;
        private static bool webPermission;
        private static bool aspNetHostingPermission;
        private static bool unManagedCodePermission;

        public static string Permissions
        {
            get
            {
                string strPermissions = string.Empty;
                if (HasReflectionPermission())
                {
                    strPermissions += ", " + ReflectionPermission;
                }

                if (HasWebPermission())
                {
                    strPermissions += ", " + WebPermission;
                }

                if (HasAspNetHostingPermission())
                {
                    strPermissions += ", " + AspNetHostingPermission;
                }

                if (!string.IsNullOrEmpty(strPermissions))
                {
                    strPermissions = strPermissions.Substring(2);
                }

                return strPermissions;
            }
        }

        public static bool HasAspNetHostingPermission()
        {
            GetPermissions();
            return aspNetHostingPermission;
        }

        public static bool HasReflectionPermission()
        {
            GetPermissions();
            return reflectionPermission;
        }

        public static bool HasWebPermission()
        {
            GetPermissions();
            return webPermission;
        }

        public static bool HasUnManagedCodePermission()
        {
            GetPermissions();
            return unManagedCodePermission;
        }

        public static bool HasPermissions(string permissions, ref string permission)
        {
            bool hasPermission = true;
            if (!string.IsNullOrEmpty(permissions))
            {
                foreach (string per in (permissions + ";").Split(Convert.ToChar(";")))
                {
                    if (!string.IsNullOrEmpty(per.Trim()))
                    {
                        permission = per;
                        switch (permission)
                        {
                            case AspNetHostingPermission:
                                if (HasAspNetHostingPermission() == false)
                                {
                                    hasPermission = false;
                                }

                                break;
                            case ReflectionPermission:
                                if (HasReflectionPermission() == false)
                                {
                                    hasPermission = false;
                                }

                                break;
                            case UnManagedCodePermission:
                                if (HasUnManagedCodePermission() == false)
                                {
                                    hasPermission = false;
                                }

                                break;
                            case WebPermission:
                                if (HasWebPermission() == false)
                                {
                                    hasPermission = false;
                                }

                                break;
                        }
                    }
                }
            }

            return hasPermission;
        }

        private static void GetPermissions()
        {
            if (!initialized)
            {
                // Code access security (partial trust) does not exist on .NET, all code runs with full trust.
                reflectionPermission = true;
                webPermission = true;
                aspNetHostingPermission = true;
                unManagedCodePermission = true;
                initialized = true;
            }
        }
    }
}
