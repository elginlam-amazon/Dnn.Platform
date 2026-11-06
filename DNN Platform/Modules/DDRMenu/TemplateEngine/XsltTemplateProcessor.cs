// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.DDRMenu.TemplateEngine
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Xml;
    using System.Xml.Xsl;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Common;
    using DotNetNuke.Web.DDRMenu.DNNCommon;

    using Microsoft.Extensions.DependencyInjection;

    /// <summary>An <see cref="ITemplateProcessor"/> for XSLT files.</summary>
    /// <param name="hostSettings">The host settings.</param>
    public class XsltTemplateProcessor(IHostSettings hostSettings) : ITemplateProcessor
    {
        private const string ResolverKey = "Resolver";

        private static readonly AsyncLocal<PathResolver> AmbientResolver = new AsyncLocal<PathResolver>();

        private readonly IHostSettings hostSettings = hostSettings ?? DotNetNuke.Common.Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettings>();
        private XslCompiledTransform xsl;

        /// <summary>Gets or sets the path resolver of the template being rendered.</summary>
        /// <remarks>
        /// The slot was <c>HttpContext.Current.Items["Resolver"]</c>, which XsltFunctions reads
        /// back. The request items are still written whenever a request is in flight (so a
        /// cross-component read during a request behaves as before), with an
        /// <see cref="AsyncLocal{T}"/> carrying the value during interactive (SignalR) rendering,
        /// when <see cref="HttpContextSource.Current"/> is <see langword="null"/>.
        /// </remarks>
        internal static PathResolver CurrentResolver
        {
            get
            {
                var items = HttpContextSource.Current?.Items;
                if (items != null && items.TryGetValue(ResolverKey, out var stored))
                {
                    return (PathResolver)stored;
                }

                return AmbientResolver.Value;
            }

            set
            {
                AmbientResolver.Value = value;

                var items = HttpContextSource.Current?.Items;
                if (items != null)
                {
                    items[ResolverKey] = value;
                }
            }
        }

        /// <summary>Initializes a new instance of the <see cref="XsltTemplateProcessor"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.4. Please use overload with IHostSettings. Scheduled removal in v12.0.0.")]
        public XsltTemplateProcessor()
            : this(null)
        {
        }

        /// <inheritdoc />
        public bool LoadDefinition(TemplateDefinition baseDefinition)
        {
            try
            {
                var virtualPath = baseDefinition.TemplateVirtualPath;
                if (!virtualPath.EndsWith(".xsl", StringComparison.InvariantCultureIgnoreCase) &&
                    !virtualPath.EndsWith(".xslt", StringComparison.InvariantCultureIgnoreCase))
                {
                    return false;
                }

                this.xsl = Utilities.CachedXslt(baseDefinition.TemplatePath);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <inheritdoc />
        public void Render(object source, TextWriter htmlWriter, TemplateDefinition liveDefinition)
        {
            var resolver = new PathResolver(this.hostSettings, liveDefinition.Folder);

            var args = new XsltArgumentList();
            args.AddExtensionObject("urn:ddrmenu", new XsltFunctions());
            args.AddExtensionObject("urn:dnngarden", new XsltFunctions());
            args.AddParam("ControlID", string.Empty, DNNContext.Current.ClientId);
            args.AddParam("Options", string.Empty, ConvertToJson(liveDefinition.ClientOptions));
            // Page.ResolveUrl has no IClientApiPage equivalent; the DNN context performs the same
            // application-relative resolution and works for module-control hosts, which have no page.
            var context = DNNContext.Current;
            args.AddParam("DNNPath", string.Empty, context.ResolveUrl(resolver.Resolve("/", PathResolver.RelativeTo.Dnn)));
            args.AddParam("ManifestPath", string.Empty, context.ResolveUrl(resolver.Resolve("/", PathResolver.RelativeTo.Manifest)));
            args.AddParam("PortalPath", string.Empty, context.ResolveUrl(resolver.Resolve("/", PathResolver.RelativeTo.Portal)));
            args.AddParam("SkinPath", string.Empty, context.ResolveUrl(resolver.Resolve("/", PathResolver.RelativeTo.Skin)));
            liveDefinition.TemplateArguments.ForEach(a => args.AddParam(a.Name, string.Empty, a.Value));

            // The resolver is handed to the XSLT extension functions through the request items
            // when a request is in flight; HttpContextSource.Current is null during interactive
            // (SignalR) rendering, so the AsyncLocal fallback carries it instead.
            CurrentResolver = resolver;

            using var xmlStream = new MemoryStream();
            Utilities.SerialiserFor(source.GetType()).Serialize(xmlStream, source);
            xmlStream.Seek(0, SeekOrigin.Begin);
            using var xmlReader = XmlReader.Create(xmlStream);
            this.xsl.Transform(xmlReader, args, htmlWriter);
        }

        protected static string ConvertToJson(List<ClientOption> options)
        {
            var result = new StringBuilder();
            result.Append("{");

            if (options != null)
            {
                foreach (var option in options)
                {
                    if (option is ClientNumber)
                    {
                        result.AppendFormat("{0}:{1},", option.Name, Utilities.ConvertToJs(Convert.ToDecimal(option.Value)));
                    }
                    else if (option is ClientString)
                    {
                        result.AppendFormat("{0}:{1},", option.Name, Utilities.ConvertToJs(option.Value));
                    }
                    else if (option is ClientBoolean)
                    {
                        result.AppendFormat(
                            "{0}:{1},", option.Name, Utilities.ConvertToJs(Convert.ToBoolean(option.Value.ToLowerInvariant())));
                    }
                    else
                    {
                        result.AppendFormat("{0}:{1},", option.Name, option.Value);
                    }
                }

                if (options.Count > 0)
                {
                    result.Remove(result.Length - 1, 1);
                }
            }

            result.Append("}");
            return result.ToString();
        }
    }
}
