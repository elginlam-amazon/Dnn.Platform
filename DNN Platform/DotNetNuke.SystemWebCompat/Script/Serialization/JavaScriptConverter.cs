// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.Script.Serialization
{
    using System.Collections.Generic;

    /// <summary>Provides an abstract base class for a custom type converter of the <see cref="JavaScriptSerializer"/>.</summary>
    public abstract class JavaScriptConverter
    {
        /// <summary>Gets the types which are supported by the converter.</summary>
        public abstract IEnumerable<Type> SupportedTypes { get; }

        /// <summary>Converts the provided dictionary into an object of the specified type.</summary>
        /// <param name="dictionary">The JSON object data.</param>
        /// <param name="type">The type of the resulting object.</param>
        /// <param name="serializer">The serializer instance.</param>
        /// <returns>The deserialized object.</returns>
        public abstract object Deserialize(IDictionary<string, object> dictionary, Type type, JavaScriptSerializer serializer);

        /// <summary>Builds a dictionary of name/value pairs.</summary>
        /// <param name="obj">The object to serialize.</param>
        /// <param name="serializer">The serializer instance.</param>
        /// <returns>The dictionary that represents the object data.</returns>
        public abstract IDictionary<string, object> Serialize(object obj, JavaScriptSerializer serializer);
    }
}
