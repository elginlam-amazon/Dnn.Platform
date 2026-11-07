// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Content;
    using DotNetNuke.Entities.Content.Taxonomy;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>A tags control.</summary>
    /// <param name="vocabularyController">The vocabulary controller.</param>
    /// <param name="termController">The term controller.</param>
    /// <remarks>
    /// This was a <c>WebControl, IPostBackEventHandler, IPostBackDataHandler</c>. On ASP.NET Core it
    /// is a <see cref="DnnComponentBase"/> rendering the identical markup (the outer direction div,
    /// the <c>ul.categories</c>/<c>ul.tags</c> lists with their <c>first_tag</c>/<c>last_tag</c>
    /// classes, the term anchors with <c>rel="tag"</c>, and the Add/Save/Cancel buttons). The
    /// Add/Save/Cancel postbacks became click handlers that call the preserved
    /// <see cref="RaisePostBackEvent"/>, and the typed tags are captured by
    /// <see cref="LoadPostData"/> from the input's <c>change</c> event, so the term add logic in
    /// <c>SaveTags</c> is byte-for-byte the original.
    /// </remarks>
    public class Tags(IVocabularyController vocabularyController, ITermController termController)
        : DnnComponentBase
    {
        private readonly IVocabularyController vocabularyController = vocabularyController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IVocabularyController>();
        private readonly ITermController termController = termController ?? Globals.GetCurrentServiceProvider().GetRequiredService<ITermController>();
        private string tags;
        private bool isEditMode;

        /// <summary>Initializes a new instance of the <see cref="Tags"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.4. Please use overload with IVocabularyController. Scheduled removal in v12.0.0.")]
        protected Tags()
            : this(null, null)
        {
        }

        /// <summary>An event which is triggered when the tags are updated.</summary>
        public event EventHandler<EventArgs> TagsUpdated;

        /// <summary>Gets or sets the URL of the add image.</summary>
        [Parameter]
        public string AddImageUrl { get; set; }

        /// <summary>Gets or sets a value indicating whether to allow tagging.</summary>
        [Parameter]
        public bool AllowTagging { get; set; }

        /// <summary>Gets or sets the URL of the cancel image.</summary>
        [Parameter]
        public string CancelImageUrl { get; set; }

        /// <summary>Gets or sets the content item.</summary>
        [Parameter]
        public ContentItem ContentItem { get; set; }

        /// <summary>Gets or sets a value indicating whether it's in edit mode.</summary>
        /// <remarks>This lived in view state; it is now a field of the component.</remarks>
        public bool IsEditMode
        {
            get => this.isEditMode;
            set => this.isEditMode = value;
        }

        /// <summary>Gets or sets the URL format string.</summary>
        [Parameter]
        public string NavigateUrlFormatString { get; set; }

        /// <summary>Gets or sets the repeat direction.</summary>
        [Parameter]
        public string RepeatDirection { get; set; } = "Horizontal";

        /// <summary>Gets or sets the URL of the save image.</summary>
        [Parameter]
        public string SaveImageUrl { get; set; }

        /// <summary>Gets or sets the separator.</summary>
        [Parameter]
        public string Separator { get; set; } = ",&nbsp;";

        /// <summary>Gets or sets a value indicating whether to show categories.</summary>
        [Parameter]
        public bool ShowCategories { get; set; }

        /// <summary>Gets or sets a value indicating whether to show tags.</summary>
        [Parameter]
        public bool ShowTags { get; set; }

        /// <summary>Gets or sets a callback raised in addition to the <see cref="TagsUpdated"/> event.</summary>
        [Parameter]
        public EventCallback<EventArgs> TagsUpdatedCallback { get; set; }

        private Vocabulary TagVocabulary =>
            this.vocabularyController.GetVocabularies().SingleOrDefault(v => v.IsSystem && v.Name == "Tags");

        /// <summary>Captures the tags the user typed.</summary>
        /// <param name="postDataKey">The name of the posted field.</param>
        /// <param name="postCollection">The posted values.</param>
        /// <returns>Always <see langword="true"/>, as it did under Web Forms.</returns>
        public bool LoadPostData(string postDataKey, System.Collections.Specialized.NameValueCollection postCollection)
        {
            ArgumentNullException.ThrowIfNull(postCollection);

            this.tags = postCollection[postDataKey];

            return true;
        }

        // Public API that preserves the former IPostBackDataHandler contract of this control; making it static would be a breaking change.
        #pragma warning disable CA1822 // Mark members as static
        /// <summary>Runs when the captured tags changed.</summary>
        public void RaisePostDataChangedEvent()
        {
        }
        #pragma warning restore CA1822 // Mark members as static

        /// <summary>Handles an Add/Save/Cancel command.</summary>
        /// <param name="eventArgument">The command name.</param>
        public void RaisePostBackEvent(string eventArgument)
        {
            switch (eventArgument)
            {
                case "Add":
                    this.IsEditMode = true;
                    break;
                case "Cancel":
                    this.IsEditMode = false;
                    break;
                case "Save":
                    this.SaveTags();
                    break;
                default:
                    this.IsEditMode = false;
                    break;
            }
        }

        // ASP0006: the render tree of this ported Web Forms control is built imperatively through helper methods that share a running sequence number, so literal sequence numbers cannot express its structure.
        #pragma warning disable ASP0006
        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            if (!this.Visible)
            {
                return;
            }

            var sequence = 0;

            // Render Outer Div
            builder.OpenElement(sequence++, "div");
            builder.AddAttribute(sequence++, "class", this.RepeatDirection.ToLowerInvariant());

            // Render Categories
            if (this.ShowCategories)
            {
                builder.OpenElement(sequence++, "ul");
                builder.AddAttribute(sequence++, "class", "categories");
                builder.AddAttribute(sequence++, "title", this.LocalizeString("Category.ToolTip"));

                var categories = (from cat in this.ContentItem.Terms where cat.VocabularyId != this.TagVocabulary.VocabularyId select cat).ToList();
                this.RenderTermList(builder, ref sequence, categories);

                builder.CloseElement();
            }

            if (this.ShowTags)
            {
                builder.OpenElement(sequence++, "ul");
                builder.AddAttribute(sequence++, "class", "tags");
                builder.AddAttribute(sequence++, "title", this.LocalizeString("Tag.ToolTip"));

                var tagTerms = (from cat in this.ContentItem.Terms where cat.VocabularyId == this.TagVocabulary.VocabularyId select cat).ToList();
                this.RenderTermList(builder, ref sequence, tagTerms);

                if (this.AllowTagging)
                {
                    builder.OpenElement(sequence++, "li");

                    if (this.IsEditMode)
                    {
                        builder.AddMarkupContent(sequence++, "&nbsp;&nbsp;");

                        builder.OpenElement(sequence++, "input");
                        builder.AddAttribute(sequence++, "name", this.UniqueId);

                        // Preserved verbatim: the Enter key must not submit the surrounding form.
                        builder.AddAttribute(sequence++, "onkeypress", "return event.keyCode != 13;");
                        builder.AddAttribute(sequence++, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, this.OnTagsInput));
                        builder.CloseElement();

                        builder.AddMarkupContent(sequence++, "&nbsp;&nbsp;");

                        // Render Save Button
                        this.RenderButton(builder, ref sequence, "Save", this.SaveImageUrl);

                        builder.AddMarkupContent(sequence++, "&nbsp;&nbsp;");

                        // Render Add Button
                        this.RenderButton(builder, ref sequence, "Cancel", this.CancelImageUrl);
                    }
                    else
                    {
                        builder.AddMarkupContent(sequence++, "&nbsp;&nbsp;");

                        // Render Add Button
                        this.RenderButton(builder, ref sequence, "Add", this.AddImageUrl);
                    }

                    builder.CloseElement();
                }

                builder.CloseElement();
            }

            builder.CloseElement();
        }
        #pragma warning restore ASP0006

        /// <summary>A method which triggers the <see cref="TagsUpdated"/> event.</summary>
        /// <param name="e">The event args.</param>
        protected void OnTagsUpdate(EventArgs e)
        {
            this.TagsUpdated?.Invoke(this, e);
            if (this.TagsUpdatedCallback.HasDelegate)
            {
                this.TagsUpdatedCallback.InvokeAsync(e);
            }
        }

        private void OnTagsInput(ChangeEventArgs args)
        {
            this.tags = args?.Value?.ToString();
        }

        // ASP0006: the render tree of this ported Web Forms control is built imperatively through helper methods that share a running sequence number, so literal sequence numbers cannot express its structure.
        #pragma warning disable ASP0006
        private void RenderTermList(RenderTreeBuilder builder, ref int sequence, List<Term> terms)
        {
            for (int i = 0; i <= terms.Count - 1; i++)
            {
                builder.OpenElement(sequence++, "li");
                if (i == 0)
                {
                    // First term
                    builder.AddAttribute(sequence++, "class", "first_tag");
                }
                else if (i == terms.Count - 1)
                {
                    // Last term
                    builder.AddAttribute(sequence++, "class", "last_tag");
                }

                this.RenderTerm(builder, ref sequence, terms[i], i < terms.Count - 1 && this.RepeatDirection.Equals("horizontal", StringComparison.OrdinalIgnoreCase));

                builder.CloseElement();
            }
        }
        #pragma warning restore ASP0006

        private string LocalizeString(string key)
        {
            string localResourceFile = Utilities.GetLocalResourceFile(this);
            string localizedString = null;
            if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(localResourceFile))
            {
                localizedString = Localization.GetString(key, localResourceFile);
            }
            else
            {
                localizedString = Null.NullString;
            }

            return localizedString;
        }

        // ASP0006: the render tree of this ported Web Forms control is built imperatively through helper methods that share a running sequence number, so literal sequence numbers cannot express its structure.
        #pragma warning disable ASP0006
        private void RenderButton(RenderTreeBuilder builder, ref int sequence, string buttonType, string imageUrl)
        {
            var command = buttonType;

            builder.OpenElement(sequence++, "a");
            builder.AddAttribute(sequence++, "title", this.LocalizeString($"{buttonType}.ToolTip"));
            builder.AddAttribute(sequence++, "href", "javascript:void(0);");
            builder.AddAttribute(
                sequence++,
                "onclick",
                EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(this, _ => this.RaisePostBackEvent(command)));

            // Image
            if (!string.IsNullOrEmpty(imageUrl))
            {
                builder.OpenElement(sequence++, "img");
                builder.AddAttribute(sequence++, "src", Globals.ResolveUrl(imageUrl));
                builder.CloseElement();
            }

            builder.AddContent(sequence++, this.LocalizeString(buttonType));
            builder.CloseElement();
        }
        #pragma warning restore ASP0006

        // ASP0006: the render tree of this ported Web Forms control is built imperatively through helper methods that share a running sequence number, so literal sequence numbers cannot express its structure.
        #pragma warning disable ASP0006
        private void RenderTerm(RenderTreeBuilder builder, ref int sequence, Term term, bool renderSeparator)
        {
            builder.OpenElement(sequence++, "a");
            builder.AddAttribute(sequence++, "href", string.Format(CultureInfo.InvariantCulture, this.NavigateUrlFormatString, term.Name));
            builder.AddAttribute(sequence++, "title", term.Name);
            builder.AddAttribute(sequence++, "rel", "tag");
            builder.AddContent(sequence++, term.Name);
            builder.CloseElement();

            if (renderSeparator)
            {
                builder.AddMarkupContent(sequence++, this.Separator);
            }
        }
        #pragma warning restore ASP0006

        private void SaveTags()
        {
            string tags = this.tags;

            if (!string.IsNullOrEmpty(tags))
            {
                foreach (string t in tags.Split(','))
                {
                    if (!string.IsNullOrEmpty(t))
                    {
                        string tagName = t.Trim(' ');
                        Term existingTerm = (from term in this.ContentItem.Terms.AsQueryable() where term.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase) select term).SingleOrDefault();

                        if (existingTerm == null)
                        {
                            // Not tagged
                            Term term = this.termController.GetTermsByVocabulary(this.TagVocabulary.VocabularyId).SingleOrDefault(te => te.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase));
                            if (term == null)
                            {
                                // Add term
                                term = new Term(this.TagVocabulary.VocabularyId) { Name = tagName, };
                                this.termController.AddTerm(term);
                            }

                            // Add term to content
                            this.ContentItem.Terms.Add(term);
                            this.termController.AddTermToContent(term, this.ContentItem);
                        }
                    }
                }
            }

            this.IsEditMode = false;

            // Raise the Tags Updated Event
            this.OnTagsUpdate(EventArgs.Empty);
        }
    }
}
