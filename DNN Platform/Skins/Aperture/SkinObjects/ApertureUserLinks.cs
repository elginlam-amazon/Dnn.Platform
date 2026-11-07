// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.Skins.Aperture.SkinObjects
{
    /// <summary>
    /// The authentication-dependent state used by the <c>dnn:Login</c> and <c>dnn:User</c> replacements.
    /// </summary>
    /// <param name="DisplayName">The display name of the current user, empty when anonymous.</param>
    /// <param name="LoginHref">The destination of the login link.</param>
    /// <param name="LogoutHref">The destination of the logout link.</param>
    /// <param name="RegisterHref">The destination of the registration link, empty when registration is disabled.</param>
    /// <param name="ProfileHref">The destination of the user profile link.</param>
    public readonly record struct ApertureUserLinks(
        string DisplayName,
        string LoginHref,
        string LogoutHref,
        string RegisterHref,
        string ProfileHref);
}
