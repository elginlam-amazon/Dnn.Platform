// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Client.Providers
{
    using System.Collections.Generic;
    using System.Text;

    /// <summary>Extension methods to serialize HTML attributes.</summary>
    /// <remarks>Replaces the <c>ToHtmlAttributes()</c> extension method from <c>ClientDependency.Core</c>, preserving the identical serialization.</remarks>
    public static class HtmlAttributesExtensions
    {
        /// <summary>Serializes a dictionary of HTML attributes into a <c>key="value"</c> space separated string.</summary>
        /// <param name="attributes">The attributes to serialize, may be <see langword="null"/>.</param>
        /// <returns>A string of HTML attributes, or <see cref="string.Empty"/> when there are none.</returns>
        public static string ToHtmlAttributes(this IDictionary<string, string> attributes)
        {
            if (attributes is null || attributes.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            foreach (var attribute in attributes)
            {
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(attribute.Key).Append("=\"").Append(attribute.Value).Append('"');
            }

            return builder.ToString();
        }
    }
}
