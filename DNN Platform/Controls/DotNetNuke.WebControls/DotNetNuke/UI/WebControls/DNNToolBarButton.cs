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
    using System.IO;
    using System.Text;

    /// <summary>
    /// One button of a <see cref="DNNToolBar"/>.
    /// </summary>
    /// <remarks>
    /// Converted from <c>Control</c> to <see cref="BaseWebControl"/>; the state-bag backed
    /// properties became entries of an ordinary state dictionary keeping the very same defaults the
    /// view state getters returned, and the design-time notification still runs on every write.
    /// <see cref="ICustomTypeDescriptor"/> lives in <c>System.ComponentModel</c> and is kept, which
    /// is what makes the per-action property filtering work.
    /// <para>
    /// The <c>ControlAction</c> property carried a
    /// <c>TypeConverter(GetType(DNNToolBarButtonActionTypeConverter))</c> annotation. That converter
    /// still awaits its own conversion (checklist task 104); the annotation has to be re-applied
    /// once the type exists in C#.
    /// </para>
    /// </remarks>
    public class DNNToolBarButton : BaseWebControl, ICustomTypeDescriptor
    {
        private readonly Dictionary<string, object> state = new Dictionary<string, object>(StringComparer.Ordinal);
        private DNNToolBar toolbar;

        /// <summary>Initializes a new instance of the <see cref="DNNToolBarButton"/> class.</summary>
        public DNNToolBarButton()
        {
            this.CommonActions = new[] { "js", "navigate" };
        }

        /// <summary>Gets or sets the key of the action the button raises.</summary>
        public string Key
        {
            get
            {
                if (Array.IndexOf(this.CommonActions, this.ControlAction) <= -1)
                {
                    return this.ControlAction;
                }

                return this.GetState("Key");
            }

            set
            {
                this.SetState("Key", value);
            }
        }

        /// <summary>Gets or sets the CSS class of the button.</summary>
        [Category("Appearance")]
        [DefaultValue("")]
        public override string CssClass
        {
            get { return this.GetState("CssClass"); }
            set { this.SetState("CssClass", value); }
        }

        /// <summary>Gets or sets the CSS class applied while the pointer is over the button.</summary>
        [Category("Appearance")]
        [DefaultValue("")]
        public string CssClassHover
        {
            get { return this.GetState("CssClassHover"); }
            set { this.SetState("CssClassHover", value); }
        }

        /// <summary>Gets or sets the action the button raises.</summary>
        [DefaultValue("")]
        [Category("Behavior")]
        [TypeConverter(typeof(DotNetNuke.UI.Design.WebControls.DNNToolBarButtonActionTypeConverter))]
        public string ControlAction
        {
            get
            {
                return this.GetState("ControlAction");
            }

            set
            {
                this.SetState("ControlAction", value);
                TypeDescriptor.Refresh(this);
            }
        }

        /// <summary>Gets or sets the text of the button.</summary>
        [Category("Appearance")]
        [DefaultValue("")]
        public string Text
        {
            get { return this.GetState("Text"); }
            set { this.SetState("Text", value); }
        }

        /// <summary>Gets or sets the tooltip of the button.</summary>
        [DefaultValue("")]
        [Category("Appearance")]
        public override string ToolTip
        {
            get { return this.GetState("ToolTip"); }
            set { this.SetState("ToolTip", value); }
        }

        /// <summary>Gets or sets the image of the button.</summary>
        [Category("Appearance")]
        [DefaultValue("")]
        public string ImageUrl
        {
            get { return this.GetState("ImageUrl"); }
            set { this.SetState("ImageUrl", value); }
        }

        /// <summary>Gets or sets the URL the button navigates to.</summary>
        [DefaultValue("")]
        [Category("Behavior")]
        public string NavigateUrl
        {
            get { return this.GetState("NavigateUrl"); }
            set { this.SetState("NavigateUrl", value); }
        }

        /// <summary>Gets or sets the script the button runs.</summary>
        [Category("Behavior")]
        [DefaultValue("")]
        public string JSFunction
        {
            get { return this.GetState("JSFunction"); }
            set { this.SetState("JSFunction", value); }
        }

        /// <summary>Gets or sets the actions for which the <see cref="Key"/> is meaningful.</summary>
        internal string[] CommonActions { get; set; }

        /// <summary>Gets the tool bar this button belongs to.</summary>
        [Browsable(false)]
        private DNNToolBar Owner
        {
            get
            {
                if (this.toolbar is null)
                {
                    this.toolbar = this.Parent as DNNToolBar;
                }

                return this.toolbar;
            }
        }

        /// <inheritdoc/>
        public AttributeCollection GetAttributes()
        {
            return TypeDescriptor.GetAttributes(this.GetType());
        }

        /// <inheritdoc/>
        public string GetClassName()
        {
            return TypeDescriptor.GetClassName(this.GetType());
        }

        /// <inheritdoc/>
        public string GetComponentName()
        {
            return TypeDescriptor.GetComponentName(this.GetType());
        }

        /// <inheritdoc/>
        public TypeConverter GetConverter()
        {
            return TypeDescriptor.GetConverter(this.GetType());
        }

        /// <inheritdoc/>
        public EventDescriptor GetDefaultEvent()
        {
            return TypeDescriptor.GetDefaultEvent(this.GetType());
        }

        /// <inheritdoc/>
        public PropertyDescriptor GetDefaultProperty()
        {
            return TypeDescriptor.GetDefaultProperty(this.GetType());
        }

        /// <inheritdoc/>
        public object GetEditor(Type editorBaseType)
        {
            return TypeDescriptor.GetEditor(this.GetType(), editorBaseType);
        }

        /// <inheritdoc/>
        public EventDescriptorCollection GetEvents(Attribute[] attributes)
        {
            return TypeDescriptor.GetEvents(this.GetType(), attributes);
        }

        /// <inheritdoc/>
        public EventDescriptorCollection GetEvents()
        {
            return TypeDescriptor.GetEvents(this.GetType());
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Hides the properties that do not apply to the currently selected
        /// <see cref="ControlAction"/>, which is what the original did.
        /// </remarks>
        public PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            var filtered = new PropertyDescriptorCollection(null);
            var properties = TypeDescriptor.GetProperties(this.GetType(), attributes);
            foreach (PropertyDescriptor current in properties)
            {
                switch (current.Name)
                {
                    case "NavigateUrl":
                        if (this.ControlAction == "navigate")
                        {
                            this.HandlePropertyFilters(current, filtered);
                        }

                        break;

                    case "JSFunction":
                        if (this.ControlAction == "js")
                        {
                            this.HandlePropertyFilters(current, filtered);
                        }

                        break;

                    case "Key":
                        if (Array.IndexOf(this.CommonActions, this.ControlAction) > -1)
                        {
                            this.HandlePropertyFilters(current, filtered);
                        }

                        break;

                    default:
                        this.HandlePropertyFilters(current, filtered);
                        break;
                }
            }

            return filtered;
        }

        /// <inheritdoc/>
        public PropertyDescriptorCollection GetProperties()
        {
            return TypeDescriptor.GetProperties(this.GetType());
        }

        /// <inheritdoc/>
        public object GetPropertyOwner(PropertyDescriptor pd)
        {
            return this;
        }

        /// <summary>Serializes the button for the client script.</summary>
        /// <returns>The JSON object literal of the button.</returns>
        public string ToJSON()
        {
            var values = new Hashtable();
            if (!string.IsNullOrEmpty(this.CssClass))
            {
                values.Add("css", SafeJSONString(this.CssClass));
            }

            if (!string.IsNullOrEmpty(this.CssClassHover))
            {
                values.Add("cssh", SafeJSONString(this.CssClassHover));
            }

            if (!string.IsNullOrEmpty(this.ImageUrl))
            {
                values.Add("img", SafeJSONString(this.ImageUrl));
            }

            if (!string.IsNullOrEmpty(this.Key))
            {
                values.Add("key", SafeJSONString(this.Key));
            }

            if (!string.IsNullOrEmpty(this.JSFunction) && this.ControlAction == "js")
            {
                values.Add("js", SafeJSONString(this.JSFunction));
            }

            if (!string.IsNullOrEmpty(this.NavigateUrl) && this.ControlAction == "navigate")
            {
                values.Add("url", SafeJSONString(this.NavigateUrl));
            }

            if (!string.IsNullOrEmpty(this.ControlAction))
            {
                values.Add("ca", SafeJSONString(this.ControlAction));
            }

            if (!string.IsNullOrEmpty(this.Text))
            {
                values.Add("txt", SafeJSONString(this.Text));
            }

            if (!string.IsNullOrEmpty(this.ToolTip))
            {
                values.Add("alt", SafeJSONString(this.ToolTip));
            }

            if (!this.Visible)
            {
                values.Add("vis", this.Visible ? 1 : 0);
            }

            var builder = new StringBuilder();
            builder.Append('{');
            foreach (var key in values.Keys)
            {
                var name = Convert.ToString(key, CultureInfo.InvariantCulture);
                if (builder.Length > 1)
                {
                    builder.Append(',');
                }

                builder.Append(name).Append(':').Append(Convert.ToString(values[key], CultureInfo.InvariantCulture));
            }

            builder.Append('}');
            return builder.ToString();
        }

        /// <summary>
        /// Renders the button.
        /// </summary>
        /// <param name="writer">The writer to render to.</param>
        /// <remarks>
        /// Preserved from the original, where it was deliberately empty: the client script builds
        /// the button from the JSON its tool bar registers.
        /// </remarks>
        protected override void Render(TextWriter writer)
        {
        }

        /// <summary>Quotes and escapes a value for the JSON object literal.</summary>
        /// <param name="value">The value to quote.</param>
        /// <returns>The quoted value.</returns>
        private static string SafeJSONString(string value)
        {
            return "'" + (value ?? string.Empty).Replace("'", "\\'", StringComparison.Ordinal) + "'";
        }

        /// <summary>Reads a state entry, defaulting to an empty string as the original did.</summary>
        /// <param name="key">The name of the entry.</param>
        /// <returns>The stored value, or an empty string.</returns>
        private string GetState(string key)
        {
            return this.state.TryGetValue(key, out var value) && value is not null
                ? Convert.ToString(value, CultureInfo.InvariantCulture)
                : string.Empty;
        }

        /// <summary>Writes a state entry and notifies the design surface.</summary>
        /// <param name="key">The name of the entry.</param>
        /// <param name="value">The value to store.</param>
        private void SetState(string key, object value)
        {
            this.state[key] = value;
            this.NotifyDesigner();
        }

        /// <summary>Adds a property to the filtered collection, reading it first as the original did.</summary>
        /// <param name="pd">The property to add.</param>
        /// <param name="target">The collection to add the property to.</param>
        private void HandlePropertyFilters(PropertyDescriptor pd, PropertyDescriptorCollection target)
        {
            pd.GetValue(this);
            target.Add(pd);
        }

        /// <summary>Tells the designer of the owning tool bar that this button changed.</summary>
        private void NotifyDesigner()
        {
            if (this.Owner is not null && WebControls.IsDesignMode())
            {
                this.Owner.NotifyDesigner();
            }
        }
    }
}
