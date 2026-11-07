// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DNNConnect.CKEditorProvider.Browser;

using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// The endpoint which replaces the <c>[WebMethod] Browser.SetFile</c> page method: it receives the
/// URL of the image or link currently selected inside the editor, so that the browser can preselect
/// the matching folder and file.
/// </summary>
/// <remarks>
/// ASP.NET AJAX page methods (<c>[ScriptService]</c>/<c>[WebMethod]</c>, called as
/// <c>Browser.aspx/SetFile</c>) do not exist on ASP.NET Core, so the pair is replaced by this
/// endpoint. <c>Browser/js/jquery.pagemethod.js</c> posts the same JSON body
/// (<c>{"fileUrl":"..."}</c>) to it and still receives a JSON response, so the calling code the
/// browser emits (<c>jQuery.PageMethod('Browser.aspx', 'SetFile', ...)</c>) is unchanged.
/// </remarks>
public static class SetFileEndpoint
{
    /// <summary>The URL of the endpoint which replaces the <c>SetFile</c> page method.</summary>
    public const string SetFilePath = "/Providers/HtmlEditorProviders/DNNConnect.CKE/Browser/SetFile";

    /// <summary>Maps the endpoint which replaces the <c>SetFile</c> page method.</summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same <paramref name="endpoints"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> is <see langword="null"/>.</exception>
    public static IEndpointRouteBuilder MapCKEditorSetFileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // The page method was called with POST and a JSON body. The page it belongs to denies
        // anonymous users, so the endpoint does too.
        endpoints
            .MapPost(SetFilePath, HandleRequestAsync)
            .RequireAuthorization()
            .DisableAntiforgery();

        return endpoints;
    }

    private static Task HandleRequestAsync(HttpContext context, SetFileRequest body)
    {
        Browser.SetFile(body?.FileUrl);

        // The page method returned {"d":null}; the success callback ignores the value but the client
        // asks for (and parses) JSON.
        return context.Response.WriteAsJsonAsync(new SetFileResponse { D = null });
    }

    /// <summary>The body of a <c>SetFile</c> request.</summary>
    public sealed class SetFileRequest
    {
        /// <summary>Gets or sets the file url.</summary>
        public string FileUrl { get; set; }
    }

    /// <summary>The ASP.NET AJAX shaped response of a <c>SetFile</c> request.</summary>
    public sealed class SetFileResponse
    {
        /// <summary>Gets or sets the (always empty) return value of the method.</summary>
        [System.Text.Json.Serialization.JsonPropertyName("d")]
        public string D { get; set; }
    }
}
