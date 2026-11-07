// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.Skins.Aperture.SkinObjects
{
    /// <summary>
    /// A single navigable link rendered by a skin object (for example terms or privacy).
    /// </summary>
    /// <param name="Text">The visible link text.</param>
    /// <param name="Href">The link destination. When empty the owning skin object renders nothing.</param>
    public readonly record struct ApertureLink(string Text, string Href);
}
