// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.Net;

    using DotNetNuke.Entities.Users;
    using DotNetNuke.Security;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Web;

    /// <summary>The EditControl control provides a standard UI component for editing properties.</summary>
    /// <remarks>
    /// <para>
    /// This was a <c>WebControl</c> implementing <c>IPostBackDataHandler</c>. On ASP.NET Core it is a
    /// <see cref="PropertyEditorComponentBase"/>: <c>Render(HtmlTextWriter)</c> became
    /// <see cref="RenderContent"/>, and <c>RenderEditMode</c>/<c>RenderViewMode</c> now take a
    /// <see cref="RenderTreeBuilder"/>.
    /// </para>
    /// <para>
    /// <see cref="LoadPostData"/> is deliberately preserved: there is no Web Forms postback any more,
    /// but it still is the single place where "did the value change?" is decided, and third-party
    /// editors override it. Interactive input is funnelled through
    /// <see cref="SetPostValue"/>/<see cref="CommitPostData"/>, which call
    /// <see cref="LoadPostData"/> with the same keys the posted form used and then raise
    /// <see cref="RaisePostDataChangedEvent"/>. Overrides therefore keep working unchanged.
    /// </para>
    /// </remarks>
    public abstract class EditControl : PropertyEditorComponentBase
    {
        private readonly NameValueCollection postData = new NameValueCollection(StringComparer.Ordinal);

        private object[] customAttributes;

        /// <summary>Occurs when an item is added to a collection type property.</summary>
        public event PropertyChangedEventHandler ItemAdded;

        /// <summary>Occurs when an item is deleted from a collection type property.</summary>
        public event PropertyChangedEventHandler ItemDeleted;

        /// <summary>Occurs when the value of this editor has changed.</summary>
        public event PropertyChangedEventHandler ValueChanged;

        /// <summary>Gets a value indicating whether the control is valid.</summary>
        /// <value>A boolean.</value>
        public virtual bool IsValid => true;

        /// <summary>Gets the identifier of the element that a label should be associated with.</summary>
        public virtual string EditControlClientId => this.ClientID;

        /// <summary>Gets or sets the Custom Attributes for this Control.</summary>
        /// <value>An array of Attributes.</value>
        [Parameter]
        public object[] CustomAttributes
        {
            get
            {
                return this.customAttributes;
            }

            set
            {
                this.customAttributes = value;
                if ((this.customAttributes != null) && this.customAttributes.Length > 0)
                {
                    this.OnAttributesChanged();
                }
            }
        }

        /// <summary>Gets or sets the Edit Mode of the Editor.</summary>
        /// <value>A boolean.</value>
        [Parameter]
        public PropertyEditorMode EditMode { get; set; }

        /// <summary>Gets or sets the Local Resource File for the Control.</summary>
        /// <value>A String.</value>
        [Parameter]
        public string LocalResourceFile { get; set; }

        /// <summary>Gets or sets name is the name of the field as a string.</summary>
        /// <value>A string representing the Name of the property.</value>
        [Parameter]
        public string Name { get; set; }

        /// <summary>Gets or sets the Category to which this edit control belongs.</summary>
        /// <value>A string representing the Category of the property.</value>
        [Parameter]
        public string Category { get; set; }

        /// <summary>Gets or sets oldValue is the initial value of the field.</summary>
        /// <value>The initial Value of the property.</value>
        [Parameter]
        public object OldValue { get; set; }

        /// <summary>Gets or sets a value indicating whether the Property is required.</summary>
        /// <value>The initial Value of the property.</value>
        [Parameter]
        public bool Required { get; set; }

        /// <summary>Gets or sets systemType is the System Data Type for the property.</summary>
        /// <value>A string representing the Type of the property.</value>
        [Parameter]
        public string SystemType { get; set; }

        /// <summary>Gets or sets value is the value of the control.</summary>
        /// <value>The Value of the property.</value>
        [Parameter]
        public object Value { get; set; }

        /// <summary>Gets or sets set Data Field of the control.</summary>
        [Parameter]
        public string DataField { get; set; }

        /// <summary>Gets or sets the user whose property is being edited.</summary>
        [Parameter]
        public UserInfo User { get; set; }

        /// <summary>Gets or sets a callback raised in addition to the <see cref="ValueChanged"/> event.</summary>
        /// <remarks>Lets this editor be consumed declaratively from Blazor markup. The
        /// <c>Callback</c> suffix avoids colliding with the <see cref="OnValueChanged"/> method, which
        /// is part of the extension contract.</remarks>
        [Parameter]
        public EventCallback<PropertyEditorEventArgs> ValueChangedCallback { get; set; }

        /// <summary>Gets or sets a callback raised in addition to the <see cref="ItemAdded"/> event.</summary>
        [Parameter]
        public EventCallback<PropertyEditorEventArgs> ItemAddedCallback { get; set; }

        /// <summary>Gets or sets a callback raised in addition to the <see cref="ItemDeleted"/> event.</summary>
        [Parameter]
        public EventCallback<PropertyEditorEventArgs> ItemDeletedCallback { get; set; }

        /// <summary>Gets or sets stringValue is the value of the control expressed as a String.</summary>
        /// <value>A string representing the Value.</value>
        protected abstract string StringValue { get; set; }

        /// <summary>Gets the values that <see cref="LoadPostData"/> reads from.</summary>
        /// <remarks>Replaces the <c>NameValueCollection</c> that Web Forms handed to
        /// <c>IPostBackDataHandler.LoadPostData</c>. It is populated by
        /// <see cref="SetPostValue"/> as the user edits.</remarks>
        protected NameValueCollection PostData => this.postData;

        /// <summary>LoadPostData loads the Post Back Data and determines whether the value has change.</summary>
        /// <param name="postDataKey">A key to the PostBack Data to load.</param>
        /// <param name="postCollection">A name value collection of postback data.</param>
        /// <returns><see langword="true"/> if the value has changed, otherwise <see langword="false"/>.</returns>
        public virtual bool LoadPostData(string postDataKey, NameValueCollection postCollection)
        {
            ArgumentNullException.ThrowIfNull(postCollection);

            bool dataChanged = false;
            string presentValue = this.StringValue;
            string postedValue = postCollection[postDataKey];
            if (!presentValue.Equals(postedValue, StringComparison.Ordinal))
            {
                this.Value = postedValue;
                dataChanged = true;
            }

            return dataChanged;
        }

        /// <summary>RaisePostDataChangedEvent runs when the PostBackData has changed.  It triggers a <see cref="ValueChanged"/> Event.</summary>
        public void RaisePostDataChangedEvent()
        {
            this.OnDataChanged(EventArgs.Empty);
        }

        /// <summary>Records a value that the user entered, under the form field name the editor renders.</summary>
        /// <param name="suffix">The suffix appended to <see cref="PropertyEditorComponentBase.UniqueID"/> for the field (may be empty).</param>
        /// <param name="value">The entered value.</param>
        protected void SetPostValue(string suffix, string value)
        {
            this.postData[this.UniqueID + suffix] = value;
        }

        /// <summary>Applies the recorded input through <see cref="LoadPostData"/> and raises the change event.</summary>
        /// <remarks>This is the Blazor equivalent of the Web Forms postback: it replaces
        /// <c>Page.RegisterRequiresPostBack(this)</c> plus the framework's
        /// <c>LoadPostData</c>/<c>RaisePostDataChangedEvent</c> call pair.</remarks>
        protected void CommitPostData()
        {
            if (this.LoadPostData(this.UniqueID, this.postData))
            {
                this.RaisePostDataChangedEvent();
            }
        }

        /// <summary>Handles a change of the single input that most editors render.</summary>
        /// <param name="args">The change event arguments.</param>
        protected void OnInputChanged(ChangeEventArgs args)
        {
            ArgumentNullException.ThrowIfNull(args);

            this.SetPostValue(string.Empty, args.Value?.ToString());
            this.CommitPostData();
        }

        /// <summary>OnDataChanged runs when the PostbackData has changed.  It raises the <see cref="ValueChanged"/> Event.</summary>
        /// <param name="e">The event arguments.</param>
        protected abstract void OnDataChanged(EventArgs e);

        /// <summary>OnAttributesChanged runs when the CustomAttributes property has changed.</summary>
        protected virtual void OnAttributesChanged()
        {
        }

        /// <summary>Runs when an item is added to a collection type property. It raises the <see cref="ItemAdded"/> event.</summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnItemAdded(PropertyEditorEventArgs e)
        {
            this.ItemAdded?.Invoke(this, e);
            if (this.ItemAddedCallback.HasDelegate)
            {
                this.ItemAddedCallback.InvokeAsync(e);
            }
        }

        /// <summary>Runs when an item is deleted from a collection type property. It raises the <see cref="ItemDeleted"/> event.</summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnItemDeleted(PropertyEditorEventArgs e)
        {
            this.ItemDeleted?.Invoke(this, e);
            if (this.ItemDeletedCallback.HasDelegate)
            {
                this.ItemDeletedCallback.InvokeAsync(e);
            }
        }

        /// <summary>OnValueChanged runs when the Value has changed.  It raises the <see cref="ValueChanged"/> Event.</summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnValueChanged(PropertyEditorEventArgs e)
        {
            this.ValueChanged?.Invoke(this, e);
            if (this.ValueChangedCallback.HasDelegate)
            {
                this.ValueChangedCallback.InvokeAsync(e);
            }
        }

        /// <summary>RenderViewMode renders the View (readonly) mode of the control.</summary>
        /// <param name="builder">The render tree builder to write to.</param>
        protected virtual void RenderViewMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            string propValue = WebUtility.HtmlDecode(Convert.ToString(this.Value, CultureInfo.InvariantCulture));

            builder.OpenElement(0, "span");
            this.AddStyleAttributes(builder, 1);
            var security = PortalSecurity.Instance;
#pragma warning disable CS0618 // Type or member is obsolete
            builder.AddContent(5, security.InputFilter(propValue, PortalSecurity.FilterFlag.NoScripting));
#pragma warning restore CS0618 // Type or member is obsolete
            builder.CloseElement();
        }

        /// <summary>RenderEditMode renders the Edit mode of the control.</summary>
        /// <param name="builder">The render tree builder to write to.</param>
        protected virtual void RenderEditMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            string propValue = Convert.ToString(this.Value, CultureInfo.InvariantCulture);

            builder.OpenElement(0, "input");
            this.AddStyleAttributes(builder, 1);
            builder.AddAttribute(5, "type", "text");
            builder.AddAttribute(6, "value", propValue);
            builder.AddAttribute(7, "name", this.UniqueID);
            builder.AddAttribute(8, "id", this.ClientID);
            builder.AddAttribute(9, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, this.OnInputChanged));
            builder.CloseElement();
        }

        /// <inheritdoc />
        protected override void RenderContent(RenderTreeBuilder builder)
        {
            var strOldValue = this.OldValue as string;
            if (this.EditMode == PropertyEditorMode.Edit || (this.Required && string.IsNullOrEmpty(strOldValue)))
            {
                this.RenderEditMode(builder);
            }
            else
            {
                this.RenderViewMode(builder);
            }
        }
    }
}
