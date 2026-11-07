// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Framework.Providers
{
    using System;
    using System.Collections.Specialized;
    using System.Xml;

    using Microsoft.Extensions.Configuration;

    /// <summary>The configuration of a single provider: its name, its type and any extra attributes.</summary>
    public class Provider
    {
        private readonly NameValueCollection providerAttributes = new NameValueCollection();
        private readonly string providerName;
        private readonly string providerType;

        /// <summary>Initializes a new instance of the <see cref="Provider"/> class.</summary>
        /// <param name="attributes">The XML attributes. <c>name</c> and <c>type</c> are required, any additional attributes will be in <see cref="Attributes"/>.</param>
        public Provider(XmlAttributeCollection attributes)
        {
            // Set the name of the provider
            this.providerName = attributes["name"].Value;

            // Set the type of the provider
            this.providerType = attributes["type"].Value;

            // Store all the attributes in the attributes bucket
            foreach (XmlAttribute attribute in attributes)
            {
                if (attribute.Name != "name" && attribute.Name != "type")
                {
                    this.providerAttributes.Add(attribute.Name, attribute.Value);
                }
            }
        }

        /// <summary>Initializes a new instance of the <see cref="Provider"/> class from a configuration section.</summary>
        /// <param name="section">
        /// One entry of a <c>DotNetNuke:&lt;group&gt;:providers</c> configuration section. The
        /// <c>name</c> and <c>type</c> keys map to <see cref="Name"/> and <see cref="Type"/>; every
        /// other key becomes an entry in <see cref="Attributes"/>, exactly as the extra XML
        /// attributes used to.
        /// </param>
        public Provider(IConfigurationSection section)
        {
            if (section == null)
            {
                throw new ArgumentNullException(nameof(section));
            }

            this.providerName = section["name"];
            this.providerType = section["type"];

            foreach (var child in section.GetChildren())
            {
                if (!string.Equals(child.Key, "name", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(child.Key, "type", StringComparison.OrdinalIgnoreCase))
                {
                    this.providerAttributes.Add(child.Key, child.Value);
                }
            }
        }

        /// <summary>Initializes a new instance of the <see cref="Provider"/> class.</summary>
        /// <param name="name">The provider name.</param>
        /// <param name="type">The assembly-qualified provider type name.</param>
        /// <param name="attributes">Any additional attributes, or <see langword="null"/>.</param>
        public Provider(string name, string type, NameValueCollection attributes)
        {
            this.providerName = name;
            this.providerType = type;
            if (attributes != null)
            {
                this.providerAttributes.Add(attributes);
            }
        }

        /// <summary>Gets the provider name.</summary>
        public string Name
        {
            get
            {
                return this.providerName;
            }
        }

        /// <summary>Gets the assembly-qualified provider type name.</summary>
        public string Type
        {
            get
            {
                return this.providerType;
            }
        }

        /// <summary>Gets any additional provider attributes, such as <c>providerPath</c>.</summary>
        public NameValueCollection Attributes
        {
            get
            {
                return this.providerAttributes;
            }
        }
    }
}
