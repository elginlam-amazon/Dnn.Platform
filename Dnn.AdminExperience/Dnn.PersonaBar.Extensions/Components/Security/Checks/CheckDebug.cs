// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.PersonaBar.Security.Components.Checks
{
    using System.Diagnostics;
    using System.Globalization;
    using System.Reflection;

    using DotNetNuke.Common;

    /// <summary>
    /// Audits whether the application is running a debug build.
    /// </summary>
    /// <remarks>
    /// On .NET Framework this check read <c>HttpContext.Current.IsDebuggingEnabled</c>, which is
    /// nothing more than the value of <c>&lt;compilation debug="true"&gt;</c> in <c>web.config</c>:
    /// it reported that the site was compiled in debug mode (no JIT optimizations, verbose error
    /// output, timeouts disabled). ASP.NET Core does not compile pages at runtime and honours no
    /// <c>&lt;system.web/compilation&gt;</c> section, so the same fact is determined from the
    /// compiled output itself: a debug build carries a <see cref="DebuggableAttribute"/> with the
    /// JIT optimizer disabled. That is exactly the condition the legacy flag described, so the
    /// severity this check reports is unchanged (<see cref="SeverityEnum.Warning"/> when debugging
    /// is enabled, <see cref="SeverityEnum.Pass"/> when it is not).
    /// </remarks>
    public class CheckDebug : IAuditCheck
    {
        /// <inheritdoc />
        public string Id => "CheckDebug";

        /// <inheritdoc />
        public bool LazyLoad => false;

        /// <inheritdoc />
        public CheckResult Execute()
        {
            var result = new CheckResult(SeverityEnum.Unverified, this.Id);

            // The hosting application assembly is the equivalent of the legacy web application
            // whose <compilation debug="..."/> setting the Framework check reported on. Fall back
            // to the DNN platform assembly when there is no managed entry assembly (for example
            // when hosted by a native host or exercised from a test runner).
            var applicationAssembly = Assembly.GetEntryAssembly() ?? typeof(Globals).Assembly;
            var debuggable = applicationAssembly.GetCustomAttribute<DebuggableAttribute>();

            // No attribute at all means the compiler emitted an optimized (release) assembly.
            var isDebugBuild = debuggable != null &&
                               (debuggable.IsJITOptimizerDisabled ||
                                (debuggable.DebuggingFlags & DebuggableAttribute.DebuggingModes.DisableOptimizations) != 0);

            var isDebuggerAttached = Debugger.IsAttached;

            result.Severity = isDebugBuild || isDebuggerAttached
                ? SeverityEnum.Warning
                : SeverityEnum.Pass;

            if (isDebugBuild)
            {
                result.Notes.Add(string.Format(
                    CultureInfo.CurrentCulture,
                    "The application assembly '{0}' was compiled in debug mode (JIT optimizations disabled).",
                    applicationAssembly.GetName().Name));
            }

            if (isDebuggerAttached)
            {
                result.Notes.Add("A debugger is currently attached to the application process.");
            }

            return result;
        }
    }
}
