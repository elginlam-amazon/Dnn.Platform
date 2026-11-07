// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Text;

    using DotNetNuke.UI.Utilities;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>
    /// A DNN skinning component that owns an ordered, dynamically built collection of child
    /// components.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the ASP.NET Core replacement for <c>System.Web.UI.Control</c> as DNN's skinning
    /// engine used it: a node with an <c>ID</c>, an attribute bag, a <c>Visible</c> flag and a
    /// <c>Controls</c> collection that the engine appends to at initialization time
    /// (<c>Controls.Add</c>/<c>Controls.AddAt</c>). Blazor has no server-side control tree, so the
    /// tree is modelled explicitly here and rendered in order by
    /// <see cref="DnnComponentBase.BuildRenderTree"/>.
    /// </para>
    /// <para>
    /// It extends <see cref="IClientApiControl"/> so that everything in the skinning engine can be
    /// handed to the Client API (<c>DNNClientAPI</c>, <c>ClientAPI</c>) without a second adapter.
    /// </para>
    /// </remarks>
    public interface IDnnControl : IClientApiControl, IComponent
    {
        /// <summary>Gets or sets a value indicating whether this control renders.</summary>
        /// <remarks>Replaces <c>Control.Visible</c>; a non-visible control emits no markup.</remarks>
        bool Visible { get; set; }

        /// <summary>Gets the inline style declarations rendered onto this control's markup.</summary>
        /// <remarks>Replaces <c>HtmlControl.Style</c>/<c>WebControl.Style</c>.</remarks>
        IDictionary<string, string> Style { get; }

        /// <summary>Gets the ordered child controls of this control.</summary>
        /// <remarks>Replaces <c>Control.Controls</c>.</remarks>
        DnnControlCollection Controls { get; }

        /// <summary>Gets a value indicating whether this control has child controls.</summary>
        /// <returns><see langword="true"/> when there is at least one child.</returns>
        /// <remarks>Replaces <c>Control.HasControls()</c>.</remarks>
        bool HasControls();
    }

    /// <summary>
    /// The live, ordered child collection of an <see cref="IDnnControl"/>, which keeps the
    /// <see cref="IClientApiControl.Parent"/> of the controls added to it up to date.
    /// </summary>
    /// <remarks>Replaces <c>System.Web.UI.ControlCollection</c>.</remarks>
    public sealed class DnnControlCollection : Collection<IClientApiControl>
    {
        private readonly DnnComponentBase owner;

        /// <summary>Initializes a new instance of the <see cref="DnnControlCollection"/> class.</summary>
        /// <param name="owner">The control owning this collection.</param>
        internal DnnControlCollection(DnnComponentBase owner)
        {
            this.owner = owner;
        }

        /// <summary>Inserts a control at the given position, mirroring <c>ControlCollection.AddAt</c>.</summary>
        /// <param name="index">The position to insert at.</param>
        /// <param name="control">The control to insert.</param>
        public void AddAt(int index, IClientApiControl control)
        {
            if (control == null)
            {
                return;
            }

            this.Insert(Math.Min(index, this.Count), control);
        }

        /// <inheritdoc />
        protected override void InsertItem(int index, IClientApiControl item)
        {
            base.InsertItem(index, item);
            this.Adopt(item);
        }

        /// <inheritdoc />
        protected override void SetItem(int index, IClientApiControl item)
        {
            base.SetItem(index, item);
            this.Adopt(item);
        }

        private void Adopt(IClientApiControl item)
        {
            if (item is DnnComponentBase child)
            {
                child.SetParent(this.owner);
            }

            this.owner?.NotifyTreeChanged();
        }
    }

    /// <summary>
    /// The base component of DNN's skinning engine: a Blazor component that also exposes the
    /// control-tree surface (<c>ID</c>, <c>Visible</c>, <c>Attributes</c>, <c>Style</c>,
    /// <c>Controls</c>, <c>FindControl</c>) the engine is written against.
    /// </summary>
    /// <remarks>
    /// Derived components either render declaratively (a <c>.razor</c> file overriding
    /// <see cref="BuildRenderTree"/>) or let this base render an optional wrapper element named by
    /// <see cref="TagName"/> around <see cref="ChildContent"/> and <see cref="Controls"/>.
    /// </remarks>
    public abstract class DnnComponentBase : ComponentBase, IDnnControl, IDisposable
    {
        private readonly Dictionary<string, string> attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> style = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly DnnControlCollection controls;
        private Microsoft.AspNetCore.Http.HttpRequest capturedRequest;
        private bool childControlsCreated;
        private bool disposed;

        /// <summary>Initializes a new instance of the <see cref="DnnComponentBase"/> class.</summary>
        protected DnnComponentBase()
        {
            this.controls = new DnnControlCollection(this);
        }

        /// <inheritdoc />
        public event EventHandler PreRender;

        /// <summary>Gets or sets the server side identifier of this control.</summary>
        /// <remarks>
        /// Preserved from <c>Control.ID</c> because DNN's skinning engine and third party themes
        /// assign and read it (<c>container.ID = "ctr" + moduleId</c>, <c>skin.ID = "dnn"</c>).
        /// </remarks>
        [Parameter]
        public string ID { get; set; }

        /// <inheritdoc />
        [Parameter]
        public bool Visible { get; set; } = true;

        /// <summary>Gets or sets the CSS class rendered onto this control's markup.</summary>
        /// <remarks>Replaces <c>WebControl.CssClass</c>.</remarks>
        [Parameter]
        public string CssClass
        {
            get => this.Attributes.TryGetValue("class", out var cssClass) ? cssClass : null;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    this.Attributes.Remove("class");
                }
                else
                {
                    this.Attributes["class"] = value;
                }
            }
        }

        /// <summary>Gets or sets the content declared inside this component in markup.</summary>
        [Parameter]
        public RenderFragment ChildContent { get; set; }

        /// <inheritdoc />
        string IClientApiControl.Id
        {
            get => this.ID;
            set => this.ID = value;
        }

        /// <inheritdoc />
        public virtual string ClientId =>
            this.NamingContainer is null ? this.ID : this.NamingContainer.ClientId + "_" + this.ID;

        /// <inheritdoc />
        public virtual string UniqueId =>
            this.NamingContainer is null ? this.ID : this.NamingContainer.UniqueId + "$" + this.ID;

        /// <inheritdoc />
        public IDictionary<string, string> Attributes => this.attributes;

        /// <inheritdoc />
        public IDictionary<string, string> Style => this.style;

        /// <inheritdoc />
        public IClientApiControl Parent { get; private set; }

        /// <inheritdoc />
        public virtual IClientApiControl NamingContainer => this.Parent;

        /// <inheritdoc />
        public DnnControlCollection Controls => this.controls;

        /// <inheritdoc />
        public IList<IClientApiControl> Children => this.controls;

        /// <inheritdoc />
        public bool HasChildren => this.controls.Count > 0;

        /// <inheritdoc />
        public virtual IClientApiPage Page => this.Parent?.Page;

        /// <summary>
        /// Gets or sets the markup source this control was loaded from, e.g.
        /// <c>Portals/_default/Skins/Xcillion/Index.ascx</c>.
        /// </summary>
        /// <remarks>
        /// Assigned by <see cref="ControlUtilities.LoadControl{T}"/>. It carries the information
        /// <c>Control.TemplateSourceDirectory</c> used to carry, which DNN needs in order to locate
        /// a control's stylesheet and its <c>App_LocalResources</c> file.
        /// </remarks>
        [Parameter]
        public string ControlSrc { get; set; }

        /// <summary>
        /// Gets the name of the HTML element wrapping this control's content, or
        /// <see langword="null"/> when the content is rendered without a wrapper.
        /// </summary>
        /// <remarks>Replaces <c>HtmlControl.TagName</c>.</remarks>
        protected virtual string TagName => null;

        /// <summary>Gets the virtual directory of the markup this control was loaded from.</summary>
        /// <remarks>Replaces <c>Control.TemplateSourceDirectory</c>.</remarks>
        public string TemplateSourceDirectory
        {
            get
            {
                var source = this.ControlSrc;
                if (string.IsNullOrEmpty(source))
                {
                    return string.Empty;
                }

                var normalized = source.Replace('\\', '/').TrimEnd('/');
                var lastSlash = normalized.LastIndexOf('/');
                return lastSlash <= 0 ? string.Empty : normalized.Substring(0, lastSlash);
            }
        }

        /// <inheritdoc />
        public bool HasControls() => this.controls.Count > 0;

        /// <inheritdoc />
        public virtual IClientApiControl FindControl(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            foreach (var child in this.controls)
            {
                if (string.Equals(child.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }

            // like WebForms, the search continues into children that do not open a naming scope.
            foreach (var child in this.controls)
            {
                if (child is IClientApiPage)
                {
                    continue;
                }

                var found = child.FindControl(id);
                if (found is not null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>Requests a re-render of this control, e.g. after its child tree changed.</summary>
        /// <remarks>
        /// Replaces the WebForms behaviour where appending to <c>Controls</c> during the page
        /// lifecycle was enough for the new child to be rendered.
        /// </remarks>
        public void NotifyTreeChanged()
        {
            if (!this.disposed)
            {
                this.InvokeStateChange();
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>Makes this control a child of the given control.</summary>
        /// <param name="parent">The new parent.</param>
        internal void SetParent(IClientApiControl parent)
        {
            this.Parent = parent;
        }

        /// <summary>Builds this control's child controls once.</summary>
        /// <remarks>Replaces <c>Control.EnsureChildControls()</c>.</remarks>
        protected void EnsureChildControls()
        {
            if (this.childControlsCreated)
            {
                return;
            }

            this.childControlsCreated = true;
            this.CreateChildControls();
        }

        /// <summary>Builds this control's child controls.</summary>
        /// <remarks>Replaces <c>Control.CreateChildControls()</c>.</remarks>
        protected virtual void CreateChildControls()
        {
        }

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            this.EnsureChildControls();

            if (!this.Visible)
            {
                return;
            }

            var tagName = this.TagName;
            var sequence = 0;

            if (!string.IsNullOrEmpty(tagName))
            {
                builder.OpenElement(sequence++, tagName);

                if (!string.IsNullOrEmpty(this.ID))
                {
                    builder.AddAttribute(sequence++, "id", this.ClientId);
                }

                foreach (var attribute in this.attributes)
                {
                    builder.AddAttribute(sequence++, attribute.Key, attribute.Value);
                }

                var inlineStyle = this.BuildStyleAttribute();
                if (!string.IsNullOrEmpty(inlineStyle))
                {
                    builder.AddAttribute(sequence++, "style", inlineStyle);
                }
            }

            this.RenderContents(builder, ref sequence);

            if (!string.IsNullOrEmpty(tagName))
            {
                builder.CloseElement();
            }
        }

        /// <summary>Renders the content of this control (its declared content and its children).</summary>
        /// <param name="builder">The render tree builder.</param>
        /// <param name="sequence">The current render tree sequence number.</param>
        /// <remarks>Replaces <c>Control.RenderContents(HtmlTextWriter)</c>.</remarks>
        protected virtual void RenderContents(RenderTreeBuilder builder, ref int sequence)
        {
            if (this.ChildContent != null)
            {
                builder.AddContent(sequence++, this.ChildContent);
            }

            this.RenderChildControls(builder, ref sequence);
        }

        /// <summary>Renders the <see cref="Controls"/> collection in order.</summary>
        /// <param name="builder">The render tree builder.</param>
        /// <param name="sequence">The current render tree sequence number.</param>
        protected void RenderChildControls(RenderTreeBuilder builder, ref int sequence)
        {
            foreach (var child in this.controls.ToList())
            {
                RenderControl(builder, sequence++, child);
            }
        }

        /// <summary>Renders a single child control into the render tree.</summary>
        /// <param name="builder">The render tree builder.</param>
        /// <param name="sequence">The render tree sequence number to use.</param>
        /// <param name="control">The control to render.</param>
        /// <remarks>
        /// Replaces <c>Control.RenderControl(HtmlTextWriter)</c>. A control that is a Blazor
        /// component is rendered as a component of its own type so that its lifecycle runs; the
        /// parameters the skinning engine assigned to the instance are carried over by
        /// <see cref="DnnComponentParameters.Capture"/>.
        /// </remarks>
        protected static void RenderControl(RenderTreeBuilder builder, int sequence, IClientApiControl control)
        {
            if (control == null)
            {
                return;
            }

            if (control is IDnnControl dnnControl && !dnnControl.Visible)
            {
                return;
            }

            builder.OpenRegion(sequence);
            builder.OpenComponent(0, control.GetType());
            var parameters = DnnComponentParameters.Capture(control);
            if (parameters.Count > 0)
            {
                builder.AddMultipleAttributes(1, parameters);
            }

            builder.CloseComponent();
            builder.CloseRegion();
        }

        /// <summary>Releases the resources held by this control and its children.</summary>
        /// <param name="disposing"><see langword="true"/> when called from <see cref="Dispose()"/>.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                this.OnUnload(EventArgs.Empty);

                foreach (var child in this.controls.OfType<IDisposable>().ToList())
                {
                    child.Dispose();
                }

                this.controls.Clear();
            }

            this.disposed = true;
        }

        /// <summary>Raises the <see cref="PreRender"/> event.</summary>
        /// <param name="e">The arguments of the event.</param>
        protected virtual void OnPreRender(EventArgs e)
        {
            this.PreRender?.Invoke(this, e);
        }

        /// <summary>
        /// Gets the request this control was created for.
        /// </summary>
        /// <remarks>
        /// Replaces <c>Control.Page.Request</c>. It is captured while the component initializes
        /// because <c>HttpContext</c> is null once the Blazor circuit is interactive.
        /// </remarks>
        public Microsoft.AspNetCore.Http.HttpRequest Request =>
            this.capturedRequest ?? Common.HttpContextSource.Current?.Request;

        /// <summary>Gets the response of the request this control was created for.</summary>
        /// <remarks>Replaces <c>Control.Page.Response</c>.</remarks>
        public Microsoft.AspNetCore.Http.HttpResponse Response =>
            this.capturedRequest?.HttpContext?.Response ?? Common.HttpContextSource.Current?.Response;

        /// <summary>Runs when this control initializes.</summary>
        /// <param name="e">The event arguments.</param>
        /// <remarks>Replaces the WebForms <c>Init</c> lifecycle event.</remarks>
        protected virtual void OnInit(EventArgs e)
        {
        }

        /// <summary>Runs when this control's parameters have been set.</summary>
        /// <param name="e">The event arguments.</param>
        /// <remarks>Replaces the WebForms <c>Load</c> lifecycle event.</remarks>
        protected virtual void OnLoad(EventArgs e)
        {
        }

        /// <summary>Runs when this control is torn down.</summary>
        /// <param name="e">The event arguments.</param>
        /// <remarks>Replaces the WebForms <c>Unload</c> lifecycle event.</remarks>
        protected virtual void OnUnload(EventArgs e)
        {
        }

        /// <inheritdoc />
        protected override void OnInitialized()
        {
            base.OnInitialized();

            this.capturedRequest = Common.HttpContextSource.Current?.Request;
            this.EnsureChildControls();
            this.OnInit(EventArgs.Empty);
        }

        /// <inheritdoc />
        protected override void OnParametersSet()
        {
            base.OnParametersSet();

            this.OnLoad(EventArgs.Empty);
        }

        /// <inheritdoc />
        protected override void OnAfterRender(bool firstRender)
        {
            base.OnAfterRender(firstRender);

            if (firstRender)
            {
                this.OnPreRender(EventArgs.Empty);
            }
        }

        /// <summary>Builds the value of the <c>style</c> attribute from <see cref="Style"/>.</summary>
        /// <returns>The inline style declarations, or an empty string when there are none.</returns>
        protected string BuildStyleAttribute()
        {
            if (this.style.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            foreach (var declaration in this.style)
            {
                builder.Append(declaration.Key).Append(':').Append(declaration.Value).Append(';');
            }

            return builder.ToString();
        }

        private void InvokeStateChange()
        {
            try
            {
                this.StateHasChanged();
            }
            catch (InvalidOperationException)
            {
                // the component is not attached to a renderer yet (the skinning engine builds the
                // tree before the first render); the pending children are picked up by that render.
            }
        }
    }

    /// <summary>
    /// Captures the Blazor parameters the skinning engine assigned to a component instance so they
    /// can be handed to the renderer, which creates and owns the instance it renders.
    /// </summary>
    public static class DnnComponentParameters
    {
        /// <summary>Captures the non-default <c>[Parameter]</c> values of the given component.</summary>
        /// <param name="component">The configured component instance.</param>
        /// <returns>The parameter name/value pairs to pass to the renderer.</returns>
        public static IDictionary<string, object> Capture(object component)
        {
            var parameters = new Dictionary<string, object>(StringComparer.Ordinal);
            if (component == null)
            {
                return parameters;
            }

            foreach (var property in component.GetType().GetProperties())
            {
                if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                if (!Attribute.IsDefined(property, typeof(ParameterAttribute)))
                {
                    continue;
                }

                var value = property.GetValue(component);
                if (value == null)
                {
                    continue;
                }

                parameters[property.Name] = value;
            }

            return parameters;
        }
    }

    /// <summary>A control that renders a fixed block of markup.</summary>
    /// <remarks>Replaces <c>System.Web.UI.LiteralControl</c> and <c>System.Web.UI.WebControls.Literal</c>.</remarks>
    public class DnnLiteralControl : DnnComponentBase
    {
        /// <summary>Initializes a new instance of the <see cref="DnnLiteralControl"/> class.</summary>
        public DnnLiteralControl()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnLiteralControl"/> class.</summary>
        /// <param name="text">The markup to render.</param>
        public DnnLiteralControl(string text)
        {
            this.Text = text;
        }

        /// <summary>Gets or sets the markup rendered by this control.</summary>
        [Parameter]
        public string Text { get; set; }

        /// <inheritdoc />
        protected override void RenderContents(RenderTreeBuilder builder, ref int sequence)
        {
            if (!string.IsNullOrEmpty(this.Text))
            {
                builder.AddMarkupContent(sequence++, this.Text);
            }

            base.RenderContents(builder, ref sequence);
        }
    }

    /// <summary>A control that renders an HTML element of a configurable name around its children.</summary>
    /// <remarks>
    /// Replaces <c>System.Web.UI.HtmlControls.HtmlGenericControl</c>,
    /// <c>HtmlContainerControl</c>, <c>System.Web.UI.WebControls.Panel</c> (a <c>div</c>) and
    /// <c>PlaceHolder</c> (no wrapper element).
    /// </remarks>
    public class DnnGenericControl : DnnComponentBase
    {
        /// <summary>Initializes a new instance of the <see cref="DnnGenericControl"/> class.</summary>
        public DnnGenericControl()
            : this("div")
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnGenericControl"/> class.</summary>
        /// <param name="tagName">The HTML element name, or <see langword="null"/> to render no wrapper.</param>
        public DnnGenericControl(string tagName)
        {
            this.Tag = tagName;
        }

        /// <summary>Gets or sets the HTML element name this control renders.</summary>
        [Parameter]
        public string Tag { get; set; }

        /// <summary>Gets or sets the inner markup of this control.</summary>
        /// <remarks>Replaces <c>HtmlContainerControl.InnerHtml</c>.</remarks>
        [Parameter]
        public string InnerHtml { get; set; }

        /// <inheritdoc />
        protected override string TagName => this.Tag;

        /// <inheritdoc />
        protected override void RenderContents(RenderTreeBuilder builder, ref int sequence)
        {
            if (!string.IsNullOrEmpty(this.InnerHtml))
            {
                builder.AddMarkupContent(sequence++, this.InnerHtml);
            }

            base.RenderContents(builder, ref sequence);
        }
    }

    /// <summary>A control that holds controls added at runtime without rendering a wrapper element.</summary>
    /// <remarks>Replaces <c>System.Web.UI.WebControls.PlaceHolder</c>.</remarks>
    public class DnnPlaceHolder : DnnComponentBase
    {
    }

    /// <summary>A control that renders a <c>div</c> around its children.</summary>
    /// <remarks>Replaces <c>System.Web.UI.WebControls.Panel</c>.</remarks>
    public class DnnPanel : DnnGenericControl
    {
        /// <summary>Initializes a new instance of the <see cref="DnnPanel"/> class.</summary>
        public DnnPanel()
            : base("div")
        {
        }
    }

    /// <summary>An item of a <see cref="DnnDropDownList"/>.</summary>
    /// <remarks>Replaces <c>System.Web.UI.WebControls.ListItem</c>.</remarks>
    public class DnnListItem
    {
        /// <summary>Initializes a new instance of the <see cref="DnnListItem"/> class.</summary>
        public DnnListItem()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnListItem"/> class.</summary>
        /// <param name="text">The display text of the item.</param>
        /// <param name="value">The value of the item.</param>
        public DnnListItem(string text, string value)
        {
            this.Text = text;
            this.Value = value;
        }

        /// <summary>Gets or sets the display text of the item.</summary>
        public string Text { get; set; }

        /// <summary>Gets or sets the value of the item.</summary>
        public string Value { get; set; }

        /// <summary>Gets or sets a value indicating whether the item is selected.</summary>
        public bool Selected { get; set; }
    }

    /// <summary>A control that renders a <c>select</c> element.</summary>
    /// <remarks>
    /// Replaces <c>System.Web.UI.WebControls.DropDownList</c>. The selection change raises
    /// <see cref="SelectedIndexChanged"/>, which replaces the WebForms auto-postback event.
    /// </remarks>
    public class DnnDropDownList : DnnComponentBase
    {
        private readonly List<DnnListItem> items = new List<DnnListItem>();

        /// <summary>Occurs when the selected item changed.</summary>
        public event EventHandler SelectedIndexChanged;

        /// <summary>Gets the items of the list.</summary>
        public IList<DnnListItem> Items => this.items;

        /// <summary>Gets or sets the CSS width of the list, e.g. <c>"300px"</c>.</summary>
        /// <remarks>Replaces <c>WebControl.Width</c>, which was a <c>System.Web.UI.WebControls.Unit</c>.</remarks>
        [Parameter]
        public string Width
        {
            get => this.Style.TryGetValue("width", out var width) ? width : null;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    this.Style.Remove("width");
                }
                else
                {
                    this.Style["width"] = value;
                }
            }
        }

        /// <summary>Gets the selected item, or <see langword="null"/> when nothing is selected.</summary>
        public DnnListItem SelectedItem => this.items.FirstOrDefault(item => item.Selected);

        /// <summary>Gets or sets the value of the selected item.</summary>
        public string SelectedValue
        {
            get => this.SelectedItem?.Value ?? string.Empty;
            set
            {
                foreach (var item in this.items)
                {
                    item.Selected = string.Equals(item.Value, value, StringComparison.Ordinal);
                }
            }
        }

        /// <inheritdoc />
        protected override string TagName => "select";

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (!this.Visible)
            {
                return;
            }

            var sequence = 0;
            builder.OpenElement(sequence++, "select");
            if (!string.IsNullOrEmpty(this.ID))
            {
                builder.AddAttribute(sequence++, "id", this.ClientId);
                builder.AddAttribute(sequence++, "name", this.UniqueId);
            }

            foreach (var attribute in this.Attributes)
            {
                builder.AddAttribute(sequence++, attribute.Key, attribute.Value);
            }

            var inlineStyle = this.BuildStyleAttribute();
            if (!string.IsNullOrEmpty(inlineStyle))
            {
                builder.AddAttribute(sequence++, "style", inlineStyle);
            }

            builder.AddAttribute(sequence++, "value", this.SelectedValue);
            builder.AddAttribute(sequence++, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, this.OnSelectionChanged));

            foreach (var item in this.items)
            {
                builder.OpenElement(sequence, "option");
                builder.AddAttribute(sequence + 1, "value", item.Value);
                if (item.Selected)
                {
                    builder.AddAttribute(sequence + 2, "selected", true);
                }

                builder.AddContent(sequence + 3, item.Text);
                builder.CloseElement();
            }

            builder.CloseElement();
        }

        private void OnSelectionChanged(ChangeEventArgs args)
        {
            this.SelectedValue = args.Value?.ToString();
            this.SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>A control that renders a list of radio buttons laid out in columns.</summary>
    /// <remarks>Replaces <c>System.Web.UI.WebControls.RadioButtonList</c>.</remarks>
    public class DnnRadioButtonList : DnnComponentBase
    {
        private readonly List<DnnListItem> items = new List<DnnListItem>();

        /// <summary>Occurs when the selected item changed.</summary>
        public event EventHandler SelectedIndexChanged;

        /// <summary>Gets the items of the list.</summary>
        public IList<DnnListItem> Items => this.items;

        /// <summary>Gets or sets the number of columns the items are laid out in.</summary>
        [Parameter]
        public int RepeatColumns { get; set; }

        /// <summary>Gets the selected item, or <see langword="null"/> when nothing is selected.</summary>
        public DnnListItem SelectedItem => this.items.FirstOrDefault(item => item.Selected);

        /// <summary>Gets or sets the value of the selected item.</summary>
        public string SelectedValue
        {
            get => this.SelectedItem?.Value ?? string.Empty;
            set
            {
                foreach (var item in this.items)
                {
                    item.Selected = string.Equals(item.Value, value, StringComparison.Ordinal);
                }
            }
        }

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (!this.Visible)
            {
                return;
            }

            var sequence = 0;
            builder.OpenElement(sequence++, "div");
            if (!string.IsNullOrEmpty(this.ID))
            {
                builder.AddAttribute(sequence++, "id", this.ClientId);
            }

            foreach (var attribute in this.Attributes)
            {
                builder.AddAttribute(sequence++, attribute.Key, attribute.Value);
            }

            var style = this.BuildStyleAttribute();
            if (this.RepeatColumns > 0)
            {
                style += $"display:grid;grid-template-columns:repeat({this.RepeatColumns}, auto);";
            }

            if (!string.IsNullOrEmpty(style))
            {
                builder.AddAttribute(sequence++, "style", style);
            }

            var groupName = string.IsNullOrEmpty(this.ID) ? "DnnRadioButtonList" : this.ClientId;
            foreach (var item in this.items)
            {
                var currentItem = item;
                builder.OpenElement(sequence, "label");
                builder.OpenElement(sequence + 1, "input");
                builder.AddAttribute(sequence + 2, "type", "radio");
                builder.AddAttribute(sequence + 3, "name", groupName);
                builder.AddAttribute(sequence + 4, "value", currentItem.Value);
                builder.AddAttribute(sequence + 5, "checked", currentItem.Selected);
                builder.AddAttribute(
                    sequence + 6,
                    "onchange",
                    EventCallback.Factory.Create<ChangeEventArgs>(this, _ => this.OnItemSelected(currentItem)));
                builder.CloseElement();

                // the item text is markup (DNN renders the skin's thumbnail image in it)
                builder.AddMarkupContent(sequence + 7, currentItem.Text);
                builder.CloseElement();
            }

            builder.CloseElement();
        }

        private void OnItemSelected(DnnListItem item)
        {
            this.SelectedValue = item.Value;
            this.SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>A control that renders a radio button.</summary>
    /// <remarks>Replaces <c>System.Web.UI.WebControls.RadioButton</c>.</remarks>
    public class DnnRadioButton : DnnComponentBase
    {
        /// <summary>Occurs when <see cref="Checked"/> changed.</summary>
        public event EventHandler CheckedChanged;

        /// <summary>Gets or sets a value indicating whether the radio button is selected.</summary>
        [Parameter]
        public bool Checked { get; set; }

        /// <summary>Gets or sets the group the radio button belongs to.</summary>
        [Parameter]
        public string GroupName { get; set; }

        /// <summary>Gets or sets the label rendered next to the radio button.</summary>
        [Parameter]
        public string Text { get; set; }

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (!this.Visible)
            {
                return;
            }

            var sequence = 0;
            builder.OpenElement(sequence++, "input");
            builder.AddAttribute(sequence++, "type", "radio");
            if (!string.IsNullOrEmpty(this.ID))
            {
                builder.AddAttribute(sequence++, "id", this.ClientId);
            }

            if (!string.IsNullOrEmpty(this.GroupName))
            {
                builder.AddAttribute(sequence++, "name", this.GroupName);
            }

            foreach (var attribute in this.Attributes)
            {
                builder.AddAttribute(sequence++, attribute.Key, attribute.Value);
            }

            builder.AddAttribute(sequence++, "checked", this.Checked);
            builder.AddAttribute(sequence++, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, this.OnCheckedChanged));
            builder.CloseElement();

            if (!string.IsNullOrEmpty(this.Text))
            {
                builder.OpenElement(sequence++, "label");
                if (!string.IsNullOrEmpty(this.ID))
                {
                    builder.AddAttribute(sequence++, "for", this.ClientId);
                }

                builder.AddContent(sequence++, this.Text);
                builder.CloseElement();
            }
        }

        private void OnCheckedChanged(ChangeEventArgs args)
        {
            var isChecked = args.Value is bool flag ? flag : bool.TryParse(args.Value?.ToString(), out var parsed) && parsed;
            if (this.Checked == isChecked)
            {
                return;
            }

            this.Checked = isChecked;
            this.CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>A control that renders text inside a <c>span</c>.</summary>
    /// <remarks>Replaces <c>System.Web.UI.WebControls.Label</c>.</remarks>
    public class DnnLabel : DnnComponentBase
    {
        /// <summary>Gets or sets the text (markup) rendered by this label.</summary>
        [Parameter]
        public string Text { get; set; }

        /// <inheritdoc />
        protected override string TagName => "span";

        /// <inheritdoc />
        protected override void RenderContents(RenderTreeBuilder builder, ref int sequence)
        {
            if (!string.IsNullOrEmpty(this.Text))
            {
                builder.AddMarkupContent(sequence++, this.Text);
            }

            base.RenderContents(builder, ref sequence);
        }
    }
}
