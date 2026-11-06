// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.DDRMenu.TemplateEngine
{
    using System;
    using System.Collections.Generic;
    using System.Dynamic;
    using System.IO;
    using System.Text;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Common;
    using DotNetNuke.Web.DDRMenu.DNNCommon;
    using DotNetNuke.Web.Razor;

    using Microsoft.Extensions.DependencyInjection;

    /// <summary>An <see cref="ITemplateProcessor"/> for Razor files.</summary>
    /// <param name="hostSettings">The host settings.</param>
    public class RazorTemplateProcessor(IHostSettings hostSettings) : ITemplateProcessor
    {
        private readonly IHostSettings hostSettings = hostSettings ?? Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettings>();

        /// <summary>Initializes a new instance of the <see cref="RazorTemplateProcessor"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.4. Please use overload with IHostSettings. Scheduled removal in v12.0.0.")]
        public RazorTemplateProcessor()
            : this(null)
        {
        }

        /// <inheritdoc />
        public bool LoadDefinition(TemplateDefinition baseDefinition)
        {
            var virtualPath = baseDefinition.TemplateVirtualPath;

            if (!virtualPath.EndsWith(".cshtml", StringComparison.InvariantCultureIgnoreCase) &&
                !virtualPath.EndsWith(".vbhtml", StringComparison.InvariantCultureIgnoreCase))
            {
                return false;
            }

            return true;
        }

        /// <inheritdoc />
        public void Render(object source, TextWriter htmlWriter, TemplateDefinition liveDefinition)
        {
            if (!string.IsNullOrEmpty(liveDefinition.TemplateVirtualPath))
            {
                var resolver = new PathResolver(this.hostSettings, liveDefinition.Folder);
                dynamic model = new ExpandoObject();
                model.Source = source;
                model.ControlID = DNNContext.Current.ClientId;
                model.Options = ConvertToJson(liveDefinition.ClientOptions);
                model.DNNPath = resolver.Resolve("/", PathResolver.RelativeTo.Dnn);
                model.ManifestPath = resolver.Resolve("/", PathResolver.RelativeTo.Manifest);
                model.PortalPath = resolver.Resolve("/", PathResolver.RelativeTo.Portal);
                model.SkinPath = resolver.Resolve("/", PathResolver.RelativeTo.Skin);
                var modelDictionary = model as IDictionary<string, object>;
                liveDefinition.TemplateArguments.ForEach(a => modelDictionary.Add(a.Name, a.Value));
                htmlWriter.Write(this.RenderTemplate(liveDefinition.TemplateVirtualPath, model));
            }
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

        /// <summary>Renders a razor template into a string.</summary>
        /// <param name="virtualPath">The virtual path of the template.</param>
        /// <param name="model">The model handed to the template.</param>
        /// <returns>The writer the template rendered into.</returns>
        /// <remarks>
        /// <c>WebPageBase.CreateInstanceFromVirtualPath</c>, <c>HttpContextWrapper</c>,
        /// <c>WebPageContext</c> and <c>WebPageBase.ExecutePageHierarchy</c> are all gone with
        /// System.Web. Every DNN razor template now derives from <c>DotNetNukeWebPage</c>, so the
        /// WebPage-versus-DotNetNukeWebPage branch collapses to the single RazorEngine path:
        /// RazorEngine compiles the script and executes its page hierarchy (layouts included).
        /// The engine builds its own <c>RazorPageContext</c> from the context it resolves itself;
        /// handing it the ambient one would pass an explicit null whenever the caller runs outside
        /// a request, which is why callers render the menu during the load phase (see
        /// <c>MenuView.OnLoad</c>) while the request is still live.
        /// </remarks>
        private StringWriter RenderTemplate(string virtualPath, dynamic model)
        {
            var writer = new StringWriter();

#pragma warning disable CS0618 // Type or member is obsolete
            var razorEngine = new RazorEngine(virtualPath, null, null);
            razorEngine.Render<dynamic>(writer, model);
#pragma warning restore CS0618 // Type or member is obsolete

            return writer;
        }
    }
}
