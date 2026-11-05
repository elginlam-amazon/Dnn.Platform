// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Syndication
{
    using System.Collections;

    /// <summary>Provides the items of an RSS feed for data binding.</summary>
    /// <remarks>
    /// This used to be a WebForms <c>DataSourceControl</c> (with a companion
    /// <c>RssDataSourceView</c>). ASP.NET Core has no data-source control model, so this is a plain
    /// service: set <see cref="Url"/> and <see cref="MaxItems"/>, then enumerate
    /// <see cref="SelectItems"/> (for example from a Razor <c>@foreach</c>).
    /// </remarks>
    public class RssDataSource
    {
        private GenericRssChannel channel;
        private string url;

        /// <summary>Gets the channel.</summary>
        public GenericRssChannel Channel
        {
            get
            {
                if (this.channel == null)
                {
                    if (string.IsNullOrEmpty(this.url))
                    {
                        this.channel = new GenericRssChannel();
                    }
                    else
                    {
                        this.channel = GenericRssChannel.LoadChannel(this.url);
                    }
                }

                return this.channel;
            }
        }

        /// <summary>Gets or sets the maximum number of items.</summary>
        public int MaxItems { get; set; }

        /// <summary>Gets or sets the URL.</summary>
        public string Url
        {
            get
            {
                return this.url;
            }

            set
            {
                this.channel = null;
                this.url = value;
            }
        }

        /// <summary>Gets the channel's items, limited to <see cref="MaxItems"/>.</summary>
        /// <returns>A sequence of <see cref="RssElementCustomTypeDescriptor"/> instances to bind to.</returns>
        /// <remarks>This replaces <c>RssDataSourceView.ExecuteSelect</c>.</remarks>
        public IEnumerable SelectItems()
        {
            return this.Channel.SelectItems(this.MaxItems);
        }
    }
}
