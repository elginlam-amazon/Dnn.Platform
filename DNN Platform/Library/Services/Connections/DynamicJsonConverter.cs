// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Services.Connections
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Dynamic;
    using System.Globalization;
    using System.Linq;
    using System.Text;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// A <see cref="JsonConverter"/> which materializes arbitrary JSON objects as late-bound
    /// <c>dynamic</c> instances whose missing members evaluate to <c>null</c> instead of throwing.
    /// </summary>
    /// <remarks>
    /// This replaces the former <c>System.Web.Script.Serialization.JavaScriptConverter</c>
    /// implementation (that API does not exist on modern .NET). The dynamic object semantics
    /// — member/index access returning <c>null</c> for unknown keys, nested objects wrapped
    /// recursively and arrays projected to <see cref="List{T}"/> — are preserved exactly.
    /// </remarks>
    public sealed class DynamicJsonConverter : JsonConverter
    {
        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <summary>Deserializes a JSON document into a late-bound <c>dynamic</c> object.</summary>
        /// <param name="json">The JSON document.</param>
        /// <returns>A <c>dynamic</c> object, or <c>null</c> when <paramref name="json"/> is empty.</returns>
        public static dynamic Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            var settings = new JsonSerializerSettings();
            settings.Converters.Add(new DynamicJsonConverter());
            return JsonConvert.DeserializeObject<object>(json, settings);
        }

        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(object);
        }

        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            if (objectType != typeof(object))
            {
                return null;
            }

            var token = JToken.ReadFrom(reader);
            return WrapToken(token);
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            // Parity with the original JavaScriptConverter implementation, which never supported
            // serialization. CanWrite is false, so Newtonsoft never routes writes through here.
            throw new NotSupportedException($"{nameof(DynamicJsonConverter)} only supports deserialization.");
        }

        private static object WrapToken(JToken token)
        {
            switch (token)
            {
                case null:
                    return null;
                case JObject obj:
                    return new DynamicJsonObject(obj);
                case JArray array:
                    return new List<object>(array.Select(WrapToken));
                case JValue value:
                    return value.Value;
                default:
                    return token;
            }
        }

        private sealed class DynamicJsonObject : DynamicObject
        {
            private readonly IDictionary<string, object> dictionary;

            public DynamicJsonObject(IDictionary<string, object> dictionary)
            {
                this.dictionary = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
            }

            public DynamicJsonObject(JObject source)
            {
                if (source == null)
                {
                    throw new ArgumentNullException(nameof(source));
                }

                this.dictionary = source.Properties().ToDictionary(p => p.Name, p => (object)p.Value);
            }

            public override string ToString()
            {
                var sb = new StringBuilder("{");
                this.ToString(sb);
                return sb.ToString();
            }

            public override bool TryGetMember(GetMemberBinder binder, out object result)
            {
                if (!this.dictionary.TryGetValue(binder.Name, out result))
                {
                    // return null to avoid exception.  caller can check for null this way...
                    result = null;
                    return true;
                }

                result = WrapResultObject(result);
                return true;
            }

            public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
            {
                if (indexes.Length == 1 && indexes[0] != null)
                {
                    if (!this.dictionary.TryGetValue(indexes[0].ToString(), out result))
                    {
                        // return null to avoid exception.  caller can check for null this way...
                        result = null;
                        return true;
                    }

                    result = WrapResultObject(result);
                    return true;
                }

                return base.TryGetIndex(binder, indexes, out result);
            }

            private static object WrapResultObject(object result)
            {
                if (result is JToken token)
                {
                    return WrapToken(token);
                }

                if (result is IDictionary<string, object> dictionary)
                {
                    return new DynamicJsonObject(dictionary);
                }

                var arrayList = result as ArrayList;
                if (arrayList != null && arrayList.Count > 0)
                {
                    return arrayList[0] is IDictionary<string, object>
                        ? new List<object>(arrayList.Cast<IDictionary<string, object>>().Select(x => new DynamicJsonObject(x)))
                        : new List<object>(arrayList.Cast<object>());
                }

                return result;
            }

            /// <summary>Encodes a string as a double-quoted, escaped JavaScript/JSON string literal.</summary>
            /// <param name="value">The value to encode.</param>
            /// <returns>The quoted literal, equivalent to the former <c>HttpUtility.JavaScriptStringEncode(value, true)</c>.</returns>
            private static string EncodeJavaScriptString(string value)
            {
                return JsonConvert.ToString(value ?? string.Empty);
            }

            private void ToString(StringBuilder sb)
            {
                var firstInDictionary = true;
                foreach (var pair in this.dictionary)
                {
                    if (!firstInDictionary)
                    {
                        sb.Append(',');
                    }

                    firstInDictionary = false;
                    var value = pair.Value is JValue jValue ? jValue.Value : pair.Value;
                    var name = pair.Key;
                    if (value is string stringValue)
                    {
                        sb.AppendFormat(
                            CultureInfo.InvariantCulture,
                            "{0}:{1}",
                            EncodeJavaScriptString(name),
                            EncodeJavaScriptString(stringValue));
                    }
                    else if (value is JObject jObjectValue)
                    {
                        new DynamicJsonObject(jObjectValue).ToString(sb);
                    }
                    else if (value is IDictionary<string, object> dictValue)
                    {
                        new DynamicJsonObject(dictValue).ToString(sb);
                    }
                    else if (value is JArray jArrayValue)
                    {
                        AppendArray(sb, name, jArrayValue.Cast<object>());
                    }
                    else if (value is ArrayList list)
                    {
                        AppendArray(sb, name, list.Cast<object>());
                    }
                    else
                    {
                        sb.AppendFormat(CultureInfo.InvariantCulture, "{0}:{1}", name, value);
                    }
                }

                sb.Append('}');
            }

            private void AppendArray(StringBuilder sb, string name, IEnumerable<object> values)
            {
                sb.Append(name + ":[");
                var firstInArray = true;
                foreach (var rawValue in values)
                {
                    if (!firstInArray)
                    {
                        sb.Append(',');
                    }

                    firstInArray = false;

                    var arrayValue = rawValue is JValue jValue ? jValue.Value : rawValue;

                    if (arrayValue is JObject jObjectArrayValue)
                    {
                        sb.Append(new DynamicJsonObject(jObjectArrayValue));
                    }
                    else if (arrayValue is IDictionary<string, object> dictArrayValue)
                    {
                        sb.Append(new DynamicJsonObject(dictArrayValue));
                    }
                    else if (arrayValue is string stringArrayValue)
                    {
                        sb.Append(EncodeJavaScriptString(stringArrayValue));
                    }
                    else
                    {
                        sb.Append(arrayValue);
                    }
                }

                sb.Append(']');
            }
        }
    }
}
