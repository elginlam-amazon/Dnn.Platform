// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System.Collections.Generic;

    /// <summary>
    /// A table of editor rows built by a <see cref="PropertyEditorControl"/>.
    /// </summary>
    /// <remarks>
    /// Replaces the <c>System.Web.UI.WebControls.Table</c> that <c>CreateEditor</c> filled with one
    /// <c>TableRow</c>/<c>TableCell</c> per field. Each entry of <see cref="Rows"/> is one row holding
    /// one cell holding one <see cref="FieldEditorControl"/>, which is how the Web Forms version was
    /// always built.
    /// </remarks>
    public class PropertyEditorTable
    {
        /// <summary>Gets or sets the identifier of the rendered <c>table</c>.</summary>
        public string ID { get; set; }

        /// <summary>Gets the editors, one per row.</summary>
        public IList<FieldEditorControl> Rows { get; } = new List<FieldEditorControl>();

        /// <summary>Gets or sets the collapsible group header rendered above this table, if any.</summary>
        public PropertyEditorSectionHeader Header { get; set; }
    }

    /// <summary>
    /// A container of editors built by a <see cref="PropertyEditorControl"/> in
    /// <see cref="EditorDisplayMode.Div"/> mode.
    /// </summary>
    /// <remarks>Replaces the <c>System.Web.UI.WebControls.Panel</c> that <c>CreateEditor</c> filled.</remarks>
    public class PropertyEditorPanel
    {
        /// <summary>Gets or sets the identifier of the rendered <c>div</c>.</summary>
        public string ID { get; set; }

        /// <summary>Gets or sets the css class of the rendered <c>div</c>.</summary>
        public string CssClass { get; set; }

        /// <summary>Gets or sets a value indicating whether this panel is wrapped in a <c>fieldset</c>.</summary>
        public bool WrapInFieldSet { get; set; }

        /// <summary>Gets or sets the section header rendered above this panel, if any.</summary>
        public PropertyEditorSectionHeader Header { get; set; }

        /// <summary>Gets the editors held by this panel.</summary>
        public IList<FieldEditorControl> Editors { get; } = new List<FieldEditorControl>();
    }

    /// <summary>The header of a group of editors.</summary>
    /// <remarks>
    /// Replaces the <c>Panel</c> with an <c>Image</c>, a spacer and a <c>Label</c> that
    /// <c>AddHeader</c> built for <see cref="GroupByMode.Section"/> in table mode, and the
    /// <c>h2 class="dnnFormSectionHead"</c> it built in div mode.
    /// </remarks>
    public class PropertyEditorSectionHeader
    {
        /// <summary>Gets or sets the group name.</summary>
        public string Group { get; set; }

        /// <summary>Gets or sets the displayed text.</summary>
        public string Text { get; set; }

        /// <summary>Gets or sets the localization key applied to the rendered text.</summary>
        public string ResourceKey { get; set; }

        /// <summary>Gets or sets the style applied to the rendered text.</summary>
        public Style Style { get; set; }

        /// <summary>Gets or sets a value indicating whether a rule (<c>hr</c>) follows the header.</summary>
        public bool IncludeRule { get; set; }

        /// <summary>Gets or sets a value indicating whether the header renders as the
        /// <c>h2</c>/<c>a</c> section head used in <see cref="EditorDisplayMode.Div"/> mode.</summary>
        public bool IsSectionHead { get; set; }

        /// <summary>Gets or sets the identifier of the rendered minimise/maximise icon.</summary>
        public string IconId { get; set; }
    }
}
