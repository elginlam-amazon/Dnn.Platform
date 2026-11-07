// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.Modules.TelerikRemoval.UserControls
{
    using System;

    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Web.UI.WebControls;

    using Microsoft.Extensions.DependencyInjection;

    /// <summary>A simple wrapper around <see cref="DnnButton"/>.</summary>
    /// <remarks>
    /// On .NET Framework this type existed only to add <c>System.Web.UI.ITextControl</c> to
    /// <see cref="DnnButton"/>, so that
    /// <see cref="DotNetNuke.Web.UI.Utilities.GetLocalizedStringFromParent(string, DotNetNuke.UI.Utilities.IClientApiControl)"/>
    /// could read the button's <c>Text</c>. <c>ITextControl</c> is a
    /// <c>System.Web.UI</c> type and does not exist on modern .NET, and the migrated
    /// <see cref="DnnButton"/> is a Blazor component that already declares
    /// <see cref="DnnButton.Text"/> and implements <see cref="DotNetNuke.Web.UI.ILocalizable"/>
    /// itself, so the interface is no longer needed. The type is kept (rather than deleted and its
    /// usages rewritten) because it is the component the module's view instantiates, and because
    /// its <c>IClientResourceController</c> resolution is part of the public surface.
    /// </remarks>
    public class DnnTextButton : DnnButton
    {
        /// <summary>Initializes a new instance of the <see cref="DnnTextButton"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IClientResourceController. Scheduled removal in v12.0.0.")]
        public DnnTextButton()
            : this(null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnTextButton"/> class.</summary>
        /// <param name="clientResourceController">The client resource controller.</param>
        public DnnTextButton(IClientResourceController clientResourceController)
            : base(clientResourceController ?? HttpContextSource.Current.GetScope().ServiceProvider.GetRequiredService<IClientResourceController>())
        {
        }
    }
}
