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
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using log4net.Core;
using log4net.Repository;
using log4net.Util;
using IRemoteLoggingSink = log4net.Appender.RemotingAppender.IRemoteLoggingSink;

namespace log4net.Plugin
{
    /// <summary>Plugin that listens for events from the <see cref="log4net.Appender.RemotingAppender"/></summary>
    /// <remarks>
    /// <para>
    /// This plugin publishes a logging sink on the URL given by <see cref="SinkUri"/>. It
    /// listens for logging events delivered from a remote
    /// <see cref="log4net.Appender.RemotingAppender"/>.
    /// </para>
    /// <para>
    /// Historically the sink was a .NET Remoting object marshalled under a well-known name.
    /// .NET Remoting does not exist on modern .NET, so the sink is now an
    /// <see cref="HttpListener"/> endpoint that accepts an HTTP <c>POST</c> whose body is the
    /// JSON batch document described by <see cref="LoggingEventWireFormat"/>. The sending
    /// <see cref="log4net.Appender.RemotingAppender"/> must therefore also be running this
    /// version of log4net — both ends of the link must be upgraded together.
    /// </para>
    /// <para>
    /// When a batch is received each event in it is relogged within the attached repository
    /// as if it had been raised locally.
    /// </para>
    /// <para>
    /// The listener runs on a background task. Any failure — a malformed request, a bad
    /// payload, a listener error — is reported through <see cref="LogLog"/> and the loop
    /// continues; it never escapes to crash the background thread.
    /// </para>
    /// </remarks>
    /// <author>Nicko Cadell</author>
    /// <author>Gert Driesen</author>
    public class RemoteLoggingServerPlugin : PluginSkeleton
    {
        /// <summary>The fully qualified type of the RemoteLoggingServerPlugin class.</summary>
        /// <remarks>
        /// Used by the internal logger to record the Type of the
        /// log message.
        /// </remarks>
        private static readonly Type declaringType = typeof(RemoteLoggingServerPlugin);

        private readonly object m_syncRoot = new object();
        private RemoteLoggingSinkImpl m_sink;
        private string m_sinkUri;
        private HttpListener m_listener;
        private CancellationTokenSource m_cancellation;
        private Task m_listenerTask;

        /// <summary>Default constructor</summary>
        /// <remarks>
        /// <para>
        /// Initializes a new instance of the <see cref="RemoteLoggingServerPlugin" /> class.
        /// </para>
        /// <para>
        /// The <see cref="SinkUri"/> property must be set.
        /// </para>
        /// </remarks>
        public RemoteLoggingServerPlugin() : base("RemoteLoggingServerPlugin:Unset URI")
        {
        }

        /// <summary>Construct with sink Uri.</summary>
        /// <param name="sinkUri">The URL to publish the sink under.
        /// See <see cref="SinkUri"/> for more details.</param>
        /// <remarks>
        /// <para>
        /// Initializes a new instance of the <see cref="RemoteLoggingServerPlugin" /> class
        /// with specified name.
        /// </para>
        /// </remarks>
        public RemoteLoggingServerPlugin(string sinkUri) : base("RemoteLoggingServerPlugin:" + sinkUri)
        {
            this.m_sinkUri = sinkUri;
        }

        /// <summary>Gets or sets the URI of this sink.</summary>
        /// <value>
        /// The URI of this sink.
        /// </value>
        /// <remarks>
        /// <para>
        /// This is the absolute HTTP or HTTPS URL that the sink listens on, and must match the
        /// <see cref="log4net.Appender.RemotingAppender.Sink"/> URL configured on the sending
        /// side — for example <c>http://localhost:8787/log4net/</c>.
        /// </para>
        /// <para>
        /// Under the former remoting transport this was a bare object name rather than a URL;
        /// configurations carrying a bare name must be updated to an absolute URL.
        /// </para>
        /// </remarks>
        public virtual string SinkUri
        {
            get { return this.m_sinkUri; }
            set { this.m_sinkUri = value; }
        }

        /// <summary>Attaches this plugin to a <see cref="ILoggerRepository"/>.</summary>
        /// <param name="repository">The <see cref="ILoggerRepository"/> that this plugin should be attached to.</param>
        /// <remarks>
        /// <para>
        /// A plugin may only be attached to a single repository.
        /// </para>
        /// <para>
        /// This method is called when the plugin is attached to the repository. It creates the
        /// sink and starts the HTTP listener that feeds it.
        /// </para>
        /// </remarks>
        public override void Attach(ILoggerRepository repository)
        {
            base.Attach(repository);

            // Create the sink and start serving it over HTTP
            this.m_sink = new RemoteLoggingSinkImpl(repository);

            try
            {
                this.StartListener(repository);
            }
            catch (Exception ex)
            {
                LogLog.Error(declaringType, "Failed to start the remote logging sink listener", ex);
            }
        }

        /// <summary>Is called when the plugin is to shutdown.</summary>
        /// <remarks>
        /// <para>
        /// When the plugin is shutdown the HTTP listener is stopped and closed so it no longer
        /// receives batches, and the background listener task is given a short grace period to
        /// observe the cancellation and exit.
        /// </para>
        /// </remarks>
        public override void Shutdown()
        {
            CancellationTokenSource cancellation;
            HttpListener listener;
            Task listenerTask;

            lock (this.m_syncRoot)
            {
                cancellation = this.m_cancellation;
                listener = this.m_listener;
                listenerTask = this.m_listenerTask;

                this.m_cancellation = null;
                this.m_listener = null;
                this.m_listenerTask = null;
            }

            try
            {
                if (cancellation != null)
                {
                    cancellation.Cancel();
                }

                if (listener != null)
                {
                    // Stop() unblocks the pending GetContextAsync; Close() releases the prefix.
                    if (listener.IsListening)
                    {
                        listener.Stop();
                    }

                    listener.Close();
                }

                if (listenerTask != null)
                {
                    // The loop only ever completes normally, but wait with a timeout so a
                    // wedged listener can never hang repository shutdown.
                    listenerTask.Wait(TimeSpan.FromSeconds(5));
                }
            }
            catch (Exception ex)
            {
                LogLog.Error(declaringType, "Failed to stop the remote logging sink listener", ex);
            }
            finally
            {
                if (cancellation != null)
                {
                    cancellation.Dispose();
                }
            }

            this.m_sink = null;

            base.Shutdown();
        }

        /// <summary>Builds the <see cref="HttpListener"/> prefix for the configured sink URL.</summary>
        /// <param name="sinkUri">The configured sink URL.</param>
        /// <returns>The listener prefix, or <c>null</c> if the URL is unusable.</returns>
        private static string BuildPrefix(string sinkUri)
        {
            if (string.IsNullOrEmpty(sinkUri))
            {
                LogLog.Error(declaringType, "RemoteLoggingServerPlugin has no SinkUri set; the sink will not listen.");
                return null;
            }

            Uri parsed;
            if (!Uri.TryCreate(sinkUri, UriKind.Absolute, out parsed) ||
                (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
            {
                LogLog.Error(
                    declaringType,
                    "RemoteLoggingServerPlugin SinkUri [" + sinkUri + "] is not an absolute http or https URL. " +
                    "The remoting transport has been replaced by HTTP, so SinkUri must now be a URL such as http://localhost:8787/log4net/.");
                return null;
            }

            // HttpListener requires the prefix to end with a slash.
            string prefix = parsed.GetLeftPart(UriPartial.Path);
            return prefix.EndsWith("/", StringComparison.Ordinal) ? prefix : prefix + "/";
        }

        private void StartListener(ILoggerRepository repository)
        {
            string prefix = BuildPrefix(this.m_sinkUri);
            if (prefix == null)
            {
                return;
            }

            if (!HttpListener.IsSupported)
            {
                LogLog.Error(declaringType, "HttpListener is not supported on this platform; the remote logging sink will not listen on [" + prefix + "].");
                return;
            }

            var listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();

            var cancellation = new CancellationTokenSource();

            lock (this.m_syncRoot)
            {
                this.m_listener = listener;
                this.m_cancellation = cancellation;
                this.m_listenerTask = Task.Run(() => this.ListenAsync(listener, repository, cancellation.Token));
            }

            LogLog.Debug(declaringType, "RemoteLoggingServerPlugin listening for logging event batches on [" + prefix + "]");
        }

        /// <summary>The accept loop. Never throws: every failure is reported through <see cref="LogLog"/>.</summary>
        private async Task ListenAsync(HttpListener listener, ILoggerRepository repository, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    // The listener was closed by Shutdown.
                    return;
                }
                catch (HttpListenerException)
                {
                    // The listener was stopped by Shutdown, or the OS aborted the wait.
                    return;
                }
                catch (InvalidOperationException)
                {
                    // The listener is no longer listening.
                    return;
                }
                catch (Exception ex)
                {
                    LogLog.Error(declaringType, "Unexpected error while accepting a logging event batch", ex);
                    return;
                }

                try
                {
                    await this.HandleRequestAsync(context, repository).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogLog.Error(declaringType, "Failed to handle a logging event batch", ex);
                }
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context, ILoggerRepository repository)
        {
            HttpListenerResponse response = context.Response;
            try
            {
                if (!string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
                {
                    response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    return;
                }

                byte[] payload;
                using (var buffer = new MemoryStream())
                {
                    await context.Request.InputStream.CopyToAsync(buffer).ConfigureAwait(false);
                    payload = buffer.ToArray();
                }

                LoggingEventBatch batch;
                try
                {
                    batch = LoggingEventWireFormat.Deserialize(payload);
                }
                catch (Exception ex)
                {
                    LogLog.Error(declaringType, "Received a logging event batch that could not be deserialized", ex);
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }

                RemoteLoggingSinkImpl sink = this.m_sink;
                if (sink == null)
                {
                    response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                    return;
                }

                var events = new LoggingEvent[batch.Events.Count];
                int count = 0;
                foreach (LoggingEventWireItem item in batch.Events)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    try
                    {
                        events[count] = LoggingEventWireFormat.FromWire(item, repository);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        LogLog.Error(declaringType, "Discarded a logging event that could not be rebuilt from the wire format", ex);
                    }
                }

                if (count != events.Length)
                {
                    Array.Resize(ref events, count);
                }

                sink.LogEvents(events);
                response.StatusCode = (int)HttpStatusCode.Accepted;
            }
            finally
            {
                try
                {
                    response.Close();
                }
                catch (Exception ex)
                {
                    LogLog.Warn(declaringType, "Failed to complete the response to a logging event batch", ex);
                }
            }
        }

        /// <summary>Delivers <see cref="LoggingEvent"/> objects into the local repository.</summary>
        /// <remarks>
        /// <para>
        /// Internal class used to receive logging events
        /// and deliver them to the local repository.
        /// </para>
        /// </remarks>
        private sealed class RemoteLoggingSinkImpl : IRemoteLoggingSink
        {
            /// <summary>
            /// The underlying <see cref="ILoggerRepository" /> that events should
            /// be logged to.
            /// </summary>
            private readonly ILoggerRepository m_repository;

            /// <summary>Constructor</summary>
            /// <param name="repository">The repository to log to.</param>
            /// <remarks>
            /// <para>
            /// Initializes a new instance of the <see cref="RemoteLoggingSinkImpl"/> for the
            /// specified <see cref="ILoggerRepository"/>.
            /// </para>
            /// </remarks>
            public RemoteLoggingSinkImpl(ILoggerRepository repository)
            {
                this.m_repository = repository;
            }

            /// <summary>Logs the events to the repository.</summary>
            /// <param name="events">The events to log.</param>
            /// <remarks>
            /// <para>
            /// The events passed are logged to the <see cref="ILoggerRepository"/>
            /// </para>
            /// </remarks>
            public void LogEvents(LoggingEvent[] events)
            {
                if (events != null)
                {
                    foreach (LoggingEvent logEvent in events)
                    {
                        if (logEvent != null)
                        {
                            this.m_repository.Log(logEvent);
                        }
                    }
                }
            }
        }
    }
}
