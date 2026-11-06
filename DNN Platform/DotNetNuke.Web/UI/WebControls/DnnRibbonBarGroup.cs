// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using DotNetNuke.UI;
    using DotNetNuke.UI.Utilities;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>A group in the <see cref="DnnRibbonBar"/>.</summary>
    /// <remarks>
    /// This was a <c>WebControl</c> with <c>[ParseChildren]</c> and two <c>ITemplate</c> properties.
    /// On ASP.NET Core the templates are <see cref="RenderFragment"/>s and the surrounding
    /// decoration divs (<c>topLeft</c>, <c>topRight</c>, <c>content</c>, <c>footer</c>,
    /// <c>bottomLeft</c>, <c>bottomRight</c>) are still built into
    /// <see cref="DnnComponentBase.Controls"/>, so the markup is unchanged. The
    /// "hide the group when all of its tools are hidden" rule is preserved by
    /// <see cref="CheckVisibility"/>.
    /// </remarks>
    public class DnnRibbonBarGroup : DnnComponentBase
    {
        private bool checkToolVisibility = true;
        private DnnGenericControl contentContainer;
        private DnnGenericControl footerContainer;

        /// <summary>Initializes a new instance of the <see cref="DnnRibbonBarGroup"/> class.</summary>
        public DnnRibbonBarGroup()
        {
            this.CssClass = "dnnRibbonGroup";
        }

        /// <summary>Gets or sets the footer template.</summary>
        /// <remarks>Replaces the <c>ITemplate Footer</c> property.</remarks>
        [Parameter]
        public virtual RenderFragment Footer { get; set; }

        /// <summary>Gets or sets the content template.</summary>
        /// <remarks>Replaces the <c>ITemplate Content</c> property.</remarks>
        [Parameter]
        public virtual RenderFragment Content { get; set; }

        /// <summary>Gets or sets a value indicating whether the check tool is visible.</summary>
        public virtual bool CheckToolVisibility
        {
            get
            {
                return this.checkToolVisibility;
            }

            set
            {
                this.checkToolVisibility = value;
            }
        }

        /// <summary>Gets the container the tools of this group are added to.</summary>
        /// <remarks>Replaces the container that <c>Content.InstantiateIn</c> was given.</remarks>
        public DnnGenericControl ContentContainer
        {
            get
            {
                this.EnsureChildControls();
                return this.contentContainer;
            }
        }

        /// <summary>Gets the container the footer of this group is added to.</summary>
        public DnnGenericControl FooterContainer
        {
            get
            {
                this.EnsureChildControls();
                return this.footerContainer;
            }
        }

        /// <inheritdoc />
        protected override string TagName => "div";

        /// <inheritdoc />
        public override IClientApiControl FindControl(string id)
        {
            this.EnsureChildControls();
            return base.FindControl(id);
        }

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            this.EnsureChildControls();

            if (!this.CheckVisibility())
            {
                return;
            }

            base.BuildRenderTree(builder);
        }

        /// <inheritdoc />
        protected override void CreateChildControls()
        {
            this.Controls.Clear();

            var topLeft = new DnnGenericControl("div");
            topLeft.Attributes["class"] = "topLeft";
            var topRight = new DnnGenericControl("div");
            topRight.Attributes["class"] = "topRight";

            var bottomLeft = new DnnGenericControl("div");
            bottomLeft.Attributes["class"] = "bottomLeft";
            var bottomRight = new DnnGenericControl("div");
            bottomRight.Attributes["class"] = "bottomRight";

            this.contentContainer = new DnnGenericControl("div");
            this.contentContainer.Attributes["class"] = "content";

            this.footerContainer = new DnnGenericControl("div");
            this.footerContainer.Attributes["class"] = "footer";

            this.Controls.Add(topLeft);
            this.Controls.Add(topRight);
            this.Controls.Add(this.contentContainer);
            this.Controls.Add(this.footerContainer);
            this.Controls.Add(bottomLeft);
            this.Controls.Add(bottomRight);

            if (this.Content != null)
            {
                this.contentContainer.ChildContent = this.Content;
            }

            if (this.Footer != null)
            {
                this.footerContainer.ChildContent = this.Footer;
            }
        }

        private static bool AreChildToolsVisible(IEnumerable<IClientApiControl> children, ref bool foundTool)
        {
            bool returnValue = false;

            foreach (var ctrl in children)
            {
                if (ctrl is IDnnRibbonBarTool)
                {
                    foundTool = true;
                    if (ctrl is not IDnnControl dnnControl || dnnControl.Visible)
                    {
                        returnValue = true;
                        break;
                    }
                }
                else
                {
                    if (AreChildToolsVisible(ctrl.Children, ref foundTool))
                    {
                        if (foundTool)
                        {
                            returnValue = true;
                            break;
                        }
                    }
                }
            }

            if (!foundTool)
            {
                return true;
            }

            return returnValue;
        }

        private bool CheckVisibility()
        {
            bool returnValue = true;
            if (this.Visible && this.CheckToolVisibility)
            {
                // Hide group if all tools are invisible
                bool foundTool = false;
                returnValue = AreChildToolsVisible(this.contentContainer.Controls.ToList(), ref foundTool);
            }
            else if (!this.Visible)
            {
                returnValue = false;
            }

            return returnValue;
        }
    }
}
