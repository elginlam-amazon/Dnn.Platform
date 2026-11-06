// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Modules.Html
{
    using System;
    using System.Collections;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Common;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Web.UI.WebControls.Internal;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>  MyWork allows a user to view any outstanding workflow items.</summary>
    /// <remarks>
    /// Was <c>MyWork.ascx</c> + <c>MyWork.ascx.cs</c> + <c>MyWork.ascx.designer.cs</c>; the markup
    /// is <c>MyWork.razor</c> and the two designer-generated fields are the <c>dgTabs</c> grid
    /// below and the <c>hlCancel</c> link, whose <c>NavigateUrl</c> is <see cref="CancelUrl"/>.
    /// </remarks>
    public partial class MyWork : PortalModuleBase
    {
        private readonly INavigationManager navigationManager;

        private DnnGrid dgTabs;
        private IList workItems;
        private bool dataBound;

        /// <summary>Initializes a new instance of the <see cref="MyWork"/> class.</summary>
        public MyWork()
        {
            this.navigationManager = this.DependencyProvider.GetRequiredService<INavigationManager>();
        }

        /// <summary>Gets the URL the cancel action returns to.</summary>
        /// <remarks>Replaces <c>hlCancel.NavigateUrl</c>.</remarks>
        protected string CancelUrl { get; private set; }

        /// <summary>Gets a value indicating whether there is nothing to show.</summary>
        /// <remarks>Drives the markup that was the grid's <c>EmptyDataTemplate</c>.</remarks>
        protected bool HasNoRecords => this.dataBound && (this.workItems is null || this.workItems.Count == 0);

        /// <summary>Gets or sets the grid, replacing the designer's <c>dgTabs</c> field.</summary>
        /// <remarks>
        /// The setter is the <c>@ref</c> capture of the markup; it declares the single template
        /// column the <c>&lt;Columns&gt;</c> element of the <c>.ascx</c> declared and hands the
        /// grid the data that has already been read, because <see cref="DnnGrid.Columns"/> is a
        /// collection rather than a parameter.
        /// </remarks>
        protected DnnGrid Grid
        {
            get => this.dgTabs;

            set
            {
                this.dgTabs = value;
                if (value is null || value.Columns.Count > 0)
                {
                    return;
                }

                value.Columns.Add(new DnnGridTemplateColumn
                {
                    HeaderText = "Page",
                    ItemTemplate = row => builder =>
                        builder.AddMarkupContent(0, this.FormatURL(row.DataItem)),
                });

                if (this.workItems is not null)
                {
                    value.DataSource = this.workItems;
                    value.DataBind();
                }
            }
        }

        /// <summary>Gets or sets the client resource controller the stylesheet is registered on.</summary>
        [Inject]
        private IClientResourceController ClientResourceController { get; set; }

        /// <summary>Formats a work item as a link to the page and module it belongs to.</summary>
        /// <param name="dataItem">The <see cref="HtmlTextUserInfo"/> of the row.</param>
        /// <returns>The anchor element of the row, as markup.</returns>
        public string FormatURL(object dataItem)
        {
            var objHtmlTextUser = (HtmlTextUserInfo)dataItem;
            return "<a href=\"" + this.navigationManager.NavigateURL(objHtmlTextUser.TabID) + "#" + objHtmlTextUser.ModuleID + "\">" + objHtmlTextUser.ModuleTitle + " ( " + objHtmlTextUser.StateName + " )</a>";
        }

        /// <summary>Page_Load runs when the control is loaded.</summary>
        /// <param name="e">The event arguments.</param>
        /// <remarks>
        /// The <c>!Page.IsPostBack</c> guard of the original becomes a first-pass guard: a Blazor
        /// component is not recreated per post back, so the data is read once.
        /// </remarks>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            this.CancelUrl = this.navigationManager.NavigateURL();

            try
            {
                if (!this.dataBound)
                {
                    this.dataBound = true;
                    var objHtmlTextUsers = new HtmlTextUserController();
                    this.workItems = objHtmlTextUsers.GetHtmlTextUser(this.UserInfo.UserID);
                    if (this.dgTabs is not null)
                    {
                        this.dgTabs.DataSource = this.workItems;
                        this.dgTabs.DataBind();
                    }
                }
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// Registers the stylesheet the <c>dnncl:DnnCssInclude</c> of the markup registered. This
        /// is the component's pre-render phase, which is where the tag helper ran.
        /// </remarks>
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
            this.ClientResourceController?.CreateStylesheet(Globals.ResolveUrl("~/DesktopModules/HTML/edit.css")).Register();
        }

        /// <inheritdoc />
        /// <remarks>
        /// The migrated <see cref="DnnGrid"/> assigns its own skin CSS class in its <c>Init</c>
        /// phase, i.e. after the parameters of the first render have been applied, so the class the
        /// <c>.ascx</c> declared is re-asserted once the grid exists.
        /// </remarks>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            if (!firstRender || this.dgTabs is null || this.dgTabs.CssClass == "dnnGrid")
            {
                return;
            }

            this.dgTabs.CssClass = "dnnGrid";
            this.StateHasChanged();
        }
    }
}
