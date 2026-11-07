// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Utilities.Animation
{
    /// <summary>The kind of animation applied to an element.</summary>
    public enum AnimationType
    {
        /// <summary>No animation.</summary>
        None = 0,

        /// <summary>The element slides into view.</summary>
        Slide = 1,

        /// <summary>The element expands into view.</summary>
        Expand = 2,

        /// <summary>The element animates diagonally.</summary>
        Diagonal = 3,

        /// <summary>The element animates diagonally in the opposite direction.</summary>
        ReverseDiagonal = 4,
    }
}
