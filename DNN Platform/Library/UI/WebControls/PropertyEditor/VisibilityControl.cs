// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.Linq;
    using System.Text;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Common;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Profile;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.Security.Roles;
    using DotNetNuke.Services.Localization;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Web;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>The VisibilityControl control provides a base control for defining visibility options.</summary>
    /// <remarks>
    /// <para>
    /// The markup — the <c>dnnFormVisibility dnnDropdownSettings</c> container, the button/arrow and
    /// the two <c>ul</c> lists of radios and checkboxes — is unchanged, including the form field
    /// names, so <see cref="LoadPostData"/> (and the posted value shape it parses) is preserved.
    /// </para>
    /// <para>
    /// The inline <c>visibleChange</c> script that showed the friends-and-groups list when the
    /// matching radio was clicked is gone: the list's visibility is now rendered from the selected
    /// visibility mode, which Blazor updates on every change. Inline scripts are not allowed in
    /// Blazor components, and the behaviour does not need one.
    /// </para>
    /// </remarks>
    public class VisibilityControl : PropertyEditorComponentBase
    {
        private readonly IApplicationStatusInfo appStatus;
        private readonly IEventLogger eventLogger;
        private readonly NameValueCollection postValues = new NameValueCollection(StringComparer.Ordinal);

        /// <summary>Initializes a new instance of the <see cref="VisibilityControl"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IHostSettings. Scheduled removal in v12.0.0.")]
        public VisibilityControl()
            : this(null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="VisibilityControl"/> class.</summary>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        public VisibilityControl(IApplicationStatusInfo appStatus, IEventLogger eventLogger)
        {
            this.appStatus = appStatus ?? Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>();
            this.eventLogger = eventLogger ?? Globals.GetCurrentServiceProvider().GetRequiredService<IEventLogger>();
        }

        /// <summary>Occurs when the visibility of the property has changed.</summary>
        public event PropertyChangedEventHandler VisibilityChanged;

        /// <summary>Gets or sets a callback raised in addition to the <see cref="VisibilityChanged"/> event.</summary>
        [Parameter]
        public EventCallback<PropertyEditorEventArgs> VisibilityChangedCallback { get; set; }

        /// <summary>Gets or sets caption.</summary>
        /// <value>A string representing the Name of the property.</value>
        [Parameter]
        public string Caption { get; set; }

        /// <summary>Gets or sets name is the name of the field as a string.</summary>
        /// <value>A string representing the Name of the property.</value>
        [Parameter]
        public string Name { get; set; }

        /// <summary>Gets or sets the UserInfo object that represents the User whose profile is being displayed.</summary>
        [Parameter]
        public UserInfo User { get; set; }

        /// <summary>Gets or sets stringValue is the value of the control expressed as a String.</summary>
        /// <value>A string representing the Value.</value>
        [Parameter]
        public object Value { get; set; }

        /// <summary>Gets the values that <see cref="LoadPostData"/> reads from.</summary>
        /// <remarks>Replaces the <c>NameValueCollection</c> that Web Forms handed to
        /// <c>IPostBackDataHandler.LoadPostData</c>; it is kept up to date as the user edits.</remarks>
        protected NameValueCollection PostValues => this.postValues;

        /// <summary>Gets or sets the visibility of the property.</summary>
        protected ProfileVisibility Visibility
        {
            get => this.Value as ProfileVisibility;
            set => this.Value = value;
        }

        /// <summary>LoadPostData loads the Post Back Data and determines whether the value has changed.</summary>
        /// <param name="postDataKey">A key to the PostBack Data to load.</param>
        /// <param name="postCollection">A name value collection of postback data.</param>
        /// <returns><see langword="true"/> if the value has changed, otherwise <see langword="false"/>.</returns>
        public virtual bool LoadPostData(string postDataKey, NameValueCollection postCollection)
        {
            ArgumentNullException.ThrowIfNull(postCollection);

            var dataChanged = false;
            var presentVisibility = this.Visibility.VisibilityMode;
            var postedValue = Convert.ToInt32(postCollection[postDataKey], CultureInfo.InvariantCulture);
            var postedVisibility = (UserVisibilityMode)Enum.ToObject(typeof(UserVisibilityMode), postedValue);
            if (!presentVisibility.Equals(postedVisibility) || postedVisibility == UserVisibilityMode.FriendsAndGroups)
            {
                if (postedVisibility == UserVisibilityMode.FriendsAndGroups)
                {
                    var sb = new StringBuilder();
                    sb.Append("G:");
                    foreach (var role in this.User.Social.Roles)
                    {
                        if (postCollection[postDataKey + ":group_" + role.RoleID.ToString(CultureInfo.InvariantCulture)] != null)
                        {
                            sb.Append(role.RoleID.ToString(CultureInfo.InvariantCulture) + ",");
                        }
                    }

                    sb.Append(";R:");
                    foreach (var relationship in this.User.Social.Relationships)
                    {
                        if (postCollection[postDataKey + ":relationship_" + relationship.RelationshipId.ToString(CultureInfo.InvariantCulture)] != null)
                        {
                            sb.Append(relationship.RelationshipId.ToString(CultureInfo.InvariantCulture) + ",");
                        }
                    }

                    this.Value = new ProfileVisibility(this.User.PortalID, sb.ToString())
                    {
                        VisibilityMode = postedVisibility,
                    };
                }
                else
                {
                    this.Value = new ProfileVisibility
                    {
                        VisibilityMode = postedVisibility,
                    };
                }

                dataChanged = true;
            }

            return dataChanged;
        }

        /// <summary>
        /// RaisePostDataChangedEvent runs when the PostBackData has changed.  It triggers
        /// a ValueChanged Event.
        /// </summary>
        public void RaisePostDataChangedEvent()
        {
            // Raise the VisibilityChanged Event
            var args = new PropertyEditorEventArgs(this.Name) { Value = this.Value };
            this.OnVisibilityChanged(args);
        }

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            JavaScript.RequestRegistration(this.appStatus, this.eventLogger, PortalSettings.Current, CommonJs.jQuery);
        }

        /// <summary>OnVisibilityChanged runs when the Visibility has changed.  It raises the <see cref="VisibilityChanged"/> Event.</summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnVisibilityChanged(PropertyEditorEventArgs e)
        {
            this.VisibilityChanged?.Invoke(this, e);
            if (this.VisibilityChangedCallback.HasDelegate)
            {
                this.VisibilityChangedCallback.InvokeAsync(e);
            }
        }

        /// <inheritdoc />
        protected override void RenderContent(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            // Render Div container
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "dnnFormVisibility dnnDropdownSettings");
            builder.AddAttribute(2, "name", this.UniqueID);

            // Render dnnButtonDropdown
            builder.OpenElement(3, "div");
            builder.AddAttribute(4, "class", "dnnButtonDropdown");

            // Render dnnButton Icon
            builder.OpenElement(5, "div");
            builder.AddAttribute(6, "class", "dnnButtonIcon");
            builder.CloseElement();

            // render dnnButton Arrow
            builder.OpenElement(7, "div");
            builder.AddAttribute(8, "class", "dnnButtonArrow");
            builder.CloseElement();

            // Close dnnButton Dropdown
            builder.CloseElement();

            // Render UL for radio Button List
            builder.OpenElement(10, "ul");
            builder.AddAttribute(11, "class", "dnnButtonDropdown-ul");

            this.RenderVisibility(builder, 20, "0", UserVisibilityMode.AllUsers, Localization.GetString("Public").Trim());

            this.RenderVisibility(builder, 30, "1", UserVisibilityMode.MembersOnly, Localization.GetString("MembersOnly").Trim());

            this.RenderVisibility(builder, 40, "2", UserVisibilityMode.AdminOnly, Localization.GetString("AdminOnly").Trim());

            this.RenderVisibility(builder, 50, "3", UserVisibilityMode.FriendsAndGroups, Localization.GetString("FriendsGroups").Trim());

            // Render UL for check Box List
            builder.OpenElement(60, "ul");
            builder.AddAttribute(
                61,
                "style",
                this.Visibility.VisibilityMode == UserVisibilityMode.FriendsAndGroups ? "display:block;" : "display:none;");

            this.RenderRelationships(builder, 70);
            this.RenderGroups(builder, 80);

            // Close UL
            builder.CloseElement();

            // Close UL
            builder.CloseElement();

            // Close Div
            builder.CloseElement();
        }

        private void RenderVisibility(RenderTreeBuilder builder, int sequence, string optionValue, UserVisibilityMode selectedVisibility, string optionText)
        {
            // Render Li
            builder.OpenElement(sequence, "li");

            // Render radio button
            builder.OpenElement(sequence + 1, "input");
            builder.AddAttribute(sequence + 2, "type", "radio");
            builder.AddAttribute(sequence + 3, "aria-label", this.ID);
            builder.AddAttribute(sequence + 4, "name", this.UniqueID);
            builder.AddAttribute(sequence + 5, "value", optionValue);
            if (this.Visibility.VisibilityMode == selectedVisibility)
            {
                builder.AddAttribute(sequence + 6, "checked", "checked");
            }

            builder.AddAttribute(
                sequence + 7,
                "onchange",
                EventCallback.Factory.Create<ChangeEventArgs>(this, () => this.OnModeSelected(selectedVisibility)));
            builder.CloseElement();

            builder.AddContent(sequence + 8, optionText);

            // Close Li
            builder.CloseElement();
        }

        private void RenderCheckboxItem(RenderTreeBuilder builder, int sequence, string prefix, string value, string text, bool selected)
        {
            // Render Li
            builder.OpenElement(sequence, "li");

            // Render check box
            builder.OpenElement(sequence + 1, "input");
            builder.AddAttribute(sequence + 2, "type", "checkbox");
            builder.AddAttribute(sequence + 3, "aria-label", this.ID);
            builder.AddAttribute(sequence + 4, "name", this.UniqueID + prefix + value);
            builder.AddAttribute(sequence + 5, "value", value);
            if (selected)
            {
                builder.AddAttribute(sequence + 6, "checked", "checked");
            }

            builder.AddAttribute(
                sequence + 7,
                "onchange",
                EventCallback.Factory.Create<ChangeEventArgs>(this, args => this.OnMemberToggled(prefix, value, args)));
            builder.CloseElement();

            builder.AddContent(sequence + 8, text);

            // Close Li
            builder.CloseElement();
        }

        private void RenderGroups(RenderTreeBuilder builder, int sequence)
        {
            builder.OpenRegion(sequence);
            foreach (var group in this.User.Social.Roles.Where((role) => role.SecurityMode != SecurityMode.SecurityRole))
            {
                this.RenderCheckboxItem(
                    builder,
                    0,
                    ":group_",
                    group.RoleID.ToString(CultureInfo.InvariantCulture),
                    group.RoleName,
                    this.Visibility.RoleVisibilities.Count(r => r.RoleID == group.RoleID) == 1);
            }

            builder.CloseRegion();
        }

        private void RenderRelationships(RenderTreeBuilder builder, int sequence)
        {
            builder.OpenRegion(sequence);
            foreach (var relationship in this.User.Social.Relationships)
            {
                var selected = this.Visibility.RelationshipVisibilities.Count(r => r.RelationshipId == relationship.RelationshipId) == 1;
                this.RenderCheckboxItem(
                    builder,
                    0,
                    ":relationship_",
                    relationship.RelationshipId.ToString(CultureInfo.InvariantCulture),
                    relationship.Name,
                    selected);
            }

            builder.CloseRegion();
        }

        /// <summary>Records the selected visibility mode and applies it.</summary>
        /// <param name="mode">The selected mode.</param>
        /// <remarks>
        /// This replaces the Web Forms postback: the posted values are assembled from the current
        /// state of the control and handed to <see cref="LoadPostData"/>, which is where the posted
        /// value shape is interpreted, so overrides of it keep working.
        /// </remarks>
        private void OnModeSelected(UserVisibilityMode mode)
        {
            this.PostValues[this.UniqueID] = ((int)mode).ToString(CultureInfo.InvariantCulture);
            this.Commit();
        }

        private void OnMemberToggled(string prefix, string value, ChangeEventArgs args)
        {
            var key = this.UniqueID + prefix + value;
            var isChecked = args?.Value is bool boolValue
                ? boolValue
                : !string.IsNullOrEmpty(args?.Value?.ToString());

            // A checkbox only posted a value when it was checked, and LoadPostData tests for the
            // presence of the key, so an unchecked box has to remove it again.
            if (isChecked)
            {
                this.PostValues[key] = value;
            }
            else
            {
                this.PostValues.Remove(key);
            }

            this.PostValues[this.UniqueID] = ((int)UserVisibilityMode.FriendsAndGroups).ToString(CultureInfo.InvariantCulture);
            this.Commit();
        }

        private void Commit()
        {
            if (this.LoadPostData(this.UniqueID, this.PostValues))
            {
                this.RaisePostDataChangedEvent();
            }
        }
    }
}
