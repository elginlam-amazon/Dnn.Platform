// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Razor
{
    using System;
    using System.IO;

    using DotNetNuke.Common;
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.UI;
    using DotNetNuke.UI.Modules;

    /// <summary>A base class for razor modules.</summary>
    [DnnDeprecated(9, 3, 2, "Use Razor Pages instead")]
    public partial class RazorModuleBase : ModuleUserControlBase
    {
        /// <summary>Whether the razor script has already been rendered into the control tree.</summary>
        private bool razorScriptRendered;

        /// <summary>Gets the virtual path of the razor script backing this module, or an empty string when there is none.</summary>
        /// <remarks>
        /// The net48 implementation combined <c>Control.AppRelativeTemplateSourceDirectory</c> with
        /// <c>Control.AppRelativeVirtualPath</c> and probed the file with
        /// <c>HttpServerUtility.MapPath</c>. The directory and the file name now come from the
        /// component's <c>ControlSrc</c>/<c>TemplateSourceDirectory</c> and the probe goes through
        /// <see cref="HttpContextSource.MapPath(string)"/>, which maps a virtual path with the
        /// registered <c>IWebHostEnvironment</c>.
        /// </remarks>
        protected virtual string RazorScriptFile
        {
            get
            {
                var scriptFolder = this.TemplateSourceDirectory ?? string.Empty;
                if (scriptFolder.Length > 0 && !scriptFolder.EndsWith('/'))
                {
                    scriptFolder += "/";
                }

                var fileRoot = Path.GetFileNameWithoutExtension(this.ControlSrc ?? string.Empty);
                var scriptFile = scriptFolder + "_" + fileRoot + ".cshtml";

                if (!File.Exists(HttpContextSource.MapPath(scriptFile)))
                {
                    // Try VB (vbhtml)
                    scriptFile = scriptFolder + "_" + fileRoot + ".vbhtml";

                    if (!File.Exists(HttpContextSource.MapPath(scriptFile)))
                    {
                        ////Return ""
                        scriptFile = string.Empty;
                    }
                }

                return scriptFile;
            }
        }

        /// <inheritdoc />
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // The razor script is rendered here, during the load phase, and NOT from OnPreRender as
            // the net48 control did. DnnComponentBase raises OnPreRender from OnAfterRender, i.e.
            // *after* the render tree has been built: under static server-side rendering
            // OnAfterRender never runs at all, so a module whose entire output comes from the script
            // would render blank. OnLoad runs from OnParametersSet, before the render pass, so the
            // DnnLiteralControl is already in Controls by the time RenderContents emits them.
            this.EnsureRazorScriptRendered();
        }

        /// <inheritdoc />
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);

            // Kept for contract parity with the net48 lifecycle: if a derived module suppressed the
            // load phase, the script still renders here. EnsureRazorScriptRendered is idempotent.
            this.EnsureRazorScriptRendered();
        }

        /// <summary>Renders the razor script into this control's tree, at most once.</summary>
        private void EnsureRazorScriptRendered()
        {
            if (this.razorScriptRendered)
            {
                return;
            }

            this.razorScriptRendered = true;
            try
            {
                if (!string.IsNullOrEmpty(this.RazorScriptFile))
                {
                    var razorEngine = new RazorEngine(this.RazorScriptFile, this.ModuleContext, this.LocalResourceFile);
                    var writer = new StringWriter();
                    razorEngine.Render(writer);

                    this.Controls.Add(new DnnLiteralControl(writer.ToString()));
                }
            }
            catch (Exception ex)
            {
                Exceptions.ProcessModuleLoadException(this, ex);
            }
        }
    }
}
