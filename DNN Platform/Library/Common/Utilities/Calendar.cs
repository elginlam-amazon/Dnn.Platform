// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Common.Utilities
{
    using System.Globalization;
    using System.Text;
    using System.Text.Encodings.Web;

    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI.Utilities;

    public class Calendar
    {
        /// <summary>Opens a popup Calendar.</summary>
        /// <param name="field">The control to return the date value to (was a <c>System.Web.UI.WebControls.TextBox</c>).</param>
        /// <returns>A JavaScript URL.</returns>
        public static string InvokePopupCal(IClientApiControl field)
        {
            // Define character array to trim from language strings
            char[] trimChars = { ',', ' ' };

            // Get culture array of month names and convert to string for
            // passing to the popup calendar
            var monthBuilder = new StringBuilder();
            foreach (string month in DateTimeFormatInfo.CurrentInfo.MonthNames)
            {
                monthBuilder.AppendFormat(CultureInfo.InvariantCulture, "{0},", month);
            }

            var monthNameString = monthBuilder.ToString().TrimEnd(trimChars);

            // Get culture array of day names and convert to string for
            // passing to the popup calendar
            var dayBuilder = new StringBuilder();
            foreach (string day in DateTimeFormatInfo.CurrentInfo.AbbreviatedDayNames)
            {
                dayBuilder.AppendFormat(CultureInfo.InvariantCulture, "{0},", day);
            }

            var dayNameString = dayBuilder.ToString().TrimEnd(trimChars);

            // Get the short date pattern for the culture
            string formatString = DateTimeFormatInfo.CurrentInfo.ShortDatePattern;

            // Was: if (!field.Page.ClientScript.IsClientScriptIncludeRegistered(...)) ScriptManager.RegisterClientScriptInclude(...).
            // Registration is keyed, so registering is already once-per-page and the guard is redundant.
            field?.Page?.RegisterClientScriptInclude("PopupCalendar.js", ClientAPI.ScriptPath + "PopupCalendar.js");

            string strToday = ClientAPI.GetSafeJSString(Localization.GetString("Today"));
            string strClose = ClientAPI.GetSafeJSString(Localization.GetString("Close"));
            string strCalendar = ClientAPI.GetSafeJSString(Localization.GetString("Calendar"));
            return
                string.Format(
                    CultureInfo.InvariantCulture,
                    "javascript:popupCal('Cal','{0}','{1}','{2}','{3}','{4}','{5}','{6}',{7});",
                    JavaScriptEncoder.Default.Encode(field?.ClientId),
                    JavaScriptEncoder.Default.Encode(formatString),
                    JavaScriptEncoder.Default.Encode(monthNameString),
                    JavaScriptEncoder.Default.Encode(dayNameString),
                    JavaScriptEncoder.Default.Encode(strToday),
                    JavaScriptEncoder.Default.Encode(strClose),
                    JavaScriptEncoder.Default.Encode(strCalendar),
                    (int)DateTimeFormatInfo.CurrentInfo.FirstDayOfWeek);
        }
    }
}
