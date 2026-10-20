// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.UI
{
    /// <summary>Specifies when the content of an <see cref="UpdatePanel"/> is updated.</summary>
    public enum UpdatePanelUpdateMode
    {
        /// <summary>The content is updated on every postback.</summary>
        Always = 0,

        /// <summary>The content is updated under specific conditions.</summary>
        Conditional = 1,
    }
}
