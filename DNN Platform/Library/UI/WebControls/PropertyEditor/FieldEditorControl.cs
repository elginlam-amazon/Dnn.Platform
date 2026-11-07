// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Globalization;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Users;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// The possible editor display modes.
    /// </summary>
    public enum EditorDisplayMode
    {
        /// <summary>
        /// Displays the control in a div.
        /// </summary>
        Div = 0,

        /// <summary>
        /// Displays the control in a table.
        /// </summary>
        Table = 1,
    }

    /// <summary>
    /// Defines how help is displayed.
    /// </summary>
    public enum HelpDisplayMode
    {
        /// <summary>
        /// Never display the help.
        /// </summary>
        Never = 0,

        /// <summary>
        /// Display help only in edit mode.
        /// </summary>
        EditOnly = 1,

        /// <summary>
        /// Always display help.
        /// </summary>
        Always = 2,
    }

    /// <summary>
    /// The FieldEditorControl control provides a Control to display Profile Properties.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>CreateEditor</c> still builds the editor for one property — it just builds an object graph
    /// (<see cref="Editor"/>, the label, the visibility control and the validators) instead of a
    /// Web Forms control tree, and <see cref="RenderContent"/> lays that graph out as a <c>div</c> or
    /// a <c>table</c> exactly as <c>BuildDiv</c>/<c>BuildTable</c> did. The editor instance that
    /// <see cref="EditControlFactory"/> produces is rendered through
    /// <see cref="PropertyEditorComponentBase.Render"/>, so it is the very instance whose
    /// <see cref="EditControl.ValueChanged"/> event this control subscribes to.
    /// </para>
    /// <para>
    /// The <c>RequiredFieldValidator</c>/<c>RegularExpressionValidator</c> instances became
    /// <see cref="PropertyEditorValidator"/> instances, which enforce the same two rules and render
    /// the same <c>dnnFormMessage dnnFormError</c> message.
    /// </para>
    /// </remarks>
    public class FieldEditorControl : PropertyEditorComponentBase
    {
        private readonly IServiceProvider serviceProvider;
        private readonly List<IValidator> validators = new List<IValidator>();
        private IEditorInfoAdapter editorInfoAdapter;
        private bool isValid = true;
        private StandardEditorInfoAdapter stdAdapter;
        private bool validated;
        private EditorInfo editInfo;
        private PropertyLabelControl propLabel;
        private VisibilityControl visibilityControl;

        /// <summary>Initializes a new instance of the <see cref="FieldEditorControl"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.0.0. Please use overload with IServiceProvider. Scheduled removal in v12.0.0.")]
        public FieldEditorControl()
            : this(Globals.GetCurrentServiceProvider())
        {
        }

        /// <summary>Initializes a new instance of the <see cref="FieldEditorControl"/> class.</summary>
        /// <param name="serviceProvider">The DI container.</param>
        public FieldEditorControl(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
            this.ValidationExpression = Null.NullString;
            this.ShowRequired = true;
            this.LabelMode = LabelMode.None;
            this.EditorTypeName = Null.NullString;
            this.EditorDisplayMode = EditorDisplayMode.Div;
            this.HelpDisplayMode = HelpDisplayMode.Always;
        }

        /// <summary>
        /// Occurs when an item is added.
        /// </summary>
        public event PropertyChangedEventHandler ItemAdded;

        /// <summary>
        /// Occurs when an item was changed.
        /// </summary>
        public event PropertyChangedEventHandler ItemChanged;

        /// <summary>
        /// Occurs when an item was created.
        /// </summary>
        public event EditorCreatedEventHandler ItemCreated;

        /// <summary>
        /// Occurs when an item was deleted.
        /// </summary>
        public event PropertyChangedEventHandler ItemDeleted;

        /// <summary>Gets a value indicating whether all of the properties are Valid.</summary>
        public bool IsValid
        {
            get
            {
                if (!this.validated)
                {
                    this.Validate();
                }

                return this.isValid;
            }
        }

        /// <summary>Gets or sets the DataSource that is bound to this control.</summary>
        [Browsable(false)]
        [Parameter]
        public object DataSource { get; set; }

        /// <summary>Gets or sets the value of the Field/property that this control displays.</summary>
        /// <value>A string representing the Name of the Field.</value>
        [Parameter]
        public string DataField { get; set; }

        /// <summary>Gets or sets whether the control uses Divs or Tables.</summary>
        [Parameter]
        public EditorDisplayMode EditorDisplayMode { get; set; }

        /// <summary>Gets or sets the Edit Mode of the Editor.</summary>
        /// <value>The mode of the editor.</value>
        [Parameter]
        public PropertyEditorMode EditMode { get; set; }

        /// <summary>Gets the Edit Control associated with the Editor.</summary>
        public EditControl Editor { get; private set; }

        /// <summary>Gets or sets the Factory used to create the Control.</summary>
        /// <value>The mode of the editor.</value>
        [Parameter]
        public IEditorInfoAdapter EditorInfoAdapter
        {
            get
            {
                if (this.editorInfoAdapter == null)
                {
                    if (this.stdAdapter == null)
                    {
                        this.stdAdapter = new StandardEditorInfoAdapter(this.DataSource, this.DataField);
                    }

                    return this.stdAdapter;
                }
                else
                {
                    return this.editorInfoAdapter;
                }
            }

            set
            {
                this.editorInfoAdapter = value;
            }
        }

        /// <summary>Gets or sets the Editor Type to use.</summary>
        /// <value>The typename of the editor.</value>
        [Parameter]
        public string EditorTypeName { get; set; }

        /// <summary>Gets or sets a value indicating whether the Validators should use client-side validation.</summary>
        [Parameter]
        public bool EnableClientValidation { get; set; }

        /// <summary>Gets or sets whether the control displays Help.</summary>
        [Parameter]
        public HelpDisplayMode HelpDisplayMode { get; set; }

        /// <summary>Gets a value indicating whether any of the properties have been changed.</summary>
        public bool IsDirty { get; private set; }

        /// <summary>
        /// Gets or sets the label mode.
        /// </summary>
        [Parameter]
        public LabelMode LabelMode { get; set; }

        /// <summary>Gets or sets the Local Resource File for the Control.</summary>
        [Parameter]
        public string LocalResourceFile { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the field is required.
        /// </summary>
        [Parameter]
        public bool Required { get; set; }

        /// <summary>Gets or sets the Url of the Required Image.</summary>
        [Parameter]
        public string RequiredUrl { get; set; }

        /// <summary>Gets or sets a value indicating whether the Required icon is used.</summary>
        [Parameter]
        public bool ShowRequired { get; set; }

        /// <summary>Gets or sets a value indicating whether the Visibility control is used.</summary>
        [Parameter]
        public bool ShowVisibility { get; set; }

        /// <summary>
        /// Gets or sets the user.
        /// </summary>
        [Parameter]
        public UserInfo User { get; set; }

        /// <summary>
        /// Gets or sets the validation expression.
        /// </summary>
        [Parameter]
        public string ValidationExpression { get; set; }

        /// <summary>Gets the value of the Edit Control Style.</summary>
        [Browsable(true)]
        [Category("Styles")]
        [Description("Set the Style for the Edit Control.")]
        public Style EditControlStyle { get; } = new Style();

        /// <summary>Gets or sets the width of the Edit Control Column.</summary>
        [Browsable(true)]
        [Category("Appearance")]
        [Description("Set the Width for the Edit Control.")]
        public Unit EditControlWidth { get; set; }

        /// <summary>Gets the value of the Error Style.</summary>
        [Browsable(true)]
        [Category("Styles")]
        [Description("Set the Style for the Error Text.")]
        public Style ErrorStyle { get; } = new Style();

        /// <summary>Gets the value of the Help Style.</summary>
        [Browsable(true)]
        [Category("Styles")]
        [Description("Set the Style for the Help Text.")]
        public Style HelpStyle { get; } = new Style();

        /// <summary>Gets the value of the Label Style.</summary>
        [Browsable(true)]
        [Category("Styles")]
        [Description("Set the Style for the Label Text")]
        public Style LabelStyle { get; } = new Style();

        /// <summary>Gets or sets the width of the Label Column.</summary>
        [Browsable(true)]
        [Category("Appearance")]
        [Description("Set the Width for the Label Control.")]
        public Unit LabelWidth { get; set; }

        /// <summary>Gets the value of the Visibility Style.</summary>
        [Browsable(true)]
        [Category("Styles")]
        [Description("Set the Style for the Visibility Control")]
        public Style VisibilityStyle { get; } = new Style();

        /// <summary>Binds the controls to the DataSource.</summary>
        public override void DataBind()
        {
            // Invoke OnDataBinding so DataBinding Event is raised
            this.OnDataBinding(EventArgs.Empty);

            // Create the editor
            this.CreateEditor();
        }

        /// <summary>Validates the data, and sets the IsValid Property.</summary>
        public virtual void Validate()
        {
            this.isValid = this.Editor.IsValid;

            if (this.isValid)
            {
                IEnumerator valEnumerator = this.validators.GetEnumerator();
                while (valEnumerator.MoveNext())
                {
                    var validator = (IValidator)valEnumerator.Current;
                    validator.Validate();
                    if (!validator.IsValid)
                    {
                        this.isValid = false;
                        break;
                    }
                }

                this.validated = true;
            }
        }

        /// <summary>CreateEditor creates the control collection for this control.</summary>
        protected virtual void CreateEditor()
        {
            EditorInfo editInfo = this.EditorInfoAdapter.CreateEditControl();

            this.ID = editInfo.Name;

            if (editInfo != null)
            {
                editInfo.User = this.User;

                if (editInfo.EditMode == PropertyEditorMode.Edit)
                {
                    editInfo.EditMode = this.EditMode;
                }

                // Get the Editor Type to use (if specified)
                if (!string.IsNullOrEmpty(this.EditorTypeName))
                {
                    editInfo.Editor = this.EditorTypeName;
                }

                // Get the Label Mode to use (if specified)
                if (this.LabelMode != LabelMode.Left)
                {
                    editInfo.LabelMode = this.LabelMode;
                }

                // if Required is specified set editors property
                if (this.Required)
                {
                    editInfo.Required = this.Required;
                }

                // Get the ValidationExpression to use (if specified)
                if (!string.IsNullOrEmpty(this.ValidationExpression))
                {
                    editInfo.ValidationExpression = this.ValidationExpression;
                }

                // Raise the ItemCreated Event
                this.OnItemCreated(new PropertyEditorItemEventArgs(editInfo));

                this.Visible = editInfo.Visible;

                this.editInfo = editInfo;
                this.BuildEditorParts(editInfo);
            }
        }

        /// <summary>
        /// Runs when an item is added to a collection type property.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event arguments <see cref="PropertyEditorEventArgs"/>.</param>
        protected virtual void CollectionItemAdded(object sender, PropertyEditorEventArgs e)
        {
            this.OnItemAdded(e);
        }

        /// <summary>
        /// Runs when an item is removed from a collection type property.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event arguments <see cref="PropertyEditorEventArgs"/>.</param>
        protected virtual void CollectionItemDeleted(object sender, PropertyEditorEventArgs e)
        {
            this.OnItemDeleted(e);
        }

        /// <summary>
        /// Runs when an item is added to a collection type property.
        /// </summary>
        /// <param name="e">The event arguments <see cref="PropertyEditorEventArgs"/>.</param>
        protected virtual void OnItemAdded(PropertyEditorEventArgs e)
        {
            this.ItemAdded?.Invoke(this, e);
        }

        /// <summary>
        /// Runs when the Editor is Created.
        /// </summary>
        /// <param name="e">The event arguments <see cref="PropertyEditorItemEventArgs"/>.</param>
        protected virtual void OnItemCreated(PropertyEditorItemEventArgs e)
        {
            this.ItemCreated?.Invoke(this, e);
        }

        /// <summary>
        /// Runs when an item is removed from a collection type property.
        /// </summary>
        /// <param name="e">The event arguments <see cref="PropertyEditorEventArgs"/>.</param>
        protected virtual void OnItemDeleted(PropertyEditorEventArgs e)
        {
            this.ItemDeleted?.Invoke(this, e);
        }

        /// <inheritdoc />
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);

            if (string.IsNullOrEmpty(this.CssClass))
            {
                this.CssClass = "dnnFormItem";
            }
        }

        /// <summary>
        /// Runs when the Value of a Property changes.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event arguments <see cref="PropertyEditorEventArgs"/>.</param>
        protected virtual void ValueChanged(object sender, PropertyEditorEventArgs e)
        {
            this.IsDirty = this.EditorInfoAdapter.UpdateValue(e);
        }

        /// <summary>
        /// Runs when the Visibility of a Property changes.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event arguments <see cref="PropertyEditorEventArgs"/>.</param>
        protected virtual void VisibilityChanged(object sender, PropertyEditorEventArgs e)
        {
            this.IsDirty = this.EditorInfoAdapter.UpdateVisibility(e);
        }

        /// <summary>
        /// Runs when an Item in the List Is Changed.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event arguments <see cref="PropertyEditorEventArgs"/>.</param>
        /// <remarks>Raises an ItemChanged event.</remarks>
        protected virtual void ListItemChanged(object sender, PropertyEditorEventArgs e)
        {
            this.ItemChanged?.Invoke(this, e);
        }

        /// <inheritdoc />
        protected override void RenderContent(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            if (this.editInfo == null)
            {
                // The editor is created by DataBind(); create it on demand so that the control also
                // renders when it is used declaratively as a Blazor component.
                this.DataBind();
                if (this.editInfo == null || !this.Visible)
                {
                    return;
                }
            }

            builder.OpenElement(0, "div");
            this.AddStyleAttributes(builder, 1);

            if (this.EditorDisplayMode == EditorDisplayMode.Div)
            {
                this.RenderDiv(builder, 10, this.editInfo);
            }
            else
            {
                this.RenderTable(builder, 10, this.editInfo);
            }

            builder.CloseElement();
        }

        /// <summary>Builds the editor, label, visibility control and validators of this field.</summary>
        /// <param name="editInfo">The EditorInfo object for this control.</param>
        /// <remarks>Replaces the object construction half of <c>BuildDiv</c>/<c>BuildTable</c>.</remarks>
        private void BuildEditorParts(EditorInfo editInfo)
        {
            var propEditor = this.BuildEditor(editInfo);
            this.visibilityControl = this.BuildVisibility(editInfo);

            this.propLabel = editInfo.LabelMode != LabelMode.None ? this.BuildLabel(editInfo) : null;
            if (this.propLabel != null)
            {
                this.propLabel.EditControl = propEditor;

                var strValue = editInfo.Value as string;
                if (this.ShowRequired && editInfo.Required && (editInfo.EditMode == PropertyEditorMode.Edit || (editInfo.Required && string.IsNullOrEmpty(strValue))))
                {
                    this.propLabel.Required = true;
                }
            }

            this.BuildValidators(editInfo, propEditor.ID);
        }

        /// <summary>Renders the control as a Div.</summary>
        private void RenderDiv(RenderTreeBuilder builder, int sequence, EditorInfo editInfo)
        {
            builder.OpenRegion(sequence);

            if (editInfo.LabelMode == LabelMode.Left || editInfo.LabelMode == LabelMode.Top)
            {
                this.RenderLabel(builder, 0);
                builder.AddContent(1, this.Editor.Render());
                this.RenderVisibility(builder, 2);
            }
            else
            {
                builder.AddContent(3, this.Editor.Render());
                this.RenderVisibility(builder, 4);
                this.RenderLabel(builder, 5);
            }

            // Render the Validators
            this.RenderValidators(builder, 6, true);

            builder.CloseRegion();
        }

        /// <summary>Renders the control as a Table.</summary>
        private void RenderTable(RenderTreeBuilder builder, int sequence, EditorInfo editInfo)
        {
            builder.OpenRegion(sequence);

            builder.OpenElement(0, "table");

            var labelFirst = editInfo.LabelMode == LabelMode.Left;
            var sideBySide = editInfo.LabelMode == LabelMode.Left || editInfo.LabelMode == LabelMode.Right;
            var fullWidth = !sideBySide;

            if (fullWidth)
            {
                if (editInfo.LabelMode == LabelMode.Top)
                {
                    this.RenderLabelRow(builder, 1, 2);
                }

                this.RenderEditorRow(builder, 2, 2);

                if (editInfo.LabelMode == LabelMode.Bottom)
                {
                    this.RenderLabelRow(builder, 3, 2);
                }
            }
            else
            {
                builder.OpenElement(4, "tr");
                if (labelFirst)
                {
                    this.RenderLabelCell(builder, 5, 0);
                    this.RenderEditorCell(builder, 6, 0);
                }
                else
                {
                    this.RenderEditorCell(builder, 7, 0);
                    this.RenderLabelCell(builder, 8, 0);
                }

                builder.CloseElement();
            }

            // Build the Validators row
            builder.OpenElement(9, "tr");
            builder.OpenElement(10, "td");
            builder.AddAttribute(11, "colspan", "2");
            this.RenderValidators(builder, 12, false);
            builder.CloseElement();
            builder.CloseElement();

            builder.CloseElement();

            builder.CloseRegion();
        }

        private void RenderLabelRow(RenderTreeBuilder builder, int sequence, int columnSpan)
        {
            builder.OpenElement(sequence, "tr");
            this.RenderLabelCell(builder, sequence + 1, columnSpan);
            builder.CloseElement();
        }

        private void RenderEditorRow(RenderTreeBuilder builder, int sequence, int columnSpan)
        {
            builder.OpenElement(sequence, "tr");
            this.RenderEditorCell(builder, sequence + 1, columnSpan);
            builder.CloseElement();
        }

        private void RenderLabelCell(RenderTreeBuilder builder, int sequence, int columnSpan)
        {
            builder.OpenElement(sequence, "td");
            builder.AddAttribute(sequence + 1, "valign", "top");
            if (columnSpan > 0)
            {
                builder.AddAttribute(sequence + 2, "colspan", columnSpan.ToString(CultureInfo.InvariantCulture));
            }
            else if (!this.LabelWidth.IsEmpty)
            {
                builder.AddAttribute(sequence + 3, "style", $"width:{this.LabelWidth.ToString(CultureInfo.InvariantCulture)};");
            }

            this.RenderLabel(builder, sequence + 4);
            builder.CloseElement();
        }

        private void RenderEditorCell(RenderTreeBuilder builder, int sequence, int columnSpan)
        {
            builder.OpenElement(sequence, "td");
            builder.AddAttribute(sequence + 1, "valign", "top");
            if (columnSpan > 0)
            {
                builder.AddAttribute(sequence + 2, "colspan", columnSpan.ToString(CultureInfo.InvariantCulture));
            }
            else if (!this.EditControlWidth.IsEmpty)
            {
                builder.AddAttribute(sequence + 3, "style", $"width:{this.EditControlWidth.ToString(CultureInfo.InvariantCulture)};");
            }

            builder.AddContent(sequence + 4, this.Editor.Render());
            this.RenderRequiredIcon(builder, sequence + 5, this.editInfo);

            if (this.visibilityControl != null)
            {
                builder.AddMarkupContent(sequence + 6, "&nbsp;&nbsp;");
                this.RenderVisibility(builder, sequence + 7);
            }

            builder.CloseElement();
        }

        private void RenderLabel(RenderTreeBuilder builder, int sequence)
        {
            if (this.propLabel != null)
            {
                builder.AddContent(sequence, this.propLabel.Render());
            }
        }

        private void RenderVisibility(RenderTreeBuilder builder, int sequence)
        {
            if (this.visibilityControl != null)
            {
                builder.AddContent(sequence, this.visibilityControl.Render());
            }
        }

        private void RenderValidators(RenderTreeBuilder builder, int sequence, bool applyWidth)
        {
            builder.OpenRegion(sequence);
            foreach (var validator in this.validators)
            {
                if (validator is PropertyEditorValidator propertyValidator)
                {
                    if (applyWidth)
                    {
                        propertyValidator.Width = this.Width;
                    }

                    propertyValidator.RenderContent(builder, 0);
                }
            }

            builder.CloseRegion();
        }

        /// <summary>BuildEditor creates the editor part of the Control.</summary>
        /// <param name="editInfo">The EditorInfo object for this control.</param>
        private EditControl BuildEditor(EditorInfo editInfo)
        {
            var propEditor = EditControlFactory.CreateEditControl(this.serviceProvider, editInfo);
            propEditor.ControlStyle.CopyFrom(this.EditControlStyle);
            propEditor.LocalResourceFile = this.LocalResourceFile;
            propEditor.User = this.User;
            if (editInfo.ControlStyle != null)
            {
                propEditor.ControlStyle.CopyFrom(editInfo.ControlStyle);
            }

            propEditor.ItemAdded += this.CollectionItemAdded;
            propEditor.ItemDeleted += this.CollectionItemDeleted;
            propEditor.ValueChanged += this.ValueChanged;
            if (propEditor is DNNListEditControl listEditor)
            {
                listEditor.ItemChanged += this.ListItemChanged;
            }

            this.Editor = propEditor;

            return propEditor;
        }

        /// <summary>BuildLabel creates the label part of the Control.</summary>
        /// <param name="editInfo">The EditorInfo object for this control.</param>
        private PropertyLabelControl BuildLabel(EditorInfo editInfo)
        {
            var propLabel = this.serviceProvider == null
                ? new PropertyLabelControl(null, null, null)
                : ActivatorUtilities.GetServiceOrCreateInstance<PropertyLabelControl>(this.serviceProvider);
            propLabel.ID = editInfo.Name + "_Label";
            propLabel.HelpStyle.CopyFrom(this.HelpStyle);
            propLabel.LabelStyle.CopyFrom(this.LabelStyle);
            var strValue = editInfo.Value as string;
            switch (this.HelpDisplayMode)
            {
                case HelpDisplayMode.Always:
                    propLabel.ShowHelp = true;
                    break;
                case HelpDisplayMode.EditOnly:
                    if (editInfo.EditMode == PropertyEditorMode.Edit || (editInfo.Required && string.IsNullOrEmpty(strValue)))
                    {
                        propLabel.ShowHelp = true;
                    }
                    else
                    {
                        propLabel.ShowHelp = false;
                    }

                    break;
                case HelpDisplayMode.Never:
                    propLabel.ShowHelp = false;
                    break;
            }

            propLabel.Caption = editInfo.Name;
            propLabel.HelpText = editInfo.Name;
            propLabel.ResourceKey = editInfo.ResourceKey;
            if (editInfo.LabelMode == LabelMode.Left || editInfo.LabelMode == LabelMode.Right)
            {
                propLabel.Width = this.LabelWidth;
            }

            return propLabel;
        }

        /// <summary>Renders the required icon of the Control.</summary>
        /// <param name="builder">The render tree builder to write to.</param>
        /// <param name="sequence">The render tree sequence number to use.</param>
        /// <param name="editInfo">The EditorInfo object for this control.</param>
        /// <remarks>Replaces <c>BuildRequiredIcon</c>, which returned an <c>Image</c> control.</remarks>
        private void RenderRequiredIcon(RenderTreeBuilder builder, int sequence, EditorInfo editInfo)
        {
            var strValue = editInfo.Value as string;
            if (!(this.ShowRequired && editInfo.Required && (editInfo.EditMode == PropertyEditorMode.Edit || (editInfo.Required && string.IsNullOrEmpty(strValue)))))
            {
                return;
            }

            builder.OpenElement(sequence, "img");
            builder.AddAttribute(
                sequence + 1,
                "src",
                string.IsNullOrEmpty(this.RequiredUrl) || this.RequiredUrl == Null.NullString
                    ? Globals.ResolveUrl("~/images/required.gif")
                    : this.RequiredUrl);
            builder.AddAttribute(sequence + 2, "alt", string.Empty);
            builder.AddAttribute(sequence + 3, "resourcekey", editInfo.ResourceKey + ".Required");
            builder.CloseElement();
        }

        /// <summary>BuildValidators creates the validators part of the Control.</summary>
        /// <param name="editInfo">The EditorInfo object for this control.</param>
        /// <param name="targetId">Target Control Id.</param>
        private void BuildValidators(EditorInfo editInfo, string targetId)
        {
            this.validators.Clear();

            // Add Required Validators
            if (editInfo.Required)
            {
                var reqValidator = new PropertyEditorValidator(PropertyEditorValidatorType.Required, this.GetEditorValue)
                {
                    ID = editInfo.Name + "_Req",
                    ControlToValidate = targetId,
                    ResourceKey = editInfo.ResourceKey + ".Required",
                    ErrorMessage = editInfo.Name + " is Required",
                };
                reqValidator.ControlStyle.CopyFrom(this.ErrorStyle);
                if (!string.IsNullOrEmpty(this.ErrorStyle.CssClass))
                {
                    reqValidator.CssClass = this.ErrorStyle.CssClass;
                }

                this.validators.Add(reqValidator);
            }

            // Add Regular Expression Validators
            if (!string.IsNullOrEmpty(editInfo.ValidationExpression))
            {
                var regExValidator = new PropertyEditorValidator(PropertyEditorValidatorType.RegularExpression, this.GetEditorValue)
                {
                    ID = editInfo.Name + "_RegEx",
                    ControlToValidate = targetId,
                    ValidationExpression = editInfo.ValidationExpression,
                    ResourceKey = editInfo.ResourceKey + ".Validation",
                    ErrorMessage = editInfo.Name + " is Invalid",
                };
                regExValidator.ControlStyle.CopyFrom(this.ErrorStyle);
                if (!string.IsNullOrEmpty(this.ErrorStyle.CssClass))
                {
                    regExValidator.CssClass = this.ErrorStyle.CssClass;
                }

                this.validators.Add(regExValidator);
            }
        }

        /// <summary>Reads the current editor value for the validators.</summary>
        /// <returns>The value being validated.</returns>
        /// <remarks>Replaces <c>BaseValidator.ControlToValidate</c>, which resolved the control from
        /// the control tree and read its <c>ValidationPropertyAttribute</c> property
        /// (<c>EditControl.Value</c>).</remarks>
        private string GetEditorValue()
        {
            return Convert.ToString(this.Editor?.Value, CultureInfo.InvariantCulture);
        }

        /// <summary>BuildVisibility creates the visibility part of the Control.</summary>
        /// <param name="editInfo">The EditorInfo object for this control.</param>
        private VisibilityControl BuildVisibility(EditorInfo editInfo)
        {
            VisibilityControl visControl = null;

            if (this.ShowVisibility)
            {
                visControl = this.serviceProvider == null
                    ? new VisibilityControl(null, null)
                    : ActivatorUtilities.GetServiceOrCreateInstance<VisibilityControl>(this.serviceProvider);
                visControl.ID = "_visibility";
                visControl.Name = editInfo.Name;
                visControl.User = this.User;
                visControl.Value = editInfo.ProfileVisibility;
                visControl.ControlStyle.CopyFrom(this.VisibilityStyle);
                visControl.VisibilityChanged += this.VisibilityChanged;
            }

            return visControl;
        }
    }
}
