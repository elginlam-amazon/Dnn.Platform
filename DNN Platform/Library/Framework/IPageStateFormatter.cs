// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Framework
{
    /// <summary>Turns a <see cref="PageState"/> into text and back.</summary>
    /// <remarks>
    /// Replaces <c>System.Web.UI.IStateFormatter</c> (whose implementation was <c>LosFormatter</c>,
    /// which does not exist on modern .NET and was built on <c>BinaryFormatter</c>, removed for
    /// security reasons).
    /// </remarks>
    public interface IPageStateFormatter
    {
        /// <summary>Serializes the given state.</summary>
        /// <param name="state">The state to serialize.</param>
        /// <returns>The serialized state.</returns>
        string Serialize(PageState state);

        /// <summary>Deserializes state produced by <see cref="Serialize"/>.</summary>
        /// <param name="serializedState">The serialized state.</param>
        /// <returns>The deserialized state.</returns>
        PageState Deserialize(string serializedState);
    }
}
