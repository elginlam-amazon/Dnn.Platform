// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Services.Upgrade.Internals.Steps
{
    using System;
    using System.Globalization;
    using System.Web;

    using Localization = DotNetNuke.Services.Localization.Localization;

    /// <summary>Performs verifications about the IIS environment.</summary>
    public class IISVerificationStep : BaseInstallationStep
    {
        /// <summary>Executes verifications on the IIS environment.</summary>
        public override void Execute()
        {
            this.Status = StepStatus.Running;

            this.Details = Localization.GetString("CheckingIIS", this.LocalInstallResourceFile);

            // ASP.NET Core always runs the integrated pipeline, check the .NET runtime version instead of the .NET Framework 4.8 registry key.
            if (Environment.Version.Major < 10)
            {
                this.Errors.Add(Localization.GetString("DotNetVersion48Required", this.LocalInstallResourceFile));
                this.Status = StepStatus.Abort;
                return;
            }

            this.Status = StepStatus.Done;
        }
    }
}
