//
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
//
namespace DotNetNuke.Tests.UI.WatiN.Utilities;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

using DotNetNuke.Common;
using DotNetNuke.Common.Options;
using DotNetNuke.Services.Installer;
using DotNetNuke.Tests.Instance.Utilities;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

/// <summary>Helpers that read and update the website under test's <c>web.config</c>.</summary>
public static class WebConfigManager
{
    /// <summary>The configuration key holding the machine key validation key for the test host.</summary>
    public const string MachineKeyValidationKeyName = "MachineKey:ValidationKey";

    /// <summary>The configuration key holding the machine key decryption key for the test host.</summary>
    public const string MachineKeyDecryptionKeyName = "MachineKey:DecryptionKey";

    private const string WebConfigFileName = "web.config";

    /// <summary>
    /// Gets the validation key currently in effect for this process, as last synchronized from
    /// the site's <c>web.config</c> by <see cref="SyncConfig"/>.
    /// </summary>
    /// <value>The validation key, or <see langword="null"/> when none has been configured.</value>
    public static string MachineKeyValidationKey => TestConfiguration.GetSetting(MachineKeyValidationKeyName);

    /// <summary>
    /// Gets the decryption key currently in effect for this process, as last synchronized from
    /// the site's <c>web.config</c> by <see cref="SyncConfig"/>.
    /// </summary>
    /// <value>The decryption key, or <see langword="null"/> when none has been configured.</value>
    public static string MachineKeyDecryptionKey => TestConfiguration.GetSetting(MachineKeyDecryptionKeyName);

    /// <summary>Gets the physical path of the website under test.</summary>
    /// <returns>
    /// The configured <c>DefaultPhysicalAppPath</c> test setting when present, otherwise the
    /// current directory with the test-fixtures folder swapped for the website folder.
    /// </returns>
    public static string GetWebPath()
    {
        return TestConfiguration.DefaultPhysicalAppPath
               ?? Directory.GetCurrentDirectory().Replace("\\Tests\\Fixtures", "\\Website");
    }

    /// <summary>Updates the web.config so that the site will run in full trust.</summary>
    public static void UpdateConfigForFullTrust()
    {
        UpdateConfigForFullTrust(GetWebPath());
    }

    /// <summary>Updates the web.config so that the site will run in medium trust.</summary>
    public static void UpdateConfigForMediumTrust()
    {
        SetTrustLevel(GetWebPath(), "Medium");
    }

    /// <summary>Updates the web.config file to drop emails to a local folder.</summary>
    /// <param name="mailDropPath">The path to the mailDrop.xml file that contains the xml for the mail dump.</param>
    /// <param name="emailPath">The path that emails will be sent to.</param>
    public static void UpdateConfigForMailDrop(string mailDropPath, string emailPath)
    {
        var physicalPath = GetWebPath();
        var mailDropFragment = XDocument.Load(Path.Combine(mailDropPath, "mailDrop.xml"));
        var specifiedPickupDirectory = mailDropFragment.XPathSelectElement("configuration/nodes/node/system.net/mailSettings/smtp/specifiedPickupDirectory");
        specifiedPickupDirectory.Attribute("pickupDirectoryLocation").Value = emailPath;
        mailDropFragment.Save(Path.Combine(physicalPath, "UpdatedMailDrop.xml"));
        var mailDrop = new FileStream(Path.Combine(physicalPath, "UpdatedMailDrop.xml"), FileMode.Open, FileAccess.Read);
        try
        {
            var fileName = Path.Combine(physicalPath, WebConfigFileName);
            var targetDocument = new XmlDocument();
            using (var xmlReader = XmlReader.Create(fileName, new XmlReaderSettings { XmlResolver = null, }))
            {
                targetDocument.Load(xmlReader);
            }

            var mailNodes = (from mail in targetDocument.DocumentElement.ChildNodes.Cast<XmlNode>()
                where mail.Name == "system.net"
                select mail).ToList();
            if (mailNodes.Count == 0)
            {
                var merge = new XmlMerge(mailDrop, string.Empty, string.Empty);
                merge.UpdateConfig(targetDocument);
                targetDocument.Save(fileName);
            }
        }
        finally
        {
            mailDrop.Close();
        }
    }

    /// <summary>Updates the web.config so that the site will run in full trust.</summary>
    /// <param name="physicalPath">The path for the folder containing the web.config.</param>
    public static void UpdateConfigForFullTrust(string physicalPath)
    {
        SetTrustLevel(physicalPath, "Full");
    }

    /// <summary>
    /// Copies the site's machine key into the test host's own configuration so that the test
    /// process signs and encrypts payloads with the same keys as the site under test.
    /// </summary>
    /// <param name="sitePath">The path of the folder containing the site's <c>web.config</c>.</param>
    /// <remarks>
    /// When the site's <c>validationKey</c> differs from the one currently in effect, the site's
    /// <c>validationKey</c> and <c>decryptionKey</c> are written to the test host's
    /// <c>appsettings.json</c> under the <c>MachineKey</c> section that
    /// <see cref="MachineKeyOptions"/> binds (so a later run picks them up), and are made
    /// effective immediately for this process by publishing them to the process environment,
    /// dropping the cached <see cref="TestConfiguration.Configuration"/>, and reloading/clearing
    /// the bound <see cref="MachineKeyOptions"/> in the container when one has already been
    /// built. This replaces the .NET Framework implementation, which saved a
    /// <c>MachineKeySection</c> through <c>ConfigurationManager.OpenExeConfiguration</c> and then
    /// reset <c>System.Web</c>'s cached client runtime config by reflection.
    /// </remarks>
    public static void SyncConfig(string sitePath)
    {
        var webConfigPath = Path.Combine(sitePath, WebConfigFileName);
        var webConfig = XDocument.Load(webConfigPath);
        var webNode = webConfig.XPathSelectElement("configuration/system.web/machineKey");

        var siteValidationKey = webNode.Attribute("validationKey").Value;
        var siteDecryptionKey = webNode.Attribute("decryptionKey").Value;

        if (MachineKeyValidationKey == siteValidationKey)
        {
            return;
        }

        // Persist into the test host's appsettings.json, the modern stand-in for the
        // <system.web><machineKey> element that OpenExeConfiguration used to update.
        SaveHostMachineKey(siteValidationKey, siteDecryptionKey);

        // Make the new values effective for the current process. The environment-variable
        // configuration provider has the highest precedence in TestConfiguration, and clearing
        // the cached instance forces a rebuild that observes these values.
        Environment.SetEnvironmentVariable("MachineKey__ValidationKey", siteValidationKey);
        Environment.SetEnvironmentVariable("MachineKey__DecryptionKey", siteDecryptionKey);
        TestConfiguration.RegisterInstance(null);

        // And flush the copy already bound inside the container, if the fixture built one before
        // calling SyncConfig.
        ClearBoundMachineKeyOptions();
    }

    /// <summary>Marks the site's web.config as modified, triggering an application restart.</summary>
    /// <param name="sitePath">The path of the folder containing the site's <c>web.config</c>.</param>
    public static void TouchConfig(string sitePath)
    {
        var webConfigPath = Path.Combine(sitePath, WebConfigFileName);
        File.SetLastWriteTime(webConfigPath, DateTime.Now);
    }

    private static void SetTrustLevel(string physicalPath, string level)
    {
        var configPath = Path.Combine(physicalPath, WebConfigFileName);
        var webConfig = XDocument.Load(configPath);
        var trustLevel = webConfig.XPathSelectElement("configuration/system.web/trust");
        using (var outfile = new StreamWriter(Path.Combine(Directory.GetCurrentDirectory(), "log.txt")))
        {
            outfile.Write(physicalPath);
            outfile.Write(trustLevel.ToString());
        }

        trustLevel.Attribute("level").Value = level;
        webConfig.Save(configPath);
    }

    private static void SaveHostMachineKey(string validationKey, string decryptionKey)
    {
        // Persist into the very file TestConfiguration reads back (appsettings.json next to the
        // test host), under the "MachineKey" section that MachineKeyOptions binds. Writing a
        // <system.web><machineKey> XML file would be dead on modern .NET: nothing reads it.
        var hostConfigPath = GetHostConfigPath();

        JsonObject root = null;
        if (File.Exists(hostConfigPath))
        {
            var existing = File.ReadAllText(hostConfigPath);
            if (!string.IsNullOrWhiteSpace(existing))
            {
                root = JsonNode.Parse(existing) as JsonObject;
            }
        }

        root ??= new JsonObject();

        if (root["MachineKey"] is not JsonObject machineKey)
        {
            machineKey = new JsonObject();
            root["MachineKey"] = machineKey;
        }

        machineKey["ValidationKey"] = validationKey;
        machineKey["DecryptionKey"] = decryptionKey;

        File.WriteAllText(
            hostConfigPath,
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true, }));
    }

    private static string GetHostConfigPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    }

    private static void ClearBoundMachineKeyOptions()
    {
        // The in-process container (when one has already been built) caches the bound
        // MachineKeyOptions. Reloading the configuration root and clearing the monitor cache is
        // the modern equivalent of the net48
        // ConfigurationManager.RefreshSection("system.web/machineKey") call, so a SyncConfig that
        // runs after the container was built still takes effect.
        var provider = Globals.DependencyProvider;
        if (provider is null)
        {
            return;
        }

        if (provider.GetService(typeof(IConfiguration)) is IConfigurationRoot configurationRoot)
        {
            configurationRoot.Reload();
        }

        if (provider.GetService(typeof(IOptionsMonitorCache<MachineKeyOptions>)) is IOptionsMonitorCache<MachineKeyOptions> cache)
        {
            cache.Clear();
        }
    }
}
