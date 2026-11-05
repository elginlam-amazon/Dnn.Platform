// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Utilities
{
    /// <summary>
    /// A hidden input that never uses a naming container, so that the identifier it renders with
    /// client side is always exactly its own identifier.
    /// </summary>
    /// <remarks>
    /// We do not want the control holding the <c>__dnnVariable</c> state to ever use a naming
    /// container, because the client side script addresses it by its plain name. Replaces the
    /// original <c>System.Web.UI.HtmlControls.HtmlInputHidden</c> derived control.
    /// </remarks>
    public class NonNamingHiddenInput : ClientApiControlBase, IClientApiHiddenInput
    {
        private bool valueSet;
        private string value;

        /// <summary>Gets or sets the value of the hidden input.</summary>
        /// <remarks>
        /// Until the value has been set server side, the value posted by the client (which is keyed
        /// by the plain identifier of this control, since it has no naming container) wins.
        /// </remarks>
        public string Value
        {
            get
            {
                if (this.valueSet == false)
                {
                    var posted = this.Page?.GetRequestValue(this.Id);
                    if (string.IsNullOrEmpty(posted) == false)
                    {
                        return posted;
                    }
                }

                return this.value;
            }

            set
            {
                this.valueSet = true;
                this.value = value;
            }
        }

        /// <inheritdoc/>
        /// <remarks>This control suppresses naming containers altogether.</remarks>
        public override IClientApiControl NamingContainer => null;
    }
}
