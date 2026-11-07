// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.Design.WebControls
{
    using System.Collections.Generic;
    using System.ComponentModel;

    using DotNetNuke.UI.Utilities;
    using DotNetNuke.UI.WebControls;

    /// <summary>
    /// Supplies the list of actions a <see cref="DNNToolBarButton"/> can be bound to.
    /// </summary>
    /// <remarks>
    /// <see cref="TypeConverter"/> itself exists on .NET 10, so this class keeps deriving from it
    /// and keeps all of its behavior. Only the <c>System.Design</c>/<c>System.Web.UI</c> coupling
    /// was removed: the attached control is now an <see cref="IClientApiControl"/> instead of a
    /// <c>System.Web.UI.Control</c>, and the <c>Microsoft.VisualBasic.Collection</c> the standard
    /// values were accumulated in became a <see cref="List{T}"/>.
    /// </remarks>
    public class DNNToolBarButtonActionTypeConverter : TypeConverter
    {
        private IClientApiControl attached;

        /// <inheritdoc/>
        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            var values = new List<string>();
            var commonActions = ((DNNToolBarButton)context.Instance).CommonActions;
            if (commonActions is not null)
            {
                foreach (var item in commonActions)
                {
                    values.Add(item);
                }
            }

            if (this.SupportsActions(context))
            {
                foreach (var action in ((IDNNToolBarSupportedActions)this.AttachedControl(context)).Actions)
                {
                    values.Add(action);
                }
            }

            return new StandardValuesCollection(values);
        }

        /// <inheritdoc/>
        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
        {
            return false;
        }

        /// <inheritdoc/>
        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            return this.SupportsActions(context);
        }

        /// <summary>Gets, and caches, the control the designed button's tool bar is attached to.</summary>
        /// <param name="context">The descriptor context of the conversion.</param>
        /// <returns>The attached control, or <see langword="null"/> when there is none.</returns>
        private IClientApiControl AttachedControl(ITypeDescriptorContext context)
        {
            if (this.attached is null)
            {
                var parent = (context?.Instance as DNNToolBarButton)?.Parent as DNNToolBar;
                this.attached = parent?.AttachedControl;
            }

            return this.attached;
        }

        /// <summary>Gets a value indicating whether the attached control publishes its own actions.</summary>
        /// <param name="context">The descriptor context of the conversion.</param>
        /// <returns><see langword="true"/> when the attached control implements <see cref="IDNNToolBarSupportedActions"/>.</returns>
        private bool SupportsActions(ITypeDescriptorContext context)
        {
            return this.AttachedControl(context) is IDNNToolBarSupportedActions;
        }
    }
}
