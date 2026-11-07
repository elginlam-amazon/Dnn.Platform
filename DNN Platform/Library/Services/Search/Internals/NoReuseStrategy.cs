// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Search.Internals
{
    using Lucene.Net.Analysis;

    /// <summary>
    /// An <see cref="ReuseStrategy"/> that never reuses a filter chain, so
    /// <see cref="Analyzer.CreateComponents"/> runs for every single call.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Lucene.NET 3.0.3 called <c>Analyzer.TokenStream(field, reader)</c> for every document and
    /// every query, so the platform's analyzers could legitimately resolve per-call state —
    /// the portal's and culture's stop words, its synonym groups and the host's configured
    /// minimum/maximum word length — while building their filter chain.
    /// </para>
    /// <para>
    /// Lucene.NET 4.8 replaced that with <c>CreateComponents</c> plus a reuse strategy that caches
    /// the resulting <see cref="TokenStreamComponents"/> per thread (globally, by default).
    /// With caching, the state captured while analyzing the FIRST document is silently applied to
    /// every later document and query on that thread: a Spanish page would be indexed with the
    /// English stop-word list, and a portal would be queried with another portal's synonyms. Opting
    /// out of reuse restores the 3.0.3 contract these analyzers were written against.
    /// </para>
    /// </remarks>
    internal sealed class NoReuseStrategy : ReuseStrategy
    {
        /// <inheritdoc />
        public override TokenStreamComponents GetReusableComponents(Analyzer analyzer, string fieldName)
        {
            return null;
        }

        /// <inheritdoc />
        public override void SetReusableComponents(Analyzer analyzer, string fieldName, TokenStreamComponents components)
        {
            // Deliberately not stored: see the remarks on this type.
        }
    }
}
