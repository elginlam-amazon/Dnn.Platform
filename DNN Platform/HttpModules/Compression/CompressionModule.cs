// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.HttpModules.Compression
{
    using DotNetNuke.Internal.SourceGenerators;

    /// <summary>A deprecated type which used to compress responses.</summary>
    /// <remarks>
    /// This type was an <c>IHttpModule</c> with an empty <c>Init</c>, i.e. it had no behavior
    /// left, so nothing was lost in the move to ASP.NET Core and nothing is wired up in its
    /// place. A host that wants response compression opts in for itself with
    /// <c>services.AddResponseCompression()</c> / <c>app.UseResponseCompression()</c>; that is a
    /// new behavior, not a restoration of this type's.
    /// </remarks>
    [DnnDeprecated(9, 2, 0, "No replacement")]
    public partial class CompressionModule
    {
    }
}
