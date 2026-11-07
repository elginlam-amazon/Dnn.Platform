// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Common.Utilities
{
    using System;
    using System.Collections.Specialized;
    using System.Text;

    /// <summary>
    /// Parses and builds URL query strings, replacing <c>System.Web.HttpUtility.ParseQueryString</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ASP.NET Core offers <c>Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery</c>, but it
    /// returns a <c>Dictionary&lt;string, StringValues&gt;</c>, whereas the platform's query-string
    /// code is written against <see cref="NameValueCollection"/> — including the
    /// <c>HttpValueCollection.ToString()</c> behaviour of re-encoding itself back into a query
    /// string. This type reproduces both so the call sites keep their semantics.
    /// </para>
    /// </remarks>
    public static class QueryStringUtils
    {
        /// <summary>Parses a query string into a name/value collection.</summary>
        /// <param name="query">
        /// The query string to parse. A leading <c>?</c> is ignored. May be <see langword="null"/>
        /// or empty, which produces an empty — but writable — collection.
        /// </param>
        /// <returns>
        /// A collection whose <see cref="object.ToString"/> renders the entries back into a
        /// URL-encoded query string.
        /// </returns>
        public static NameValueCollection ParseQueryString(string query)
        {
            var result = new QueryStringCollection();
            if (string.IsNullOrEmpty(query))
            {
                return result;
            }

            var span = query.AsSpan();
            if (span[0] == '?')
            {
                span = span.Slice(1);
            }

            foreach (var pairRange in span.Split('&'))
            {
                var pair = span[pairRange];
                if (pair.IsEmpty)
                {
                    continue;
                }

                var separator = pair.IndexOf('=');
                if (separator < 0)
                {
                    // A valueless entry is stored under the null key, matching HttpValueCollection.
                    result.Add(null, Uri.UnescapeDataString(pair.ToString().Replace('+', ' ')));
                    continue;
                }

                var name = pair.Slice(0, separator).ToString();
                var value = pair.Slice(separator + 1).ToString();
                result.Add(
                    Uri.UnescapeDataString(name.Replace('+', ' ')),
                    Uri.UnescapeDataString(value.Replace('+', ' ')));
            }

            return result;
        }

        /// <summary>Renders a name/value collection as a URL-encoded query string.</summary>
        /// <param name="collection">The collection to render.</param>
        /// <returns>The query string, without a leading <c>?</c>.</returns>
        public static string ToQueryString(NameValueCollection collection)
        {
            if (collection == null || collection.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            foreach (string key in collection.AllKeys)
            {
                var values = collection.GetValues(key);
                if (values == null)
                {
                    continue;
                }

                foreach (var value in values)
                {
                    if (builder.Length > 0)
                    {
                        builder.Append('&');
                    }

                    if (key != null)
                    {
                        builder.Append(Uri.EscapeDataString(key)).Append('=');
                    }

                    builder.Append(Uri.EscapeDataString(value ?? string.Empty));
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// A <see cref="NameValueCollection"/> that renders itself as a query string, reproducing
        /// the behaviour of the internal <c>System.Web.HttpValueCollection</c>.
        /// </summary>
        private sealed class QueryStringCollection : NameValueCollection
        {
            public QueryStringCollection()
                : base(StringComparer.OrdinalIgnoreCase)
            {
            }

            /// <inheritdoc />
            public override string ToString() => ToQueryString(this);
        }
    }
}
