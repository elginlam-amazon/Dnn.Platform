// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;

    using DotNetNuke.Common;
    using DotNetNuke.Internal.SourceGenerators;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// The EditControlFactory control provides a factory for creating the
    /// appropriate Edit Control.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The instantiation contract is unchanged: <see cref="CreateEditControl(IServiceProvider, EditorInfo)"/>
    /// resolves the editor type by name (<see cref="EditorInfo.Editor"/>) through
    /// <see cref="ActivatorUtilities.GetServiceOrCreateInstance(IServiceProvider, Type)"/>, or falls
    /// back to a built-in editor chosen from <see cref="EditorInfo.Type"/>, and returns a fully
    /// configured <see cref="EditControl"/> instance.
    /// </para>
    /// <para>
    /// The <em>rendering</em> contract is what changed. A Blazor component cannot be rendered from a
    /// pre-built instance through <c>RenderTreeBuilder.OpenComponent</c>, so <see cref="EditControl"/>
    /// derives from <see cref="PropertyEditorComponentBase"/>, which exposes
    /// <see cref="PropertyEditorComponentBase.Render"/>: a <see cref="RenderFragment"/> that renders
    /// <em>that instance</em>. The instance the factory returns is therefore the instance that
    /// renders, holds the value and raises the events — callers place it in markup with
    /// <c>@editor.Render()</c> (which is what <see cref="FieldEditorControl"/> does), or use
    /// <see cref="CreateEditControlFragment(IServiceProvider, EditorInfo, out EditControl)"/>.
    /// </para>
    /// </remarks>
    public partial class EditControlFactory
    {
        /// <summary>CreateEditControl creates the appropriate Control based on the EditorField or TypeDataField. </summary>
        /// <param name="editorInfo">An EditorInfo object.</param>
        /// <returns>A new <see cref="EditControl"/> instance.</returns>
        [DnnDeprecated(10, 0, 0, "Please use overload with IServiceProvider")]
        public static partial EditControl CreateEditControl(EditorInfo editorInfo)
        {
            return CreateEditControl(Globals.GetCurrentServiceProvider(), editorInfo);
        }

        /// <summary>CreateEditControl creates the appropriate Control based on the EditorField or TypeDataField. </summary>
        /// <param name="serviceProvider">The DI container.</param>
        /// <param name="editorInfo">An EditorInfo object.</param>
        /// <returns>A new <see cref="EditControl"/> instance.</returns>
        public static EditControl CreateEditControl(IServiceProvider serviceProvider, EditorInfo editorInfo)
        {
            EditControl propEditor;

            if (editorInfo.Editor == "UseSystemType")
            {
                // Use System Type
                propEditor = CreateEditControlInternal(editorInfo.Type);
            }
            else
            {
                // Use Editor
                try
                {
                    var editType = Type.GetType(editorInfo.Editor, true, true);
                    propEditor = (EditControl)ActivatorUtilities.GetServiceOrCreateInstance(serviceProvider, editType);
                }
                catch (TypeLoadException)
                {
                    // Use System Type
                    propEditor = CreateEditControlInternal(editorInfo.Type);
                }
            }

            propEditor.ID = editorInfo.Name;
            propEditor.Name = editorInfo.Name;
            propEditor.DataField = editorInfo.Name;
            propEditor.Category = editorInfo.Category;

            propEditor.EditMode = editorInfo.EditMode;
            propEditor.Required = editorInfo.Required;

            propEditor.Value = editorInfo.Value;
            propEditor.OldValue = editorInfo.Value;

            propEditor.CustomAttributes = editorInfo.Attributes;

            return propEditor;
        }

        /// <summary>Creates the appropriate edit control and returns a fragment that renders it.</summary>
        /// <param name="serviceProvider">The DI container.</param>
        /// <param name="editorInfo">An EditorInfo object.</param>
        /// <param name="editControl">The created <see cref="EditControl"/>, so the caller can read its
        /// value and subscribe to its events.</param>
        /// <returns>A <see cref="RenderFragment"/> that renders <paramref name="editControl"/>.</returns>
        public static RenderFragment CreateEditControlFragment(IServiceProvider serviceProvider, EditorInfo editorInfo, out EditControl editControl)
        {
            editControl = CreateEditControl(serviceProvider, editorInfo);
            return editControl.Render();
        }

        private static EditControl CreateEditControlInternal(string systemType)
        {
            EditControl propEditor = null;
            try
            {
                var type = Type.GetType(systemType);
                if (type != null)
                {
                    switch (type.FullName)
                    {
                        case "System.DateTime":
                            propEditor = new DateTimeEditControl();
                            break;
                        case "System.Boolean":
                            propEditor = new CheckEditControl();
                            break;
                        case "System.Int32":
                        case "System.Int16":
                            propEditor = new IntegerEditControl();
                            break;
                        default:
                            if (type.IsEnum)
                            {
                                propEditor = new EnumEditControl(systemType);
                            }

                            break;
                    }
                }
            }
            catch (Exception)
            {
                // ignore
            }

            return propEditor ?? new TextEditControl(systemType);
        }
    }
}
