// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace Dnn.PersonaBar.Users.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;

    using Dnn.PersonaBar.Library;
    using Dnn.PersonaBar.Library.Attributes;
    using Dnn.PersonaBar.Users.Components;
    using Dnn.PersonaBar.Users.Components.Contracts;
    using Dnn.PersonaBar.Users.Components.Dto;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Host;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Entities.Users.Membership;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Security.Membership;
    using DotNetNuke.Security.Roles;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Services.Mail;
    using DotNetNuke.Web.Api;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Data.SqlClient;
    using Microsoft.Extensions.Logging;

    using ValidateAntiForgeryToken = DotNetNuke.Web.Api.ValidateAntiForgeryTokenAttribute;

    [MenuPermission(MenuName = "Dnn.Users")]
    public class UsersController : PersonaBarApiController
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<UsersController>();

        /// <summary>Create a User.</summary>
        /// <param name="contract">Information about the user to create.</param>
        /// <returns>A response with a (potentially <see langword="null"/>) <see cref="UserBasicDto"/>.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdvancedPermission(MenuName = Components.Constants.MenuName, Permission = Components.Constants.AddUser)]
        public IActionResult CreateUser(CreateUserContract contract)
        {
            try
            {
                var settings = new RegisterationDetails
                {
                    PortalSettings = this.PortalSettings,
                    Email = contract.Email,
                    FirstName = contract.FirstName,
                    LastName = contract.LastName,
                    UserName = contract.UserName,
                    Password = contract.Password,
                    Question = contract.Question,
                    Answer = contract.Answer,
                    Notify = contract.Notify,
                    Authorize = contract.Authorize,
                    RandomPassword = contract.RandomPassword,
                    IgnoreRegistrationMode = true,
                };

                var userInfo = RegisterController.Instance.Register(settings);
                UserBasicDto response = null;
                if (userInfo != null)
                {
                    var userDetail = Components.UsersController.Instance.GetUserDetail(this.PortalId, userInfo.UserId);
                    response = UserBasicDto.FromUserDetails(userDetail);
                }

                return this.Ok(response);
            }
            catch (Exception ex)
            {
                Logger.UsersControllerCreateUserException(ex);
                if (ex.GetType() == typeof(InvalidUserRegisterException))
                {
                    return DnnApiResults.Error(HttpStatusCode.BadRequest, ex.Message);
                }
                else
                {
                    return DnnApiResults.Error(HttpStatusCode.InternalServerError, ex.Message);
                }
            }
        }

        /// <summary>Perform a search on Users registered in the Site.</summary>
        /// <param name="searchText">Search filter text (if any).</param>
        /// <param name="filter">User filter. Send -1 to disable.</param>
        /// <param name="pageIndex">Page index to begin from (0, 1, 2).</param>
        /// <param name="pageSize">Number of records to return per page.</param>
        /// <param name="sortColumn">Column to sort on.</param>
        /// <param name="sortAscending">Sort ascending or descending.</param>
        /// <returns>A response with a collection of <see cref="UserBasicDto"/> and total results count.</returns>
        [HttpGet]
        public IActionResult GetUsers(string searchText, UserFilters filter, int pageIndex, int pageSize, string sortColumn, bool sortAscending)
        {
            try
            {
                int totalRecords;
                var getUsersContract = new GetUsersContract
                {
                    SearchText = searchText,
                    PageIndex = pageIndex,
                    PageSize = pageSize,
                    SortColumn = sortColumn,
                    SortAscending = sortAscending,
                    PortalId = PortalController.GetEffectivePortalId(this.PortalId),
                    Filter = filter,
                };

                var results = Components.UsersController.Instance.GetUsers(getUsersContract, this.UserInfo.IsSuperUser, out totalRecords);
                var response = new
                {
                    Results = results,
                    TotalResults = totalRecords,
                };

                return this.Ok(response);
            }
            catch (Exception ex)
            {
                Logger.UsersControllerGetUsersException(ex);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpGet]
        public IActionResult GetUserFilters()
        {
            try
            {
                return this.Ok(Components.UsersController.Instance.GetUserFilters(this.UserInfo.IsSuperUser));
            }
            catch (Exception exc)
            {
                Logger.UsersControllerGetUserFiltersException(exc);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, exc);
            }
        }

        /// <summary>Get User Detail Info.</summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>A response with a <see cref="UserDetailDto"/>.</returns>
        [HttpGet]
        public IActionResult GetUserDetail(int userId)
        {
            try
            {
                var userDetail = Components.UsersController.Instance.GetUserDetail(this.PortalId, userId);
                if (userDetail == null)
                {
                    return DnnApiResults.Error(
                        HttpStatusCode.NotFound,
                        Localization.GetString("UserNotFound", Components.Constants.LocalResourcesFile));
                }

                if (userDetail.IsSuperUser)
                {
                    if (!this.UserInfo.IsSuperUser)
                    {
                        return DnnApiResults.Error(
                            HttpStatusCode.Unauthorized,
                            Localization.GetString("InSufficientPermissions.Text", Components.Constants.LocalResourcesFile));
                    }

                    userDetail = Components.UsersController.Instance.GetUserDetail(Null.NullInteger, userId);
                }

                return this.Ok(userDetail);
            }
            catch (Exception ex)
            {
                Logger.UsersControllerGetUserDetailException(ex);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdvancedPermission(MenuName = Components.Constants.MenuName, Permission = Components.Constants.ManagePassword)]
        public IActionResult ChangePassword(ChangePasswordDto changePasswordDto)
        {
            try
            {
                var userId = changePasswordDto.UserId;
                var password = changePasswordDto.Password;
                var user = Components.UsersController.GetUser(userId, this.PortalSettings, this.UserInfo, out var response);
                if (user == null)
                {
                    return DnnApiResults.Error(response.Key, response.Value);
                }

                var controller = Components.UsersController.Instance;
                controller.ChangePassword(this.PortalId, userId, password);

                return this.Ok(new { Success = true, });
            }
            catch (InvalidPasswordException exc)
            {
                Logger.UsersControllerChangePasswordInvalidPasswordException(exc);
                return DnnApiResults.Error(HttpStatusCode.BadRequest, exc.Message);
            }
            catch (Exception ex)
            {
                Logger.UsersControllerChangePasswordGeneralException(ex);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdvancedPermission(MenuName = Components.Constants.MenuName, Permission = Components.Constants.ManagePassword)]
        public IActionResult ForceChangePassword(int userId)
        {
            try
            {
                var user = Components.UsersController.GetUser(userId, this.PortalSettings, this.UserInfo, out var response);
                if (user == null)
                {
                    return DnnApiResults.Error(response.Key, response.Value);
                }

                if (this.IsCurrentUser(userId, out var errorResponse))
                {
                    return errorResponse;
                }

                return Components.UsersController.Instance.ForceChangePassword(user, this.PortalId, true)
                    ? this.Ok(new { Success = true, })
                    : DnnApiResults.Error(
                        HttpStatusCode.BadRequest,
                        Localization.GetString("OptionUnavailable", Components.Constants.LocalResourcesFile));
            }
            catch (Exception ex)
            {
                Logger.UsersControllerForceChangePasswordException(ex);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdvancedPermission(MenuName = Components.Constants.MenuName, Permission = Components.Constants.ManagePassword)]
        public IActionResult SendPasswordResetLink(int userId)
        {
            try
            {
                KeyValuePair<HttpStatusCode, string> response;
                var user = Components.UsersController.GetUser(userId, this.PortalSettings, this.UserInfo, out response);
                if (user == null)
                {
                    return DnnApiResults.Error(response.Key, response.Value);
                }

                var errorMessage = string.Empty;
                if (MembershipProviderConfig.RequiresQuestionAndAnswer)
                {
                    errorMessage = Localization.GetString("OptionUnavailable", Components.Constants.LocalResourcesFile);
                }
                else
                {
                    try
                    {
                        // create resettoken
                        UserController.ResetPasswordToken(user, Host.AdminMembershipResetLinkValidity);

                        var canSend = Mail.SendMail(user, MessageType.PasswordReminder, this.PortalSettings) == string.Empty;
                        if (!canSend)
                        {
                            errorMessage = Localization.GetString("OptionUnavailable", Components.Constants.LocalResourcesFile);
                        }
                        else
                        {
                            return this.Ok(new { Success = true, });
                        }
                    }
                    catch (ArgumentException exc)
                    {
                        Logger.UsersControllerCreateResetTokenArgumentException(exc);
                        errorMessage = Localization.GetString("InvalidPasswordAnswer", Components.Constants.LocalResourcesFile);
                    }
                    catch (Exception exc)
                    {
                        Logger.UsersControllerCreateResetTokenGeneralException(exc);
                        errorMessage = Localization.GetString("PasswordResetFailed", Components.Constants.LocalResourcesFile);
                    }
                }

                return DnnApiResults.Error(HttpStatusCode.BadRequest, errorMessage);
            }
            catch (Exception ex)
            {
                Logger.UsersControllerSendPasswordResetLinkException(ex);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdvancedPermission(MenuName = Components.Constants.MenuName, Permission = Components.Constants.AuthorizeUnAuthorizeUser)]
        public IActionResult UpdateAuthorizeStatus(int userId, bool authorized)
        {
            try
            {
                var user = Components.UsersController.GetUser(userId, this.PortalSettings, this.UserInfo, out var response);
                if (user == null)
                {
                    return DnnApiResults.Error(response.Key, response.Value);
                }

                if (this.IsCurrentUser(userId, out var errorResponse))
                {
                    return errorResponse;
                }

                if (user.Membership.Approved == authorized)
                {
                    // Do nothing if the new status is same as current status.
                    return this.Ok(new { Success = true, });
                }

                Components.UsersController.Instance.UpdateAuthorizeStatus(user, this.PortalId, authorized);
                return this.Ok(new { Success = true, });
            }
            catch (Exception ex)
            {
                Logger.UsersControllerUpdateAuthorizeStatusException(ex);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdvancedPermission(MenuName = Components.Constants.MenuName, Permission = Components.Constants.DeleteUser)]
        public IActionResult SoftDeleteUser(int userId)
        {
            try
            {
                KeyValuePair<HttpStatusCode, string> response;
                var user = Components.UsersController.GetUser(userId, this.PortalSettings, this.UserInfo, out response);
                if (user == null)
                {
                    return DnnApiResults.Error(response.Key, response.Value);
                }

                var deleted = !user.IsDeleted && UserController.DeleteUser(ref user, true, false);

                return !deleted
                    ? DnnApiResults.Error(
                        HttpStatusCode.InternalServerError,
                        Localization.GetString("UserDeleteError", Components.Constants.LocalResourcesFile))
                    : this.Ok(new { Success = true, });
            }
            catch (Exception ex)
            {
                Logger.UsersControllerSoftDeleteUserException(ex);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdvancedPermission(MenuName = Components.Constants.MenuName, Permission = Components.Constants.DeleteUser)]
        public IActionResult HardDeleteUser(int userId)
        {
            try
            {
                var user = Components.UsersController.GetUser(userId, this.PortalSettings, this.UserInfo, out var response);
                if (user == null)
                {
                    return DnnApiResults.Error(response.Key, response.Value);
                }

                var deleted = user.IsDeleted && UserController.RemoveUser(user);
                return !deleted
                    ? DnnApiResults.Error(
                        HttpStatusCode.InternalServerError,
                        Localization.GetString("UserRemoveError", Components.Constants.LocalResourcesFile))
                    : this.Ok(new { Success = true, });
            }
            catch (Exception ex)
            {
                Logger.UsersControllerHardDeleteUserException(ex);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveDeletedUsers()
        {
            if (!this.UserInfo.IsSuperUser)
            {
                if (!this.UserInfo.IsInRole(this.PortalSettings.AdministratorRoleName))
                {
                    return DnnApiResults.Error(HttpStatusCode.BadRequest, Localization.GetString("InSufficientPermissions", Components.Constants.LocalResourcesFile));
                }
            }

            UserController.RemoveDeletedUsers(this.PortalSettings.PortalId);
            var remaining = UserController.GetDeletedUsers(this.PortalSettings.PortalId);
            if (remaining.Count > 0)
            {
                return DnnApiResults.Error(HttpStatusCode.BadRequest, Localization.GetString("CouldNotRemoveAll", Components.Constants.LocalResourcesFile));
            }

            return this.Ok(new { Success = true, });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdvancedPermission(MenuName = Components.Constants.MenuName, Permission = Components.Constants.DeleteUser)]
        public IActionResult RestoreDeletedUser(int userId)
        {
            try
            {
                var user = Components.UsersController.GetUser(userId, this.PortalSettings, this.UserInfo, out var response);
                if (user == null)
                {
                    return DnnApiResults.Error(response.Key, response.Value);
                }

                var restored = user.IsDeleted && UserController.RestoreUser(ref user);
                return !restored
                    ? DnnApiResults.Error(
                        HttpStatusCode.InternalServerError,
                        Localization.GetString("UserRestoreError", Components.Constants.LocalResourcesFile))
                    : this.Ok(new { Success = true, });
            }
            catch (Exception ex)
            {
                Logger.UsersControllerRestoreDeletedUserException(ex);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireHost]
        public IActionResult UpdateSuperUserStatus(int userId, bool setSuperUser)
        {
            try
            {
                var user = Components.UsersController.GetUser(userId, this.PortalSettings, this.UserInfo, out var response);
                if (user == null)
                {
                    return DnnApiResults.Error(response.Key, response.Value);
                }

                user.IsSuperUser = setSuperUser;

                // Update User
                UserController.UpdateUser(this.PortalId, user);
                DataCache.ClearCache();

                return this.Ok(new { Success = true, });
            }
            catch (Exception ex)
            {
                Logger.UsersControllerUpdateSuperUserStatusException(ex);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdvancedPermission(MenuName = Components.Constants.MenuName, Permission = Components.Constants.EditSettings)]
        public IActionResult UpdateUserBasicInfo(UserBasicDto userBasicDto)
        {
            try
            {
                Validate(userBasicDto);
                var user = Components.UsersController.GetUser(userBasicDto.UserId, this.PortalSettings, this.UserInfo, out var response);
                if (user == null)
                {
                    return DnnApiResults.Error(response.Key, response.Value);
                }

                var updatedUser = Components.UsersController.Instance.UpdateUserBasicInfo(userBasicDto);

                return this.Ok(updatedUser);
            }
            catch (SqlException ex)
            {
                Logger.UsersControllerUpdateUserBasicInfoSqlException(ex);
                return DnnApiResults.Error(
                    HttpStatusCode.BadRequest,
                    Localization.GetString("UsernameNotUnique", Components.Constants.LocalResourcesFile));
            }
            catch (Exception ex)
            {
                Logger.UsersControllerUpdateUserBasicInfoGeneralException(ex);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdvancedPermission(MenuName = Components.Constants.MenuName, Permission = Components.Constants.AuthorizeUnAuthorizeUser)]
        public IActionResult UnlockUser(int userId)
        {
            try
            {
                var user = Components.UsersController.GetUser(userId, this.PortalSettings, this.UserInfo, out var response);
                if (user == null)
                {
                    return DnnApiResults.Error(response.Key, response.Value);
                }

                if (this.IsCurrentUser(userId, out var errorResponse))
                {
                    return errorResponse;
                }

                var unlocked = user.Membership.LockedOut && UserController.UnLockUser(user);
                return !unlocked
                    ? DnnApiResults.Error(
                        HttpStatusCode.InternalServerError,
                        Localization.GetString("UserUnlockError", Components.Constants.LocalResourcesFile))
                    : this.Ok(new { Success = true, });
            }
            catch (Exception ex)
            {
                Logger.UsersControllerUnlockUserException(ex);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpGet]
        [AdvancedPermission(MenuName = Components.Constants.MenuName, Permission = Components.Constants.ManageRoles)]
        public IActionResult GetSuggestRoles(string keyword, int count)
        {
            try
            {
                if (string.IsNullOrEmpty(keyword))
                {
                    return this.Ok(new List<UserRoleInfo>());
                }

                var isAdmin = Components.UsersController.IsAdmin(this.PortalSettings);

                var roles = RoleController.Instance.GetRoles(
                    this.PortalId,
                    x => x.RoleName.ToUpperInvariant().Contains(keyword.ToUpperInvariant()));
                var matchedRoles = roles
                    .Where(
                        r =>
                            (isAdmin || r.RoleID != this.PortalSettings.AdministratorRoleId) &&
                            r.Status == RoleStatus.Approved)
                    .ToList().Take(count).Select(u => new UserRoleInfo
                    {
                        RoleID = u.RoleID,
                        RoleName = $"{u.RoleName}",
                        SecurityMode = u.SecurityMode,
                    });

                return this.Ok(matchedRoles.ToList().Select(r => UserRoleDto.FromRoleInfo(this.PortalSettings, r)));
            }
            catch (Exception ex)
            {
                Logger.UsersControllerGetSuggestRolesException(ex);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpGet]
        [AdvancedPermission(MenuName = Components.Constants.MenuName, Permission = Components.Constants.ManageRoles)]
        public IActionResult GetUserRoles(string keyword, int userId, int pageIndex, int pageSize)
        {
            try
            {
                var user = Components.UsersController.GetUser(userId, this.PortalSettings, this.UserInfo, out var response);
                if (user == null)
                {
                    return DnnApiResults.Error(response.Key, response.Value);
                }

                var userRoles = Components.UsersController.Instance.GetUserRoles(user, keyword, out var totalRoles, pageIndex, pageSize)
                        .Select(r => UserRoleDto.FromRoleInfo(this.PortalSettings, r));

                return this.Ok(new { UserRoles = userRoles, TotalRecords = totalRoles });
            }
            catch (Exception ex)
            {
                Logger.UsersControllerGetUserRolesException(ex);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdvancedPermission(MenuName = Components.Constants.MenuName, Permission = Components.Constants.ManageRoles)]
        public IActionResult SaveUserRole(UserRoleDto userRoleDto, bool notifyUser, bool isOwner)
        {
            try
            {
                Validate(userRoleDto);
                var user = Components.UsersController.GetUser(userRoleDto.UserId, this.PortalSettings, this.UserInfo, out var response);
                if (user == null)
                {
                    return DnnApiResults.Error(response.Key, response.Value);
                }

                var result = Components.UsersController.Instance.SaveUserRole(
                    this.PortalId,
                    this.UserInfo,
                    userRoleDto,
                    notifyUser,
                    isOwner);

                return this.Ok(result);
            }
            catch (Exception ex)
            {
                Logger.UsersControllerSaveUserRoleException(ex);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdvancedPermission(MenuName = Components.Constants.MenuName, Permission = Components.Constants.ManageRoles)]
        public IActionResult RemoveUserRole(UserRoleDto userRoleDto)
        {
            try
            {
                Validate(userRoleDto);
                var user = Components.UsersController.GetUser(userRoleDto.UserId, this.PortalSettings, this.UserInfo, out var response);
                if (user == null)
                {
                    return DnnApiResults.Error(response.Key, response.Value);
                }

                RoleController.Instance.UpdateUserRole(
                    this.PortalId,
                    userRoleDto.UserId,
                    userRoleDto.RoleId,
                    RoleStatus.Approved,
                    false,
                    true);

                return this.Ok(new { Success = true, });
            }
            catch (Exception ex)
            {
                Logger.UsersControllerRemoveUserRoleException(ex);
                return DnnApiResults.Error(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        // User profile is not implemented currently
        //        [HttpGet]
        //        public IActionResult GetProfileDefinitions()
        //        {
        //            var profileDefinitions = ProfileController.GetPropertyDefinitionsByPortal(PortalId)
        //                .Cast<ProfilePropertyDefinition>().Select(d => new ProfileDefinitionDto(d));
        //
        //            return this.Ok(profileDefinitions);
        //        }
        //
        //        [HttpGet]
        //        public IActionResult GetUserProfile(int userId)
        //        {
        //            return this.Ok(new {Success = true});
        //        }

        /// <summary>Return Password security options from server.</summary>
        /// <returns>MembershipPasswordSettings.</returns>
        [HttpGet]
        [ValidateAntiForgeryToken]
        [AdvancedPermission(MenuName = Components.Constants.MenuName, Permission = Components.Constants.ManageRoles)]
        public IActionResult PasswordStrengthOptions()
        {
            var settings = new MembershipPasswordSettings(this.PortalId);

            var passwordSettings = new PasswordSettingsDto
            {
                MinLength = settings.MinPasswordLength,
                MinNumberOfSpecialChars = settings.MinNonAlphanumericCharacters,
                ValidationExpression = settings.ValidationExpression,
            };

            return this.Ok(passwordSettings);
        }

        private static void Validate(UserRoleDto userRoleDto)
        {
            Requires.NotNegative("UserId", userRoleDto.UserId);
            Requires.NotNegative("RoleId", userRoleDto.RoleId);
        }

        private static void Validate(UserBasicDto userBasicDto)
        {
            Requires.NotNegative("UserId", userBasicDto.UserId);
        }

        private bool IsCurrentUser(int userId, out IActionResult response)
        {
            response = null;
            if (userId == this.UserInfo.UserID)
            {
                response = DnnApiResults.Error(
                    HttpStatusCode.Unauthorized,
                    Localization.GetString("InSufficientPermissions", Components.Constants.LocalResourcesFile));
                return true;
            }

            return false;
        }
    }
}
