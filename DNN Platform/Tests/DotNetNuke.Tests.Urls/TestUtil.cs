// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Urls
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;

    using DotNetNuke.Entities.Users;
    using DotNetNuke.Tests.Utilities;

    public class TestUtil
    {
        internal static string EmbeddedFilePath
        {
            get { return "DotNetNuke.Tests.Urls.TestFiles"; }
        }

        /// <summary>Gets the directory holding the CSV test-case sources and the SQL set-up scripts.</summary>
        /// <remarks>
        /// On net48 this walked up from <c>Assembly.CodeBase</c> to the first "bin" segment, which
        /// landed on the project directory and read <c>TestFiles</c> straight out of the source
        /// tree. <c>Assembly.CodeBase</c> throws on modern .NET, so the files are now copied next to
        /// the test assembly (see the <c>TestFiles\**</c> item in the project file) and resolved
        /// from <see cref="AppContext.BaseDirectory"/> — the same approach
        /// <c>DnnUnitTest.OneTimeSetUp</c> uses. Composed with <see cref="Path.Combine(string, string)"/>
        /// so the path is correct on Linux as well as Windows.
        /// </remarks>
        internal static string FilePath
        {
            get { return Path.Combine(Path.GetFullPath(AppContext.BaseDirectory), "TestFiles"); }
        }

        internal static void AddUser(int portalId, string userName, string password, string vanityUrl)
        {
            var user = UserController.GetUserByName(portalId, userName);
            if (user == null)
            {
                // Add User
                user = new UserInfo
                {
                    PortalID = portalId,
                    Username = userName,
                    Email = userName + "@changeme.invalid",
                    VanityUrl = vanityUrl,
                    Membership = { Password = password, Approved = true },
                };
                UserController.CreateUser(ref user);
            }
            else
            {
                // Update User
                user.VanityUrl = vanityUrl;
                user.IsDeleted = false;
                UserController.UpdateUser(portalId, user);
            }
        }

        internal static void DeleteUser(int portalId, string userName)
        {
            var user = UserController.GetUserByName(portalId, userName);
            if (user != null)
            {
                UserController.RemoveUser(user);
            }
        }

        internal static string GetEmbeddedFileName(string fileName)
        {
            string fullName = string.Format("{0}.{1}", EmbeddedFilePath, fileName);
            if (!fullName.ToLowerInvariant().EndsWith(".csv"))
            {
                fullName += ".csv";
            }

            return fullName;
        }

        internal static Stream GetEmbeddedFileStream(string fileName)
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceStream(GetEmbeddedFileName(fileName));
        }

        internal static void GetReplaceCharDictionary(Dictionary<string, string> testFields, Dictionary<string, string> replaceCharacterDictionary)
        {
            string replaceCharWithChar = testFields.GetValue("ReplaceChars");

            if (!string.IsNullOrEmpty(replaceCharWithChar))
            {
                string[] pairs = replaceCharWithChar.Split(';');
                foreach (string pair in pairs)
                {
                    string[] vals = pair.Split(':');
                    string key = null;
                    string val = null;
                    if (vals.GetUpperBound(0) >= 0)
                    {
                        key = vals[0];
                    }

                    if (vals.GetUpperBound(0) >= 1)
                    {
                        val = vals[1];
                    }

                    if (key != null && val != null)
                    {
                        replaceCharacterDictionary.Add(key, val);
                    }
                }
            }
        }

        internal static string ReadStream(string fileName)
        {
            return Util.ReadStream(FilePath, fileName);
        }

        internal static void ReadStream(string fileName, Action<string, string> onReadLine)
        {
            Util.ReadStream(FilePath, fileName, onReadLine);
        }
    }
}
