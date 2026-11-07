// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.PersonaBar.Security.Components.Checks
{
    using DotNetNuke.Common;

    /// <summary>
    /// Audits whether the integrity of round-tripped page state is cryptographically protected.
    /// </summary>
    /// <remarks>
    /// On .NET Framework this check read <c>Page.EnableViewStateMac</c> off the current handler:
    /// with the message authentication code switched off, an attacker could tamper with (or, with
    /// the MS14-059 class of bugs, forge) the <c>__VIEWSTATE</c> payload that the server
    /// deserializes. ASP.NET Core has no WebForms, no legacy page handler and no
    /// ViewState: there is no client round-tripped, server-deserialized state field at all, so the
    /// tampering surface this check guarded is structurally absent and the honest answer for the
    /// target framework is a definite <see cref="SeverityEnum.Pass"/>. (On Framework the check
    /// already returned its initial <see cref="SeverityEnum.Unverified"/> whenever the current
    /// handler was not a <c>Page</c> — which is the case for every Web API request, including the
    /// security-analyzer request itself — so a definite Pass is strictly more informative than
    /// what the legacy code reported in practice.)
    /// <para>
    /// The check is also only ever registered by <see cref="AuditChecks"/> when
    /// <see cref="Globals.NETFrameworkVersion"/> is 4.5.1 or lower, a condition that cannot hold
    /// on modern .NET; it is kept (rather than deleted) so an upgraded installation's audit report
    /// still accounts for the item, and so this reasoning stays discoverable.
    /// </para>
    /// </remarks>
    public class CheckViewstatemac : IAuditCheck
    {
        /// <inheritdoc />
        public string Id => "CheckViewstatemac";

        /// <inheritdoc />
        public bool LazyLoad => false;

        /// <inheritdoc />
        public CheckResult Execute()
        {
            var result = new CheckResult(SeverityEnum.Pass, this.Id);
            result.Notes.Add(
                "ViewState does not exist on ASP.NET Core, so no unauthenticated page state can be round-tripped to the server.");
            return result;
        }
    }
}
