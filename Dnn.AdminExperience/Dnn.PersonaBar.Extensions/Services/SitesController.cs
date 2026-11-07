// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace Dnn.PersonaBar.Sites.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;

    using Dnn.PersonaBar.Library;
    using Dnn.PersonaBar.Library.Attributes;
    using Dnn.PersonaBar.Sites.Services.Dto;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Security.Membership;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Web.Api;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;

    using ValidateAntiForgeryToken = DotNetNuke.Web.Api.ValidateAntiForgeryTokenAttribute;

    [MenuPermission(Scope = ServiceScope.Host)]
    public class SitesController : PersonaBarApiController
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<SitesController>();
        private readonly Components.SitesController controller = new Components.SitesController();

        /// <summary>Gets list of portals.</summary>
        /// <param name="portalGroupId">The portal group ID or <see cref="Null.NullInteger"/> for no group.</param>
        /// <param name="filter">THe portal name filter.</param>
        /// <param name="pageIndex">The page index.</param>
        /// <param name="pageSize">The page size.</param>
        /// <returns>List of portals.</returns>
        /// <example>
        /// <code>
        /// GET <c>/api/personabar/sites/GetPortals?portalGroupId=-1&amp;filter=mysite&amp;pageIndex=0&amp;pageSize=10</c>
        /// </code>
        /// </example>
        [HttpGet]
        public IActionResult GetPortals(int portalGroupId, string filter, int pageIndex, int pageSize)
        {
            try
            {
                var totalRecords = 0;
                IEnumerable<PortalInfo> portals;

                if (portalGroupId > Null.NullInteger)
                {
                    portals = PortalGroupController.Instance.GetPortalsByGroup(portalGroupId)
                                .Where(p => string.IsNullOrEmpty(filter)
                                                || p.PortalName.ToLowerInvariant().Contains(filter.ToLowerInvariant()))
                                .ToList();
                    totalRecords = portals.Count();
                    portals = portals.Skip(pageIndex * pageSize).Take(pageSize);
                }
                else
                {
                    portals = PortalController.GetPortalsByName(
                            $"%{filter}%",
                            pageIndex,
                            pageSize,
                            ref totalRecords)
                        .Cast<PortalInfo>();
                }

                var response = new
                {
                    Results = portals.Select(this.GetPortalDto).ToList(),
                    TotalResults = totalRecords,
                };

                return this.Ok(response);
            }
            catch (Exception exc)
            {
                Logger.SitesControllerGetPortalsException(exc);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, exc);
            }
        }

        /// POST: api/Sites/CreatePortal
        /// <summary>Adds a portal.</summary>
        /// <param name="request">The creation request.</param>
        /// <returns>A response with portal info and error messages.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireHost]
        public IActionResult CreatePortal(CreatePortalRequest request)
        {
            try
            {
                var errors = new List<string>();
                var portalId = this.controller.CreatePortal(
                    errors,
                    this.GetDomainName(),
                    this.GetAbsoluteServerPath(),
                    request.SiteTemplate,
                    request.SiteName,
                    request.SiteAlias,
                    request.SiteDescription,
                    request.SiteKeywords,
                    request.IsChildSite,
                    request.HomeDirectory,
                    request.SiteGroupId,
                    request.UseCurrentUserAsAdmin,
                    request.Firstname,
                    request.Lastname,
                    request.Username,
                    request.Email,
                    request.Password,
                    request.PasswordConfirm,
                    request.Question,
                    request.Answer);
                if (portalId < 0)
                {
                    return DnnApiResults.Error(HttpStatusCode.BadRequest, string.Join("<br/>", errors));
                }

                var portal = PortalController.Instance.GetPortal(portalId);
                return this.Ok(new
                {
                    Portal = this.GetPortalDto(portal),
                    ErrorMessage = errors,
                });
            }
            catch (Exception exc)
            {
                Logger.SitesControllerCreatePortalException(exc);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, exc);
            }
        }

        /// POST: api/Sites/DeletePortal
        /// <summary>Deletes a portal.</summary>
        /// <param name="portalId">The portal ID.</param>
        /// <returns>A response indicating success.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeletePortal(int portalId)
        {
            try
            {
                var portal = PortalController.Instance.GetPortal(portalId);
                if (portal != null)
                {
                    if (portal.PortalID != this.PortalSettings.PortalId &&
                        !PortalController.IsMemberOfPortalGroup(portal.PortalID))
                    {
                        var strMessage = PortalController.DeletePortal(portal, this.GetAbsoluteServerPath());
                        if (string.IsNullOrEmpty(strMessage))
                        {
                            return this.Ok(new { Success = true, });
                        }

                        return DnnApiResults.Error(HttpStatusCode.BadRequest, strMessage);
                    }

                    return DnnApiResults.Error(
                        HttpStatusCode.Unauthorized,
                        Localization.GetString("PortalDeletionDenied", Components.Constants.LocalResourcesFile));
                }

                return DnnApiResults.Error(
                    HttpStatusCode.NotFound,
                    Localization.GetString("PortalNotFound", Components.Constants.LocalResourcesFile));
            }
            catch (Exception exc)
            {
                Logger.SitesControllerDeletePortalException(exc);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, exc);
            }
        }

        /// POST: api/Sites/ExportPortalTemplate
        /// <summary>Exports portal template.</summary>
        /// <param name="request">The export request.</param>
        /// <returns>A response with a message and template info.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ExportPortalTemplate(ExportTemplateRequest request)
        {
            try
            {
                var message = this.controller.ExportPortalTemplate(request, this.UserInfo, out var success);
                if (!success)
                {
                    return DnnApiResults.Error(HttpStatusCode.BadRequest, message);
                }

                var template = this.controller.GetPortalTemplates().First(x => x.Name == request.FileName);
                var templateItem = this.controller.CreateListItem(template);
                return this.Ok(new
                {
                    Message = message,
                    Template = new
                    {
                        Name = templateItem.Text,
                        templateItem.Value,
                    },
                });
            }
            catch (Exception exc)
            {
                Logger.SitesControllerExportPortalTemplateException(exc);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, exc);
            }
        }

        /// GET: api/Sites/GetPortalLocales
        /// <summary>Gets list of portal locales.</summary>
        /// <param name="portalId">The portal ID.</param>
        /// <returns>List of portal locales.</returns>
        [HttpGet]
        public IActionResult GetPortalLocales(int portalId)
        {
            try
            {
                var locales = LocaleController.Instance.GetLocales(portalId).Values;
                var response = new
                {
                    Success = true,
                    Results = locales.Select(l => new
                    {
                        l.Code,
                        l.EnglishName,
                        l.LanguageId,
                    }),
                    TotalResults = locales.Count,
                };

                return this.Ok(response);
            }
            catch (Exception exc)
            {
                Logger.SitesControllerGetPortalLocalesException(exc);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, exc);
            }
        }

        /// POST: api/Sites/DeleteExpiredPortals
        /// <summary>Deletes expired portals.</summary>
        /// <returns>A response indicating success.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteExpiredPortals()
        {
            try
            {
                PortalController.DeleteExpiredPortals(this.GetAbsoluteServerPath());
                return this.Ok(new { Success = true, });
            }
            catch (Exception exc)
            {
                Logger.SitesControllerDeleteExpiredPortalsException(exc);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, exc);
            }
        }

        /// GET: api/Sites/GetPortalTemplates
        /// <summary>Gets list of portal templates.</summary>
        /// <returns>List of portal templates.</returns>
        [HttpGet]
        public IActionResult GetPortalTemplates()
        {
            try
            {
                var defaultTemplate = this.controller.GetDefaultTemplate();
                var temps = new List<Tuple<string, string>>();
                var templates = this.controller.GetPortalTemplates();
                foreach (var template in templates)
                {
                    var item = this.controller.CreateListItem(template);
                    temps.Add(new Tuple<string, string>(item.Text, item.Value));
                    if (item.Value.StartsWith(defaultTemplate, StringComparison.OrdinalIgnoreCase))
                    {
                        defaultTemplate = item.Value;
                    }
                }

                var response = new
                {
                    Success = true,
                    Results = new
                    {
                        Templates = temps.Select(t => new
                        {
                            Name = t.Item1,
                            Value = t.Item2,
                        }),
                        DefaultTemplate = defaultTemplate,
                    },
                    TotalResults = temps.Count,
                };

                return this.Ok(response);
            }
            catch (Exception exc)
            {
                Logger.SitesControllerGetPortalTemplatesException(exc);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, exc);
            }
        }

        /// GET: api/Sites/RequiresQuestionAndAnswer
        /// <summary>Gets whether a Question/Answer is required for Password retrieval.</summary>
        /// <returns>A response with a <c>RequiresQuestionAndAnswer</c> field.</returns>
        [HttpGet]
        public IActionResult RequiresQuestionAndAnswer()
        {
            try
            {
                return this.Ok(new
                {
                    MembershipProviderConfig.RequiresQuestionAndAnswer,
                });
            }
            catch (Exception exc)
            {
                Logger.SitesControllerRequiresQuestionAndAnswerException(exc);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, exc);
            }
        }

        private string GetAbsoluteServerPath()
        {
            // The legacy code mapped the application path of the ambient request; in ASP.NET Core the
            // application root is resolved from the hosting environment instead.
            var strServerPath = this.Request?.HttpContext != null
                ? HttpContextSource.MapPath("~/")
                : string.Empty;

            var separator = Path.DirectorySeparatorChar.ToString();
            if (!strServerPath.EndsWith(separator, StringComparison.OrdinalIgnoreCase))
            {
                strServerPath += separator;
            }

            return strServerPath;
        }

        private string GetDomainName()
        {
            return this.Request != null
                ? Globals.GetDomainName(this.Request, true)
                : string.Empty;
        }

        private object GetPortalDto(PortalInfo portal)
        {
            string contentLocalizable;
            var portalDto = new
            {
                portal.PortalID,
                portal.PortalName,
                PortalAliases = this.controller.FormatPortalAliases(portal.PortalID),
                portal.Users,
                portal.Pages,
                portal.HostSpace,
                portal.HostFee,
                portal.DefaultLanguage,
                ExpiryDate = this.controller.FormatExpiryDate(portal.ExpiryDate),
                portal.LastModifiedOnDate,
                contentLocalizable =
                    PortalController.Instance.GetPortalSettings(portal.PortalID)
                        .TryGetValue("ContentLocalizationEnabled", out contentLocalizable) &&
                    Convert.ToBoolean(contentLocalizable),
                allowDelete =
                    portal.PortalID != this.PortalSettings.PortalId &&
                     !PortalController.IsMemberOfPortalGroup(portal.PortalID),
            };
            return portalDto;
        }
    }
}
