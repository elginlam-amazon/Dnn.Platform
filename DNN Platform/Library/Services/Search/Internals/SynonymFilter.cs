// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Search.Internals
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;

    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Services.Search.Entities;
    using Lucene.Net.Analysis;
    using Lucene.Net.Analysis.TokenAttributes;

    /// <summary>SynonymFilter.</summary>
    /// <remarks>Implementation is inspired by sample code in Manning Lucene In Action 2nd Edition, pg. 133.</remarks>
    internal sealed class SynonymFilter : TokenFilter
    {
        private readonly Stack<string> synonymStack = new Stack<string>();
        private readonly ICharTermAttribute termAtt;
        private readonly IPositionIncrementAttribute posIncrAtt;
        private State current;

        /// <summary>Initializes a new instance of the <see cref="SynonymFilter"/> class.</summary>
        /// <param name="input">The token stream.</param>
        public SynonymFilter(TokenStream input)
            : base(input)
        {
            this.termAtt = this.AddAttribute<ICharTermAttribute>();
            this.posIncrAtt = this.AddAttribute<IPositionIncrementAttribute>();
        }

        /// <inheritdoc />
        public override bool IncrementToken()
        {
            // Pop buffered synonyms
            if (this.synonymStack.Count > 0)
            {
                var syn = this.synonymStack.Pop();
                this.RestoreState(this.current);
                this.termAtt.SetEmpty().Append(syn);

                // set position increment to 0
                this.posIncrAtt.PositionIncrement = 0;
                return true;
            }

            // read next token
            if (!this.m_input.IncrementToken())
            {
                return false;
            }

            // push synonyms onto stack
            if (this.AddAliasesToStack())
            {
                this.current = this.CaptureState(); // save current token
            }

            return true;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Clearing the buffered synonyms here is REQUIRED on Lucene.NET 4.8. In 3.0.3 the
        /// analyzer produced a fresh <see cref="TokenStream"/> for every call, so a filter
        /// instance never outlived one stream. 4.8's <c>Analyzer.ReuseStrategy</c> caches
        /// <c>TokenStreamComponents</c> per thread and reuses this filter chain, so a stream
        /// abandoned mid-stack (an exception while pushing aliases, or a consumer that stops
        /// before <see cref="IncrementToken"/> returns <see langword="false"/>) would otherwise
        /// leak its synonyms into the next document or query handled on that thread — producing
        /// wrong index terms or wrong query expansion, silently.
        /// </remarks>
        public override void Reset()
        {
            base.Reset();
            this.synonymStack.Clear();
            this.current = null;
        }

        private bool AddAliasesToStack()
        {
            var portalId = 0; // default
            string cultureCode;
            var searchDoc = Thread.GetData(Thread.GetNamedDataSlot(Constants.TlsSearchInfo)) as SearchDocument;
            if (searchDoc != null)
            {
                portalId = searchDoc.PortalId;
                cultureCode = searchDoc.CultureCode;
                if (string.IsNullOrEmpty(cultureCode))
                {
                    var portalInfo = PortalController.Instance.GetPortal(portalId);
                    if (portalInfo != null)
                    {
                        cultureCode = portalInfo.DefaultLanguage;
                    }
                }
            }
            else
            {
                cultureCode = Thread.CurrentThread.CurrentCulture.Name;
            }

            var synonyms = SearchHelper.Instance.GetSynonyms(portalId, cultureCode, this.termAtt.ToString()).ToArray();
            if (synonyms.Length == 0)
            {
                return false;
            }

            var cultureInfo = new CultureInfo(cultureCode);
            foreach (var synonym in synonyms)
            {
                this.synonymStack.Push(synonym.ToLower(cultureInfo));
            }

            return true;
        }
    }
}
