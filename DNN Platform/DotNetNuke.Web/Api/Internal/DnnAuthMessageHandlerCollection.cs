// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api.Internal
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    using DotNetNuke.Web.Api.Auth;

    /// <summary>The default <see cref="IDnnAuthMessageHandlerCollection"/> implementation.</summary>
    internal sealed class DnnAuthMessageHandlerCollection : IDnnAuthMessageHandlerCollection
    {
        private readonly List<IDnnAuthMessageHandler> handlers = new();
        private readonly object syncRoot = new();

        /// <inheritdoc />
        public int Count
        {
            get
            {
                lock (this.syncRoot)
                {
                    return this.handlers.Count;
                }
            }
        }

        /// <inheritdoc />
        public IDnnAuthMessageHandler this[int index]
        {
            get
            {
                lock (this.syncRoot)
                {
                    return this.handlers[index];
                }
            }
        }

        /// <inheritdoc />
        public void Add(IDnnAuthMessageHandler handler)
        {
            lock (this.syncRoot)
            {
                this.handlers.Add(handler);
            }
        }

        /// <inheritdoc />
        public IEnumerator<IDnnAuthMessageHandler> GetEnumerator()
        {
            lock (this.syncRoot)
            {
                return this.handlers.ToArray().AsEnumerable().GetEnumerator();
            }
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
