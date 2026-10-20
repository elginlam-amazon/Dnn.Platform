// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.Script.Services
{
    /// <summary>Specifies which HTTP verb is used to invoke a method from client script and the format of the response.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ScriptMethodAttribute : Attribute
    {
        /// <summary>Gets or sets a value indicating whether the method is invoked with HTTP GET.</summary>
        public bool UseHttpGet { get; set; }
    }
}
