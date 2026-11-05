// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Security.Profile
{
    using DotNetNuke.ComponentModel;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Framework;

    public abstract class ProfileProvider
    {
        public abstract bool CanEditProviderProperties { get; }

        // return the provider
        public static ProfileProvider Instance()
        {
            // Prefer the application's dependency-injection container. The legacy
            // ComponentFactory registration (populated from the provider configuration)
            // remains the fallback so that third-party providers keep working.
            return ServiceLocator.GetService<ProfileProvider>() ?? ComponentFactory.GetComponent<ProfileProvider>();
        }

        public abstract void GetUserProfile(ref UserInfo user);

        public abstract void UpdateUserProfile(UserInfo user);
    }
}
