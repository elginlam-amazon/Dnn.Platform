// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Common.Options
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Settings for the generated-image handler. Replaces the <c>&lt;appSettings&gt;</c> keys
    /// <c>DnnImageHandler</c> (a semicolon-delimited <c>name=value</c> list) and
    /// <c>DnnImageHandler.&lt;mode&gt;</c> (a <c>Type, Assembly</c> pair per custom transform).
    /// </summary>
    public class ImageHandlerOptions
    {
        /// <summary>The configuration section name.</summary>
        public const string SectionName = "ImageHandler";

        /// <summary>
        /// Gets or sets the semicolon-delimited default settings, in the same
        /// <c>name=value;name=value</c> shape the <c>DnnImageHandler</c> application setting used.
        /// </summary>
        public string Settings { get; set; }

        /// <summary>
        /// Gets or sets the custom image transforms, keyed by the <c>mode</c> query-string value,
        /// with a <c>TypeName, AssemblyName</c> value.
        /// </summary>
        public Dictionary<string, string> Transforms { get; set; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
