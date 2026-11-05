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
using System.Security;
using System.Threading;

namespace log4net.Util
{
    /// <summary>Implementation of Properties collection for the <see cref="log4net.LogicalThreadContext"/></summary>
    /// <remarks>
    /// <para>
    /// Class implements a collection of properties that is specific to each thread.
    /// The class is not synchronized as each thread has its own <see cref="PropertiesDictionary"/>.
    /// </para>
    /// <para>
    /// This class stores its properties in an <see cref="AsyncLocal{T}"/>, so the
    /// values flow with the asynchronous control flow (across <c>await</c> points and
    /// into child tasks) just as the legacy logical call context slot
    /// <c>log4net.Util.LogicalThreadContextProperties</c> did.
    /// </para>
    /// <para>
    /// The stored dictionary is treated as immutable: every mutation stores a copy
    /// (copy-on-write) so that values already captured by another control flow are
    /// never changed underneath it.
    /// </para>
    /// </remarks>
    /// <author>Nicko Cadell</author>
    public sealed class LogicalThreadContextProperties : ContextPropertiesBase
    {
        private static readonly AsyncLocal<PropertiesDictionary> AsyncLocalDictionary = new AsyncLocal<PropertiesDictionary>();

        /// <summary>Flag used to disable this context if the backing store cannot be accessed.</summary>
        private bool m_disabled = false;

        /// <summary>Constructor</summary>
        /// <remarks>
        /// <para>
        /// Initializes a new instance of the <see cref="LogicalThreadContextProperties" /> class.
        /// </para>
        /// </remarks>
        internal LogicalThreadContextProperties()
        {
        }

        /// <summary>Gets or sets the value of a property</summary>
        /// <value>
        /// The value for the property with the specified key
        /// </value>
        /// <remarks>
        /// <para>
        /// Get or set the property value for the <paramref name="key"/> specified.
        /// </para>
        /// </remarks>
        public override object this[string key]
        {
            get 
            { 
                // Don't create the dictionary if it does not already exist
                PropertiesDictionary dictionary = this.GetProperties(false);
                if (dictionary != null)
                {
                    return dictionary[key]; 
                }
                return null;
            }
            set 
            {
                // Force the dictionary to be created
                PropertiesDictionary props = this.GetProperties(true);
                // Reason for cloning the dictionary below: object instances held in the
                // async-local store need to be immutable to correctly flow through async/await
                PropertiesDictionary immutableProps = new PropertiesDictionary(props);
                immutableProps[key] = value;
                SetLogicalProperties(immutableProps);
            }
        }

        /// <summary>Remove a property</summary>
        /// <param name="key">the key for the entry to remove</param>
        /// <remarks>
        /// <para>
        /// Remove the value for the specified <paramref name="key"/> from the context.
        /// </para>
        /// </remarks>
        public void Remove(string key)
        {
            PropertiesDictionary dictionary = this.GetProperties(false);
            if (dictionary != null)
            {
                PropertiesDictionary immutableProps = new PropertiesDictionary(dictionary);
                immutableProps.Remove(key);
                SetLogicalProperties(immutableProps);
            }
        }

        /// <summary>Clear all the context properties</summary>
        /// <remarks>
        /// <para>
        /// Clear all the context properties
        /// </para>
        /// </remarks>
        public void Clear()
        {
            PropertiesDictionary dictionary = this.GetProperties(false);
            if (dictionary != null)
            {
                PropertiesDictionary immutableProps = new PropertiesDictionary();
                SetLogicalProperties(immutableProps);
            }
        }

        /// <summary>Get the PropertiesDictionary stored in the async-local store for the current control flow.</summary>
        /// <param name="create">create the dictionary if it does not exist, otherwise return null if is does not exist</param>
        /// <returns>the properties for this thread</returns>
        /// <remarks>
        /// <para>
        /// The collection returned is only to be used on the calling thread. If the
        /// caller needs to share the collection between different threads then the 
        /// caller must clone the collection before doings so.
        /// </para>
        /// </remarks>
        internal PropertiesDictionary GetProperties(bool create)
        {
            if (!this.m_disabled)
            {
                try
                {
                    PropertiesDictionary properties = GetLogicalProperties();
                    if (properties == null && create)
                    {
                        properties = new PropertiesDictionary();
                        SetLogicalProperties(properties);
                    }
                    return properties;
                }
                catch (SecurityException secEx)
                {
                    this.m_disabled = true;

                    // Thrown if we don't have permission to read or write the async-local store
                    LogLog.Warn(declaringType, "SecurityException while accessing the logical thread context store. Disabling LogicalThreadContextProperties", secEx);
                }
            }
            
            // Only get here is we are disabled because of a security exception
            if (create)
            {
                return new PropertiesDictionary();
            }
            return null;
        }

        /// <summary>Gets the dictionary held in the async-local store.</summary>
        /// <returns>The properties dictionary flowing with the current control flow, or <c>null</c>.</returns>
        private static PropertiesDictionary GetLogicalProperties()
        {
            return AsyncLocalDictionary.Value;
        }

        /// <summary>Sets the dictionary held in the async-local store.</summary>
        /// <param name="properties">The properties.</param>
        /// <remarks>
        /// The dictionary passed in must not be mutated afterwards; callers create a
        /// copy first (copy-on-write) so that already-captured flows are unaffected.
        /// </remarks>
        private static void SetLogicalProperties(PropertiesDictionary properties)
        {
            AsyncLocalDictionary.Value = properties;
        }

        /// <summary>The fully qualified type of the LogicalThreadContextProperties class.</summary>
        /// <remarks>
        /// Used by the internal logger to record the Type of the
        /// log message.
        /// </remarks>
        private static readonly Type declaringType = typeof(LogicalThreadContextProperties);
    }
}
