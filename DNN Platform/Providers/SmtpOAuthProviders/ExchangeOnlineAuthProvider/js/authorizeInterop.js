// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// The client side seam of Authorize.razor. It replaces the
// "<script type='text/javascript'>window.close();</script>" that the WebForms page
// Authorize.aspx wrote into its response from CloseWindow(): a Blazor render cannot execute a
// <script> element it writes, so the call is made over JS interop instead.

/** Closes the OAuth popup the administrator started the authorization from. */
export function closeWindow() {
    window.close();
}
