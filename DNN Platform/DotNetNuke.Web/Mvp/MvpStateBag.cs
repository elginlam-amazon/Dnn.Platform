// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Mvp
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Text.Json;

    using DotNetNuke.Internal.SourceGenerators;

    /// <summary>The explicit, component owned state of an MVP view or presenter.</summary>
    /// <remarks>
    /// <para>
    /// <b>This is the replacement for WebForms view state.</b> There is no view state in Blazor: no
    /// hidden <c>__VIEWSTATE</c> field is posted back, so <c>System.Web.UI.StateBag</c> has no
    /// equivalent. The state a view or presenter used to round-trip through view state now lives in
    /// a plain dictionary owned by the component that declares it
    /// (<see cref="ModuleViewBase.State"/>), and it survives the pre-render to interactive
    /// transition through Blazor's <c>PersistentComponentState</c> rather than through the page.
    /// </para>
    /// <para>
    /// Because the dictionary is ordinary component state, values live for as long as the component
    /// does — which for an interactive Blazor circuit is the whole user session with the component,
    /// not a single request. The pair <see cref="Persist"/>/<see cref="Restore"/> exists only for
    /// the hand-off from the pre-rendered server render to the interactive circuit, and serializes
    /// each entry with its type so the value comes back as the type it went in as.
    /// </para>
    /// </remarks>
    [DnnDeprecated(9, 2, 0, "Replace WebFormsMvp and DotNetNuke.Web.Mvp with MVC or SPA patterns instead")]
    public partial class MvpStateBag : IEnumerable<KeyValuePair<string, object>>
    {
        private readonly Dictionary<string, object> values = new Dictionary<string, object>(StringComparer.Ordinal);

        /// <summary>Gets the keys currently stored.</summary>
        public ICollection<string> Keys => this.values.Keys;

        /// <summary>Gets the number of entries currently stored.</summary>
        public int Count => this.values.Count;

        /// <summary>Gets or sets the value stored under the given key.</summary>
        /// <param name="key">The state key.</param>
        /// <returns>The value, or <see langword="null"/> when nothing is stored under the key.</returns>
        /// <remarks>
        /// Returns <see langword="null"/> for an unknown key, which is the behaviour
        /// <c>StateBag</c>'s indexer had and which the attribute based serializer relies on.
        /// </remarks>
        public object this[string key]
        {
            get => key != null && this.values.TryGetValue(key, out var value) ? value : null;

            set
            {
                ArgumentNullException.ThrowIfNull(key);

                this.values[key] = value;
            }
        }

        /// <summary>Removes the entry stored under the given key.</summary>
        /// <param name="key">The state key.</param>
        /// <returns><see langword="true"/> when an entry was removed.</returns>
        public bool Remove(string key) => key != null && this.values.Remove(key);

        /// <summary>Removes every entry.</summary>
        public void Clear() => this.values.Clear();

        /// <summary>Determines whether an entry is stored under the given key.</summary>
        /// <param name="key">The state key.</param>
        /// <returns><see langword="true"/> when the key is present.</returns>
        public bool ContainsKey(string key) => key != null && this.values.ContainsKey(key);

        /// <summary>Gets the value stored under the given key, typed.</summary>
        /// <typeparam name="T">The expected type of the value.</typeparam>
        /// <param name="key">The state key.</param>
        /// <param name="defaultValue">The value to return when the key is absent.</param>
        /// <returns>The stored value, or <paramref name="defaultValue"/>.</returns>
        public T Get<T>(string key, T defaultValue = default)
        {
            return this[key] is T typed ? typed : defaultValue;
        }

        /// <summary>Serializes this state so it can be handed to an interactive Blazor circuit.</summary>
        /// <returns>The serialized entries, keyed by state key.</returns>
        /// <remarks>Each entry carries its type name so <see cref="Restore"/> can rebuild the value.</remarks>
        public Dictionary<string, MvpStateEntry> Persist()
        {
            var persisted = new Dictionary<string, MvpStateEntry>(StringComparer.Ordinal);
            foreach (var entry in this.values)
            {
                if (entry.Value == null)
                {
                    persisted[entry.Key] = new MvpStateEntry();
                    continue;
                }

                var type = entry.Value.GetType();
                persisted[entry.Key] = new MvpStateEntry
                {
                    TypeName = type.AssemblyQualifiedName,
                    Json = JsonSerializer.Serialize(entry.Value, type),
                };
            }

            return persisted;
        }

        /// <summary>Repopulates this state from the entries <see cref="Persist"/> produced.</summary>
        /// <param name="persisted">The serialized entries.</param>
        public void Restore(Dictionary<string, MvpStateEntry> persisted)
        {
            if (persisted == null)
            {
                return;
            }

            foreach (var entry in persisted)
            {
                if (entry.Value?.TypeName == null)
                {
                    this.values[entry.Key] = null;
                    continue;
                }

                var type = Type.GetType(entry.Value.TypeName, throwOnError: false);
                if (type == null)
                {
                    // the type is no longer available; the entry is dropped rather than failing the
                    // whole render, which is what a view state type load failure did too.
                    continue;
                }

                try
                {
                    this.values[entry.Key] = JsonSerializer.Deserialize(entry.Value.Json ?? "null", type);
                }
                catch (JsonException)
                {
                    // a value that cannot be read back is treated as absent.
                }
            }
        }

        /// <inheritdoc />
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => this.values.GetEnumerator();

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}
