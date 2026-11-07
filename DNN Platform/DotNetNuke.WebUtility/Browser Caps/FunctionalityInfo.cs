// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Utilities
{
    using System;
    using System.Xml.Serialization;

    /// <summary>
    /// Describes one piece of client side functionality and the browsers supporting it.
    /// </summary>
    public class FunctionalityInfo
    {
        /// <summary>Gets or sets the functionality this entry describes.</summary>
        [XmlAttribute("nm")]
        public ClientAPI.ClientFunctionality Functionality { get; set; }

        /// <summary>Gets or sets the human readable description of the functionality.</summary>
        [XmlAttribute("desc")]
        public string Desc { get; set; }

        /// <summary>Gets or sets the browsers explicitly excluded from the functionality.</summary>
        [XmlElement("excludes")]
        public BrowserCollection Excludes { get; set; }

        /// <summary>Gets or sets the browsers supporting the functionality.</summary>
        [XmlElement("supports")]
        public BrowserCollection Supports { get; set; }

        /// <summary>Determines whether the given browser supports this functionality.</summary>
        /// <param name="strAgent">The full user agent string of the request.</param>
        /// <param name="strBrowser">The name of the browser.</param>
        /// <param name="dblVersion">The version of the browser.</param>
        /// <returns><see langword="true"/> when the browser supports the functionality and is not excluded from it.</returns>
        public bool HasMatch(string strAgent, string strBrowser, double dblVersion)
        {
            // Parse through the supported browsers to find a match
            var hasMatch = HasMatch(this.Supports, strAgent, strBrowser, dblVersion);

            // If has Match check the excluded browsers to find a match
            if (hasMatch)
            {
                hasMatch = !HasMatch(this.Excludes, strAgent, strBrowser, dblVersion);
            }

            return hasMatch;
        }

        private static bool HasMatch(BrowserCollection browsers, string strAgent, string strBrowser, double dblVersion)
        {
            var hasMatch = false;

            if (browsers is null)
            {
                return false;
            }

            // Parse through the browsers to find a match based on name/minversion
            foreach (Browser browser in browsers)
            {
                // Check by browser name and min version
                if (!string.IsNullOrEmpty(browser.Name) && browser.Name.Equals(strBrowser, StringComparison.OrdinalIgnoreCase) && browser.MinVersion <= dblVersion)
                {
                    hasMatch = true;
                    break;
                }

                // Check for special browser name of "*"
                if (browser.Name == "*")
                {
                    hasMatch = true;
                    break;
                }
            }

            if (!hasMatch)
            {
                // Parse through the browsers to find a match based on contains (more expensive so only try if NoMatch
                foreach (Browser browser in browsers)
                {
                    // Check if UserAgent contains the string (Contains)
                    if (!string.IsNullOrEmpty(browser.Contains))
                    {
                        if (strAgent is not null && strAgent.IndexOf(browser.Contains, StringComparison.OrdinalIgnoreCase) > -1)
                        {
                            hasMatch = true;
                            break;
                        }
                    }
                }
            }

            return hasMatch;
        }
    }
}
