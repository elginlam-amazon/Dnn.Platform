// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DNNConnect.CKEditorProvider
{
    using System;

    using DNNConnect.CKEditorProvider.Browser;

    using Microsoft.AspNetCore.Routing;

    /// <summary>
    /// Maps every URL of this extension which was served by an <c>IHttpHandler</c> (<c>.ashx</c>) or
    /// an ASP.NET AJAX page method on .NET Framework.
    /// </summary>
    /// <remarks>
    /// Called once from <c>DNN Platform/Website/Program.cs</c>. Every original URL is preserved: the
    /// CKEditor plugins and the browser's JavaScript build these URLs themselves.
    /// </remarks>
    public static class CKEditorProviderEndpoints
    {
        /// <summary>Maps all endpoints of the DNNConnect CKEditor provider.</summary>
        /// <param name="endpoints">The endpoint route builder.</param>
        /// <returns>The same <paramref name="endpoints"/>, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> is <see langword="null"/>.</exception>
        public static IEndpointRouteBuilder MapCKEditorProviderEndpoints(this IEndpointRouteBuilder endpoints)
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            // Tabs.ashx — the tab tree script for the page-link picker.
            endpoints.MapCKEditorTabsEndpoints();

            // Browser/FileUploader.ashx — the jQuery-File-Upload target.
            endpoints.MapCKEditorFileUploaderEndpoints();

            // Browser/ProcessImage.ashx — the resize/rotate/crop pipeline.
            endpoints.MapCKEditorProcessImageEndpoints();

            // Replaces the Browser.aspx/SetFile page method.
            endpoints.MapCKEditorSetFileEndpoints();

            // Browser/Browser.aspx with Command=FileUpload|FlashUpload|ImageUpload|EasyImageUpload
            // — the CKEditor quick uploads, whose response body is only the JSON (or only the one
            // callback <script>) the original wrote with Response.End(). Mapped here (before
            // MapRazorComponents) so it wins the POST route from the Browser component.
            endpoints.MapCKEditorQuickUploadEndpoints();

            return endpoints;
        }
    }
}
