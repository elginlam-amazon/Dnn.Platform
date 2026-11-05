// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Syndication
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>A function which handles the <see cref="RssHttpHandlerBase{TRssChannelType,TRssItemType,TRssImageType}.Init"/> event.</summary>
    /// <param name="source">The event source, i.e. the <see cref="RssHttpHandlerBase{TRssChannelType,TRssItemType,TRssImageType}"/> instance.</param>
    /// <param name="e">Empty event args.</param>
    [SuppressMessage("Microsoft.Design", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix", Justification = "Breaking change")]
    public delegate void InitEventHandler(object source, EventArgs e);

    /// <summary>A function which handles the <see cref="RssHttpHandlerBase{TRssChannelType,TRssItemType,TRssImageType}.PreRender"/> event.</summary>
    /// <param name="source">The event source, i.e. the <see cref="RssHttpHandlerBase{TRssChannelType,TRssItemType,TRssImageType}"/> instance.</param>
    /// <param name="e">Empty event args.</param>
    [SuppressMessage("Microsoft.Design", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix", Justification = "Breaking change")]
    public delegate void PreRenderEventHandler(object source, EventArgs e);

    /// <summary>Base class for the RSS feed request handler — the generic handler and strongly typed ones are derived from it.</summary>
    /// <typeparam name="TRssChannelType">The channel type.</typeparam>
    /// <typeparam name="TRssItemType">The item type.</typeparam>
    /// <typeparam name="TRssImageType">The image type.</typeparam>
    /// <remarks>
    /// This used to be an <c>IHttpHandler</c> registered for an <c>.ashx</c> path. ASP.NET Core has
    /// no HTTP handler pipeline, so the request is handled from an endpoint; see
    /// <see cref="SyndicationEndpointRouteBuilderExtensions.MapGenericRssHandler"/>.
    /// </remarks>
    public abstract class RssHttpHandlerBase<TRssChannelType, TRssItemType, TRssImageType>
        where TRssChannelType : RssChannelBase<TRssItemType, TRssImageType>, new()
        where TRssItemType : RssElementBase, new()
        where TRssImageType : RssElementBase, new()
    {
        /// <summary>An event which is fired when the handler is initialized.</summary>
        public event InitEventHandler Init;

        /// <summary>An event which is fired just before the handler's contents are rendered.</summary>
        public event PreRenderEventHandler PreRender;

        /// <summary>Gets the channel.</summary>
        protected TRssChannelType Channel { get; private set; }

        /// <summary>Gets the HTTP context.</summary>
        [CLSCompliant(false)]
        protected HttpContext Context { get; private set; }

        /// <summary>Handles a request for the feed, writing the channel's XML to the response.</summary>
        /// <param name="context">The HTTP context of the request.</param>
        /// <returns>A task which completes once the feed has been written to the response.</returns>
        [CLSCompliant(false)]
        public async Task HandleRequestAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            this.InternalInit(context);

            // let inherited handlers setup any special handling
            this.OnInit(EventArgs.Empty);

            // parse the channel name and the user name from the query string
            var helper = context.RequestServices.GetRequiredService<RssHttpHandlerHelper>();
            helper.ParseChannelQueryString(context.Request, out var channelName, out var userName);

            // populate items (call the derived class)
            this.PopulateChannel(channelName, userName);

            this.OnPreRender(EventArgs.Empty);

            // UTF8Encoding(false): the net48 handler used new XmlTextWriter(stream, null), which
            // emitted no byte-order mark; XmlWriter.Create would add one by default.
            var settings = new XmlWriterSettings
            {
                Async = true,
                CloseOutput = false,
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            };
            using (var writer = XmlWriter.Create(this.Context.Response.Body, settings))
            {
                this.Render(writer);
                await writer.FlushAsync().ConfigureAwait(false);
            }

            await this.Context.Response.Body.FlushAsync(context.RequestAborted).ConfigureAwait(false);
        }

        /// <summary>Triggers the <see cref="Init"/> event.</summary>
        /// <param name="ea">The event args.</param>
        protected virtual void OnInit(EventArgs ea)
        {
            this.Init?.Invoke(this, ea);
        }

        /// <summary>Triggers the <see cref="PreRender"/> event.</summary>
        /// <param name="ea">The event args.</param>
        protected virtual void OnPreRender(EventArgs ea)
        {
            this.PreRender?.Invoke(this, ea);
        }

        /// <summary>When overridden in a derived class, populates the channel name and user name.</summary>
        /// <param name="channelName">The channel name.</param>
        /// <param name="userName">The user name.</param>
        protected virtual void PopulateChannel(string channelName, string userName)
        {
        }

        /// <summary>Renders the channel as XML to the <paramref name="writer"/>.</summary>
        /// <param name="writer">The writer to which the channel should be rendered.</param>
        protected virtual void Render(XmlWriter writer)
        {
            XmlDocument doc = this.Channel.SaveAsXml();
            doc.Save(writer);
        }

        private void InternalInit(HttpContext context)
        {
            this.Context = context;

            // create the channel
            this.Channel = new TRssChannelType();
            this.Channel.SetDefaults();

            this.Context.Response.ContentType = "text/xml";
        }
    }
}
