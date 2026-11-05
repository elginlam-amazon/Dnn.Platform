// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Globalization;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.Web.Client.ClientResourceManagement;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Web;

    /// <summary>
    /// The AutoCompleteControl is the same as a TextEditControl but it looks up similar values
    /// in the profile of other users in the same portal and offers those in a dropdown under the text
    /// box where the user is entering the value. So if you'd use this as the input control
    /// for "City" it will find all entered cities and look up values as the user types the city in
    /// the textbox. Selection is not enforced and if a user enters a new city it is added to the list.
    /// </summary>
    internal class AutoCompleteControl : TextEditControl
    {
        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            // ClientResourceManager is per-request: it collects into the IClientResourceController of
            // the current request scope. HttpContextSource.Current is null once the Blazor circuit is
            // interactive, in which case the resources were already collected during the render that
            // served the page and re-registering them is neither possible nor needed.
            var context = HttpContextSource.Current;
            if (context != null)
            {
                ClientResourceManager.RegisterScript(context, "~/Resources/Shared/components/ProfileAutoComplete/dnn.ProfileAutoComplete.js");
                ClientResourceManager.RegisterFeatureStylesheet(context, "~/Resources/Shared/components/ProfileAutoComplete/dnn.AutoComplete.css");
            }

            JavaScript.RequestRegistration(CommonJs.jQuery);
            JavaScript.RequestRegistration(CommonJs.jQueryUI);
        }

        /// <inheritdoc />
        protected override void RenderEditMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            int length = this.MaxLength;

            builder.OpenElement(0, "input");
            this.AddStyleAttributes(builder, 1);
            builder.AddAttribute(5, "type", "text");
            builder.AddAttribute(6, "value", this.StringValue);
            if (length > Null.NullInteger)
            {
                builder.AddAttribute(7, "maxlength", length.ToString(CultureInfo.InvariantCulture));
            }

            builder.AddAttribute(8, "name", this.UniqueID);
            builder.AddAttribute(9, "id", this.ClientID);
            builder.AddAttribute(10, "data-name", this.Name);
            builder.AddAttribute(11, "data-pid", Entities.Portals.PortalSettings.Current.PortalId.ToString(CultureInfo.InvariantCulture));
            builder.AddAttribute(12, "data-editor", "AutoCompleteControl");
            builder.AddAttribute(13, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, this.OnInputChanged));
            builder.CloseElement();
        }
    }
}
