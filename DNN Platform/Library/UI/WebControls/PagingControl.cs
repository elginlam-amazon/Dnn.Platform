// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Text;

    using DotNetNuke.Common.Internal;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI.Utilities;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Web;

    /// <summary>A pager that renders first/previous/page-number/next/last links.</summary>
    /// <remarks>
    /// Was a <c>WebControl</c> implementing <c>IPostBackEventHandler</c> that built a
    /// <c>Table</c> with a <c>Repeater</c> of page links in <c>CreateChildControls</c> and rendered
    /// it onto an <c>HtmlTextWriter</c>. It is now a <see cref="DnnComponentBase"/> that renders the
    /// same table through the render tree. In <see cref="PagingControlMode.PostBack"/> mode the page
    /// links call <see cref="RaisePostBackEvent"/> directly instead of going through
    /// <c>__doPostBack</c>.
    /// </remarks>
    public class PagingControl : DnnComponentBase
    {
        /// <summary>The page numbers that are rendered as links.</summary>
        /// <remarks>
        /// Replaces the <c>Repeater PageNumbers</c> field: it is the data that used to be bound to
        /// that repeater, which is all a derived control could usefully reach through it.
        /// </remarks>
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1306:FieldNamesMustBeginWithLowerCaseLetter", Justification = "Breaking Change")]
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]

        // ReSharper disable once InconsistentNaming
        protected IList<int> PageNumbers;

        /// <summary>The markup of the paging links cell.</summary>
        /// <remarks>Replaces the <c>TableCell cellDisplayLinks</c> field.</remarks>
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected string cellDisplayLinks;

        /// <summary>The text of the paging status cell.</summary>
        /// <remarks>Replaces the <c>TableCell cellDisplayStatus</c> field.</remarks>
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected string cellDisplayStatus;

        /// <summary>The CSS class of the paging table.</summary>
        /// <remarks>Replaces the <c>Table tablePageNumbers</c> field.</remarks>
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected string tablePageNumbers;

        private int totalPages = -1;
        private string cssClassLinkActive;
        private string cssClassLinkInactive;
        private string cssClassPagingStatus;
        private PagingControlMode mode = PagingControlMode.URL;

        /// <summary>Occurs when the user navigates to another page.</summary>
        public event EventHandler PageChanged;

        /// <summary>Gets or sets the CSS class of an active (clickable) link.</summary>
        [Bindable(true)]
        [Category("Behavior")]
        [DefaultValue("")]
        [Parameter]
        public string CSSClassLinkActive
        {
            get
            {
                return string.IsNullOrEmpty(this.cssClassLinkActive) ? string.Empty : this.cssClassLinkActive;
            }

            set
            {
                this.cssClassLinkActive = value;
            }
        }

        /// <summary>Gets or sets the CSS class of an inactive link.</summary>
        [Bindable(true)]
        [Category("Behavior")]
        [DefaultValue("")]
        [Parameter]
        public string CSSClassLinkInactive
        {
            get
            {
                return string.IsNullOrEmpty(this.cssClassLinkInactive) ? string.Empty : this.cssClassLinkInactive;
            }

            set
            {
                this.cssClassLinkInactive = value;
            }
        }

        /// <summary>Gets or sets the CSS class of the paging status text.</summary>
        [Bindable(true)]
        [Category("Behavior")]
        [DefaultValue("")]
        [Parameter]
        public string CSSClassPagingStatus
        {
            get
            {
                return string.IsNullOrEmpty(this.cssClassPagingStatus) ? string.Empty : this.cssClassPagingStatus;
            }

            set
            {
                this.cssClassPagingStatus = value;
            }
        }

        /// <summary>Gets or sets the page the user is on, which is one based.</summary>
        [Bindable(true)]
        [Category("Behavior")]
        [DefaultValue("1")]
        [Parameter]
        public int CurrentPage { get; set; }

        /// <summary>Gets or sets the way the pager navigates.</summary>
        [Parameter]
        public PagingControlMode Mode
        {
            get
            {
                return this.mode;
            }

            set
            {
                this.mode = value;
            }
        }

        /// <summary>Gets or sets the number of records on one page.</summary>
        [Bindable(true)]
        [Category("Behavior")]
        [DefaultValue("10")]
        [Parameter]
        public int PageSize { get; set; }

        /// <summary>Gets or sets the query string parameters the paging links carry over.</summary>
        [Bindable(true)]
        [Category("Behavior")]
        [DefaultValue("")]
        [Parameter]
        public string QuerystringParams { get; set; }

        /// <summary>Gets or sets the tab the paging links navigate to.</summary>
        [Bindable(true)]
        [Category("Behavior")]
        [DefaultValue("-1")]
        [Parameter]
        public int TabID { get; set; }

        /// <summary>Gets or sets the total number of records being paged.</summary>
        [Bindable(true)]
        [Category("Behavior")]
        [DefaultValue("0")]
        [Parameter]
        public int TotalRecords { get; set; }

        /// <summary>Gets or sets the callback raised when the user navigates to another page.</summary>
        /// <remarks>The Blazor equivalent of the <see cref="PageChanged"/> event, for markup consumers.</remarks>
        [Parameter]
        public EventCallback<EventArgs> OnPageChangedCallback { get; set; }

        /// <inheritdoc />
        protected override string TagName => null;

        /// <summary>Navigates to the page named by the argument and raises <see cref="PageChanged"/>.</summary>
        /// <param name="eventArgument">The page, in the form <c>Page_3</c>.</param>
        /// <remarks>Replaces <c>IPostBackEventHandler.RaisePostBackEvent</c>.</remarks>
        public void RaisePostBackEvent(string eventArgument)
        {
            ArgumentNullException.ThrowIfNull(eventArgument);

            this.CurrentPage = int.Parse(eventArgument.Replace("Page_", string.Empty), CultureInfo.InvariantCulture);
            this.OnPageChanged(EventArgs.Empty);
        }

        /// <summary>Builds the paging state this control renders.</summary>
        /// <remarks>Replaces <c>CreateChildControls</c>.</remarks>
        protected override void CreateChildControls()
        {
            this.tablePageNumbers = string.IsNullOrEmpty(this.CssClass) ? "PagingTable" : this.CssClass;
            this.BindPageNumbers(this.TotalRecords, this.PageSize);

            var intTotalPages = this.totalPages;
            if (intTotalPages == 0)
            {
                intTotalPages = 1;
            }

            this.cellDisplayStatus = string.Format(CultureInfo.CurrentCulture, Localization.GetString("Pages"), this.CurrentPage, intTotalPages);

            var str = new StringBuilder();
            str.Append(this.GetFirstLink() + "&nbsp;&nbsp;&nbsp;");
            str.Append(this.GetPreviousLink() + "&nbsp;&nbsp;&nbsp;");
            var template = new PageNumberLinkTemplate(this);
            foreach (var pageNumber in this.PageNumbers)
            {
                str.Append(template.GetLinkMarkup(pageNumber));
            }

            str.Append(this.GetNextLink() + "&nbsp;&nbsp;&nbsp;");
            str.Append(this.GetLastLink() + "&nbsp;&nbsp;&nbsp;");
            this.cellDisplayLinks = str.ToString();
        }

        /// <summary>Raises the <see cref="PageChanged"/> event.</summary>
        /// <param name="e">The event arguments.</param>
        protected void OnPageChanged(EventArgs e)
        {
            this.PageChanged?.Invoke(this, e);
            if (this.OnPageChangedCallback.HasDelegate)
            {
                this.OnPageChangedCallback.InvokeAsync(e);
            }
        }

        /// <inheritdoc />
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            this.CreateChildControls();
        }

        /// <inheritdoc />
        protected override void RenderContents(RenderTreeBuilder builder, ref int sequence)
        {
            ArgumentNullException.ThrowIfNull(builder);

            if (this.PageNumbers == null)
            {
                this.CreateChildControls();
            }

            builder.OpenElement(sequence++, "table");
            builder.AddAttribute(sequence++, "class", this.tablePageNumbers);
            builder.OpenElement(sequence++, "tr");

            builder.OpenElement(sequence++, "td");
            builder.AddAttribute(sequence++, "align", "left");
            if (!string.IsNullOrEmpty(this.CSSClassPagingStatus))
            {
                builder.AddAttribute(sequence++, "class", this.CSSClassPagingStatus);
            }

            builder.AddMarkupContent(sequence++, this.cellDisplayStatus);
            builder.CloseElement();

            builder.OpenElement(sequence++, "td");
            builder.AddAttribute(sequence++, "align", "right");
            if (this.Mode == PagingControlMode.PostBack)
            {
                // the page links are server side handlers in post back mode
                this.RenderPostBackLinks(builder, ref sequence);
            }
            else
            {
                builder.AddMarkupContent(sequence++, this.cellDisplayLinks);
            }

            builder.CloseElement();

            builder.CloseElement();
            builder.CloseElement();

            base.RenderContents(builder, ref sequence);
        }

        private void RenderPostBackLinks(RenderTreeBuilder builder, ref int sequence)
        {
            this.RenderPostBackLink(builder, ref sequence, Localization.GetString("First", Localization.SharedResourceFile), 1, this.CurrentPage > 1 && this.totalPages > 0);
            this.RenderPostBackLink(builder, ref sequence, Localization.GetString("Previous", Localization.SharedResourceFile), this.CurrentPage - 1, this.CurrentPage > 1 && this.totalPages > 0);
            foreach (var pageNumber in this.PageNumbers)
            {
                this.RenderPostBackLink(builder, ref sequence, pageNumber.ToString(CultureInfo.InvariantCulture), pageNumber, pageNumber != this.CurrentPage);
            }

            this.RenderPostBackLink(builder, ref sequence, Localization.GetString("Next", Localization.SharedResourceFile), this.CurrentPage + 1, this.CurrentPage != this.totalPages && this.totalPages > 0);
            this.RenderPostBackLink(builder, ref sequence, Localization.GetString("Last", Localization.SharedResourceFile), this.totalPages, this.CurrentPage != this.totalPages && this.totalPages > 0);
        }

        private void RenderPostBackLink(RenderTreeBuilder builder, ref int sequence, string text, int pageNumber, bool isActive)
        {
            if (isActive)
            {
                builder.OpenElement(sequence++, "a");
                builder.AddAttribute(sequence++, "href", "javascript:void(0);");
                if (this.CSSClassLinkActive.Trim().Length > 0)
                {
                    builder.AddAttribute(sequence++, "class", this.CSSClassLinkActive);
                }

                builder.AddAttribute(sequence++, "onclick", EventCallback.Factory.Create<MouseEventArgs>(
                    this,
                    () => this.RaisePostBackEvent("Page_" + pageNumber.ToString(CultureInfo.InvariantCulture))));
                builder.AddContent(sequence++, text);
                builder.CloseElement();
            }
            else
            {
                builder.OpenElement(sequence++, "span");
                if (this.CSSClassLinkInactive.Trim().Length > 0)
                {
                    builder.AddAttribute(sequence++, "class", this.CSSClassLinkInactive);
                }

                builder.AddContent(sequence++, text);
                builder.CloseElement();
            }

            builder.AddMarkupContent(sequence++, "&nbsp;&nbsp;&nbsp;");
        }

        private void BindPageNumbers(int totalRecords, int recordsPerPage)
        {
            const int PageLinksPerPage = 10;
            this.PageNumbers = new List<int>();
            if (totalRecords < 1 || recordsPerPage < 1)
            {
                this.totalPages = 1;
                return;
            }

            this.totalPages = totalRecords / recordsPerPage >= 1 ? Convert.ToInt32(Math.Ceiling(Convert.ToDouble(totalRecords) / recordsPerPage)) : 0;
            if (this.totalPages > 0)
            {
                var lowNum = 1;
                var highNum = Convert.ToInt32(this.totalPages);
                double tmpNum;
                tmpNum = this.CurrentPage - (PageLinksPerPage / 2);
                if (tmpNum < 1)
                {
                    tmpNum = 1;
                }

                if (this.CurrentPage > (PageLinksPerPage / 2))
                {
                    lowNum = Convert.ToInt32(Math.Floor(tmpNum));
                }

                if (Convert.ToInt32(this.totalPages) <= PageLinksPerPage)
                {
                    highNum = Convert.ToInt32(this.totalPages);
                }
                else
                {
                    highNum = lowNum + PageLinksPerPage - 1;
                }

                if (highNum > Convert.ToInt32(this.totalPages))
                {
                    highNum = Convert.ToInt32(this.totalPages);
                    if (highNum - lowNum < PageLinksPerPage)
                    {
                        lowNum = highNum - PageLinksPerPage + 1;
                    }
                }

                if (highNum > Convert.ToInt32(this.totalPages))
                {
                    highNum = Convert.ToInt32(this.totalPages);
                }

                if (lowNum < 1)
                {
                    lowNum = 1;
                }

                int i;
                for (i = lowNum; i <= highNum; i++)
                {
                    this.PageNumbers.Add(i);
                }
            }
        }

        private string CreateUrl(string currentPage)
        {
            switch (this.Mode)
            {
                case PagingControlMode.URL:
                    return !string.IsNullOrEmpty(this.QuerystringParams)
                               ? (!string.IsNullOrEmpty(currentPage) ? TestableGlobals.Instance.NavigateURL(this.TabID, string.Empty, this.QuerystringParams, "currentpage=" + currentPage) : TestableGlobals.Instance.NavigateURL(this.TabID, string.Empty, this.QuerystringParams))
                               : (!string.IsNullOrEmpty(currentPage) ? TestableGlobals.Instance.NavigateURL(this.TabID, string.Empty, "currentpage=" + currentPage) : TestableGlobals.Instance.NavigateURL(this.TabID));
                default:
                    return ClientAPI.GetPostBackClientHyperlink(this, "Page_" + currentPage);
            }
        }

        /// <summary>GetLink returns the page number links for paging.</summary>
        private string GetLink(int pageNum)
        {
            if (pageNum == this.CurrentPage)
            {
                return this.CSSClassLinkInactive.Trim().Length > 0
                    ? $"<span class=\"{this.CSSClassLinkInactive}\">[{pageNum}]</span>"
                    : $"<span>[{pageNum}]</span>";
            }

            var url = this.CreateUrl(pageNum.ToString(CultureInfo.InvariantCulture));
            return this.CSSClassLinkActive.Trim().Length > 0
                       ? $"<a href=\"{url}\" class=\"{this.CSSClassLinkActive}\">{pageNum}</a>"
                       : $"<a href=\"{url}\">{pageNum}</a>";
        }

        /// <summary>GetPreviousLink returns the link for the Previous page for paging.</summary>
        private string GetPreviousLink()
        {
            var url = this.CreateUrl((this.CurrentPage - 1).ToString(CultureInfo.InvariantCulture));
            var previousText = Localization.GetString("Previous", Localization.SharedResourceFile);
            return this.CurrentPage > 1 && this.totalPages > 0
                       ? (this.CSSClassLinkActive.Trim().Length > 0
                              ? $"<a href=\"{url}\" class=\"{this.CSSClassLinkActive}\">{previousText}</a>"
                              : $"<a href=\"{url}\">{previousText}</a>")
                       : (this.CSSClassLinkInactive.Trim().Length > 0
                              ? $"<span class=\"{this.CSSClassLinkInactive}\">{previousText}</span>"
                              : $"<span>{previousText}</span>");
        }

        /// <summary>GetNextLink returns the link for the Next Page for paging.</summary>
        private string GetNextLink()
        {
            var url = this.CreateUrl((this.CurrentPage + 1).ToString(CultureInfo.InvariantCulture));
            var nextText = Localization.GetString("Next", Localization.SharedResourceFile);
            return this.CurrentPage != this.totalPages && this.totalPages > 0
                       ? (this.CSSClassLinkActive.Trim().Length > 0
                              ? $"<a href=\"{url}\" class=\"{this.CSSClassLinkActive}\">{nextText}</a>"
                              : $"<a href=\"{url}\">{nextText}</a>")
                       : (this.CSSClassLinkInactive.Trim().Length > 0
                              ? $"<span class=\"{this.CSSClassLinkInactive}\">{nextText}</span>"
                              : $"<span>{nextText}</span>");
        }

        /// <summary>GetFirstLink returns the First Page link for paging.</summary>
        private string GetFirstLink()
        {
            var firstText = Localization.GetString("First", Localization.SharedResourceFile);
            if (this.CurrentPage > 1 && this.totalPages > 0)
            {
                var url = this.CreateUrl("1");
                return this.CSSClassLinkActive.Trim().Length > 0
                           ? $"<a href=\"{url}\" class=\"{this.CSSClassLinkActive}\">{firstText}</a>"
                           : $"<a href=\"{url}\">{firstText}</a>";
            }

            return this.CSSClassLinkInactive.Trim().Length > 0
                       ? $"<span class=\"{this.CSSClassLinkInactive}\">{firstText}</span>"
                       : $"<span>{firstText}</span>";
        }

        /// <summary>GetLastLink returns the Last Page link for paging.</summary>
        private string GetLastLink()
        {
            var lastText = Localization.GetString("Last", Localization.SharedResourceFile);
            if (this.CurrentPage != this.totalPages && this.totalPages > 0)
            {
                var url = this.CreateUrl(this.totalPages.ToString(CultureInfo.InvariantCulture));
                return this.CSSClassLinkActive.Trim().Length > 0
                           ? $"<a href=\"{url}\" class=\"{this.CSSClassLinkActive}\">{lastText}</a>"
                           : $"<a href=\"{url}\">{lastText}</a>";
            }

            return this.CSSClassLinkInactive.Trim().Length > 0
                       ? $"<span class=\"{this.CSSClassLinkInactive}\">{lastText}</span>"
                       : $"<span>{lastText}</span>";
        }

        /// <summary>Renders one page number link of a <see cref="PagingControl"/>.</summary>
        /// <remarks>
        /// Was the <c>ITemplate</c> of the page-number <c>Repeater</c>, which instantiated a
        /// <c>Literal</c> and filled it on <c>DataBinding</c>. It now implements
        /// <see cref="IGridTemplate"/> and returns that very same markup.
        /// </remarks>
        public class PageNumberLinkTemplate : IGridTemplate
        {
            private readonly PagingControl pagingControl;

            /// <summary>Initializes a new instance of the <see cref="PageNumberLinkTemplate"/> class.</summary>
            /// <param name="ctlPagingControl">The paging control.</param>
            public PageNumberLinkTemplate(PagingControl ctlPagingControl)
            {
                this.pagingControl = ctlPagingControl;
            }

            /// <inheritdoc />
            public RenderFragment Render(DataGridCell cell)
            {
                ArgumentNullException.ThrowIfNull(cell);

                var pageNumber = Convert.ToInt32(GridDataBinder.Eval(cell.Item.DataItem, "PageNum"), CultureInfo.InvariantCulture);
                var markup = this.GetLinkMarkup(pageNumber);
                return builder => builder.AddMarkupContent(0, markup);
            }

            /// <summary>Gets the markup of the link to the given page.</summary>
            /// <param name="pageNumber">The page to link to.</param>
            /// <returns>The link, followed by the two non-breaking spaces the template appended.</returns>
            public string GetLinkMarkup(int pageNumber) =>
                this.pagingControl.GetLink(pageNumber) + "&nbsp;&nbsp;";
        }
    }
}
