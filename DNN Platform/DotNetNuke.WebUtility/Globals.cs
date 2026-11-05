// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;

    /// <summary>
    /// Helpers that walk the control tree and read/write control attributes.
    /// </summary>
    /// <remarks>
    /// The WebForms implementation worked on <c>System.Web.UI.Control</c>, <c>WebControl</c> and
    /// <c>HtmlControl</c>; all of those are replaced by <see cref="IClientApiControl"/>, whose single
    /// <see cref="IClientApiControl.Attributes"/> bag removes the need for the original type switch.
    /// </remarks>
    public class Globals
    {
        private static HashSet<string> aryScripts;

        /// <summary>
        /// Searches control hierarchy from top down to find a control matching the passed in name.
        /// </summary>
        /// <param name="objParent">Root control to begin searching.</param>
        /// <param name="strControlName">Name of control to look for.</param>
        /// <returns>The matching control, or <see langword="null"/> when none was found.</returns>
        /// <remarks>
        /// This differs from FindControlRecursive in that it looks down the control hierarchy, whereas, the
        /// FindControlRecursive starts at the passed in control and walks the tree up. Therefore, this function is
        /// more a expensive task.
        /// </remarks>
        public static IClientApiControl FindControlRecursive(IClientApiControl objParent, string strControlName)
        {
            return FindControlRecursive(objParent, strControlName, string.Empty);
        }

        /// <summary>
        /// Searches control hierarchy from top down to find a control matching the passed in name and client id.
        /// </summary>
        /// <param name="objParent">Root control to begin searching.</param>
        /// <param name="strControlName">Name of control to look for.</param>
        /// <param name="strClientId">
        /// When not empty, the client id the found control must have; a control with a different client id is
        /// discarded and the search continues.
        /// </param>
        /// <returns>The matching control, or <see langword="null"/> when none was found.</returns>
        /// <remarks>
        /// The children of a control are only visited when it reports
        /// <see cref="IClientApiControl.HasChildren"/>: in WebForms, touching the controls collection of
        /// controls like the DataList could cause problems with ViewState, and that guard is preserved.
        /// </remarks>
        public static IClientApiControl FindControlRecursive(IClientApiControl objParent, string strControlName, string strClientId)
        {
            ArgumentNullException.ThrowIfNull(objParent);

            var objCtl = objParent.FindControl(strControlName);
            if (objCtl is null)
            {
                foreach (var objChild in objParent.Children)
                {
                    if (objChild is not null && objChild.HasChildren)
                    {
                        objCtl = FindControlRecursive(objChild, strControlName, strClientId);
                    }

                    if (objCtl is not null && !string.IsNullOrEmpty(strClientId) && objCtl.ClientId != strClientId)
                    {
                        objCtl = null;
                    }

                    if (objCtl is not null)
                    {
                        break;
                    }
                }
            }

            return objCtl;
        }

        /// <summary>Gets the value of an attribute of the given control.</summary>
        /// <param name="objControl">The control to read the attribute from.</param>
        /// <param name="strAttr">The name of the attribute.</param>
        /// <returns>The value of the attribute, or <see langword="null"/> when it is not set.</returns>
        public static string GetAttribute(IClientApiControl objControl, string strAttr)
        {
            var attributes = objControl?.Attributes;
            if (attributes is null)
            {
                return null;
            }

            return attributes.TryGetValue(strAttr, out var value) ? value : null;
        }

        /// <summary>Appends a value to an attribute of the given control, setting it when it is not present yet.</summary>
        /// <param name="objControl">The control to write the attribute on.</param>
        /// <param name="strAttr">The name of the attribute.</param>
        /// <param name="strValue">The value to append to the attribute's current value.</param>
        public static void SetAttribute(IClientApiControl objControl, string strAttr, string strValue)
        {
            var attributes = objControl?.Attributes;
            if (attributes is null)
            {
                return;
            }

            var strOrigVal = GetAttribute(objControl, strAttr);
            if (!string.IsNullOrEmpty(strOrigVal))
            {
                strValue = strOrigVal + strValue;
            }

            attributes[strAttr] = strValue;
        }

        /// <summary>Determines whether the given script name is one of the scripts embedded in this assembly.</summary>
        /// <param name="key">The file name of the script, for example <c>dnn.js</c>.</param>
        /// <returns><see langword="true"/> when the script is embedded in this assembly.</returns>
        /// <remarks>
        /// The WebForms implementation hard coded the list of the ten <c>dnn.*.js</c> scripts; the list is now
        /// read from the assembly's manifest resources so that it can never drift from what is actually
        /// embedded. The <c>MicrosoftAjax*.js</c> resources are excluded, as they were in the original list.
        /// </remarks>
        public static bool IsEmbeddedScript(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            aryScripts ??= LoadEmbeddedScriptNames();

            return aryScripts.Contains(key);
        }

        private static HashSet<string> LoadEmbeddedScriptNames()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var resourceName in Assembly.GetExecutingAssembly().GetManifestResourceNames())
            {
                if (!resourceName.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // The resource name is the file path with the separators replaced by dots, so the
                // file name is everything after the "js." folder segment.
                var folderIndex = resourceName.IndexOf("js.", StringComparison.OrdinalIgnoreCase);
                var fileName = folderIndex >= 0
                    ? resourceName.Substring(folderIndex + 3)
                    : Path.GetFileName(resourceName);

                if (fileName.StartsWith("dnn.", StringComparison.OrdinalIgnoreCase))
                {
                    names.Add(fileName);
                }
            }

            return names;
        }
    }
}
