// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Security.Roles
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    using DotNetNuke.ComponentModel;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Framework;

    public abstract class RoleProvider
    {
        // return the provider
        public static RoleProvider Instance()
        {
            // Prefer the application's dependency-injection container. The legacy
            // ComponentFactory registration (populated from the provider configuration)
            // remains the fallback so that third-party providers keep working.
            return ServiceLocator.GetService<RoleProvider>() ?? ComponentFactory.GetComponent<RoleProvider>();
        }

        public virtual bool CreateRole(RoleInfo role)
        {
            throw new NotImplementedException();
        }

        public virtual void DeleteRole(RoleInfo role)
        {
            throw new NotImplementedException();
        }

        public abstract ArrayList GetRoles(int portalId);

        public abstract IList<RoleInfo> GetRolesBasicSearch(int portalID, int pageSize, string filterBy);

        public virtual IDictionary<string, string> GetRoleSettings(int roleId)
        {
            return new Dictionary<string, string>();
        }

        public abstract void UpdateRole(RoleInfo role);

        public virtual void UpdateRoleSettings(RoleInfo role)
        {
        }

        // Role Groups
        public abstract int CreateRoleGroup(RoleGroupInfo roleGroup);

        public abstract void DeleteRoleGroup(RoleGroupInfo roleGroup);

        public abstract RoleGroupInfo GetRoleGroup(int portalId, int roleGroupId);

        public abstract ArrayList GetRoleGroups(int portalId);

        public abstract void UpdateRoleGroup(RoleGroupInfo roleGroup);

        public abstract bool AddUserToRole(int portalId, UserInfo user, UserRoleInfo userRole);

        public abstract UserRoleInfo GetUserRole(int portalId, int userId, int roleId);

        public abstract ArrayList GetUserRoles(int portalId, string username, string rolename);

        public abstract ArrayList GetUsersByRoleName(int portalId, string roleName);

        public abstract void RemoveUserFromRole(int portalId, UserInfo user, UserRoleInfo userRole);

        public abstract void UpdateUserRole(UserRoleInfo userRole);

        public virtual RoleGroupInfo GetRoleGroupByName(int portalID, string roleGroupName)
        {
            return null;
        }

        public virtual IList<UserRoleInfo> GetUserRoles(UserInfo user, bool includePrivate)
        {
            throw new NotImplementedException();
        }
    }
}
