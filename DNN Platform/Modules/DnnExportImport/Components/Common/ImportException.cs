// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.ExportImport.Components.Common;

using System;

/// <summary>An exception thrown when issues are encountered importing.</summary>
public class ImportException : Exception
{
    /// <inheritdoc />
    public ImportException()
    {
    }

    /// <inheritdoc />
    public ImportException(string message)
        : base(message)
    {
    }

    /// <inheritdoc />
    public ImportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
