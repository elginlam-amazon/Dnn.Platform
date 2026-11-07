// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.PersonaBar.Extensions.Components.BulkInstall.Exceptions
{
    using System;

    using Dnn.PersonaBar.Extensions.Components.BulkInstall.DataAccess.Models;

    /// <summary>An exception indicating that an <see cref="IPSpec"/> already exists.</summary>
    public class IPSpecExistsException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="IPSpecExistsException"/> class.</summary>
        public IPSpecExistsException()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="IPSpecExistsException"/> class with a specified error message.</summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        public IPSpecExistsException(string message)
            : base(message)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="IPSpecExistsException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.</summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or <see langword="null"/> if no inner exception is specified.</param>
        public IPSpecExistsException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
