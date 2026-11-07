// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.DDRMenu.TemplateEngine
{
    using System.IO;

    internal interface ITemplateProcessor
    {
        bool LoadDefinition(TemplateDefinition baseDefinition);

        // The sink was an HtmlTextWriter, which has no .NET equivalent. Every processor only ever
        // writes an accumulated HTML string, so the contract is a plain TextWriter; the hosting
        // component renders what was written as markup.
        void Render(object source, TextWriter writer, TemplateDefinition liveDefinition);
    }
}
