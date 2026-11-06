// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Modules.RazorHost
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;

    using DotNetNuke.Abstractions;
    using DotNetNuke.Common;
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI.Modules;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Implements the AddScript view logic.</summary>
    /// <remarks>
    /// Was <c>AddScript.ascx</c> + <c>AddScript.ascx.cs</c> + <c>AddScript.ascx.designer.cs</c>. The
    /// markup is <c>AddScript.razor</c>; the designer-generated fields are the labels and action
    /// links the markup declares plus the bound values below (<c>scriptFileType</c> →
    /// <see cref="FileTypes"/>/<see cref="SelectedFileType"/>, <c>fileName</c> →
    /// <see cref="FileName"/>, <c>fileExtension</c> → <see cref="FileExtension"/>).
    /// </remarks>
    [DnnDeprecated(9, 3, 2, "Use Razor Pages instead")]
    public partial class AddScript : ModuleUserControlBase
    {
        private readonly INavigationManager navigationManager;
        private readonly string razorScriptFileFormatString = "~/DesktopModules/RazorModules/RazorHost/Scripts/{0}";

        /// <summary>Whether Add has been clicked at least once, so validation may report.</summary>
        /// <remarks>
        /// Matches the WebForms <c>Display="Dynamic"</c> validator behaviour used by
        /// <c>CreateModule</c>: the message is not shown on the first, untouched render.
        /// </remarks>
        private bool validationAttempted;

        /// <summary>Initializes a new instance of the <see cref="AddScript"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.0.0. Please use overload with INavigationManager. Scheduled removal in v12.0.0.")]
        public AddScript()
            : this(null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="AddScript"/> class.</summary>
        /// <param name="navigationManager">The navigation manager.</param>
        public AddScript(INavigationManager navigationManager)
        {
            this.navigationManager = navigationManager ?? Globals.GetCurrentServiceProvider().GetRequiredService<INavigationManager>();
        }

        /// <summary>Gets the script file types offered by the file type list.</summary>
        /// <remarks>
        /// Replaces the two <c>asp:ListItem</c>s of <c>scriptFileType</c>. VBHTML is deliberately
        /// retained even though ASP.NET Core Razor runtime compilation cannot compile VB; the host's
        /// <c>RazorModuleControlFactory.SupportsControl</c> still claims <c>.vbhtml</c>, so this is
        /// host-owned follow-up debt and not a reason to drop the option or its resource key.
        /// </remarks>
        protected IList<string> FileTypes { get; } = new List<string> { "CSHTML", "VBHTML" };

        /// <summary>Gets the selected script file type.</summary>
        /// <remarks>Replaces <c>scriptFileType.SelectedValue</c>; CSHTML was <c>Selected="True"</c>.</remarks>
        protected string SelectedFileType { get; private set; } = "CSHTML";

        /// <summary>Gets the name typed for the new script.</summary>
        /// <remarks>Replaces <c>fileName.Text</c>.</remarks>
        protected string FileName { get; private set; } = string.Empty;

        /// <summary>Gets the extension displayed next to the file name.</summary>
        /// <remarks>Replaces <c>fileExtension.Text</c>.</remarks>
        protected string FileExtension { get; private set; } = string.Empty;

        /// <summary>Gets a value indicating whether a file name was supplied.</summary>
        /// <remarks>
        /// Replaces <c>Page.IsValid</c> in <see cref="CmdAdd_Click"/>. The net48 markup declared no
        /// validator, so this check is new; it is paired with <see cref="validationAttempted"/> and
        /// a <c>dnnFormError</c> message in the markup so an empty name reports itself instead of
        /// the Add button silently doing nothing.
        /// </remarks>
        protected bool FileNameValid => !string.IsNullOrWhiteSpace(this.FileName);

        /// <summary>Gets or sets the navigation manager the redirects go through.</summary>
        /// <remarks>Replaces <c>Response.Redirect(url, true)</c>.</remarks>
        [Inject]
        protected Microsoft.AspNetCore.Components.NavigationManager Navigation { get; set; }

        /// <inheritdoc />
        /// <remarks>
        /// This is <c>OnLoad</c> of the original. The event wiring of the original <c>OnInit</c> is
        /// the markup's <c>@onclick</c>/<c>@onchange</c> attributes.
        /// </remarks>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            this.DisplayExtension();
        }

        /// <summary>Cancel button click event handler.</summary>
        protected void CmdCancel_Click()
        {
            try
            {
                this.Navigation.NavigateTo(this.ModuleContext.EditUrl("Edit"), forceLoad: true);
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <summary>Add button event handler.</summary>
        protected void CmdAdd_Click()
        {
            try
            {
                if (!this.ModuleContext.PortalSettings.UserInfo.IsSuperUser)
                {
                    this.Navigation.NavigateTo(this.navigationManager.NavigateURL("Access Denied"), forceLoad: true);
                    return;
                }

                this.validationAttempted = true;

                if (this.FileNameValid)
                {
                    string scriptFileName = "_" + Path.GetFileNameWithoutExtension(this.FileName) + "." + this.SelectedFileType.ToLowerInvariant();

                    string srcFile = HttpContextSource.MapPath(string.Format(CultureInfo.InvariantCulture, this.razorScriptFileFormatString, scriptFileName));

                    // write file
                    using (var objStream = File.CreateText(srcFile))
                    {
                        objStream.WriteLine(Localization.GetString("NewScript", this.LocalResourceFile));
                    }

                    this.Navigation.NavigateTo(this.ModuleContext.EditUrl("Edit"), forceLoad: true);
                }
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        private void DisplayExtension()
        {
            this.FileExtension = "." + this.SelectedFileType.ToLowerInvariant();
        }

        /// <summary>Handles the file type being picked, which was an AutoPostBack.</summary>
        /// <param name="args">The arguments carrying the newly selected value.</param>
        private void ScriptFileType_SelectedIndexChanged(ChangeEventArgs args)
        {
            this.SelectedFileType = args?.Value?.ToString() ?? this.FileTypes[0];
            this.DisplayExtension();
        }

        /// <summary>Handles the file name being typed into.</summary>
        /// <param name="args">The arguments carrying the new file name.</param>
        private void OnFileNameChanged(ChangeEventArgs args)
        {
            this.FileName = args?.Value?.ToString() ?? string.Empty;
        }
    }
}
