// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using System;
    using System.Globalization;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Common;
    using DotNetNuke.UI;

    using Microsoft.Extensions.DependencyInjection;

    /// <summary>A literal control.</summary>
    /// <remarks>
    /// The item renders its value as a read-only label. The <c>ViewStateMode.Disabled</c> the Web
    /// Forms version set is moot: there is no view state on ASP.NET Core.
    /// </remarks>
    public class DnnFormLiteralItem : DnnFormItemBase
    {
        /// <summary>Initializes a new instance of the <see cref="DnnFormLiteralItem"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IApplicationStatusInfo. Scheduled removal in v12.0.0.")]
        public DnnFormLiteralItem()
            : this(Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>(), Globals.GetCurrentServiceProvider().GetRequiredService<IEventLogger>())
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnFormLiteralItem"/> class.</summary>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        public DnnFormLiteralItem(IApplicationStatusInfo appStatus, IEventLogger eventLogger)
            : base(appStatus, eventLogger)
        {
        }

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        /// <inheritdoc />
        protected override IDnnControl CreateControlInternal(IDnnControl container)
        {
            var literal = new DnnLabel
            {
                ID = this.ID + "_Label",
                Text = Convert.ToString(this.Value, CultureInfo.InvariantCulture),
                Localize = false,
            };

            container?.Controls.Add(literal);
            return literal;
        }
        #pragma warning restore BL0005
    }
}
