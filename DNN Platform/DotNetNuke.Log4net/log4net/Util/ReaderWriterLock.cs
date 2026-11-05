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
using System.Threading;

namespace log4net.Util
{
    /// <summary>Defines a lock that supports single writers and multiple readers</summary>
    /// <remarks>
    /// <para>
    /// <c>ReaderWriterLock</c> is used to synchronize access to a resource. 
    /// At any given time, it allows either concurrent read access for 
    /// multiple threads, or write access for a single thread. In a 
    /// situation where a resource is changed infrequently, a 
    /// <c>ReaderWriterLock</c> provides better throughput than a simple 
    /// one-at-a-time lock, such as <see cref="System.Threading.Monitor"/>.
    /// </para>
    /// <para>
    /// This type is a thin wrapper over <see cref="ReaderWriterLockSlim"/>, which is the
    /// supported reader/writer lock on modern .NET. Recursion is permitted so that the
    /// nested acquisition patterns used by the log4net internals keep working.
    /// </para>
    /// </remarks>
    /// <author>Nicko Cadell</author>
    public sealed class ReaderWriterLock
    {
        private readonly ReaderWriterLockSlim m_lock;

        /// <summary>Constructor</summary>
        /// <remarks>
        /// <para>
        /// Initializes a new instance of the <see cref="ReaderWriterLock" /> class.
        /// </para>
        /// </remarks>
        public ReaderWriterLock()
        {
            this.m_lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        }

        /// <summary>Acquires a reader lock</summary>
        /// <remarks>
        /// <para>
        /// <see cref="AcquireReaderLock"/> blocks if a different thread has the writer 
        /// lock, or if at least one thread is waiting for the writer lock.
        /// </para>
        /// </remarks>
        public void AcquireReaderLock()
        {
            this.m_lock.EnterReadLock();
        }

        /// <summary>Decrements the lock count</summary>
        /// <remarks>
        /// <para>
        /// <see cref="ReleaseReaderLock"/> decrements the lock count. When the count 
        /// reaches zero, the lock is released.
        /// </para>
        /// </remarks>
        public void ReleaseReaderLock()
        {
            this.m_lock.ExitReadLock();
        }

        /// <summary>Acquires the writer lock</summary>
        /// <remarks>
        /// <para>
        /// This method blocks if another thread has a reader lock or writer lock.
        /// </para>
        /// </remarks>
        public void AcquireWriterLock()
        {
            this.m_lock.EnterWriteLock();
        }

        /// <summary>Decrements the lock count on the writer lock</summary>
        /// <remarks>
        /// <para>
        /// ReleaseWriterLock decrements the writer lock count. 
        /// When the count reaches zero, the writer lock is released.
        /// </para>
        /// </remarks>
        public void ReleaseWriterLock()
        {
            this.m_lock.ExitWriteLock();
        }
    }
}
