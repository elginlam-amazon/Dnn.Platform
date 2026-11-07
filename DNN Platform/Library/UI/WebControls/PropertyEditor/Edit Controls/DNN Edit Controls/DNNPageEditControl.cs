// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.Services.Localization;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Web;

    /// <summary>
    /// The DNNPageEditControl control provides a standard UI component for selecting
    /// a DNN Page.
    /// </summary>
    public class DNNPageEditControl : IntegerEditControl
    {
        /// <summary>RenderEditMode renders the Edit mode of the control.</summary>
        /// <param name="builder">The render tree builder to write to.</param>
        protected override void RenderEditMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            PortalSettings portalSettings = Globals.GetPortalSettings();

            // Get the Pages
            List<TabInfo> listTabs = TabController.GetPortalTabs(portalSettings.PortalId, Null.NullInteger, true, "<" + Localization.GetString("None_Specified") + ">", true, false, true, true, false);

            // Render the Select Tag
            builder.OpenElement(0, "select");
            this.AddStyleAttributes(builder, 1);
            builder.AddAttribute(5, "name", this.UniqueID);
            builder.AddAttribute(6, "id", this.ClientID);
            builder.AddAttribute(7, "aria-label", this.Name);
            builder.AddAttribute(8, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, this.OnInputChanged));

            builder.OpenRegion(9);
            for (int tabIndex = 0; tabIndex <= listTabs.Count - 1; tabIndex++)
            {
                TabInfo tab = listTabs[tabIndex];

                builder.OpenElement(0, "option");

                // Add the Value Attribute
                builder.AddAttribute(1, "value", tab.TabID.ToString(CultureInfo.InvariantCulture));

                if (tab.TabID == this.IntegerValue)
                {
                    // Add the Selected Attribute
                    builder.AddAttribute(2, "selected", "selected");
                }

                builder.AddContent(3, tab.IndentedTabName);
                builder.CloseElement();
            }

            builder.CloseRegion();

            // Close Select Tag
            builder.CloseElement();
        }

        /// <summary>RenderViewMode renders the View mode of the control.</summary>
        /// <param name="builder">The render tree builder to write to.</param>
        protected override void RenderViewMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            TabInfo linkedTabInfo = TabController.Instance.GetTab(this.IntegerValue, Globals.GetPortalSettings().PortalId, false);

            // don't render anything if we didn't find the tab
            if (linkedTabInfo == null)
            {
                return;
            }

            // Not really sure how to get a good TabID and ModuleID, but it's only for tracking so not to concerned.
            // The query string is read off the current request instead of Page.Request; it is null once
            // the Blazor circuit is interactive, in which case the tracking ids fall back to Null.NullInteger
            // exactly as they did when the query string parameters were absent.
            var query = HttpContextSource.Current?.Request?.Query;
            if (!int.TryParse(query?["tabid"].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var tabId))
            {
                tabId = Null.NullInteger;
            }

            if (!int.TryParse(query?["mid"].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var moduleId))
            {
                moduleId = Null.NullInteger;
            }

            string url = Globals.LinkClick(this.StringValue, tabId, moduleId, true);

            builder.OpenElement(0, "a");
            this.AddStyleAttributes(builder, 1);
            builder.AddAttribute(5, "href", url);
            builder.AddAttribute(6, "class", "Normal");
            builder.AddContent(7, linkedTabInfo.LocalizedTabName);
            builder.CloseElement();
        }
    }
}
