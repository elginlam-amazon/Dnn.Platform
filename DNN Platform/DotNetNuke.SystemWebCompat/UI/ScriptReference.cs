// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.UI
{
    /// <summary>A reference to a client script file registered with the <see cref="ScriptManager"/>.</summary>
    public class ScriptReference
    {
        /// <summary>Initializes a new instance of the <see cref="ScriptReference"/> class.</summary>
        public ScriptReference()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ScriptReference"/> class.</summary>
        /// <param name="path">The path of the script.</param>
        public ScriptReference(string path)
        {
            this.Path = path;
        }

        /// <summary>Gets or sets the (virtual) path of the script.</summary>
        public string Path { get; set; }

        /// <summary>Gets or sets the name of an embedded script resource (not supported on .NET 10, only <see cref="Path"/> is rendered).</summary>
        public string Name { get; set; }

        /// <summary>Gets or sets the assembly of an embedded script resource.</summary>
        public string Assembly { get; set; }

        /// <summary>Gets or sets a value indicating whether the script path of the <see cref="ScriptManager"/> is ignored.</summary>
        public bool IgnoreScriptPath { get; set; }

        /// <summary>Resolves the URL of the script.</summary>
        /// <param name="control">The control used to resolve relative URLs.</param>
        /// <returns>The URL or <see langword="null"/>.</returns>
        internal string GetUrl(Control control)
        {
            if (string.IsNullOrEmpty(this.Path))
            {
                return null;
            }

            return control.ResolveClientUrl(this.Path);
        }
    }
}
