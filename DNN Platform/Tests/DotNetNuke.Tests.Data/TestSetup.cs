// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

using System;
using System.IO;

using DotNetNuke.Tests.Utilities;

using NUnit.Framework;

[SetUpFixture]
public class TestSetup
{
    [OneTimeSetUp]
    public void SetUp()
    {
        AppConfigLoader.Apply(Path.Combine(AppContext.BaseDirectory, "Test.dnn.config"));
    }
}
