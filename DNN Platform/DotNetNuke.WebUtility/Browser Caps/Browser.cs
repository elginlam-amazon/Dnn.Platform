// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Utilities
{
    using System.Xml.Serialization;

    /// <summary>
    /// Describes one browser (or one user agent substring) a piece of client functionality applies to.
    /// </summary>
    [XmlRoot("browser")]
    public class Browser
    {
        /// <summary>Gets or sets a substring the user agent must contain for this entry to match.</summary>
        [XmlAttribute("contains")]
        public string Contains { get; set; }

        /// <summary>Gets or sets the name of the browser this entry matches, <c>*</c> matching every browser.</summary>
        [XmlAttribute("nm")]
        public string Name { get; set; }

        /// <summary>Gets or sets the lowest version of the browser this entry matches.</summary>
        [XmlAttribute("minversion")]
        public double MinVersion { get; set; }
    }
}
