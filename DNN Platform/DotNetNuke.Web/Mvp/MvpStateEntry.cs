// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Mvp
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Text.Json;

    using DotNetNuke.Internal.SourceGenerators;

    /// <summary>A single serialized <see cref="MvpStateBag"/> entry.</summary>
    [DnnDeprecated(9, 2, 0, "Replace WebFormsMvp and DotNetNuke.Web.Mvp with MVC or SPA patterns instead")]
    public partial class MvpStateEntry
    {
        /// <summary>Gets or sets the assembly qualified name of the value's type.</summary>
        public string TypeName { get; set; }

        /// <summary>Gets or sets the JSON representation of the value.</summary>
        public string Json { get; set; }
    }
}
