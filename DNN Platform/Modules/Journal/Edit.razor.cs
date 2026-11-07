// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Modules.Journal
{
    /*
' Copyright (c) 2010 DotNetNuke Corporation
'  All rights reserved.
'
' THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED
' TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
' THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF
' CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
' DEALINGS IN THE SOFTWARE.
'
*/

    using System;

    using DotNetNuke.Services.Exceptions;

    /// <summary>The EditJournal class is used to manage content.</summary>
    /// <remarks>
    /// Was <c>Edit.ascx</c> + <c>Edit.ascx.cs</c> + <c>Edit.ascx.designer.cs</c>; the (empty)
    /// markup is <c>Edit.razor</c>.
    /// </remarks>
    public partial class Edit : JournalModuleBase
    {
        /// <inheritdoc />
        /// <remarks>
        /// <para>
        /// This is the ported <c>Page_Load</c>. The original <c>OnInit</c> did nothing but call
        /// <c>InitializeComponent()</c>, whose only job was <c>this.Load += this.Page_Load</c>;
        /// a component has no event to subscribe to, so the handler body moved into the
        /// <c>Load</c> phase override itself and <c>InitializeComponent</c> disappeared with the
        /// event wiring it existed for.
        /// </para>
        /// <para>
        /// The body is an intentional no-op, as it was in the original: this control is the stub
        /// DNN's module template generates for an "Edit" control, and the Journal module never
        /// implemented one. It is kept - rather than dropped - because the surrounding
        /// try/<c>Exceptions.ProcessModuleLoadException</c> shape is the contract any logic added
        /// here is expected to run under, and because <c>Journal.dnn</c> registers the control.
        /// </para>
        /// </remarks>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            try
            {
                // Implement your edit logic for your module
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }
    }
}
