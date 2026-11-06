// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.DDRMenu
{
    using System;

    using DotNetNuke.Entities.Modules;

    public class ModuleBase : PortalModuleBase
    {
        /// <summary>
        /// Gets the client side identifier of this module control, which the menu renders as the id
        /// of its root element and passes to the navigation provider.
        /// </summary>
        /// <remarks>
        /// Replaces <c>Control.ClientID</c>. A module control is a Blazor component and has no
        /// WebForms naming container, so the id is built from the module instance, which is what
        /// made the WebForms id unique on the page as well.
        /// </remarks>
        public string ClientId => $"dnn_ctr{this.ModuleId}_{this.GetType().Name}";

        protected string GetStringSetting(string name, string defaultValue)
        {
            // Read from the request captured by UserControlBase during the initial (static) render;
            // HttpContextSource.Current is null for the rest of a Blazor Server circuit.
            var request = this.Request;
            var result = request == null ? null : (string)request.Query[name];
            if (string.IsNullOrEmpty(result))
            {
                result = (string)this.Settings[name];
            }

            if (string.IsNullOrEmpty(result))
            {
                result = defaultValue;
            }

            if (result != null)
            {
                result = result.Trim();
            }

            if (result == "-")
            {
                result = string.Empty;
            }

            return result;
        }

        protected string GetStringSetting(string name)
        {
            return this.GetStringSetting(name, string.Empty);
        }

        protected int GetIntSetting(string name, int defaultValue)
        {
            return Convert.ToInt32(this.GetStringSetting(name, defaultValue.ToString()));
        }

        protected int GetIntSetting(string name)
        {
            return this.GetIntSetting(name, 0);
        }

        protected bool GetBoolSetting(string name, bool defaultValue)
        {
            var result = this.GetStringSetting(name);
            return (result == string.Empty) ? defaultValue : Convert.ToBoolean(result);
        }

        protected bool GetBoolSetting(string name)
        {
            return this.GetBoolSetting(name, false);
        }
    }
}
