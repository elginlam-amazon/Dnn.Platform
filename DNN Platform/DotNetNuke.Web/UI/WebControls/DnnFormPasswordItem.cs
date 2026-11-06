// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using System;
    using System.Globalization;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.Services.ClientDependency;
    using DotNetNuke.UI;
    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.JSInterop;

    using FileOrder = DotNetNuke.Abstractions.ClientResources.FileOrder;

    /// <summary>A password control.</summary>
    /// <remarks>
    /// The <c>TextBox</c> in password mode became a <see cref="DnnTextBox"/> inside a
    /// <see cref="DnnPanel"/>, exactly the hierarchy the Web Forms version created. The password
    /// strength widget was wired up by a startup script registered through
    /// <c>ScriptManager</c>/<c>Page.ClientScript</c>; neither exists on ASP.NET Core, so the same
    /// <c>dnn.initializePasswordStrength</c> call is made once through JS interop after the first
    /// render — which is the point in the Blazor lifecycle at which the element exists.
    /// </remarks>
    public class DnnFormPasswordItem : DnnFormItemBase
    {
        private readonly IClientResourceController clientResourceController;
        private DnnTextBox password;

        /// <summary>Initializes a new instance of the <see cref="DnnFormPasswordItem"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IApplicationStatusInfo. Scheduled removal in v12.0.0.")]
        public DnnFormPasswordItem()
            : this(null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnFormPasswordItem"/> class.</summary>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="clientResourceController">The client resource controller.</param>
        public DnnFormPasswordItem(IApplicationStatusInfo appStatus, IEventLogger eventLogger, IClientResourceController clientResourceController)
            : base(appStatus ?? Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>(), eventLogger ?? Globals.GetCurrentServiceProvider().GetRequiredService<IEventLogger>())
        {
            this.clientResourceController = clientResourceController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IClientResourceController>();
        }

        /// <summary>Gets or sets the CSS class for the text box.</summary>
        [Parameter]
        public string TextBoxCssClass { get; set; } = string.Empty;

        /// <summary>Gets or sets the CSS class for the container.</summary>
        [Parameter]
        public string ContainerCssClass { get; set; } = string.Empty;

        /// <summary>Gets or sets the JavaScript runtime used to initialize the strength indicator.</summary>
        [Inject]
        protected IJSRuntime JsRuntime { get; set; }

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        /// <inheritdoc />
        protected override IDnnControl CreateControlInternal(IDnnControl container)
        {
            this.password = new DnnTextBox()
            {
                ID = this.ID + "_TextBox",
                TextMode = "password",
                CssClass = this.TextBoxCssClass,
                MaxLength = 39, // ensure password cannot be cut if too long
                Text = Convert.ToString(this.Value, CultureInfo.InvariantCulture), // Load from the item's value
            };
            this.password.Attributes["autocomplete"] = "off";
            this.password.Attributes["aria-label"] = this.DataField;
            this.password.TextChanged += this.TextChanged;

            var passwordContainer = new DnnPanel() { ID = "passwordContainer", CssClass = this.ContainerCssClass };

            // add control hierarchy to the container
            container?.Controls.Add(passwordContainer);

            passwordContainer.Controls.Add(this.password);

            // return input control that can be used for validation
            return this.password;
        }
        #pragma warning restore BL0005

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            this.clientResourceController.RegisterScript("~/Resources/Shared/scripts/dnn.jquery.extensions.js");
            this.clientResourceController.RegisterScript("~/Resources/Shared/scripts/dnn.jquery.tooltip.js");
            this.clientResourceController.RegisterScript("~/Resources/Shared/scripts/dnn.PasswordStrength.js");

            this.clientResourceController.RegisterStylesheet("~/Resources/Shared/stylesheets/dnn.PasswordStrength.css", FileOrder.Css.ResourceCss);

            JavaScript.RequestRegistration(this.AppStatus, this.EventLogger, this.PortalSettings, CommonJs.DnnPlugins);
        }

        /// <inheritdoc />
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            if (!firstRender || this.JsRuntime == null)
            {
                return;
            }

            var options = new DnnPaswordStrengthOptions();
            var optionsAsJsonString = Json.Serialize(options);
            await this.JsRuntime.InvokeVoidAsync(
                "dnn.initializePasswordStrengthJson",
                $".{this.TextBoxCssClass}",
                optionsAsJsonString);
        }

        private void TextChanged(object sender, EventArgs e)
        {
            this.UpdateDataSource(this.Value, this.password.Text, this.DataField);
        }
    }
}
