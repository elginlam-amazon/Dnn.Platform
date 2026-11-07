// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.InternalServices;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Net;

using DotNetNuke.Common;
using DotNetNuke.Common.Lists;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Web.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

/// <summary>A web API controller for retrieving regions and countries.</summary>
/// <param name="listController">The list controller.</param>
[AllowAnonymous]
public class CountryRegionController(ListController listController)
    : DnnApiController
{
    private readonly ListController listController = listController ?? Globals.GetCurrentServiceProvider().GetRequiredService<ListController>();

    /// <summary>Initializes a new instance of the <see cref="CountryRegionController"/> class.</summary>
    [Obsolete("Deprecated in DotNetNuke 10.2.4. Please use overload with ListController. Scheduled removal in v12.0.0.")]
    public CountryRegionController()
        : this(null)
    {
    }

    /// <summary>Gets the countries.</summary>
    /// <returns>A response with an alphabetized list of <see cref="CachedCountryList.Country"/> objects.</returns>
    [HttpGet]
    public IActionResult Countries()
    {
        var searchString = (this.Request.GetParam("SearchString") ?? string.Empty).NormalizeString();
        var countries = CachedCountryList.GetCountryList(this.listController);
        return DnnApiResults.Response(HttpStatusCode.OK, countries.Values.Where(
            x => x.NormalizedFullName.IndexOf(searchString, StringComparison.CurrentCulture) > -1).OrderBy(x => x.NormalizedFullName));
    }

    /// <summary>Gets the regions for a country.</summary>
    /// <param name="country">The country ID.</param>
    /// <returns>A response with an alphabetized list of <see cref="Region"/> objects.</returns>
    [HttpGet]
    public IActionResult Regions(int country)
    {
        List<Region> res = [];
        foreach (ListEntryInfo r in this.listController.GetListEntryInfoItems("Region").Where(l => l.ParentID == country))
        {
            res.Add(new Region
            {
                Text = r.Text,
                Value = r.EntryID.ToString(CultureInfo.InvariantCulture),
            });
        }

        return DnnApiResults.Response(HttpStatusCode.OK, res.OrderBy(r => r.Text));
    }

    /// <summary>A data transfer object representing a region.</summary>
    public struct Region
    {
        /// <summary>Gets or sets the text.</summary>
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        public string Text;

        /// <summary>Gets or sets the entry ID.</summary>
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        public string Value;
    }
}
