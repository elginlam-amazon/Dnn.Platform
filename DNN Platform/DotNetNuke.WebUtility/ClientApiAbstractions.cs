// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// Abstraction over the control surface the Client API needs.
    /// </summary>
    /// <remarks>
    /// Replaces the <c>System.Web.UI.Control</c> / <c>WebControl</c> / <c>HtmlControl</c> types the
    /// original WebForms implementation used in its signatures. Hosts (or the WebForms-to-Blazor
    /// adapters) supply an implementation that maps these members onto their own component model.
    /// </remarks>
    public interface IClientApiControl
    {
        /// <summary>Occurs right before this control renders, mirroring the WebForms <c>PreRender</c> event.</summary>
        event EventHandler PreRender;

        /// <summary>Gets or sets the server side identifier of the control (the WebForms <c>ID</c>).</summary>
        string Id { get; set; }

        /// <summary>Gets the rendered client side identifier of the control (the WebForms <c>ClientID</c>).</summary>
        string ClientId { get; }

        /// <summary>Gets the hierarchically qualified identifier of the control (the WebForms <c>UniqueID</c>).</summary>
        string UniqueId { get; }

        /// <summary>Gets the attribute bag rendered onto the control's markup.</summary>
        /// <remarks>Replaces both <c>WebControl.Attributes</c> and <c>HtmlControl.Attributes</c>.</remarks>
        IDictionary<string, string> Attributes { get; }

        /// <summary>Gets the parent of this control, or <see langword="null"/> when this control is the root.</summary>
        IClientApiControl Parent { get; }

        /// <summary>Gets the naming container of this control, or <see langword="null"/> when it does not use one.</summary>
        IClientApiControl NamingContainer { get; }

        /// <summary>Gets the child controls of this control.</summary>
        /// <remarks>The returned collection is live: adding to it adds the child to the control tree.</remarks>
        IList<IClientApiControl> Children { get; }

        /// <summary>Gets a value indicating whether this control has any child controls.</summary>
        /// <remarks>
        /// Mirrors <c>Control.HasControls</c>, which the original code checked before touching the
        /// children of a control so that enumerating them never forced view state to be created.
        /// </remarks>
        bool HasChildren { get; }

        /// <summary>Gets the page this control belongs to, or <see langword="null"/> when it is not on a page yet.</summary>
        IClientApiPage Page { get; }

        /// <summary>Finds a direct or naming-container-scoped child control by its identifier.</summary>
        /// <param name="id">The server side identifier to look for.</param>
        /// <returns>The matching control, or <see langword="null"/> when it was not found.</returns>
        IClientApiControl FindControl(string id);
    }

    /// <summary>
    /// A control that renders as a hidden input and exposes its posted value.
    /// </summary>
    /// <remarks>Replaces <c>System.Web.UI.HtmlControls.HtmlInputHidden</c> in signatures.</remarks>
    public interface IClientApiHiddenInput : IClientApiControl
    {
        /// <summary>Gets or sets the value of the hidden input.</summary>
        string Value { get; set; }
    }

    /// <summary>
    /// Implemented by controls that render as a table row.
    /// </summary>
    /// <remarks>
    /// Replaces the <c>TypeOf objParent Is System.Web.UI.WebControls.TableRow</c> test the client
    /// side reorder registration performed while walking up the control tree, so that the row can
    /// be marked with the <c>origidx</c> attribute.
    /// </remarks>
    public interface IClientApiTableRow : IClientApiControl
    {
    }

    /// <summary>
    /// Abstraction over the page surface the Client API needs.
    /// </summary>
    /// <remarks>
    /// Replaces <c>System.Web.UI.Page</c> (and its <c>ClientScript</c> manager) in signatures.
    /// </remarks>
    public interface IClientApiPage : IClientApiControl
    {
        /// <summary>Gets the ASP.NET Core request context of the current request, or <see langword="null"/> when there is none.</summary>
        HttpContext HttpContext { get; }

        /// <summary>Gets the per-request item bag, mirroring <c>HttpContext.Items</c>.</summary>
        IDictionary<object, object> Items { get; }

        /// <summary>Gets the service provider scoped to the current request.</summary>
        IServiceProvider RequestServices { get; }

        /// <summary>Gets the form of the page, which is the container client side state is added to.</summary>
        IClientApiControl Form { get; }

        /// <summary>Gets the value posted or queried for the given key, mirroring the WebForms <c>Request(key)</c> indexer.</summary>
        /// <param name="key">The form field, query string or cookie name.</param>
        /// <returns>The value, or <see langword="null"/> when the key was not supplied.</returns>
        string GetRequestValue(string key);

        /// <summary>Determines whether a client script block was already registered.</summary>
        /// <param name="type">The type used as the registration scope.</param>
        /// <param name="key">The registration key.</param>
        /// <returns><see langword="true"/> when the script block is already registered.</returns>
        bool IsClientScriptBlockRegistered(Type type, string key);

        /// <summary>Determines whether a client script block was already registered, scoped to the page type.</summary>
        /// <param name="key">The registration key.</param>
        /// <returns><see langword="true"/> when the script block is already registered.</returns>
        bool IsClientScriptBlockRegistered(string key);

        /// <summary>Registers a block of markup/script to be emitted once per page.</summary>
        /// <param name="type">The type used as the registration scope.</param>
        /// <param name="key">The registration key.</param>
        /// <param name="script">The script (or script tag) to emit.</param>
        void RegisterClientScriptBlock(Type type, string key, string script);

        /// <summary>Registers an external script file to be included once per page.</summary>
        /// <param name="key">The registration key.</param>
        /// <param name="url">The URL of the script.</param>
        void RegisterClientScriptInclude(string key, string url);

        /// <summary>Registers an embedded script resource to be included once per page.</summary>
        /// <param name="type">A type from the assembly holding the embedded resource.</param>
        /// <param name="resourceName">The name of the embedded script.</param>
        void RegisterClientScriptResource(Type type, string resourceName);

        /// <summary>Registers a script to run once the page has loaded.</summary>
        /// <param name="type">The type used as the registration scope.</param>
        /// <param name="key">The registration key.</param>
        /// <param name="script">The script to run.</param>
        /// <param name="addScriptTags">Whether the script needs to be wrapped in script tags.</param>
        void RegisterStartupScript(Type type, string key, string script, bool addScriptTags);

        /// <summary>Registers a hidden field to be rendered inside the page's form.</summary>
        /// <param name="name">The name of the field.</param>
        /// <param name="value">The initial value of the field.</param>
        void RegisterHiddenField(string name, string value);

        /// <summary>Gets the client side script that posts the page back on behalf of a control.</summary>
        /// <param name="control">The control raising the post back.</param>
        /// <param name="argument">The post back argument.</param>
        /// <returns>The client side post back script.</returns>
        string GetPostBackEventReference(IClientApiControl control, string argument);

        /// <summary>Gets the URL an embedded resource of the given assembly is served from.</summary>
        /// <param name="type">A type from the assembly holding the embedded resource.</param>
        /// <param name="resourceName">The name of the embedded resource.</param>
        /// <returns>The URL of the resource.</returns>
        string GetWebResourceUrl(Type type, string resourceName);

        /// <summary>Takes over rendering of the page, mirroring <c>Page.SetRenderMethodDelegate</c>.</summary>
        /// <param name="renderMethod">The method that renders the page's content.</param>
        void SetRenderMethodDelegate(Action<IClientApiHtmlWriter, IClientApiControl> renderMethod);
    }

    /// <summary>
    /// Abstraction over the markup writer handed to a render method.
    /// </summary>
    /// <remarks>Replaces <c>System.Web.UI.HtmlTextWriter</c> in signatures.</remarks>
    public interface IClientApiHtmlWriter
    {
        /// <summary>Gets the underlying writer the markup is written to.</summary>
        TextWriter Writer { get; }

        /// <summary>Writes the given text verbatim.</summary>
        /// <param name="text">The text to write.</param>
        void Write(string text);

        /// <summary>Writes the given text verbatim, followed by a line break.</summary>
        /// <param name="text">The text to write.</param>
        void WriteLine(string text);

        /// <summary>Writes an attribute name/value pair, encoding the value.</summary>
        /// <param name="name">The attribute name.</param>
        /// <param name="value">The attribute value.</param>
        void WriteAttribute(string name, string value);

        /// <summary>Writes the opening of a tag, leaving it open for attributes.</summary>
        /// <param name="tagName">The tag name.</param>
        void WriteBeginTag(string tagName);

        /// <summary>Writes a complete opening tag.</summary>
        /// <param name="tagName">The tag name.</param>
        void WriteFullBeginTag(string tagName);

        /// <summary>Writes a closing tag.</summary>
        /// <param name="tagName">The tag name.</param>
        void WriteEndTag(string tagName);

        /// <summary>Flushes everything buffered so far to the output.</summary>
        void Flush();
    }

    /// <summary>
    /// Implemented by controls that handle a Client API post back.
    /// </summary>
    /// <remarks>Replaces <c>System.Web.UI.IPostBackEventHandler</c>.</remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix", Justification = "The name intentionally mirrors the System.Web.UI.IPostBackEventHandler interface it replaces.")]
    public interface IClientApiPostBackEventHandler
    {
        /// <summary>Raises the post back event for the given argument.</summary>
        /// <param name="eventArgument">The post back argument sent by the client.</param>
        void RaisePostBackEvent(string eventArgument);
    }
}
