// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    /// <summary>Gets the iDNN Tool Bar Supported Actions.</summary>
    public interface IDNNToolBarSupportedActions
    {
        /// <summary>Gets the tool bar actions this control understands.</summary>
        string[] Actions { get; }
    }
}
