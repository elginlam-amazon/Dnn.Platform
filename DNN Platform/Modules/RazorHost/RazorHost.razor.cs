// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Modules.RazorHost
{
    using System.Globalization;

    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Modules.Actions;
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Security;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Web.Razor;

    using Microsoft.AspNetCore.Components;

    /// <summary>Implements the RazorHost view logic.</summary>
    /// <remarks>
    /// Was <c>RazorHost.ascx</c> + <c>RazorHost.ascx.cs</c> + <c>RazorHost.ascx.designer.cs</c>; the
    /// designer file declared no control, because the markup was the <c>&lt;%@ Control %&gt;</c>
    /// directive alone.
    /// </remarks>
    [DnnDeprecated(9, 3, 2, "Use Razor Pages instead")]
    public partial class RazorHost : RazorModuleBase, IActionable
    {
        private readonly string razorScriptFileFormatString = "~/DesktopModules/RazorModules/RazorHost/Scripts/{0}";

        /// <inheritdoc />
        public ModuleActionCollection ModuleActions
        {
            get
            {
                var actions = new ModuleActionCollection();
                actions.Add(
                    this.ModuleContext.GetNextActionID(),
                    Localization.GetString(ModuleActionType.EditContent, this.LocalResourceFile),
                    ModuleActionType.AddContent,
                    string.Empty,
                    "edit.gif",
                    this.ModuleContext.EditUrl(),
                    false,
                    SecurityAccessLevel.Host,
                    true,
                    false);
                actions.Add(
                    this.ModuleContext.GetNextActionID(),
                    Localization.GetString("CreateModule.Action", this.LocalResourceFile),
                    ModuleActionType.AddContent,
                    string.Empty,
                    "edit.gif",
                    this.ModuleContext.EditUrl("CreateModule"),
                    false,
                    SecurityAccessLevel.Host,
                    true,
                    false);
                return actions;
            }
        }

        /// <inheritdoc />
        protected override string RazorScriptFile
        {
            get
            {
                string razorScriptFile = base.RazorScriptFile;
                var scriptFileSetting = this.ModuleContext.Settings["ScriptFile"] as string;
                if (!string.IsNullOrEmpty(scriptFileSetting))
                {
                    razorScriptFile = string.Format(CultureInfo.InvariantCulture, this.razorScriptFileFormatString, scriptFileSetting);
                }

                return razorScriptFile;
            }
        }

        /// <summary>Gets the output <see cref="RazorModuleBase"/> appended to this component's control tree.</summary>
        /// <remarks>
        /// <c>RazorHost.razor</c> renders this fragment. It is the content <c>DnnComponentBase</c>
        /// would have rendered itself if this component had no markup file of its own, i.e. the
        /// <c>DnnLiteralControl</c> carrying the rendered razor script.
        /// </remarks>
        protected RenderFragment RenderedScriptOutput => builder =>
        {
            var sequence = 0;
            this.RenderContents(builder, ref sequence);
        };
    }
}
