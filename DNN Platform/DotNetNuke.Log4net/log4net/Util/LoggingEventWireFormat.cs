// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
//
// Licensed to the Apache Software Foundation (ASF) under one or more
// contributor license agreements. See the NOTICE file distributed with
// this work for additional information regarding copyright ownership.
// The ASF licenses this file to you under the Apache License, Version 2.0
// (the "License"); you may not use this file except in compliance with
// the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

using log4net.Core;
using log4net.Repository;

namespace log4net.Util
{
    /// <summary>
    /// The wire format used to transport <see cref="LoggingEvent"/> batches between a
    /// <see cref="log4net.Appender.RemotingAppender"/> and a
    /// <see cref="log4net.Plugin.RemoteLoggingServerPlugin"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Historically this transport used .NET Remoting, which serialized the
    /// <see cref="LoggingEvent"/> graph with the runtime binary formatter. .NET Remoting
    /// and <c>BinaryFormatter</c> do not exist on modern .NET, so the transport is now an
    /// HTTP <c>POST</c> whose body is the JSON document described by
    /// <see cref="LoggingEventBatch"/>, serialized with <c>System.Text.Json</c>.
    /// </para>
    /// <para>
    /// Only the fixed, portable fields of an event travel across the wire (the same fields
    /// the former binary serialization carried). Event property values are rendered to
    /// strings on the sending side: the receiver therefore never has to materialize an
    /// arbitrary sender-supplied type, which removes the deserialization attack surface
    /// that the binary formatter had.
    /// </para>
    /// </remarks>
    public static class LoggingEventWireFormat
    {
        /// <summary>The media type of the batch document.</summary>
        public const string MediaType = "application/json";

        /// <summary>The schema version emitted by this assembly.</summary>
        public const int CurrentVersion = 1;

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
        };

        /// <summary>Serializes a batch of events into the UTF-8 JSON wire document.</summary>
        /// <param name="events">The events to serialize. <c>null</c> entries are skipped.</param>
        /// <returns>The UTF-8 encoded JSON document.</returns>
        public static byte[] Serialize(LoggingEvent[] events)
        {
            var batch = new LoggingEventBatch { Version = CurrentVersion };
            if (events != null)
            {
                foreach (LoggingEvent loggingEvent in events)
                {
                    if (loggingEvent != null)
                    {
                        batch.Events.Add(ToWire(loggingEvent));
                    }
                }
            }

            return JsonSerializer.SerializeToUtf8Bytes(batch, Options);
        }

        /// <summary>Deserializes a UTF-8 JSON wire document into a batch.</summary>
        /// <param name="utf8Json">The UTF-8 encoded JSON document.</param>
        /// <returns>The deserialized batch; never <c>null</c>.</returns>
        public static LoggingEventBatch Deserialize(ReadOnlySpan<byte> utf8Json)
        {
            if (utf8Json.Length == 0)
            {
                return new LoggingEventBatch { Version = CurrentVersion };
            }

            return JsonSerializer.Deserialize<LoggingEventBatch>(utf8Json, Options)
                   ?? new LoggingEventBatch { Version = CurrentVersion };
        }

        /// <summary>Converts a <see cref="LoggingEvent"/> to its wire representation.</summary>
        /// <param name="loggingEvent">The event to convert.</param>
        /// <returns>The wire representation.</returns>
        public static LoggingEventWireItem ToWire(LoggingEvent loggingEvent)
        {
            if (loggingEvent == null)
            {
                throw new ArgumentNullException(nameof(loggingEvent));
            }

            // Fix the volatile data so the event remains meaningful off the originating thread,
            // exactly as the remoting serialization path required.
            LoggingEventData data = loggingEvent.GetLoggingEventData(FixFlags.All);

            var item = new LoggingEventWireItem
            {
                LoggerName = data.LoggerName,
                Message = data.Message,
                ThreadName = data.ThreadName,
                TimeStampUtc = data.TimeStampUtc,
                UserName = data.UserName,
                Identity = data.Identity,
                ExceptionString = data.ExceptionString,
                Domain = data.Domain,
            };

            if (data.Level != null)
            {
                item.Level = new LevelWireItem
                {
                    Value = data.Level.Value,
                    Name = data.Level.Name,
                    DisplayName = data.Level.DisplayName,
                };
            }

            if (data.LocationInfo != null)
            {
                item.LocationInfo = new LocationInfoWireItem
                {
                    ClassName = data.LocationInfo.ClassName,
                    MethodName = data.LocationInfo.MethodName,
                    FileName = data.LocationInfo.FileName,
                    LineNumber = data.LocationInfo.LineNumber,
                };
            }

            if (data.Properties != null && data.Properties.Count > 0)
            {
                item.Properties = new Dictionary<string, string>(data.Properties.Count);
                foreach (DictionaryEntry entry in (IDictionary)data.Properties)
                {
                    string key = entry.Key as string;
                    if (key == null)
                    {
                        continue;
                    }

                    item.Properties[key] = RenderValue(entry.Value);
                }
            }

            return item;
        }

        /// <summary>Rebuilds a <see cref="LoggingEvent"/> from its wire representation.</summary>
        /// <param name="item">The wire representation.</param>
        /// <param name="repository">
        /// The repository the event will be replayed into. Its
        /// <see cref="ILoggerRepository.LevelMap"/> is used to resolve the level by name so
        /// the event takes on the receiving hierarchy's level instance where one exists.
        /// </param>
        /// <returns>The reconstructed event with all fields marked as fixed.</returns>
        public static LoggingEvent FromWire(LoggingEventWireItem item, ILoggerRepository repository)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            var data = default(LoggingEventData);
            data.LoggerName = item.LoggerName;
            data.Message = item.Message;
            data.ThreadName = item.ThreadName;
            data.TimeStampUtc = item.TimeStampUtc == default(DateTime)
                ? DateTime.UtcNow
                : item.TimeStampUtc.ToUniversalTime();
            data.UserName = item.UserName;
            data.Identity = item.Identity;
            data.ExceptionString = item.ExceptionString;
            data.Domain = item.Domain;
            data.Level = ResolveLevel(item.Level, repository);

            if (item.LocationInfo != null)
            {
                data.LocationInfo = new LocationInfo(
                    item.LocationInfo.ClassName,
                    item.LocationInfo.MethodName,
                    item.LocationInfo.FileName,
                    item.LocationInfo.LineNumber);
            }

            if (item.Properties != null && item.Properties.Count > 0)
            {
                var properties = new PropertiesDictionary();
                foreach (KeyValuePair<string, string> property in item.Properties)
                {
                    properties[property.Key] = property.Value;
                }

                data.Properties = properties;
            }

            // FixFlags.All: every value came from the wire and must not be recaptured locally.
            return new LoggingEvent(null, repository, data, FixFlags.All);
        }

        private static Level ResolveLevel(LevelWireItem level, ILoggerRepository repository)
        {
            if (level == null || string.IsNullOrEmpty(level.Name))
            {
                return Level.Debug;
            }

            if (repository != null && repository.LevelMap != null)
            {
                Level mapped = repository.LevelMap[level.Name];
                if (mapped != null)
                {
                    return mapped;
                }
            }

            return new Level(level.Value, level.Name, level.DisplayName ?? level.Name);
        }

        private static string RenderValue(object value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is string text)
            {
                return text;
            }

            if (value is IFormattable formattable)
            {
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            }

            return value.ToString();
        }
    }

    /// <summary>A batch of logging events as it appears on the wire.</summary>
    public sealed class LoggingEventBatch
    {
        /// <summary>Gets or sets the schema version of this document.</summary>
        [JsonPropertyName("version")]
        public int Version { get; set; } = LoggingEventWireFormat.CurrentVersion;

        /// <summary>Gets the events carried by this batch.</summary>
        [JsonPropertyName("events")]
        public List<LoggingEventWireItem> Events { get; } = new List<LoggingEventWireItem>();
    }

    /// <summary>A single logging event as it appears on the wire.</summary>
    public sealed class LoggingEventWireItem
    {
        /// <summary>Gets or sets the logger name.</summary>
        [JsonPropertyName("loggerName")]
        public string LoggerName { get; set; }

        /// <summary>Gets or sets the level of the event.</summary>
        [JsonPropertyName("level")]
        public LevelWireItem Level { get; set; }

        /// <summary>Gets or sets the rendered message.</summary>
        [JsonPropertyName("message")]
        public string Message { get; set; }

        /// <summary>Gets or sets the originating thread name.</summary>
        [JsonPropertyName("threadName")]
        public string ThreadName { get; set; }

        /// <summary>Gets or sets the UTC timestamp of the event.</summary>
        [JsonPropertyName("timeStampUtc")]
        public DateTime TimeStampUtc { get; set; }

        /// <summary>Gets or sets the caller location information.</summary>
        [JsonPropertyName("locationInfo")]
        public LocationInfoWireItem LocationInfo { get; set; }

        /// <summary>Gets or sets the user name.</summary>
        [JsonPropertyName("userName")]
        public string UserName { get; set; }

        /// <summary>Gets or sets the principal identity.</summary>
        [JsonPropertyName("identity")]
        public string Identity { get; set; }

        /// <summary>Gets or sets the rendered exception text.</summary>
        [JsonPropertyName("exceptionString")]
        public string ExceptionString { get; set; }

        /// <summary>Gets or sets the originating application domain name.</summary>
        [JsonPropertyName("domain")]
        public string Domain { get; set; }

        /// <summary>Gets or sets the event properties, rendered to strings.</summary>
        [JsonPropertyName("properties")]
        public Dictionary<string, string> Properties { get; set; }
    }

    /// <summary>A <see cref="log4net.Core.Level"/> as it appears on the wire.</summary>
    public sealed class LevelWireItem
    {
        /// <summary>Gets or sets the numeric level value.</summary>
        [JsonPropertyName("value")]
        public int Value { get; set; }

        /// <summary>Gets or sets the level name, used to look the level up on the receiver.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>Gets or sets the level display name.</summary>
        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }
    }

    /// <summary>A <see cref="log4net.Core.LocationInfo"/> as it appears on the wire.</summary>
    public sealed class LocationInfoWireItem
    {
        /// <summary>Gets or sets the declaring class name.</summary>
        [JsonPropertyName("className")]
        public string ClassName { get; set; }

        /// <summary>Gets or sets the method name.</summary>
        [JsonPropertyName("methodName")]
        public string MethodName { get; set; }

        /// <summary>Gets or sets the source file name.</summary>
        [JsonPropertyName("fileName")]
        public string FileName { get; set; }

        /// <summary>Gets or sets the source line number.</summary>
        [JsonPropertyName("lineNumber")]
        public string LineNumber { get; set; }
    }
}
