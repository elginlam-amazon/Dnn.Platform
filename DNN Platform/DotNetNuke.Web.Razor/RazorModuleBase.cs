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
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
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
