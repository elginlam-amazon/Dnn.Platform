// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.Script.Services
{
    /// <summary>Indicates that a service can be called from client script.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
    public sealed class ScriptServiceAttribute : Attribute
    {
    }
}
