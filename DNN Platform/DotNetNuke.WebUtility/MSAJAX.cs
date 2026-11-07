// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Reflection;
    using System.Text.Json;

    /// <summary>
    /// Interop with the Microsoft AJAX (ASP.NET AJAX) client script libraries and with the
    /// JSON serialization the Client API uses to talk to them.
    /// </summary>
    /// <remarks>
    /// The <c>ScriptManager</c> integration is reached through reflection, exactly as in the
    /// original implementation: when the hosting framework does not provide
    /// <c>System.Web.UI.ScriptManager</c> (which is the case on ASP.NET Core)
    /// <see cref="IsInstalled"/> is <see langword="false"/> and the scripts are registered as plain
    /// client script includes instead. The JSON serialization, which was reached through reflection
    /// over <c>System.Web.Script.Serialization.JavaScriptSerializer</c>, now uses
    /// <see cref="System.Text.Json"/>.
    /// </remarks>
    public class MSAJAX
    {
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        private static Type scriptManagerType;
        private static Type scriptReferenceType;
        private static Type scriptReferenceCollectionType;
        private static int installed = -1;

        /// <summary>
        /// Gets a value indicating whether the Microsoft AJAX script manager of the hosting
        /// framework is available.
        /// </summary>
        /// <remarks>
        /// On ASP.NET Core there is no <c>System.Web.UI.ScriptManager</c> type, so this is
        /// <see langword="false"/> and the fallback registration paths (plain script includes plus a
        /// <c>Sys.Application.initialize();</c> startup script) are used, which is the behavior the
        /// original code had whenever MS AJAX was not installed.
        /// </remarks>
        public static bool IsInstalled
        {
            get
            {
                if (installed == -1)
                {
                    installed = ScriptManagerType is null ? 0 : 1;
                }

                return installed == 1;
            }
        }

        /// <summary>Gets the <c>System.Web.UI.ScriptManager</c> type, or <see langword="null"/> when it is not available.</summary>
        private static Type ScriptManagerType =>
            scriptManagerType ??= Reflection.CreateType("System.Web.UI.ScriptManager", false);

        /// <summary>Gets the <c>System.Web.UI.ScriptReferenceCollection</c> type, or <see langword="null"/> when it is not available.</summary>
        private static Type ScriptReferenceCollectionType =>
            scriptReferenceCollectionType ??= Reflection.CreateType("System.Web.UI.ScriptReferenceCollection", true);

        /// <summary>Gets the <c>System.Web.UI.ScriptReference</c> type, or <see langword="null"/> when it is not available.</summary>
        private static Type ScriptReferenceType =>
            scriptReferenceType ??= Reflection.CreateType("System.Web.UI.ScriptReference", true);

        /// <summary>
        /// Makes sure the Microsoft AJAX client framework is available on the given page, either
        /// through the script manager of the hosting framework or through plain script includes.
        /// </summary>
        /// <param name="objPage">The page being rendered.</param>
        public static void Register(IClientApiPage objPage)
        {
            ArgumentNullException.ThrowIfNull(objPage);

            if (IsInstalled)
            {
                if (ScriptManagerControl(objPage) is null)
                {
                    var sm = ScriptManagerControl(objPage) ?? Reflection.CreateInstance(ScriptManagerType);
                    if (sm is not null)
                    {
                        Reflection.SetProperty(ScriptManagerType, "ID", sm, new object[] { "ScriptManager" });
                        if (sm is IClientApiControl smControl && objPage.Form is not null)
                        {
                            objPage.Form.Children.Add(smControl);
                        }

                        if (objPage.Items is not null)
                        {
                            // Let DNN know we added it
                            objPage.Items["System.Web.UI.ScriptManager"] = true;
                        }
                    }
                }
            }
            else
            {
                if (ClientAPI.UseExternalScripts)
                {
                    MSAJAX.RegisterClientScript(objPage, ClientAPI.ScriptPath + "MicrosoftAjax.js");
                    MSAJAX.RegisterClientScript(objPage, ClientAPI.ScriptPath + "MicrosoftAjaxWebForms.js");
                }
                else
                {
                    MSAJAX.RegisterClientScript(objPage, "MicrosoftAjax.js", "DotNetNuke.WebUtility");
                    MSAJAX.RegisterClientScript(objPage, "MicrosoftAjaxWebForms.js", "DotNetNuke.WebUtility");
                }

                objPage.RegisterStartupScript(objPage.GetType(), "MSAJAXInit", "Sys.Application.initialize();", true);
            }
        }

        /// <summary>Sets a property on the script manager of the given page, when one is available.</summary>
        /// <param name="objPage">The page being rendered.</param>
        /// <param name="propertyName">The name of the property to set.</param>
        /// <param name="args">The value to set the property to.</param>
        public static void SetScriptManagerProperty(IClientApiPage objPage, string propertyName, object[] args)
        {
            if (IsInstalled)
            {
                Register(objPage);
                var sm = ScriptManagerControl(objPage);
                if (sm is not null)
                {
                    Reflection.SetProperty(ScriptManagerType, propertyName, sm, args);
                }
            }
        }

        /// <summary>Registers a script file by its path.</summary>
        /// <param name="objPage">The page being rendered.</param>
        /// <param name="path">The path of the script file.</param>
        public static void RegisterClientScript(IClientApiPage objPage, string path)
        {
            ArgumentNullException.ThrowIfNull(objPage);

            if (IsInstalled)
            {
                Register(objPage);
                var reference = GetScriptReference();
                MSAJAX.SetScriptReferenceProperty(reference, "Path", path);
                MSAJAX.SetScriptReferenceProperty(reference, "IgnoreScriptPath", false);
                RegisterClientScript(objPage, reference);
            }
            else
            {
                if (objPage.IsClientScriptBlockRegistered(path) == false)
                {
                    objPage.RegisterClientScriptInclude(path, path);
                }
            }
        }

        /// <summary>Registers an embedded script resource of an assembly.</summary>
        /// <param name="objPage">The page being rendered.</param>
        /// <param name="name">The name of the embedded script.</param>
        /// <param name="assembly">The name of the type/assembly holding the embedded script.</param>
        public static void RegisterClientScript(IClientApiPage objPage, string name, string assembly)
        {
            ArgumentNullException.ThrowIfNull(objPage);

            if (IsInstalled)
            {
                Register(objPage);
                var reference = GetScriptReference();
                MSAJAX.SetScriptReferenceProperty(reference, "Name", name);
                MSAJAX.SetScriptReferenceProperty(reference, "Assembly", assembly);
                RegisterClientScript(objPage, reference);
            }
            else
            {
                if (objPage.IsClientScriptBlockRegistered(name) == false)
                {
                    objPage.RegisterClientScriptResource(Reflection.CreateType(assembly, true), name);
                }
            }
        }

        /// <summary>Registers a script that runs once the page has loaded.</summary>
        /// <param name="objPage">The page being rendered.</param>
        /// <param name="key">The registration key.</param>
        /// <param name="script">The script to run.</param>
        public static void RegisterStartupScript(IClientApiPage objPage, string key, string script)
        {
            ArgumentNullException.ThrowIfNull(objPage);

            if (IsInstalled)
            {
                var methods = ScriptManagerType.GetMethods();
                foreach (var method in methods)
                {
                    if (method.Name == "RegisterStartupScript")
                    {
                        method.Invoke(null, new object[] { objPage, objPage.GetType(), key, script, false });
                        break;
                    }
                }
            }
            else
            {
                // The original called the 3-argument ClientScript.RegisterStartupScript overload,
                // i.e. addScriptTags: false - callers pass already-wrapped script text.
                objPage.RegisterStartupScript(objPage.GetType(), key, script, false);
            }
        }

        /// <summary>Deserializes the given JSON into the given type.</summary>
        /// <typeparam name="T">The type to deserialize into.</typeparam>
        /// <param name="data">The JSON to deserialize.</param>
        /// <returns>The deserialized object, or the default of <typeparamref name="T"/> when there is no data.</returns>
        public static T Deserialize<T>(string data)
        {
            if (string.IsNullOrEmpty(data))
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(data, SerializerOptions);
        }

        /// <summary>Deserializes the given JSON into a loosely typed object graph.</summary>
        /// <param name="data">The JSON to deserialize.</param>
        /// <returns>
        /// A <see cref="Dictionary{TKey,TValue}"/> for objects, a <see cref="List{T}"/> for arrays, a
        /// <see cref="string"/>, a number, a <see cref="bool"/> or <see langword="null"/>, which is
        /// the same shape <c>JavaScriptSerializer.DeserializeObject</c> produced.
        /// </returns>
        public static object DeserializeObject(string data)
        {
            if (string.IsNullOrEmpty(data))
            {
                return null;
            }

            using (var document = JsonDocument.Parse(data))
            {
                return ConvertElement(document.RootElement);
            }
        }

        /// <summary>Serializes the given object into JSON.</summary>
        /// <param name="obj">The object to serialize.</param>
        /// <returns>The JSON representation of the object, or an empty string when it is <see langword="null"/>.</returns>
        public static string Serialize(object obj)
        {
            if (obj is null)
            {
                return string.Empty;
            }

            // the default encoder escapes the single quote as \u0027, which is what the
            // JavaScriptSerializer of the original implementation emitted as well.
            return JsonSerializer.Serialize(obj, obj.GetType(), SerializerOptions);
        }

        /// <summary>Registers an already built script reference with the script manager of the page.</summary>
        /// <param name="objPage">The page being rendered.</param>
        /// <param name="reference">The script reference to register.</param>
        private static void RegisterClientScript(IClientApiPage objPage, object reference)
        {
            if (IsInstalled)
            {
                Register(objPage);
                var sm = ScriptManagerControl(objPage);
                var scripts = ScriptManagerScripts(sm);
                if (Convert.ToBoolean(Reflection.InvokeMethod(ScriptReferenceCollectionType, "Contains", scripts, new object[] { reference }), CultureInfo.InvariantCulture) == false)
                {
                    Reflection.InvokeMethod(ScriptReferenceCollectionType, "Add", ScriptManagerScripts(sm), new object[] { reference });
                }
            }
        }

        /// <summary>Gets the script manager of the given page through reflection.</summary>
        /// <param name="objPage">The page being rendered.</param>
        /// <returns>The script manager, or <see langword="null"/> when the page does not have one.</returns>
        private static object ScriptManagerControl(IClientApiPage objPage)
        {
            MethodInfo method = ScriptManagerType.GetMethod("GetCurrent");
            return method?.Invoke(ScriptManagerType, new object[] { objPage });
        }

        /// <summary>Creates an empty script reference through reflection.</summary>
        /// <returns>The new script reference.</returns>
        private static object GetScriptReference()
        {
            return Reflection.CreateInstance(ScriptReferenceType);
        }

        /// <summary>Sets a property of a script reference through reflection.</summary>
        /// <param name="reference">The script reference to change.</param>
        /// <param name="name">The name of the property.</param>
        /// <param name="value">The value to set the property to.</param>
        private static void SetScriptReferenceProperty(object reference, string name, object value)
        {
            Reflection.SetProperty(ScriptReferenceType, name, reference, new object[] { value });
        }

        /// <summary>Gets the script collection of the given script manager through reflection.</summary>
        /// <param name="objScriptManager">The script manager to read from.</param>
        /// <returns>The script reference collection.</returns>
        private static object ScriptManagerScripts(object objScriptManager)
        {
            return Reflection.GetProperty(ScriptManagerType, "Scripts", objScriptManager);
        }

        /// <summary>Converts a parsed JSON element into a loosely typed object graph.</summary>
        /// <param name="element">The element to convert.</param>
        /// <returns>The converted value.</returns>
        private static object ConvertElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var dictionary = new Dictionary<string, object>();
                    foreach (var property in element.EnumerateObject())
                    {
                        dictionary[property.Name] = ConvertElement(property.Value);
                    }

                    return dictionary;
                case JsonValueKind.Array:
                    var list = new List<object>();
                    foreach (var item in element.EnumerateArray())
                    {
                        list.Add(ConvertElement(item));
                    }

                    return list;
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    return element.TryGetInt32(out var intValue)
                        ? intValue
                        : element.TryGetInt64(out var longValue) ? longValue : element.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                default:
                    return null;
            }
        }
    }
}
