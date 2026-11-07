// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Skins.Controls
{
    using System.Collections.Generic;

    /// <summary>
    /// Parses the <c>HtmlAttributesAsString</c> value of the client-resource skin objects
    /// (<c>DnnJsInclude</c>, <c>DnnCssInclude</c>) into an attribute dictionary.
    /// </summary>
    /// <remarks>
    /// The WebForms skin objects forwarded this to the <c>DotNetNuke.Web.Client</c> include control,
    /// whose Tag Helper replacement keeps the parser private
    /// (<c>ClientResourceIncludeTagHelper.ParseHtmlAttributesIntoDictionary</c>). Tag Helpers do not
    /// work in Razor components, so the skin objects register through
    /// <see cref="DotNetNuke.Abstractions.ClientResources.IClientResourceController"/> directly and
    /// need the identical parsing. The implementation is the unchanged ClientDependency parser.
    /// </remarks>
    /// <seealso href="https://github.com/dnnsoftware/ClientDependency/blob/7bf46d9a9b8540e71496fa76ed5d122ec4a16257/src/ClientDependency.Core/HtmlAttributesStringParser.cs" />
    internal static class ClientResourceAttributeParser
    {
        /// <summary>Parses <paramref name="attributes"/> (<c>key1:value1,key2:value2</c>) into <paramref name="destination"/>.</summary>
        /// <param name="attributes">The attribute string to parse.</param>
        /// <param name="destination">The dictionary receiving the parsed attributes.</param>
        internal static void ParseInto(string attributes, IDictionary<string, string> destination)
        {
            if (string.IsNullOrEmpty(attributes) || destination is null)
            {
                return;
            }

            var charArray = attributes.ToCharArray();
            var key = string.Empty;
            var val = string.Empty;
            var isKey = true;
            var isVal = false;
            var isValDelimited = false;
            for (var i = 0; i < attributes.Length; i++)
            {
                var c = charArray[i];
                if (isKey && c == ':')
                {
                    isKey = false;
                    isVal = true;
                    continue;
                }

                if (isKey && c == ' ')
                {
                    // this means we have a key without a value, we should add it to the dictionary with an empty value and start a new key
                    destination[key] = null;
                    key = string.Empty;
                    continue;
                }

                if (isKey)
                {
                    key += c;
                }

                if (!isVal)
                {
                    continue;
                }

                if (c == '\'')
                {
                    if (!isValDelimited)
                    {
                        isValDelimited = true;
                        continue;
                    }
                    else
                    {
                        isValDelimited = false;
                        if (i == attributes.Length - 1)
                        {
                            // if it is the end, add/replace the value
                            destination[key] = val;
                        }

                        continue;
                    }
                }

                if (c == ',' && !isValDelimited)
                {
                    // we've reached a comma and the value is no longer delimited, this means we create a new key
                    isKey = true;
                    isVal = false;

                    // now we can add/replace the current value to the dictionary
                    destination[key] = val;
                    key = string.Empty;
                    val = string.Empty;
                    continue;
                }

                val += c;

                if (i == attributes.Length - 1)
                {
                    // if it is the end, add/replace the value
                    destination[key] = val;
                }
            }
        }
    }
}
