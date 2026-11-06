// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Utilities;

using System;
using System.IO;

/// <summary>Base class for test fixtures that need the website's app path and physical path.</summary>
public class DnnUnitTest
{
    /// <summary>Gets or sets the website's application (virtual) path.</summary>
    public string WebsiteAppPath { get; set; }

    /// <summary>Gets or sets the website's physical application path.</summary>
    public string WebsitePhysicalAppPath { get; set; }

    /// <summary>One-time fixture setup: resolves the website paths from the test host's location.</summary>
    public virtual void OneTimeSetUp()
    {
        // Assembly.CodeBase is .NET Framework-only (it throws on modern .NET); AppContext.BaseDirectory
        // is the directory the test host runs from, which is what the old Uri/CodeBase dance produced.
        // No URL-decoding here: the old code decoded Uri.AbsolutePath (where escaping was real),
        // whereas AppContext.BaseDirectory is already an unescaped filesystem path — decoding it
        // would corrupt any path containing '+' or '%'.
        var path = Path.GetFullPath(AppContext.BaseDirectory);

        this.WebsiteAppPath = "http://localhost/DNN_Platform";
        var websiteRootPath = path.Substring(0, path.IndexOf("DNN Platform", StringComparison.Ordinal));
        this.WebsitePhysicalAppPath = Path.Combine(websiteRootPath, "Website");
    }
}
