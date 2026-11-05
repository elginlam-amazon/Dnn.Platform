// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Framework
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>The default <see cref="IPageStateFormatter"/>, backed by <see cref="JsonSerializer"/>.</summary>
    /// <remarks>
    /// <para>
    /// Replaces <c>LosFormatter</c>. Because the page and control state are arbitrary objects, each
    /// half is written as an envelope that records the assembly qualified name of the runtime type
    /// alongside its JSON, so that <see cref="Deserialize"/> can materialize the original type again.
    /// </para>
    /// <para>
    /// A state object must therefore be JSON round-trippable (public, settable members, or a
    /// primitive/collection of them). That is a real constraint the WebForms binary formatter did not
    /// have, and it is the reason the state is never silently dropped: a value that cannot be written
    /// throws, rather than persisting an empty state that would be read back as "no state".
    /// </para>
    /// </remarks>
    public class PageStateFormatter : IPageStateFormatter
    {
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            IncludeFields = true,
        };

        /// <inheritdoc />
        public string Serialize(PageState state)
        {
            ArgumentNullException.ThrowIfNull(state);

            var envelope = new StateEnvelope
            {
                ViewStateType = GetTypeName(state.ViewState),
                ViewState = WriteValue(state.ViewState),
                ControlStateType = GetTypeName(state.ControlState),
                ControlState = WriteValue(state.ControlState),
            };

            return JsonSerializer.Serialize(envelope, SerializerOptions);
        }

        /// <inheritdoc />
        public PageState Deserialize(string serializedState)
        {
            if (string.IsNullOrEmpty(serializedState))
            {
                throw new ArgumentException("The serialized state must not be empty.", nameof(serializedState));
            }

            var envelope = JsonSerializer.Deserialize<StateEnvelope>(serializedState, SerializerOptions)
                           ?? throw new InvalidOperationException("The serialized page state could not be read.");

            return new PageState
            {
                ViewState = ReadValue(envelope.ViewStateType, envelope.ViewState),
                ControlState = ReadValue(envelope.ControlStateType, envelope.ControlState),
            };
        }

        private static string GetTypeName(object value)
            => value?.GetType().AssemblyQualifiedName;

        private static string WriteValue(object value)
            => value is null ? null : JsonSerializer.Serialize(value, value.GetType(), SerializerOptions);

        private static object ReadValue(string typeName, string json)
        {
            if (string.IsNullOrEmpty(typeName) || json is null)
            {
                return null;
            }

            var type = Type.GetType(typeName, throwOnError: false, ignoreCase: false)
                       ?? throw new InvalidOperationException($"The persisted page state references the type '{typeName}', which is not loaded.");

            return JsonSerializer.Deserialize(json, type, SerializerOptions);
        }

        private sealed class StateEnvelope
        {
            public string ViewStateType { get; set; }

            public string ViewState { get; set; }

            public string ControlStateType { get; set; }

            public string ControlState { get; set; }
        }
    }
}
