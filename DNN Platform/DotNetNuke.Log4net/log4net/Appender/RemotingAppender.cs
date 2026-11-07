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
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

using log4net.Core;
using log4net.Util;

namespace log4net.Appender
{
    /// <summary>Delivers logging events to a remote logging sink. </summary>
    /// <remarks>
    /// <para>
    /// This Appender is designed to deliver events to a remote sink, identified by the
    /// appender's <see cref="RemotingAppender.Sink"/> property.
    /// </para>
    /// <para>
    /// Historically the events were delivered with .NET Remoting to an object implementing
    /// <see cref="IRemoteLoggingSink"/>. .NET Remoting does not exist on modern .NET, so the
    /// transport is now an HTTP <c>POST</c> of a JSON batch document (see
    /// <see cref="LoggingEventWireFormat"/>) to the URL given by <see cref="Sink"/>. The
    /// receiving end is <see cref="log4net.Plugin.RemoteLoggingServerPlugin"/>, which accepts
    /// the same document. Both ends must therefore be running this version of log4net —
    /// a legacy remoting sink cannot accept the HTTP/JSON batch, and vice versa.
    /// </para>
    /// <para>
    /// The RemotingAppender buffers events before sending them. This allows it to
    /// make more efficient use of the transport. The buffering semantics are those of
    /// <see cref="BufferingAppenderSkeleton"/>: <see cref="BufferingAppenderSkeleton.BufferSize"/>,
    /// <see cref="BufferingAppenderSkeleton.Lossy"/> and
    /// <see cref="BufferingAppenderSkeleton.Evaluator"/> all behave exactly as before.
    /// </para>
    /// <para>
    /// Once the buffer is full the events are still not sent immediately.
    /// They are scheduled to be sent on a background thread pool thread, so the send occurs
    /// asynchronously and a logging failure (an unreachable sink, a slow network) never
    /// blocks or throws on the thread that produced the log call.
    /// </para>
    /// <para>
    /// Because the events are sent asynchronously using pool threads it is possible to close
    /// this appender before all the queued events have been sent.
    /// When closing, the appender attempts to wait until all the queued events have been sent, but
    /// this will timeout after 30 seconds regardless.</para>
    /// <para>
    /// If this appender is being closed because the process is exiting it may not be possible
    /// to send all the queued events: the runtime limits how long a process-exit handler may
    /// run for. To ensure that all events are sent the appender must be closed before the
    /// application exits. See <see cref="log4net.Core.LoggerManager.Shutdown"/> for details on
    /// how to shutdown log4net programmatically.</para>
    /// </remarks>
    /// <seealso cref="IRemoteLoggingSink" />
    /// <author>Nicko Cadell</author>
    /// <author>Gert Driesen</author>
    /// <author>Daniel Cazzulino</author>
    public class RemotingAppender : BufferingAppenderSkeleton
    {
        /// <summary>
        /// The single long-lived <see cref="HttpClient"/> used by every instance of this
        /// appender.
        /// </summary>
        /// <remarks>
        /// log4net is a library and has no access to the host's
        /// <c>IHttpClientFactory</c>, so a single static client is used rather than one per
        /// send: creating an <see cref="HttpClient"/> per send leaks a connection pool each
        /// time and exhausts the machine's ephemeral ports under load.
        /// </remarks>
        private static readonly HttpClient SharedClient = CreateSharedClient();

        /// <summary>The URL of the remote sink.</summary>
        private string m_sinkUrl;

        /// <summary>The absolute sink URL resolved by <see cref="ActivateOptions"/>.</summary>
        private Uri m_sinkUri;

        /// <summary>The number of queued sends currently waiting or executing</summary>
        private int m_queuedCallbackCount;

        /// <summary>Event used to signal when there are no queued work items</summary>
        /// <remarks>
        /// This event is set when there are no queued work items. In this
        /// state it is safe to close the appender.
        /// </remarks>
        private readonly ManualResetEvent m_workQueueEmptyEvent = new ManualResetEvent(true);

        /// <summary>Initializes a new instance of the <see cref="RemotingAppender" /> class.</summary>
        /// <remarks>
        /// <para>
        /// Default constructor.
        /// </para>
        /// </remarks>
        public RemotingAppender()
        {
        }

        /// <summary>
        /// Gets or sets the URL of the well-known endpoint that will accept
        /// the logging events.
        /// </summary>
        /// <value>
        /// The well-known URL of the remote sink.
        /// </value>
        /// <remarks>
        /// <para>
        /// The absolute HTTP or HTTPS URL that logging events are POSTed to as a JSON batch.
        /// The endpoint must accept the document described by
        /// <see cref="LoggingEventWireFormat"/>; an instance of
        /// <see cref="log4net.Plugin.RemoteLoggingServerPlugin"/> listening on the same URL
        /// does so.
        /// </para>
        /// </remarks>
        public string Sink
        {
            get { return this.m_sinkUrl; }
            set { this.m_sinkUrl = value; }
        }

        /// <summary>
        /// Gets or sets the time to wait for the sink to accept a batch before the send
        /// is abandoned.
        /// </summary>
        /// <value>The send timeout. The default is 30 seconds.</value>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>Initialize the appender based on the options set</summary>
        /// <remarks>
        /// <para>
        /// This is part of the <see cref="IOptionHandler"/> delayed object
        /// activation scheme. The <see cref="ActivateOptions"/> method must
        /// be called on this object after the configuration properties have
        /// been set. Until <see cref="ActivateOptions"/> is called this
        /// object is in an undefined state and must not be used.
        /// </para>
        /// <para>
        /// If any of the configuration properties are modified then
        /// <see cref="ActivateOptions"/> must be called again.
        /// </para>
        /// </remarks>
        public override void ActivateOptions()
        {
            base.ActivateOptions();

            this.m_sinkUri = null;

            if (string.IsNullOrEmpty(this.m_sinkUrl))
            {
                this.ErrorHandler.Error("RemotingAppender [" + this.Name + "] has no Sink URL set.");
                return;
            }

            Uri parsed;
            if (!Uri.TryCreate(this.m_sinkUrl, UriKind.Absolute, out parsed))
            {
                this.ErrorHandler.Error("RemotingAppender [" + this.Name + "] Sink [" + this.m_sinkUrl + "] is not an absolute URL.");
                return;
            }

            this.m_sinkUri = parsed;
        }

        /// <summary>Send the contents of the buffer to the remote sink.</summary>
        /// <remarks>
        /// <para>
        /// The events are not sent immediately. They are scheduled to be sent
        /// using a pool thread, so the send occurs asynchronously. This matters because the
        /// sink may be unreachable or slow: performing the send on the calling thread would
        /// let a logging failure have side effects on the calling application.
        /// </para>
        /// <para>
        /// The work item runs the fully asynchronous <see cref="SendBufferAsync"/> and awaits
        /// it on that background thread, so no request or application thread is ever blocked
        /// on the HTTP call.
        /// </para>
        /// </remarks>
        /// <param name="events">The events to send.</param>
        protected override void SendBuffer(LoggingEvent[] events)
        {
            if (events == null || events.Length == 0)
            {
                return;
            }

            // Setup for an async send
            this.BeginAsyncSend();

            // Send the events
            if (!ThreadPool.QueueUserWorkItem(this.SendBufferCallback, events))
            {
                // Cancel the async send
                this.EndAsyncSend();

                this.ErrorHandler.Error("RemotingAppender [" + this.Name + "] failed to ThreadPool.QueueUserWorkItem logging events in SendBuffer.");
            }
        }

        /// <summary>Override base class close.</summary>
        /// <remarks>
        /// <para>
        /// This method waits while there are queued work items. The events are
        /// sent asynchronously using thread pool work items. These items
        /// will be sent once a thread pool thread is available to send them, therefore
        /// it is possible to close the appender before all the queued events have been
        /// sent.</para>
        /// <para>
        /// This method attempts to wait until all the queued events have been sent, but this
        /// method will timeout after 30 seconds regardless.</para>
        /// </remarks>
        protected override void OnClose()
        {
            base.OnClose();

            // Wait for the work queue to become empty before closing, timeout 30 seconds
            if (!this.m_workQueueEmptyEvent.WaitOne(30 * 1000, false))
            {
                this.ErrorHandler.Error("RemotingAppender [" + this.Name + "] failed to send all queued events before close, in OnClose.");
            }
        }

        /// <summary>Flushes any buffered log data.</summary>
        /// <param name="millisecondsTimeout">The maximum time to wait for logging events to be flushed.</param>
        /// <returns><c>True</c> if all logging events were flushed successfully, else <c>false</c>.</returns>
        public override bool Flush(int millisecondsTimeout)
        {
            base.Flush();
            return this.m_workQueueEmptyEvent.WaitOne(millisecondsTimeout, false);
        }

        /// <summary>Posts a batch of events to the sink as a JSON document.</summary>
        /// <param name="events">The events to send.</param>
        /// <returns>A task that completes when the sink has accepted the batch.</returns>
        /// <remarks>
        /// This is the whole transport. It is invoked from a background thread pool thread
        /// and awaited there; it is never awaited on an application thread.
        /// </remarks>
        protected virtual async Task SendBufferAsync(LoggingEvent[] events)
        {
            Uri sinkUri = this.m_sinkUri;
            if (sinkUri == null)
            {
                this.ErrorHandler.Error("RemotingAppender [" + this.Name + "] cannot send events: the Sink URL is not set or is not valid.");
                return;
            }

            byte[] payload = LoggingEventWireFormat.Serialize(events);

            using (var cancellation = new CancellationTokenSource(this.Timeout))
            using (var content = new ByteArrayContent(payload))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue(LoggingEventWireFormat.MediaType);

                using (var request = new HttpRequestMessage(HttpMethod.Post, sinkUri) { Content = content })
                using (HttpResponseMessage response = await SharedClient
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation.Token)
                    .ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        this.ErrorHandler.Error(
                            "RemotingAppender [" + this.Name + "] sink [" + sinkUri + "] rejected the batch with status " +
                            (int)response.StatusCode + " " + response.ReasonPhrase + ": " + body);
                    }
                }
            }
        }

        /// <summary>A work item is being queued into the thread pool</summary>
        private void BeginAsyncSend()
        {
            // The work queue is not empty
            this.m_workQueueEmptyEvent.Reset();

            // Increment the queued count
            Interlocked.Increment(ref this.m_queuedCallbackCount);
        }

        /// <summary>A work item from the thread pool has completed</summary>
        private void EndAsyncSend()
        {
            // Decrement the queued count
            if (Interlocked.Decrement(ref this.m_queuedCallbackCount) <= 0)
            {
                // If the work queue is empty then set the event
                this.m_workQueueEmptyEvent.Set();
            }
        }

        /// <summary>Send the contents of the buffer to the remote sink.</summary>
        /// <remarks>
        /// This method is designed to be used as a thread pool work item and expects to be
        /// passed an array of <see cref="LoggingEvent"/> objects in the state param. It runs
        /// the asynchronous send to completion on this background thread, so blocking here is
        /// safe and affects no application thread.
        /// </remarks>
        /// <param name="state">the logging events to send</param>
        private void SendBufferCallback(object state)
        {
            try
            {
                var events = (LoggingEvent[])state;
                this.SendBufferAsync(events).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                this.ErrorHandler.Error("Failed in SendBufferCallback", ex);
            }
            finally
            {
                this.EndAsyncSend();
            }
        }

        private static HttpClient CreateSharedClient()
        {
            var handler = new SocketsHttpHandler
            {
                // Recycle pooled connections so DNS changes to the sink host are picked up,
                // which is what IHttpClientFactory's handler rotation does for applications.
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            };

            return new HttpClient(handler, disposeHandler: true)
            {
                // Per-send timeouts are applied with a CancellationToken instead.
                Timeout = System.Threading.Timeout.InfiniteTimeSpan,
            };
        }

        /// <summary>Interface used to deliver <see cref="LoggingEvent"/> objects to a remote sink.</summary>
        /// <remarks>
        /// <para>
        /// This interface describes the contract of a logging sink: accept a batch of events
        /// and log them. It is retained for compatibility with code written against the
        /// remoting transport, and is implemented on the receiving side by
        /// <see cref="log4net.Plugin.RemoteLoggingServerPlugin"/>. The events no longer travel
        /// as a remoting call: they are POSTed as the JSON document described by
        /// <see cref="LoggingEventWireFormat"/> and the receiver invokes this method locally
        /// once it has deserialized them.
        /// </para>
        /// </remarks>
        public interface IRemoteLoggingSink
        {
            /// <summary>Delivers logging events to the remote sink</summary>
            /// <param name="events">Array of events to log.</param>
            /// <remarks>
            /// <para>
            /// Delivers logging events to the remote sink
            /// </para>
            /// </remarks>
            void LogEvents(LoggingEvent[] events);
        }
    }
}
