// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.ConfigSection
{
    using System.Collections.Generic;

    /// <summary>The options for the DNN services-framework authentication handlers.</summary>
    /// <remarks>
    /// <para>
    /// This replaces the <c>AuthServicesConfiguration</c>
    /// <c>System.Configuration.ConfigurationSection</c> (<c>dotnetnuke/authServices</c>) and its
    /// <c>MessageHandlersCollection</c> <c>ConfigurationElementCollection</c>, neither of which has
    /// a modern .NET equivalent: <c>ConfigurationManager.GetSection</c> returns
    /// <see langword="null"/> without an <c>app.config</c>.
    /// </para>
    /// <para>
    /// Bind it from <see cref="SectionName"/> — <c>DotNetNuke:authServices</c> — which carries the
    /// same <c>messageHandlers</c> entries the <c>&lt;messageHandlers&gt;</c> element did.
    /// </para>
    /// </remarks>
    public class AuthServicesOptions
    {
        /// <summary>The configuration section these options bind from.</summary>
        public const string SectionName = "DotNetNuke:authServices";

        /// <summary>Gets the ordered message handlers, in the order they are added to the authentication chain.</summary>
        public IList<MessageHandlerEntry> MessageHandlers { get; } = new List<MessageHandlerEntry>();
    }
}
