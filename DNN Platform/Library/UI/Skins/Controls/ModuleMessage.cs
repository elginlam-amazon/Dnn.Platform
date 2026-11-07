// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Skins.Controls
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;

    using DotNetNuke.Services.Exceptions;

    using Microsoft.AspNetCore.Components;
    using Microsoft.JSInterop;

    /// <summary>A skin object to display a message from a module.</summary>
    /// <remarks>
    /// The WebForms version was driven by <c>~/admin/skins/ModuleMessage.ascx</c>, which declared a
    /// <c>Panel</c> (<c>dnnSkinMessage</c>), two <c>Label</c>s (<c>lblHeading</c>,
    /// <c>lblMessage</c>) and a scroll script block. The same markup is produced here by the
    /// component itself; the protected fields are preserved because skins and third party code
    /// reference them.
    /// </remarks>
    public class ModuleMessage : SkinObjectBase
    {
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DnnPanel dnnSkinMessage;
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DnnLabel lblHeading;
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DnnLabel lblMessage;
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected IDnnControl scrollScript;

        /// <summary>Initializes a new instance of the <see cref="ModuleMessage"/> class.</summary>
        public ModuleMessage()
        {
            this.dnnSkinMessage = new DnnPanel { ID = "dnnSkinMessage", CssClass = "dnnFormMessage" };
            this.lblHeading = new DnnLabel { ID = "lblHeading", CssClass = "dnnFormMessageHeading", Visible = false };
            this.lblMessage = new DnnLabel { ID = "lblMessage" };
            this.scrollScript = new DnnPlaceHolder { ID = "scrollScript", Visible = false };

            this.dnnSkinMessage.Controls.Add(this.lblHeading);
            this.dnnSkinMessage.Controls.Add(this.lblMessage);
            this.dnnSkinMessage.Controls.Add(this.scrollScript);
            this.Controls.Add(this.dnnSkinMessage);
        }

        public enum ModuleMessageType
        {
            /// <summary>A green success message.</summary>
            GreenSuccess = 0,

            /// <summary>A yellow warning message.</summary>
            YellowWarning = 1,

            /// <summary>A red error message.</summary>
            RedError = 2,

            /// <summary>A blue info message.</summary>
            BlueInfo = 3,
        }

        /// <summary>Gets a value indicating whether check this message is shown as page message or module message.</summary>
        public bool IsModuleMessage
        {
            get
            {
                return this.Parent?.Id == "MessagePlaceHolder";
            }
        }

        /// <summary>Gets or sets the message text.</summary>
        [Parameter]
        public string Text { get; set; }

        /// <summary>Gets or sets the message heading.</summary>
        [Parameter]
        public string Heading { get; set; }

        /// <summary>Gets or sets the type of the message, which selects its styling.</summary>
        [Parameter]
        public ModuleMessageType IconType { get; set; }

        /// <summary>Gets or sets the URL of an icon to display with the message.</summary>
        [Parameter]
        public string IconImage { get; set; }

        /// <summary>Gets the JavaScript runtime used to scroll a module message into view.</summary>
        [Inject]
        protected IJSRuntime JavaScriptRuntime { get; set; }

        /// <summary>The Page_Load server event handler on this page is used to populate the role information for the page.</summary>
        /// <param name="e">The event arguments.</param>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            try
            {
                var strMessage = string.Empty;

                // check to see if a url
                // was passed in for an icon
                if (!string.IsNullOrEmpty(this.IconImage))
                {
                    strMessage += this.Text;
                    this.dnnSkinMessage.CssClass = "dnnFormMessage dnnFormWarning";
                }
                else
                {
                    switch (this.IconType)
                    {
                        case ModuleMessageType.GreenSuccess:
                            strMessage += this.Text;
                            this.dnnSkinMessage.CssClass = "dnnFormMessage dnnFormSuccess";
                            break;
                        case ModuleMessageType.YellowWarning:
                            strMessage += this.Text;
                            this.dnnSkinMessage.CssClass = "dnnFormMessage dnnFormWarning";
                            break;
                        case ModuleMessageType.BlueInfo:
                            strMessage += this.Text;
                            this.dnnSkinMessage.CssClass = "dnnFormMessage dnnFormInfo";
                            break;
                        case ModuleMessageType.RedError:
                            strMessage += this.Text;
                            this.dnnSkinMessage.CssClass = "dnnFormMessage dnnFormValidationSummary";
                            break;
                    }
                }

                this.lblMessage.Text = strMessage;

                if (!string.IsNullOrEmpty(this.Heading))
                {
                    this.lblHeading.Visible = true;
                    this.lblHeading.Text = this.Heading;
                }
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc, false);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// The WebForms version made the scroll script block visible when
        /// <c>Page.IsPostBack &amp;&amp; IsModuleMessage</c>. There are no postbacks on ASP.NET Core;
        /// a message that appears after the component's first render is the equivalent situation, and
        /// the scroll itself is performed through <see cref="IJSRuntime"/> because a Blazor component
        /// may not emit a <c>&lt;script&gt;</c> block.
        /// </remarks>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            var shouldScroll = !firstRender && this.IsModuleMessage;
            this.scrollScript.Visible = shouldScroll;

            if (shouldScroll && this.JavaScriptRuntime != null)
            {
                await this.JavaScriptRuntime.InvokeVoidAsync(
                    "eval",
                    $"(function(){{var el=document.getElementById('{this.dnnSkinMessage.ClientId}');if(el){{el.scrollIntoView({{behavior:'smooth',block:'center'}});}}}})();");
            }
        }
    }
}
