// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Framework
{
    using System;

    using DotNetNuke.Internal.SourceGenerators;

    /// <summary>Provides the ability to get information about CLR permissions.</summary>
    /// <remarks>
    /// Code Access Security (CAS) was removed from .NET (Core) onwards: partial trust and
    /// <c>AspNetHostingPermission</c> no longer exist, and all managed code runs fully trusted.
    /// The members below are kept for API compatibility and always report the permission as
    /// granted, which matches the only trust level modern .NET supports (full trust).
    /// </remarks>
    public partial class SecurityPolicy
    {
        public const string ReflectionPermission = "ReflectionPermission";
        public const string WebPermission = "WebPermission";
        public const string AspNetHostingPermission = "AspNetHostingPermission";
        public const string UnManagedCodePermission = "UnManagedCodePermission";

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

        /// <summary>Gets a value indicating whether the AspNetHosting permission is granted.</summary>
        /// <returns><see langword="true"/>; modern .NET always runs in full trust.</returns>
        public static bool HasAspNetHostingPermission()
        {
            // CAS/partial trust no longer exists in modern .NET - the permission is always granted.
            return true;
        }

        /// <summary>Gets a value indicating whether the Reflection permission is granted.</summary>
        /// <returns><see langword="true"/>; modern .NET always runs in full trust.</returns>
        public static bool HasReflectionPermission()
        {
            // CAS/partial trust no longer exists in modern .NET - the permission is always granted.
            return true;
        }

        /// <summary>Gets a value indicating whether the Web permission is granted.</summary>
        /// <returns><see langword="true"/>; modern .NET always runs in full trust.</returns>
        public static bool HasWebPermission()
        {
            // CAS/partial trust no longer exists in modern .NET - the permission is always granted.
            return true;
        }

        /// <summary>Gets a value indicating whether the UnManagedCode permission is granted.</summary>
        /// <returns><see langword="true"/>; modern .NET always runs in full trust.</returns>
        public static bool HasUnManagedCodePermission()
        {
            // CAS/partial trust no longer exists in modern .NET - the permission is always granted.
            return true;
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

    }
}
