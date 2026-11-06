// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Razor
{
    using System.Collections;
    using System.Collections.Generic;

    /// <summary>The dictionary behind <see cref="DotNetNukeWebPage.PageData"/>.</summary>
    /// <remarks>
    /// Replaces <c>System.Web.WebPages.PageDataDictionary{TKey,TValue}</c>. The only behaviour that
    /// distinguishes it from a plain <see cref="Dictionary{TKey,TValue}"/> is the one the razor
    /// scripts depend on: reading a key that was never written returns <see langword="null"/>
    /// instead of throwing <see cref="KeyNotFoundException"/>, exactly as the net48
    /// <c>WebPageBase.PageData</c> did. A layout that reads <c>PageData["ShowList"]</c> before any
    /// content page has written it must render the "not set" branch, not fail the request.
    /// </remarks>
    internal sealed class RazorPageDataDictionary : IDictionary<object, object>
    {
        private readonly Dictionary<object, object> inner = new Dictionary<object, object>();

        /// <inheritdoc/>
        public int Count => this.inner.Count;

        /// <inheritdoc/>
        public bool IsReadOnly => false;

        /// <inheritdoc/>
        public ICollection<object> Keys => this.inner.Keys;

        /// <inheritdoc/>
        public ICollection<object> Values => this.inner.Values;

        /// <inheritdoc/>
        /// <remarks>Getting an absent key returns <see langword="null"/>; it does not throw.</remarks>
        public object this[object key]
        {
            get => this.inner.TryGetValue(key, out var value) ? value : null;
            set => this.inner[key] = value;
        }

        /// <inheritdoc/>
        public void Add(object key, object value) => this.inner.Add(key, value);

        /// <inheritdoc/>
        public void Add(KeyValuePair<object, object> item) => this.inner.Add(item.Key, item.Value);

        /// <inheritdoc/>
        public void Clear() => this.inner.Clear();

        /// <inheritdoc/>
        public bool Contains(KeyValuePair<object, object> item) =>
            ((ICollection<KeyValuePair<object, object>>)this.inner).Contains(item);

        /// <inheritdoc/>
        public bool ContainsKey(object key) => this.inner.ContainsKey(key);

        /// <inheritdoc/>
        public void CopyTo(KeyValuePair<object, object>[] array, int arrayIndex) =>
            ((ICollection<KeyValuePair<object, object>>)this.inner).CopyTo(array, arrayIndex);

        /// <inheritdoc/>
        public IEnumerator<KeyValuePair<object, object>> GetEnumerator() => this.inner.GetEnumerator();

        /// <inheritdoc/>
        public bool Remove(object key) => this.inner.Remove(key);

        /// <inheritdoc/>
        public bool Remove(KeyValuePair<object, object> item) =>
            ((ICollection<KeyValuePair<object, object>>)this.inner).Remove(item);

        /// <inheritdoc/>
        public bool TryGetValue(object key, out object value) => this.inner.TryGetValue(key, out value);

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => this.inner.GetEnumerator();
    }
}
