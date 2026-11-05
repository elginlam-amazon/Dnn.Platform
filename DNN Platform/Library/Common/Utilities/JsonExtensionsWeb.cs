// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Common.Utilities
{
    using System;

    using Newtonsoft.Json;

    /// <summary>JSON Extensions, historically based on the JavaScript Serializer in System.Web.</summary>
    /// <remarks>
    /// <c>System.Web.Script.Serialization.JavaScriptSerializer</c> does not exist on modern .NET,
    /// so these helpers are implemented with Json.NET. The public surface, the serialized shape and
    /// the "no maximum document length" behavior (the former <c>MaxJsonLength = int.MaxValue</c>)
    /// are preserved.
    /// </remarks>
    public static class JsonExtensionsWeb
    {
        /// <summary>Serializes a type to JSON. Note the type must be marked Serializable or include a DataContract attribute.</summary>
        /// <param name="value">The value to serialize.</param>
        /// <returns>The JSON string.</returns>
        public static string ToJsonString(object value)
        {
            return JsonConvert.SerializeObject(value, SerializerSettings());
        }

        /// <summary>Extension method on object that serializes the value to JSON. Note the type must be marked Serializable or include a DataContract attribute.</summary>
        /// <param name="value">The value to serialize.</param>
        /// <returns>The JSON string.</returns>
        public static string ToJson(this object value)
        {
            return ToJsonString(value);
        }

        /// <summary>Deserializes a JSON string into a specific type. Note that the type specified must be serializable.</summary>
        /// <param name="json">A JSON string.</param>
        /// <param name="type">The type to deserialize the JSON into.</param>
        /// <returns>The deserialized value.</returns>
        public static object FromJsonString(string json, Type type)
        {
            return JsonConvert.DeserializeObject(json, type, SerializerSettings());
        }

        /// <summary>Extension method to string that deserializes a JSON string into a specific type. Note that the type specified must be serializable.</summary>
        /// <param name="json">A JSON string.</param>
        /// <param name="type">The type to deserialize the JSON into.</param>
        /// <returns>The deserialized value.</returns>
        public static object FromJson(this string json, Type type)
        {
            return FromJsonString(json, type);
        }

        /// <summary>Extension method to string that deserializes a JSON string into a specific type. Note that the type specified must be serializable.</summary>
        /// <typeparam name="TType">The type to deserialize the JSON into.</typeparam>
        /// <param name="json">A JSON string.</param>
        /// <returns>The deserialized value.</returns>
        public static TType FromJson<TType>(this string json)
        {
            return JsonConvert.DeserializeObject<TType>(json, SerializerSettings());
        }

        private static JsonSerializerSettings SerializerSettings()
        {
            return new JsonSerializerSettings
            {
                // JavaScriptSerializer emitted every public property, including nulls.
                NullValueHandling = NullValueHandling.Include,
            };
        }
    }
}
