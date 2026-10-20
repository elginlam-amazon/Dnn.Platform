// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web
{
    using System.Globalization;
    using System.IO;

#pragma warning disable SA1101 // StyleCop does not understand C# 14 extension members (the receiver is not an instance member).
#pragma warning disable CA1708 // the generated extension marker types differ by case only.

    /// <summary>
    /// Members of the System.Web <see cref="HttpServerUtility"/>, <see cref="HttpRequest"/>, <see cref="HttpResponse"/> and
    /// <see cref="HttpContextBase"/> types that are not provided by the System.Web adapters for ASP.NET Core.
    /// </summary>
    public static class SystemWebCompatibilityExtensions
    {
        /// <summary>The members of <see cref="HttpServerUtility"/>.</summary>
        /// <param name="server">The server utility.</param>
        extension(HttpServerUtility server)
        {
            /// <summary>Gets or sets the request time out in seconds (not enforced by ASP.NET Core, kept for compatibility).</summary>
            public int ScriptTimeout
            {
                get => 110;
                set { }
            }

            /// <summary>HTML-encodes a string.</summary>
            /// <param name="s">The string to encode.</param>
            /// <returns>The encoded string.</returns>
            public string HtmlEncode(string s) => HttpUtility.HtmlEncode(s);

            /// <summary>HTML-encodes a string and writes it to the output.</summary>
            /// <param name="s">The string to encode.</param>
            /// <param name="output">The output.</param>
            public void HtmlEncode(string s, TextWriter output) => HttpUtility.HtmlEncode(s, output);

            /// <summary>Decodes an HTML-encoded string.</summary>
            /// <param name="s">The string to decode.</param>
            /// <returns>The decoded string.</returns>
            public string HtmlDecode(string s) => HttpUtility.HtmlDecode(s);

            /// <summary>URL-encodes a string.</summary>
            /// <param name="s">The string to encode.</param>
            /// <returns>The encoded string.</returns>
            public string UrlEncode(string s) => HttpUtility.UrlEncode(s);

            /// <summary>Decodes a URL-encoded string.</summary>
            /// <param name="s">The string to decode.</param>
            /// <returns>The decoded string.</returns>
            public string UrlDecode(string s) => HttpUtility.UrlDecode(s);

            /// <summary>URL-encodes the path portion of a URL.</summary>
            /// <param name="s">The URL to encode.</param>
            /// <returns>The encoded URL.</returns>
            public string UrlPathEncode(string s) => HttpUtility.UrlPathEncode(s);
        }

        /// <summary>The members of <see cref="HttpRequest"/>.</summary>
        /// <param name="request">The request.</param>
        extension(HttpRequest request)
        {
            /// <summary>Maps a virtual path (relative to the current request) to a physical path.</summary>
            /// <param name="virtualPath">The virtual path.</param>
            /// <returns>The physical path.</returns>
            public string MapPath(string virtualPath)
            {
                if (string.IsNullOrEmpty(virtualPath))
                {
                    virtualPath = VirtualPathUtility.GetDirectory(request.FilePath) ?? "/";
                }
                else if (!VirtualPathUtility.IsAbsolute(virtualPath) && !VirtualPathUtility.IsAppRelative(virtualPath))
                {
                    virtualPath = VirtualPathUtility.Combine(VirtualPathUtility.GetDirectory(request.FilePath) ?? "/", virtualPath);
                }

                return Hosting.HostingEnvironment.MapPath(virtualPath);
            }

            /// <summary>Maps a virtual path (relative to the given base directory) to a physical path.</summary>
            /// <param name="virtualPath">The virtual path.</param>
            /// <param name="baseVirtualDir">The virtual base directory used to resolve relative paths.</param>
            /// <param name="allowCrossAppMapping">Whether the path may belong to another application (not supported, a single application is hosted).</param>
            /// <returns>The physical path.</returns>
            public string MapPath(string virtualPath, string baseVirtualDir, bool allowCrossAppMapping)
            {
                if (string.IsNullOrEmpty(virtualPath))
                {
                    virtualPath = baseVirtualDir ?? "/";
                }
                else if (!VirtualPathUtility.IsAbsolute(virtualPath) && !VirtualPathUtility.IsAppRelative(virtualPath))
                {
                    virtualPath = VirtualPathUtility.Combine(VirtualPathUtility.AppendTrailingSlash(baseVirtualDir ?? "/"), virtualPath);
                }

                return Hosting.HostingEnvironment.MapPath(virtualPath);
            }
        }

        /// <summary>The members of <see cref="HttpResponse"/>.</summary>
        /// <param name="response">The response.</param>
        extension(HttpResponse response)
        {
            /// <summary>Gets or sets a value indicating whether the output is buffered.</summary>
            public bool Buffer
            {
                get => response.BufferOutput;
                set => response.BufferOutput = value;
            }

            /// <summary>Gets or sets the status line (for example <c>404 Not Found</c>).</summary>
            public string Status
            {
                get => FormatStatus(response.StatusCode, response.StatusDescription);
                set => ParseStatus(value, code => response.StatusCode = code, description => response.StatusDescription = description);
            }
        }

        /// <summary>The members of <see cref="HttpResponseBase"/>.</summary>
        /// <param name="response">The response.</param>
        extension(HttpResponseBase response)
        {
            /// <summary>Gets or sets a value indicating whether the output is buffered.</summary>
            public bool Buffer
            {
                get => response.BufferOutput;
                set => response.BufferOutput = value;
            }

            /// <summary>Gets or sets the status line (for example <c>404 Not Found</c>).</summary>
            public string Status
            {
                get => FormatStatus(response.StatusCode, response.StatusDescription);
                set => ParseStatus(value, code => response.StatusCode = code, description => response.StatusDescription = description);
            }
        }

        /// <summary>The members of <see cref="HttpContextBase"/>.</summary>
        /// <param name="context">The context.</param>
        extension(HttpContextBase context)
        {
            /// <summary>Registers a callback which is invoked when the request is completed.</summary>
            /// <param name="callback">The callback.</param>
            public void AddOnRequestCompleted(Action<HttpContextBase> callback)
            {
                if (callback == null)
                {
                    throw new ArgumentNullException(nameof(callback));
                }

                context.DisposeOnPipelineCompleted(new CallbackDisposable(() => callback(context)));
            }
        }

        /// <summary>The members of <see cref="HttpContext"/>.</summary>
        /// <param name="context">The context.</param>
        extension(HttpContext context)
        {
            /// <summary>Registers a callback which is invoked when the request is completed.</summary>
            /// <param name="callback">The callback.</param>
            public void AddOnRequestCompleted(Action<HttpContextBase> callback)
            {
                new HttpContextWrapper(context).AddOnRequestCompleted(callback);
            }
        }

        private static string FormatStatus(int statusCode, string statusDescription)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{statusCode} {statusDescription}").TrimEnd();
        }

        private static void ParseStatus(string status, Action<int> setCode, Action<string> setDescription)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                throw new ArgumentException("The status must not be empty.", nameof(status));
            }

            var parts = status.Trim().Split(' ', 2);
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
            {
                throw new ArgumentException("The status must start with a numeric status code.", nameof(status));
            }

            setCode(code);
            if (parts.Length > 1)
            {
                setDescription(parts[1]);
            }
        }

        private sealed class CallbackDisposable : IDisposable
        {
            private Action callback;

            public CallbackDisposable(Action callback)
            {
                this.callback = callback;
            }

            public void Dispose()
            {
                var action = this.callback;
                this.callback = null;
                action?.Invoke();
            }
        }
    }
}
