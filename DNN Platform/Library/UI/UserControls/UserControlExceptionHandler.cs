// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.UserControls
{
    using System;
    using System.Globalization;

    using DotNetNuke.Entities.Host;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.Localization;

    /// <summary>
    /// The replacement for <c>Exceptions.ProcessModuleLoadException(Control, Exception)</c> used by
    /// the Blazor ports of the DNN user controls.
    /// </summary>
    /// <remarks>
    /// The <c>Control</c> taking overloads cannot be used from a Blazor component (there is no
    /// <c>System.Web.UI.Control</c> and no control tree to add a skin message to). This helper keeps
    /// the two behaviours those overloads had: the exception is rethrown as a
    /// <see cref="ModuleLoadException"/> when custom error messages are disabled, and otherwise it
    /// is logged and the friendly message is returned so the component can render it itself.
    /// </remarks>
    internal static class UserControlExceptionHandler
    {
        /// <summary>Processes an exception raised while a user control was loading.</summary>
        /// <param name="exc">The exception that was caught.</param>
        /// <returns>The friendly message the control should render, or <see langword="null"/> when there is nothing to render.</returns>
        internal static string Process(Exception exc)
        {
            return Process(Localization.GetString("ErrorOccurred"), exc);
        }

        /// <summary>Processes an exception raised while a user control was loading.</summary>
        /// <param name="friendlyMessage">The message to show to the user.</param>
        /// <param name="exc">The exception that was caught.</param>
        /// <returns>The friendly message the control should render, or <see langword="null"/> when there is nothing to render.</returns>
        internal static string Process(string friendlyMessage, Exception exc)
        {
            if (exc is null)
            {
                return null;
            }

            if (string.IsNullOrEmpty(friendlyMessage))
            {
                friendlyMessage = string.Format(CultureInfo.CurrentCulture, "{0}", exc.Message);
            }

            if (!Host.UseCustomErrorMessages)
            {
                throw new ModuleLoadException(friendlyMessage, exc);
            }

            Exceptions.LogException(new ModuleLoadException(friendlyMessage, exc));
            return friendlyMessage;
        }
    }
}
