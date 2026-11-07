// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Search.Internals
{
    using System.Text;

    using Lucene.Net.Search.VectorHighlight;

    /// <summary>
    /// An <see cref="IBoundaryScanner"/> that takes the fragment offsets exactly as the fragment
    /// list produced them.
    /// </summary>
    /// <remarks>
    /// Lucene 3.0.3's fragments builder cut the snippet at the computed offsets, so a snippet could
    /// start or end in the middle of a word — the platform's search results have always shown
    /// "...m are <b>riding</b> their bikes...". Lucene 4.8 introduced boundary scanning and defaults
    /// to <c>SimpleBoundaryScanner</c>, which silently widens every snippet to the nearest word
    /// boundary (and, because it only scans 20 characters, widens inconsistently). Keeping the
    /// offsets exact preserves the platform's long-standing snippet contract.
    /// </remarks>
    internal sealed class ExactOffsetBoundaryScanner : IBoundaryScanner
    {
        /// <inheritdoc />
        public int FindStartOffset(StringBuilder buffer, int start)
        {
            return start;
        }

        /// <inheritdoc />
        public int FindEndOffset(StringBuilder buffer, int start)
        {
            return start;
        }
    }
}
