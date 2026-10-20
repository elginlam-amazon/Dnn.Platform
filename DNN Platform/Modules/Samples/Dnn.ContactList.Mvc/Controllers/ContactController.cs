// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.ContactList.Mvc.Controllers
{
    using System;

    using Dnn.ContactList.Api;
    using DotNetNuke.Abstractions.Portals;
    using DotNetNuke.Collections;
    using DotNetNuke.Common;
    using DotNetNuke.Web.Mvc.Framework.ActionFilters;
    using DotNetNuke.Web.Mvc.Framework.Controllers;

    using Microsoft.AspNetCore.Mvc;

    /// <summary>
    /// ContactController is the MVC Controller class for managing Contacts in the UI.
    /// </summary>
    [DnnHandleError]
    public class ContactController : DnnController
    {
        private readonly IContactRepository repository;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContactController"/> class.
        /// </summary>
        public ContactController()
            : this(ContactRepository.Instance)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ContactController"/> class with a passed in repository.
        /// </summary>
        /// <param name="repository">The contact repository.</param>
        public ContactController(IContactRepository repository)
        {
            Requires.NotNull(repository);

            this.repository = repository;
        }

        /// <summary>
        /// The Delete method is used to delete a Contact.
        /// </summary>
        /// <param name="contactId">The Id of the Contact to delete.</param>
        /// <returns>A redirect to the default route.</returns>
        [HttpGet]
        public ActionResult Delete(int contactId)
        {
            var contact = this.repository.GetContact(contactId, this.PortalSettings.PortalId);

            this.repository.DeleteContact(contact);

            return this.RedirectToDefaultRoute();
        }

        /// <summary>
        /// This Edit method is used to set up editing a Contact.
        /// </summary>
        /// <param name="contactId">The Id of the Contact to edit, or -1 to add a new one.</param>
        /// <returns>The Edit view.</returns>
        [HttpGet]
        public ActionResult Edit(int contactId = -1)
        {
            var contact = (contactId == -1)
                        ? new Contact { PortalId = this.PortalSettings.PortalId }
                        : this.repository.GetContact(contactId, this.PortalSettings.PortalId);

            return this.View(contact);
        }

        /// <summary>
        /// This Edit method is used to save the posted Contact.
        /// </summary>
        /// <param name="contact">The contact to save.</param>
        /// <returns>A redirect to the default route when valid; otherwise the Edit view.</returns>
        [HttpPost]
        [DotNetNuke.Web.Mvc.Framework.ActionFilters.ValidateAntiForgeryToken]
        public ActionResult Edit(Contact contact)
        {
            if (this.ModelState.IsValid)
            {
                contact.PortalId = this.PortalSettings.PortalId;

                if (contact.ContactId == -1)
                {
                    this.repository.AddContact(contact, this.User.UserID);
                }
                else
                {
                    var existing = this.repository.GetContact(contact.ContactId, this.PortalSettings.PortalId);
                    existing.FirstName = contact.FirstName;
                    existing.LastName = contact.LastName;
                    existing.Email = contact.Email;
                    existing.Phone = contact.Phone;
                    existing.Social = contact.Social;
                    this.repository.UpdateContact(existing, this.User.UserID);
                }

                return this.RedirectToDefaultRoute();
            }
            else
            {
                return this.View(contact);
            }
        }

        /// <summary>
        /// The Index method is the default Action method.
        /// </summary>
        /// <param name="searchTerm">Term to search.</param>
        /// <param name="pageIndex">Index of the current page.</param>
        /// <returns>The Index view.</returns>
        [ModuleAction(ControlKey = "Edit", TitleKey = "AddContact")]
        public ActionResult Index(string searchTerm = "", int pageIndex = 0)
        {
            var contacts = this.repository.GetContacts(searchTerm, this.PortalSettings.PortalId, pageIndex, this.ModuleContext.Configuration.ModuleSettings.GetValueOrDefault("PageSize", 6));

            return this.View(contacts);
        }

        /// <summary>
        /// Returns a JSON payload describing the current user and portal.
        /// </summary>
        /// <returns>A JSON result.</returns>
        public JsonResult GetJsonResult()
        {
            return this.Json(new
            {
                this.User.UserID,
                this.PortalSettings.PortalId,
                Alias = ((IPortalAliasInfo)this.PortalSettings.PortalAlias).HttpAlias,
                Time = DateTime.Now.ToString("HH:mm:ss ttt"),
            });
        }

        /// <summary>
        /// Returns a demo partial view.
        /// </summary>
        /// <returns>A partial view result.</returns>
        public ActionResult GetDemoPartial()
        {
            this.TempData["UserID"] = this.User.UserID;
            this.ViewData["PortalId"] = this.PortalSettings.PortalId;
            this.ViewBag.Alias = ((IPortalAliasInfo)this.PortalSettings.PortalAlias).HttpAlias;

            return this.PartialView("_DemoPartial", DateTime.Now);
        }
    }
}
