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

    using DotNetNuke.Internal.SourceGenerators;

    /// <summary>The error raised when a view cannot be bound to a presenter.</summary>
    [DnnDeprecated(9, 2, 0, "Replace WebFormsMvp and DotNetNuke.Web.Mvp with MVC or SPA patterns instead")]
    public partial class MvpPresenterBindingException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="MvpPresenterBindingException"/> class.</summary>
        public MvpPresenterBindingException()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="MvpPresenterBindingException"/> class.</summary>
        /// <param name="message">The error message.</param>
        public MvpPresenterBindingException(string message)
            : base(message)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="MvpPresenterBindingException"/> class.</summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The underlying error.</param>
        public MvpPresenterBindingException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
