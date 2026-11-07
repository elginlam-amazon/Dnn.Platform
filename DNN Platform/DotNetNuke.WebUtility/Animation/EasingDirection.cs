// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Utilities.Animation
{
    /// <summary>The direction the easing of an animation is applied in.</summary>
    public enum EasingDirection
    {
        /// <summary>The easing is applied at the start of the animation.</summary>
        In = 0,

        /// <summary>The easing is applied at the end of the animation.</summary>
        Out = 1,

        /// <summary>The easing is applied at both ends of the animation.</summary>
        InOut = 2,
    }
}
