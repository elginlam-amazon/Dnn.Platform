// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Skins.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.RegularExpressions;

    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.UI;

    using Microsoft.AspNetCore.Components;

    /// <summary>A skin/theme object which displays a list of page links.</summary>
    /// <remarks>
    /// <para>
    /// Was <c>links.ascx</c> + <c>Links.ascx.cs</c> + <c>Links.ascx.designer.cs</c>; the markup now
    /// lives in <c>Links.razor</c>. The single designer field <c>lblLinks</c> (an
    /// <c>asp:Label</c>) becomes <see cref="LinksMarkup"/>, rendered inside the <c>span</c> an
    /// <c>asp:Label</c> produced.
    /// </para>
    /// <para>
    /// <c>CssClass</c> is no longer declared here: <see cref="DnnComponentBase.CssClass"/> already
    /// carries it, and (as before) it is only used as the class of the generated anchors, not
    /// emitted onto the control itself.
    /// </para>
    /// <para>
    /// Behaviour note: the original <c>OnLoad</c> wrapped <c>Separator</c> in a <c>span</c> and
    /// assigned the result back onto the property. A Blazor component's <c>OnLoad</c> runs on every
    /// parameter set, so the resolved separator is kept in <see cref="resolvedSeparator"/> instead
    /// and the author supplied <see cref="Separator"/> is left untouched - the rendered output is
    /// identical, it just cannot be wrapped twice.
    /// </para>
    /// </remarks>
    public partial class Links
    {
        private static readonly Regex SrcRegex = new Regex("src=[']?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private string resolvedSeparator;

        /// <summary>Gets or sets the alignment of the links ("vertical" renders one link per div).</summary>
        [Parameter]
        public string Alignment { get; set; }

        /// <summary>Gets or sets which pages to render ("same", "child", "parent" or "root").</summary>
        [Parameter]
        public string Level { get; set; }

        /// <summary>Gets or sets the markup rendered between two links.</summary>
        [Parameter]
        public string Separator { get; set; }

        /// <summary>Gets or sets a value indicating whether disabled pages are rendered.</summary>
        [Parameter]
        public bool ShowDisabled { get; set; }

        /// <summary>Gets or sets a value indicating whether to fall back to all levels when the configured level yields no link.</summary>
        [Parameter]
        public bool ForceLinks { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the active page is included in the list.</summary>
        [Parameter]
        public bool IncludeActiveTab { get; set; } = true;

        /// <summary>Gets the built list of links (replaces <c>lblLinks.Text</c>).</summary>
        protected string LinksMarkup { get; private set; }

        /// <summary>Gets the alignment, lower-cased.</summary>
        /// <remarks>
        /// The Web Forms property lower-cased the value in its setter. A component parameter has to
        /// be an auto property (Blazor assigns it directly), so the same normalization is applied
        /// here instead and every reader inside this control uses this member, which makes the
        /// comparisons below exactly as case-insensitive as they were.
        /// </remarks>
        private string NormalizedAlignment => this.Alignment?.ToLowerInvariant();

        /// <summary>Gets the level, lower-cased.</summary>
        /// <remarks>See <see cref="NormalizedAlignment"/>; the Web Forms setter lower-cased this too.</remarks>
        private string NormalizedLevel => this.Level?.ToLowerInvariant();

        /// <inheritdoc />
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            string strCssClass;
            if (!string.IsNullOrEmpty(this.CssClass))
            {
                strCssClass = this.CssClass;
            }
            else
            {
                strCssClass = "SkinObject";
            }

            if (!string.IsNullOrEmpty(this.Separator))
            {
                this.resolvedSeparator = this.Separator;
                if (this.resolvedSeparator.IndexOf("src=", StringComparison.Ordinal) != -1)
                {
                    // Add the skinpath to image paths
                    this.resolvedSeparator = SrcRegex.Replace(this.resolvedSeparator, "$&" + this.PortalSettings.ActiveTab.SkinPath);
                }

                // Wrap in a span
                this.resolvedSeparator = string.Format("<span class=\"{0}\">{1}</span>", strCssClass, this.resolvedSeparator);
            }
            else
            {
                this.resolvedSeparator = " ";
            }

            // build links
            string strLinks = string.Empty;

            strLinks = this.BuildLinks(this.NormalizedLevel, string.Empty, strCssClass);

            // Render links, even if nothing is returned with the currently set level
            if (string.IsNullOrEmpty(strLinks) && this.ForceLinks)
            {
                strLinks = this.BuildLinks(string.Empty, string.Empty, strCssClass);
            }

            this.LinksMarkup = strLinks;
        }

        private string BuildLinks(string strLevel, string strSeparator, string strCssClass)
        {
            var sbLinks = new StringBuilder();

            List<TabInfo> portalTabs = TabController.GetTabsBySortOrder(this.PortalSettings.PortalId);
            List<TabInfo> hostTabs = TabController.GetTabsBySortOrder(Null.NullInteger);

            foreach (TabInfo objTab in portalTabs)
            {
                sbLinks.Append(this.ProcessLink(this.ProcessTab(objTab, strLevel, strCssClass), sbLinks.ToString().Length));
            }

            foreach (TabInfo objTab in hostTabs)
            {
                sbLinks.Append(this.ProcessLink(this.ProcessTab(objTab, strLevel, strCssClass), sbLinks.ToString().Length));
            }

            return sbLinks.ToString();
        }

        private string ProcessTab(TabInfo objTab, string strLevel, string strCssClass)
        {
            // AUTHORIZATION: unchanged permission filter - a page the visitor may not see is not linked.
            if (Navigation.CanShowTab(objTab, this.AdminMode, this.ShowDisabled))
            {
                switch (strLevel)
                {
                    case "same": // Render tabs on the same level as the current tab
                    case "":
                        if (objTab.ParentId == this.PortalSettings.ActiveTab.ParentId)
                        {
                            if (this.IncludeActiveTab || objTab.TabID != this.PortalSettings.ActiveTab.TabID)
                            {
                                return this.AddLink(objTab.TabName, objTab.FullUrl, strCssClass);
                            }
                        }

                        break;
                    case "child": // Render the current tabs child tabs
                        if (objTab.ParentId == this.PortalSettings.ActiveTab.TabID)
                        {
                            return this.AddLink(objTab.TabName, objTab.FullUrl, strCssClass);
                        }

                        break;
                    case "parent": // Render the current tabs parenttab
                        if (objTab.TabID == this.PortalSettings.ActiveTab.ParentId)
                        {
                            return this.AddLink(objTab.TabName, objTab.FullUrl, strCssClass);
                        }

                        break;
                    case "root": // Render Root tabs
                        if (objTab.Level == 0)
                        {
                            return this.AddLink(objTab.TabName, objTab.FullUrl, strCssClass);
                        }

                        break;
                }
            }

            return string.Empty;
        }

        private string ProcessLink(string sLink, int iLinksLength)
        {
            // wrap in a div if set to vertical
            if (string.IsNullOrEmpty(sLink))
            {
                return string.Empty;
            }

            if (this.NormalizedAlignment == "vertical")
            {
                sLink = string.Concat("<div>", this.resolvedSeparator, sLink, "</div>");
            }
            else if (!string.IsNullOrEmpty(this.resolvedSeparator) && iLinksLength > 0)
            {
                // If not vertical, then render the separator
                sLink = string.Concat(this.resolvedSeparator, sLink);
            }

            return sLink;
        }

        private string AddLink(string strTabName, string strURL, string strCssClass)
        {
            return string.Format("<a class=\"{0}\" href=\"{1}\">{2}</a>", strCssClass, strURL, strTabName);
        }
    }
}
