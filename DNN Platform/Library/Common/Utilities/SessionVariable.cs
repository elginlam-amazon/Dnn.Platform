// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Common.Utilities
{
    using System;
    using System.Text.Json;

    using Microsoft.AspNetCore.Http;

    /// <summary>Wrapper class for <see cref="ISession"/> (the ASP.NET Core replacement for <c>HttpContext.Session</c>).</summary>
    /// <typeparam name="T">The type of the value to be stored.</typeparam>
    /// <remarks>
    /// ASP.NET Core's <see cref="ISession"/> is a byte-oriented store, so values are persisted as
    /// JSON (<see cref="System.Text.Json"/>) rather than as live CLR object graphs. As a
    /// consequence a value read back is a copy, not the same instance that was written, and the
    /// stored type must be JSON-serializable.
    /// <para>
    /// Note that session state is only reachable while an HTTP request is in flight; it is NOT
    /// available during Blazor Server interactive (SignalR) rendering. See
    /// <see cref="DotNetNuke.Common.HttpContextSource"/>.
    /// </para>
    /// </remarks>
    public class SessionVariable<T> : StateVariable<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SessionVariable{T}"/> class.
        /// Initializes a new session item variable.
        /// </summary>
        /// <param name="key">
        /// The key to use for storing the value in the items.
        /// </param>
        public SessionVariable(string key)
            : base(key)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionVariable{T}"/> class.
        /// Initializes a new session item variable with a initializer.
        /// </summary>
        /// <param name="key">The key to use for storing the value in the current session.</param>
        /// <param name="initializer">A function that is called in order to create a default value per session.</param>
        public SessionVariable(string key, Func<T> initializer)
            : base(key, initializer)
        {
        }

        private static ISession CurrentItems
        {
            get
            {
                var current = DotNetNuke.Common.HttpContextSource.Current;
                if (current == null)
                {
                    throw new InvalidOperationException("No HttpContext is not available.");
                }

                var items = current.Features.Get<Microsoft.AspNetCore.Http.Features.ISessionFeature>() == null
                    ? null
                    : current.Session;
                if (items == null)
                {
                    throw new InvalidOperationException("No Session State available on current HttpContext.");
                }

                return items;
            }
        }

        /// <inheritdoc />
        protected override object this[string key]
        {
            get
            {
                var json = CurrentItems.GetString(key);
                if (json == null)
                {
                    return null;
                }

                return JsonSerializer.Deserialize<T>(json);
            }

            set
            {
                var items = CurrentItems;
                if (value == null)
                {
                    items.Remove(key);
                    return;
                }

                items.SetString(key, JsonSerializer.Serialize((T)value));
            }
        }

        /// <inheritdoc />
        protected override void Remove(string key)
        {
            CurrentItems.Remove(key);
        }
    }
}
