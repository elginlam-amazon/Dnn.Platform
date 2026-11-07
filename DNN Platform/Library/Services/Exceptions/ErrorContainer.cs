// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Exceptions
{
    using System;
    using System.Net;

    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.UI;
    using DotNetNuke.UI.Skins.Controls;

    using Localization = DotNetNuke.Services.Localization.Localization;

    /// <remarks>Was a <c>System.Web.UI.Control</c>; it is now a DNN skinning component.</remarks>
    public class ErrorContainer : DnnComponentBase
    {
        /// <summary>Initializes a new instance of the <see cref="ErrorContainer"/> class.</summary>
        /// <param name="strError">The error message.</param>
        public ErrorContainer(string strError)
        {
            this.Container = FormatException(strError);
        }

        /// <summary>Initializes a new instance of the <see cref="ErrorContainer"/> class.</summary>
        /// <param name="strError">The error message.</param>
        /// <param name="exc">The exception.</param>
        public ErrorContainer(string strError, Exception exc)
        {
            UserInfo objUserInfo = UserController.Instance.GetCurrentUserInfo();
            if (objUserInfo.IsSuperUser)
            {
                this.Container = FormatException(strError, exc);
            }
            else
            {
                this.Container = FormatException(strError);
            }
        }

        /// <summary>Initializes a new instance of the <see cref="ErrorContainer"/> class.</summary>
        /// <param name="portalSettings">The portal settings.</param>
        /// <param name="strError">The error message.</param>
        /// <param name="exc">The exception.</param>
        public ErrorContainer(PortalSettings portalSettings, string strError, Exception exc)
            : this(strError, exc)
        {
        }

        public ModuleMessage Container { get; set; }

        private static ModuleMessage FormatException(string strError)
        {
            ModuleMessage m;
            m = UI.Skins.Skin.GetModuleMessageControl(Localization.GetString("ErrorOccurred"), strError, ModuleMessage.ModuleMessageType.RedError);
            return m;
        }

        private static ModuleMessage FormatException(string strError, Exception exc)
        {
            ModuleMessage m;
            if (exc != null)
            {
                m = UI.Skins.Skin.GetModuleMessageControl(strError, WebUtility.HtmlEncode(exc.ToString()), ModuleMessage.ModuleMessageType.RedError);
            }
            else
            {
                m = UI.Skins.Skin.GetModuleMessageControl(Localization.GetString("ErrorOccurred"), strError, ModuleMessage.ModuleMessageType.RedError);
            }

            return m;
        }
    }
}
