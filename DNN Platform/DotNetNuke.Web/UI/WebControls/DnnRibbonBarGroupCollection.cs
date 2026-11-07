// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls;

using System;

using DotNetNuke.UI;

/// <summary>A collection of <see cref="DnnRibbonBarGroup"/> controls.</summary>
public class DnnRibbonBarGroupCollection : TypedControlCollection<DnnRibbonBarGroup>
{
    /// <summary>Initializes a new instance of the <see cref="DnnRibbonBarGroupCollection"/> class.</summary>
    /// <param name="owner">The owner control.</param>
    public DnnRibbonBarGroupCollection(DnnComponentBase owner)
        : base(owner)
    {
    }

    /// <inheritdoc />
    protected override void ThrowOnInvalidControlType()
    {
        // NotSupportedException, not ArgumentException: this reproduces the baseline
        // DnnRibbonBar.AddParsedSubObject guard exactly. The message is unchanged, and with 31 projects
        // depending on this assembly anything that catches NotSupportedException must keep catching it.
#pragma warning disable CA2208 // Instantiate argument exceptions correctly -- deliberate parity with the baseline exception type.
        throw new NotSupportedException("DnnRibbonBarGroupCollection must contain controls of type DnnRibbonBarGroup");
#pragma warning restore CA2208
    }
}
