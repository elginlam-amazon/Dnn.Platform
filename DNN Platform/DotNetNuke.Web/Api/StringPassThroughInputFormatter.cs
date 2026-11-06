// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Formatters;

    /// <summary>
    /// An <see cref="TextInputFormatter"/> that simply allows strings to pass through the services
    /// framework and be associated with the specified MIME type.
    /// </summary>
    /// <remarks>
    /// Replaces the read half of the Web API 2 <c>StringPassThroughMediaTypeFormatter</c>
    /// (<c>MediaTypeFormatter</c>); register it through
    /// <c>AddControllers(options =&gt; options.InputFormatters.Add(...))</c>.
    /// </remarks>
    public class StringPassThroughInputFormatter : TextInputFormatter
    {
        /// <summary>The media types the formatter handles by default.</summary>
        internal static readonly string[] DefaultMediaTypes = ["text/plain", "text/html",];

        /// <summary>Initializes a new instance of the <see cref="StringPassThroughInputFormatter"/> class that can handle text/plain and text/html.</summary>
        public StringPassThroughInputFormatter()
            : this(DefaultMediaTypes)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="StringPassThroughInputFormatter"/> class that can handle the specified media types.</summary>
        /// <param name="mediaTypes">The supported media types.</param>
        public StringPassThroughInputFormatter(IEnumerable<string> mediaTypes)
        {
            foreach (var type in mediaTypes)
            {
                this.SupportedMediaTypes.Add(type);
            }

            this.SupportedEncodings.Add(Encoding.UTF8);
            this.SupportedEncodings.Add(Encoding.Unicode);
        }

        /// <inheritdoc />
        public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context, Encoding encoding)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(encoding);

            using var reader = new StreamReader(context.HttpContext.Request.Body, encoding);
            var value = await reader.ReadToEndAsync();
            return await InputFormatterResult.SuccessAsync(value);
        }

        /// <inheritdoc />
        protected override bool CanReadType(Type type)
        {
            return type == typeof(string);
        }
    }
}
