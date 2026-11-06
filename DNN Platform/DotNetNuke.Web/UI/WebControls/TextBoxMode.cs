// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using System;
    using System.Globalization;

    /// <summary>The behavior mode of a text box.</summary>
    /// <remarks>
    /// Replaces <c>System.Web.UI.WebControls.TextBoxMode</c>, which does not exist on modern .NET.
    /// The members — and therefore every <c>TextMode = TextBoxMode.X</c> assignment DNN and its
    /// modules make — are preserved, and <see cref="TextBoxModeExtensions.ToInputType"/> maps them
    /// onto the HTML <c>input</c> types the Web Forms control rendered.
    /// </remarks>
    public enum TextBoxMode
    {
        /// <summary>A single line text input.</summary>
        SingleLine = 0,

        /// <summary>A multi line text area.</summary>
        MultiLine = 1,

        /// <summary>A single line input that masks what is typed.</summary>
        Password = 2,

        /// <summary>A colour picker.</summary>
        Color = 3,

        /// <summary>A date picker.</summary>
        Date = 4,

        /// <summary>A date and time picker, with a time zone.</summary>
        DateTime = 5,

        /// <summary>A local date and time picker.</summary>
        DateTimeLocal = 6,

        /// <summary>An e-mail address input.</summary>
        Email = 7,

        /// <summary>A month picker.</summary>
        Month = 8,

        /// <summary>A number input.</summary>
        Number = 9,

        /// <summary>A range slider.</summary>
        Range = 10,

        /// <summary>A search input.</summary>
        Search = 11,

        /// <summary>A telephone number input.</summary>
        Phone = 12,

        /// <summary>A time picker.</summary>
        Time = 13,

        /// <summary>A URL input.</summary>
        Url = 14,

        /// <summary>A week picker.</summary>
        Week = 15,
    }
}
