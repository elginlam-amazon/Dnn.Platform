// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Search.Internals
{
    using System.Collections.Generic;

    using Lucene.Net.Search.VectorHighlight;

    using WeightedPhraseInfo = Lucene.Net.Search.VectorHighlight.FieldPhraseList.WeightedPhraseInfo;

    /// <summary>
    /// An <see cref="IFragListBuilder"/> that builds each highlight fragment around the match it
    /// contains, leaving a small margin of leading context.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This reproduces the behaviour of Lucene 3.0.3's <c>SimpleFragListBuilder</c>, which the
    /// platform's search snippets ("... brown <b>fox</b> jumps over the lazy dog") have always been
    /// built with. Lucene 4.8 rewrote that builder: it now tiles the field into windows starting at
    /// character 0 and keeps the first window the match falls into, so the snippet for a hit near
    /// the end of a long body begins at the start of the body instead of near the hit — the text a
    /// user reads under a search result no longer shows the context of their search term.
    /// </para>
    /// <para>
    /// The algorithm is the 3.0.3 one: for the first unconsumed phrase, open a window at
    /// <c>phraseStart - margin</c> (never before the end of the previous window), extend it to
    /// <c>fragCharSize</c> characters (or to the end of the phrase, whichever is longer), absorb
    /// every following phrase that fits entirely inside it, and emit it.
    /// </para>
    /// </remarks>
    internal sealed class MatchCentredFragListBuilder : IFragListBuilder
    {
        /// <summary>The number of characters of context kept in front of the match.</summary>
        private const int Margin = 6;

        /// <inheritdoc />
        public FieldFragList CreateFieldFragList(FieldPhraseList fieldPhraseList, int fragCharSize)
        {
            var fragList = new SimpleFieldFragList(fragCharSize);
            var phrases = fieldPhraseList.PhraseList;
            var startOffset = 0;

            for (var index = 0; index < phrases.Count;)
            {
                var phraseInfo = phrases[index++];
                if (phraseInfo is null || phraseInfo.StartOffset < startOffset)
                {
                    // The phrase falls inside a window that was already emitted.
                    continue;
                }

                var windowStart = phraseInfo.StartOffset - Margin < startOffset
                    ? startOffset
                    : phraseInfo.StartOffset - Margin;
                var windowEnd = windowStart + fragCharSize;
                if (phraseInfo.EndOffset > windowEnd)
                {
                    windowEnd = phraseInfo.EndOffset;
                }

                startOffset = windowEnd;

                var phrasesInWindow = new List<WeightedPhraseInfo> { phraseInfo };
                while (index < phrases.Count)
                {
                    var next = phrases[index];
                    if (next is null || next.EndOffset > windowEnd)
                    {
                        break;
                    }

                    phrasesInWindow.Add(next);
                    index++;
                }

                fragList.Add(windowStart, windowEnd, phrasesInWindow);
            }

            return fragList;
        }
    }
}
