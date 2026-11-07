// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.Skins.Aperture.Menus
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Configuration-backed implementation of <see cref="IApertureMenuProvider"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The legacy theme rendered its three menus through the DDRMenu <c>dnn:MENU</c> skin object,
    /// which walked the portal tab tree and applied the <c>NodeSelector</c> attribute. A host that
    /// owns a page tree registers its own <see cref="IApertureMenuProvider"/>; this default
    /// implementation reads a static tree from the <c>Aperture:Menu</c> configuration section so the
    /// theme is usable, and applies the same node-selector depth semantics the original skin object
    /// did.
    /// </para>
    /// <code>
    /// "Aperture": {
    ///   "Menu": [
    ///     { "Text": "Home", "Href": "/" },
    ///     { "Text": "About", "Href": "/About", "Children": [
    ///         { "Text": "Team", "Href": "/About/Team" } ] }
    ///   ]
    /// }
    /// </code>
    /// </remarks>
    public sealed partial class DefaultApertureMenuProvider : IApertureMenuProvider
    {
        private const string MenuSection = "Aperture:Menu";

        private readonly IConfiguration configuration;
        private readonly ILogger<DefaultApertureMenuProvider> logger;

        /// <summary>Initializes a new instance of the <see cref="DefaultApertureMenuProvider"/> class.</summary>
        /// <param name="configuration">The application configuration holding the <c>Aperture:Menu</c> section.</param>
        /// <param name="logger">The logger used to report an unconfigured menu instead of failing silently.</param>
        public DefaultApertureMenuProvider(IConfiguration configuration, ILogger<DefaultApertureMenuProvider> logger)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(logger);

            this.configuration = configuration;
            this.logger = logger;
        }

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<ApertureMenuNode>> GetNodesAsync(string nodeSelector, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(nodeSelector);

            var roots = ReadNodes(this.configuration.GetSection(MenuSection), 0);
            if (roots.Count == 0)
            {
                // Never return an empty menu silently: an invisible navigation bar is the single
                // most common symptom of an unmigrated menu data source.
                this.LogEmptyMenu(nodeSelector, MenuSection);
                return ValueTask.FromResult<IReadOnlyList<ApertureMenuNode>>(Array.Empty<ApertureMenuNode>());
            }

            var maxDepth = ParseMaxDepth(nodeSelector);
            Trim(roots, maxDepth);
            return ValueTask.FromResult<IReadOnlyList<ApertureMenuNode>>(roots);
        }

        /// <summary>
        /// Extracts the maximum depth from a DDRMenu node selector such as <c>*,0,2</c>.
        /// </summary>
        /// <param name="nodeSelector">The node selector value declared on the original skin object.</param>
        /// <returns>The maximum depth to include, where <c>0</c> means root level only.</returns>
        private static int ParseMaxDepth(string nodeSelector)
        {
            var parts = nodeSelector.Split(',', StringSplitOptions.TrimEntries);
            return parts.Length >= 3 && int.TryParse(parts[2], out var depth) && depth >= 0
                ? depth
                : int.MaxValue;
        }

        /// <summary>Reads a level of configured menu nodes, recursing into their children.</summary>
        /// <param name="section">The configuration section holding the nodes of this level.</param>
        /// <param name="depth">The zero-based depth of this level.</param>
        /// <returns>The nodes read from configuration.</returns>
        private static List<ApertureMenuNode> ReadNodes(IConfiguration section, int depth)
        {
            var nodes = new List<ApertureMenuNode>();
            foreach (var child in section.GetChildren())
            {
                var text = child["Text"];
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var node = new ApertureMenuNode
                {
                    Text = text,
                    Href = child["Href"] ?? string.Empty,
                    Target = child["Target"],
                    Enabled = !bool.TryParse(child["Enabled"], out var enabled) || enabled,
                    Selected = bool.TryParse(child["Selected"], out var selected) && selected,
                    Depth = depth,
                };

                foreach (var grandChild in ReadNodes(child.GetSection("Children"), depth + 1))
                {
                    node.AddChild(grandChild);
                }

                nodes.Add(node);
            }

            return nodes;
        }

        /// <summary>Removes every node deeper than the selector's maximum depth.</summary>
        /// <param name="nodes">The nodes of the level being trimmed.</param>
        /// <param name="maxDepth">The maximum depth to keep.</param>
        private static void Trim(IReadOnlyList<ApertureMenuNode> nodes, int maxDepth)
        {
            foreach (var node in nodes)
            {
                if (node.Depth >= maxDepth)
                {
                    node.ClearChildren();
                }
                else
                {
                    Trim(node.Children, maxDepth);
                }
            }
        }

        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Warning,
            Message = "The Aperture theme rendered a menu for node selector '{NodeSelector}' but no nodes are configured under '{Section}' and no host IApertureMenuProvider is registered, so the menu is empty.")]
        private partial void LogEmptyMenu(string nodeSelector, string section);
    }
}
