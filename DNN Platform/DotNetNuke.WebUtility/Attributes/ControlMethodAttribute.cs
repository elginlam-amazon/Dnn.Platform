// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Utilities
{
    using System;

    /// <summary>
    /// Marks a method of a control as being callable from the client through the Client API.
    /// </summary>
    /// <remarks>
    /// Only methods carrying this attribute may be invoked by
    /// <see cref="ClientAPI"/>'s control method execution, which keeps arbitrary methods from
    /// being reachable from the browser.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class ControlMethodAttribute : Attribute
    {
        /// <summary>Initializes a new instance of the <see cref="ControlMethodAttribute"/> class.</summary>
        public ControlMethodAttribute()
        {
        }
    }
}
