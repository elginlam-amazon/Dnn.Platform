// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DNNConnect.CKEditorProvider.Utilities
{
    using System;
    using System.IO;
    using System.Xml;
    using System.Xml.Serialization;

    /// <summary>Deserializes this extension's XML settings and toolbar files safely.</summary>
    /// <remarks>
    /// <c>XmlSerializer.Deserialize(TextReader)</c> creates a reader that resolves external
    /// entities and processes DTDs (CA5369). The extension reads
    /// <c>Dnn.CKEditorSettings.xml</c>, <c>Dnn.CKToolbarButtons.xml</c>,
    /// <c>Dnn.CKToolbarSets.xml</c> and the legacy <c>Dnn.CKToolbar.xml</c> from the portal's home
    /// directory, which a portal administrator can upload into, so the readers are created here
    /// with DTD processing prohibited and no resolver. The deserialized object graph is unchanged.
    /// </remarks>
    internal static class SafeXml
    {
        /// <summary>Deserializes an object graph from the given reader with DTD processing disabled.</summary>
        /// <param name="serializer">The serializer describing the graph.</param>
        /// <param name="textReader">The reader over the XML document.</param>
        /// <returns>The deserialized object.</returns>
        internal static object Deserialize(XmlSerializer serializer, TextReader textReader)
        {
            ArgumentNullException.ThrowIfNull(serializer);
            ArgumentNullException.ThrowIfNull(textReader);

            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
            };

            using (var xmlReader = XmlReader.Create(textReader, settings))
            {
                return serializer.Deserialize(xmlReader);
            }
        }
    }
}
