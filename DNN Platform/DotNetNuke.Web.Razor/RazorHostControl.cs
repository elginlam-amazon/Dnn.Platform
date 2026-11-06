// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Razor
{
    using System;
    using System.IO;
    using System.Net;

    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Modules.Actions;
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.UI;
    using DotNetNuke.UI.Modules;

    /// <summary>The host for a razor module control.</summary>
    [DnnDeprecated(9, 3, 2, "Use Razor Pages instead")]
    public partial class RazorHostControl : ModuleControlBase, IActionable
    {
        private readonly string razorScriptFile;

        private RazorEngine engine;

        /// <summary>Initializes a new instance of the <see cref="RazorHostControl"/> class.</summary>
        /// <param name="scriptFile">The path to the Razor script file.</param>
        public RazorHostControl(string scriptFile)
        {
            this.razorScriptFile = scriptFile;
        }

        /// <inheritdoc />
        public ModuleActionCollection ModuleActions
        {
            get
            {
                if (this.Engine.Webpage is IActionable)
                {
                    return (this.Engine.Webpage as IActionable).ModuleActions;
                }

                return new ModuleActionCollection();
            }
        }

        /// <summary>Gets the virtual path of the razor script this control hosts.</summary>
        protected virtual string RazorScriptFile
        {
            get { return this.razorScriptFile; }
        }

        private RazorEngine Engine
        {
            get
            {
                if (this.engine == null)
                {
                    this.engine = new RazorEngine(this.RazorScriptFile, this.ModuleContext, this.LocalResourceFile);
                }

                return this.engine;
            }
        }

        /// <inheritdoc />
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);

            if (!string.IsNullOrEmpty(this.RazorScriptFile))
            {
                var writer = new StringWriter();
                this.Engine.Render(writer);

                // WebUtility.HtmlDecode replaces System.Web.HttpUtility.HtmlDecode; the decode is
                // required because the razor output is added as literal markup.
                this.Controls.Add(new DnnLiteralControl(WebUtility.HtmlDecode(writer.ToString())));
            }
        }
    }
}
