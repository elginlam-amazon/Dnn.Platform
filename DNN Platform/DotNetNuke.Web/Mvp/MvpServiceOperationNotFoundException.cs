// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvp
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Text.Json;
    using System.Threading.Tasks;

    using DotNetNuke.Internal.SourceGenerators;

    using Microsoft.AspNetCore.Http;

    /// <summary>The error raised when a service is asked for an operation it does not expose.</summary>
    [DnnDeprecated(9, 2, 0, "Replace WebFormsMvp and DotNetNuke.Web.Mvp with MVC or SPA patterns instead")]
    public partial class MvpServiceOperationNotFoundException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="MvpServiceOperationNotFoundException"/> class.</summary>
        public MvpServiceOperationNotFoundException()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="MvpServiceOperationNotFoundException"/> class.</summary>
        /// <param name="message">The error message.</param>
        public MvpServiceOperationNotFoundException(string message)
            : base(message)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="MvpServiceOperationNotFoundException"/> class.</summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The underlying error.</param>
        public MvpServiceOperationNotFoundException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
