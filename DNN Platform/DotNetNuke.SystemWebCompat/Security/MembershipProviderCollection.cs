// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.Security
{
    using System.Configuration.Provider;

    /// <summary>A collection of <see cref="MembershipProvider"/> instances.</summary>
    public sealed class MembershipProviderCollection : ProviderCollection
    {
        /// <summary>Gets the provider with the given name.</summary>
        /// <param name="name">The name of the provider.</param>
        /// <returns>The provider or <see langword="null"/>.</returns>
        public new MembershipProvider this[string name] => (MembershipProvider)base[name];

        /// <inheritdoc />
        public override void Add(ProviderBase provider)
        {
            if (provider is not MembershipProvider)
            {
                throw new ArgumentException("The provider must derive from MembershipProvider.", nameof(provider));
            }

            base.Add(provider);
        }
    }
}
