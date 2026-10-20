// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.Security
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;

    /// <summary>A collection of <see cref="MembershipUser"/> instances, keyed by user name.</summary>
    [Serializable]
    public sealed class MembershipUserCollection : IEnumerable<MembershipUser>, ICollection
    {
        private readonly OrderedDictionary users = new OrderedDictionary(StringComparer.CurrentCultureIgnoreCase);

        /// <inheritdoc />
        public int Count => this.users.Count;

        /// <inheritdoc />
        public bool IsSynchronized => false;

        /// <inheritdoc />
        public object SyncRoot => this;

        /// <summary>Gets the user with the given name.</summary>
        /// <param name="name">The user name.</param>
        /// <returns>The user, or <see langword="null"/>.</returns>
        public MembershipUser this[string name] => (MembershipUser)this.users[name];

        /// <summary>Adds a user to the collection.</summary>
        /// <param name="user">The user.</param>
        public void Add(MembershipUser user)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            this.users.Add(user.UserName, user);
        }

        /// <summary>Removes a user from the collection.</summary>
        /// <param name="name">The user name.</param>
        public void Remove(string name)
        {
            this.users.Remove(name);
        }

        /// <inheritdoc />
        public void CopyTo(Array array, int index)
        {
            this.users.Values.CopyTo(array, index);
        }

        /// <inheritdoc />
        public IEnumerator<MembershipUser> GetEnumerator()
        {
            foreach (MembershipUser user in this.users.Values)
            {
                yield return user;
            }
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}
