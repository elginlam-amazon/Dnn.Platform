// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc.Formatters;

    /// <summary>
    /// A <see cref="TextOutputFormatter"/> that simply allows strings to pass through the services
    /// framework and be associated with the specified MIME type.
    /// </summary>
    /// <remarks>
    /// Replaces the write half of the Web API 2 <c>StringPassThroughMediaTypeFormatter</c>
    /// (<c>MediaTypeFormatter</c>); register it through
    /// <c>AddControllers(options =&gt; options.OutputFormatters.Add(...))</c>. The string is written
    /// verbatim, exactly as <c>WriteToStreamAsync</c> did.
    /// </remarks>
    public class StringPassThroughOutputFormatter : TextOutputFormatter
    {
        /// <summary>Initializes a new instance of the <see cref="StringPassThroughOutputFormatter"/> class that can handle text/plain and text/html.</summary>
        public StringPassThroughOutputFormatter()
            : this(StringPassThroughInputFormatter.DefaultMediaTypes)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="StringPassThroughOutputFormatter"/> class that can handle the specified media types.</summary>
        /// <param name="mediaTypes">The supported media types.</param>
        public StringPassThroughOutputFormatter(IEnumerable<string> mediaTypes)
        {
            foreach (var type in mediaTypes)
            {
                this.SupportedMediaTypes.Add(type);
            }

            this.SupportedEncodings.Add(Encoding.UTF8);
            this.SupportedEncodings.Add(Encoding.Unicode);
        }

        /// <inheritdoc />
        public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(selectedEncoding);

            var value = (string)context.Object;
            await context.HttpContext.Response.WriteAsync(value ?? string.Empty, selectedEncoding);
        }

        /// <inheritdoc />
        protected override bool CanWriteType(Type type)
        {
            return type == typeof(string);
        }
    }
}
