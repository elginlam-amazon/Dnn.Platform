// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.Script.Serialization
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// Replacement for the System.Web.Extensions <c>JavaScriptSerializer</c> on .NET 10, implemented on top of Json.NET.
    /// </summary>
    /// <remarks>
    /// Like the original serializer: members marked with <see cref="ScriptIgnoreAttribute"/> are skipped, untyped JSON objects
    /// are deserialized to <see cref="Dictionary{TKey,TValue}"/> of <see cref="string"/> and <see cref="object"/>, arrays to
    /// <see cref="object"/> arrays, and registered <see cref="JavaScriptConverter"/> instances are applied to JSON objects.
    /// </remarks>
    public class JavaScriptSerializer
    {
        private const int DefaultMaxJsonLength = 2097152;
        private const int DefaultRecursionLimit = 100;

        private readonly List<JavaScriptConverter> converters = new List<JavaScriptConverter>();

        /// <summary>Gets or sets the maximum length of the JSON strings.</summary>
        public int MaxJsonLength { get; set; } = DefaultMaxJsonLength;

        /// <summary>Gets or sets the maximum depth of nested objects.</summary>
        public int RecursionLimit { get; set; } = DefaultRecursionLimit;

        /// <summary>Registers custom converters.</summary>
        /// <param name="converters">The converters.</param>
        public void RegisterConverters(IEnumerable<JavaScriptConverter> converters)
        {
            if (converters == null)
            {
                throw new ArgumentNullException(nameof(converters));
            }

            this.converters.AddRange(converters);
        }

        /// <summary>Converts an object to a JSON string.</summary>
        /// <param name="obj">The object.</param>
        /// <returns>The JSON string.</returns>
        public string Serialize(object obj)
        {
            var builder = new StringBuilder();
            this.Serialize(obj, builder);
            return builder.ToString();
        }

        /// <summary>Converts an object to a JSON string and appends it to the builder.</summary>
        /// <param name="obj">The object.</param>
        /// <param name="output">The output builder.</param>
        public void Serialize(object obj, StringBuilder output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            var json = JsonConvert.SerializeObject(obj, this.CreateSettings());
            if (json.Length > this.MaxJsonLength)
            {
                throw new InvalidOperationException("The length of the string exceeds the value set on the maxJsonLength property.");
            }

            output.Append(json);
        }

        /// <summary>Converts a JSON string to an object of the given type.</summary>
        /// <typeparam name="T">The type of the resulting object.</typeparam>
        /// <param name="input">The JSON string.</param>
        /// <returns>The deserialized object.</returns>
        public T Deserialize<T>(string input)
        {
            return (T)this.Deserialize(input, typeof(T));
        }

        /// <summary>Converts a JSON string to an object of the given type.</summary>
        /// <param name="input">The JSON string.</param>
        /// <param name="targetType">The type of the resulting object.</param>
        /// <returns>The deserialized object.</returns>
        public object Deserialize(string input, Type targetType)
        {
            if (targetType == null)
            {
                throw new ArgumentNullException(nameof(targetType));
            }

            var token = this.Parse(input);
            if (token == null)
            {
                return null;
            }

            return this.ConvertToken(token, targetType);
        }

        /// <summary>Converts a JSON string to an object graph (<see cref="Dictionary{TKey,TValue}"/>, <see cref="object"/> arrays and primitives).</summary>
        /// <param name="input">The JSON string.</param>
        /// <returns>The deserialized object.</returns>
        public object DeserializeObject(string input)
        {
            return this.Deserialize(input, typeof(object));
        }

        /// <summary>Converts an object graph created by <see cref="DeserializeObject"/> to the given type.</summary>
        /// <typeparam name="T">The type of the resulting object.</typeparam>
        /// <param name="obj">The object graph.</param>
        /// <returns>The converted object.</returns>
        public T ConvertToType<T>(object obj)
        {
            return (T)this.ConvertToType(obj, typeof(T));
        }

        /// <summary>Converts an object graph created by <see cref="DeserializeObject"/> to the given type.</summary>
        /// <param name="obj">The object graph.</param>
        /// <param name="targetType">The type of the resulting object.</param>
        /// <returns>The converted object.</returns>
        public object ConvertToType(object obj, Type targetType)
        {
            if (targetType == null)
            {
                throw new ArgumentNullException(nameof(targetType));
            }

            if (obj == null)
            {
                return null;
            }

            if (targetType.IsInstanceOfType(obj))
            {
                return obj;
            }

            var token = JToken.FromObject(obj, JsonSerializer.Create(this.CreateSettings()));
            return this.ConvertToken(token, targetType);
        }

        private static object ToUntyped(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    var dictionary = new Dictionary<string, object>(StringComparer.Ordinal);
                    foreach (var property in ((JObject)token).Properties())
                    {
                        dictionary[property.Name] = ToUntyped(property.Value);
                    }

                    return dictionary;
                case JTokenType.Array:
                    return token.Select(ToUntyped).ToArray();
                case JTokenType.Integer:
                    var integer = token.Value<long>();
                    return integer >= int.MinValue && integer <= int.MaxValue ? (object)(int)integer : integer;
                case JTokenType.Float:
                    return token.Value<decimal>();
                case JTokenType.Boolean:
                    return token.Value<bool>();
                case JTokenType.Date:
                    return token.Value<DateTime>();
                case JTokenType.Null:
                case JTokenType.Undefined:
                    return null;
                default:
                    return token.ToString(Formatting.None).Trim('"');
            }
        }

        private JToken Parse(string input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (input.Length > this.MaxJsonLength)
            {
                throw new ArgumentException("The length of the string exceeds the value set on the maxJsonLength property.", nameof(input));
            }

            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }

            using var reader = new JsonTextReader(new StringReader(input))
            {
                MaxDepth = this.RecursionLimit,
                DateParseHandling = DateParseHandling.None,
            };

            return JToken.ReadFrom(reader);
        }

        private object ConvertToken(JToken token, Type targetType)
        {
            if (token.Type == JTokenType.Object)
            {
                var converter = this.converters.FirstOrDefault(c => c.SupportedTypes.Contains(targetType));
                if (converter != null)
                {
                    return converter.Deserialize((IDictionary<string, object>)ToUntyped(token), targetType, this);
                }
            }

            if (targetType == typeof(object))
            {
                return ToUntyped(token);
            }

            return token.ToObject(targetType, JsonSerializer.Create(this.CreateSettings()));
        }

        private JsonSerializerSettings CreateSettings()
        {
            return new JsonSerializerSettings
            {
                ContractResolver = ScriptIgnoreContractResolver.Instance,
                MaxDepth = this.RecursionLimit,
                ReferenceLoopHandling = ReferenceLoopHandling.Error,
                DateFormatHandling = DateFormatHandling.MicrosoftDateFormat,
                Culture = CultureInfo.InvariantCulture,
            };
        }

        /// <summary>Skips members marked with <see cref="ScriptIgnoreAttribute"/> and serializes public members only.</summary>
        private sealed class ScriptIgnoreContractResolver : DefaultContractResolver
        {
            public static readonly ScriptIgnoreContractResolver Instance = new ScriptIgnoreContractResolver();

            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                var property = base.CreateProperty(member, memberSerialization);
                if (member.GetCustomAttribute<ScriptIgnoreAttribute>(true) != null)
                {
                    property.Ignored = true;
                }

                return property;
            }
        }
    }
}
