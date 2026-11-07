// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Search.Internals
{
    using System.Collections.Generic;

    using Lucene.Net.Search.VectorHighlight;

    using Toffs = Lucene.Net.Search.VectorHighlight.FieldPhraseList.WeightedPhraseInfo.Toffs;
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

                foreach (var phrase in phrasesInWindow)
                {
                    MergeConsecutiveTermOffsets(phrase);
                }

                MergeAdjacentPhrases(phrasesInWindow);

                fragList.Add(windowStart, windowEnd, phrasesInWindow);
            }

            return fragList;
        }

        /// <summary>
        /// Collapses matches that touch each other in the text into a single highlighted span.
        /// </summary>
        /// <remarks>
        /// Separate matches can still end up side by side with no character between them - most
        /// visibly with analyzers that emit one token per character, where a CJK keyword matches as
        /// several independent single-character matches and the snippet renders as
        /// <c>&lt;b&gt;中&lt;/b&gt;&lt;b&gt;文&lt;/b&gt;</c> rather than <c>&lt;b&gt;中文&lt;/b&gt;</c>.
        /// Because the snippet is emitted with one pair of tags for every match, two spans that are
        /// contiguous carry exactly the same information as the single span that covers both, so
        /// coalescing them removes the doubled tags without losing any highlight.
        /// </remarks>
        /// <param name="phrases">The matches of one fragment, in ascending start-offset order. Coalesced matches are removed from the list.</param>
        private static void MergeAdjacentPhrases(IList<WeightedPhraseInfo> phrases)
        {
            for (var index = 0; index + 1 < phrases.Count;)
            {
                var currentSpans = phrases[index].TermsOffsets;
                var nextSpans = phrases[index + 1].TermsOffsets;

                // Only a match that is a single span can be absorbed whole into its predecessor.
                if (currentSpans is null || currentSpans.Count == 0 || currentSpans.IsReadOnly ||
                    nextSpans is null || nextSpans.Count != 1)
                {
                    index++;
                    continue;
                }

                var last = currentSpans[currentSpans.Count - 1];
                var candidate = nextSpans[0];
                if (candidate.StartOffset > last.EndOffset)
                {
                    // There is text between the two matches, so they are genuinely separate spans.
                    index++;
                    continue;
                }

                if (candidate.EndOffset > last.EndOffset)
                {
                    last.EndOffset = candidate.EndOffset;
                }

                phrases.RemoveAt(index + 1);
            }
        }

        /// <summary>
        /// Collapses the character ranges of terms that are consecutive within a single phrase
        /// match into one range, so the phrase is emitted as a single highlighted span.
        /// </summary>
        /// <remarks>
        /// Lucene 3.0.3's <c>WeightedPhraseInfo</c> did this while it built the phrase: each term
        /// whose position was exactly one past the previous term's extended the previous character
        /// range instead of starting a new one, and the fragments builder then wrapped each range
        /// in one pair of tags. Lucene 4.8 keeps one range per term, so every phrase match is
        /// emitted as one tag pair per term, which splits a multi-word phrase match into one tag
        /// per word.
        /// </remarks>
        /// <param name="phraseInfo">The phrase match whose term ranges should be collapsed.</param>
        private static void MergeConsecutiveTermOffsets(WeightedPhraseInfo phraseInfo)
        {
            var offsets = phraseInfo.TermsOffsets;
            var terms = phraseInfo.TermsInfos;
            if (offsets is null || offsets.IsReadOnly || terms is null || terms.Count < 2)
            {
                return;
            }

            var merged = new List<Toffs>();
            var previousPosition = 0;
            var isFirst = true;
            foreach (var term in terms)
            {
                if (!isFirst && term.Position - previousPosition == 1)
                {
                    // Adjacent position in the same phrase: extend the span over this term too.
                    merged[merged.Count - 1].EndOffset = term.EndOffset;
                }
                else
                {
                    merged.Add(new Toffs(term.StartOffset, term.EndOffset));
                }

                previousPosition = term.Position;
                isFirst = false;
            }

            if (merged.Count >= offsets.Count)
            {
                // Nothing was consecutive, so leave the phrase exactly as Lucene built it.
                return;
            }

            offsets.Clear();
            foreach (var span in merged)
            {
                offsets.Add(span);
            }
        }
    }
}
