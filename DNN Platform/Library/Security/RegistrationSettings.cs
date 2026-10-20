// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Security
{
    using System.Collections;
    using System.Collections.Generic;

    using DotNetNuke.Collections;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;

    public class RegistrationSettings
    {
        /// <summary>Initializes a new instance of the <see cref="RegistrationSettings"/> class.</summary>
        public RegistrationSettings()
        {
            this.RandomPassword = false;
            this.RedirectAfterRegistration = -1;
            this.RedirectAfterLogout = -1;
            this.RedirectAfterLogin = -1;
            this.RegistrationFields = string.Empty;
            this.ExcludeTerms = string.Empty;
            this.ExcludeTermsRegex = Null.NullString;
            this.RegistrationFormType = 0;
            this.RequirePasswordConfirm = true;
            this.RequireUniqueDisplayName = false;
            this.UseAuthProviders = false;
            this.UseEmailAsUserName = false;
            this.UseProfanityFilter = false;
            this.RequireValidProfile = false;
            this.RequireValidProfileAtLogin = true;
            this.UseCaptcha = false;
            this.UserNameValidator = Globals.glbUserNameRegEx;
            this.DisplayNameFormat = string.Empty;
            this.EmailValidator = Globals.glbEmailRegEx;
        }

        /// <summary>Initializes a new instance of the <see cref="RegistrationSettings"/> class.</summary>
        /// <param name="settings">The settings dictionary.</param>
        public RegistrationSettings(Dictionary<string, string> settings)
            : this()
        {
            // use the DNN (IDictionary) GetValueOrDefault overload, which converts the setting values
            IDictionary settingsDictionary = settings;
            this.RandomPassword = settingsDictionary.GetValueOrDefault("Registration_RandomPassword", this.RandomPassword);
            this.RedirectAfterRegistration = settingsDictionary.GetValueOrDefault("Redirect_AfterRegistration", this.RedirectAfterRegistration);
            this.RedirectAfterLogout = settingsDictionary.GetValueOrDefault("Redirect_AfterLogout", this.RedirectAfterLogout);
            this.RedirectAfterLogin = settingsDictionary.GetValueOrDefault("Redirect_AfterLogin", this.RedirectAfterLogin);
            this.RegistrationFields = settingsDictionary.GetValueOrDefault("Registration_RegistrationFields", this.RegistrationFields);
            this.ExcludeTerms = settingsDictionary.GetValueOrDefault("Registration_ExcludeTerms", this.ExcludeTerms);
            this.RegistrationFormType = settingsDictionary.GetValueOrDefault("Registration_RegistrationFormType", this.RegistrationFormType);
            this.RequirePasswordConfirm = settingsDictionary.GetValueOrDefault("Registration_RequireConfirmPassword", this.RequirePasswordConfirm);
            this.RequireUniqueDisplayName = settingsDictionary.GetValueOrDefault("Registration_RequireUniqueDisplayName", this.RequireUniqueDisplayName);
            this.UseAuthProviders = settingsDictionary.GetValueOrDefault("Registration_UseAuthProviders", this.UseAuthProviders);
            this.UseEmailAsUserName = settingsDictionary.GetValueOrDefault("Registration_UseEmailAsUserName", this.UseEmailAsUserName);
            this.UseProfanityFilter = settingsDictionary.GetValueOrDefault("Registration_UseProfanityFilter", this.UseProfanityFilter);
            this.RequireValidProfile = settingsDictionary.GetValueOrDefault("Security_RequireValidProfile", this.RequireValidProfile);
            this.RequireValidProfileAtLogin = settingsDictionary.GetValueOrDefault("Security_RequireValidProfileAtLogin", this.RequireValidProfileAtLogin);
            this.UseCaptcha = settingsDictionary.GetValueOrDefault("Security_CaptchaRegister", this.UseCaptcha);
            this.UserNameValidator = settingsDictionary.GetValueOrDefault("Security_UserNameValidation", this.UserNameValidator);
            this.DisplayNameFormat = settingsDictionary.GetValueOrDefault("Security_DisplayNameFormat", this.DisplayNameFormat);
            this.EmailValidator = settingsDictionary.GetValueOrDefault("Security_EmailValidation", this.EmailValidator);

            this.ExcludeTermsRegex = "^(?:(?!" + this.ExcludeTerms.Replace(" ", string.Empty).Replace(",", "|") + ").)*$\\r?\\n?";
        }

        public bool RandomPassword { get; set; }

        public int RedirectAfterRegistration { get; set; }

        public int RedirectAfterLogout { get; set; }

        public int RedirectAfterLogin { get; set; }

        public string RegistrationFields { get; set; }

        public string ExcludeTerms { get; set; }

        public string ExcludeTermsRegex { get; set; }

        public int RegistrationFormType { get; set; }

        public bool RequirePasswordConfirm { get; set; }

        public bool RequireUniqueDisplayName { get; set; }

        public bool UseAuthProviders { get; set; }

        public bool UseEmailAsUserName { get; set; }

        public bool UseProfanityFilter { get; set; }

        public bool RequireValidProfile { get; set; }

        public bool RequireValidProfileAtLogin { get; set; }

        public bool UseCaptcha { get; set; }

        public string UserNameValidator { get; set; }

        public string DisplayNameFormat { get; set; }

        public string EmailValidator { get; set; }
    }
}
