// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
// 
// Licensed to the Apache Software Foundation (ASF) under one or more
// contributor license agreements. See the NOTICE file distributed with
// this work for additional information regarding copyright ownership.
// The ASF licenses this file to you under the Apache License, Version 2.0
// (the "License"); you may not use this file except in compliance with
// the License. You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// 

using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;

using Microsoft.Win32.SafeHandles;

using log4net.Core;

namespace log4net.Util
{
    /// <summary>Impersonate a Windows Account</summary>
    /// <remarks>
    /// <para>
    /// This <see cref="SecurityContext"/> impersonates a Windows account.
    /// </para>
    /// <para>
    /// How the impersonation is done depends on the value of <see cref="Impersonate"/>.
    /// This allows the context to either impersonate a set of user credentials specified 
    /// using username, domain name and password or to revert to the process credentials.
    /// </para>
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public class WindowsSecurityContext : SecurityContext, IOptionHandler
    {
        /// <summary>The impersonation modes for the <see cref="WindowsSecurityContext"/></summary>
        /// <remarks>
        /// <para>
        /// See the <see cref="WindowsSecurityContext.Credentials"/> property for
        /// details.
        /// </para>
        /// </remarks>
        public enum ImpersonationMode
        {
            /// <summary>Impersonate a user using the credentials supplied</summary>
            User,

            /// <summary>Revert this the thread to the credentials of the process</summary>
            Process
        }

        /// <summary>
        /// The fully qualified type of the WindowsSecurityContext class.
        /// </summary>
        private static readonly Type declaringType = typeof(WindowsSecurityContext);

        private ImpersonationMode m_impersonationMode = ImpersonationMode.User;
        private string m_userName;
        private string m_domainName = Environment.MachineName;
        private string m_password;
        private WindowsIdentity m_identity;

        /// <summary>Default constructor</summary>
        /// <remarks>
        /// <para>
        /// Default constructor
        /// </para>
        /// </remarks>
        public WindowsSecurityContext()
        {
        }

        /// <summary>Gets or sets the impersonation mode for this security context</summary>
        /// <value>
        /// The impersonation mode for this security context
        /// </value>
        /// <remarks>
        /// <para>
        /// Impersonate either a user with user credentials or
        /// revert this thread to the credentials of the process.
        /// The value is one of the <see cref="ImpersonationMode"/>
        /// enum.
        /// </para>
        /// <para>
        /// The default value is <see cref="ImpersonationMode.User"/>
        /// </para>
        /// <para>
        /// When the mode is set to <see cref="ImpersonationMode.User"/>
        /// the user's credentials are established using the
        /// <see cref="UserName"/>, <see cref="DomainName"/> and <see cref="Password"/>
        /// values.
        /// </para>
        /// <para>
        /// When the mode is set to <see cref="ImpersonationMode.Process"/>
        /// no other properties need to be set. If the calling thread is 
        /// impersonating then it will be reverted back to the process credentials.
        /// </para>
        /// </remarks>
        public ImpersonationMode Credentials
        {
            get { return this.m_impersonationMode; }
            set { this.m_impersonationMode = value; }
        }

        /// <summary>Gets or sets the Windows username for this security context</summary>
        /// <value>
        /// The Windows username for this security context
        /// </value>
        /// <remarks>
        /// <para>
        /// This property must be set if <see cref="Credentials"/>
        /// is set to <see cref="ImpersonationMode.User"/> (the default setting).
        /// </para>
        /// </remarks>
        public string UserName
        {
            get { return this.m_userName; }
            set { this.m_userName = value; }
        }

        /// <summary>Gets or sets the Windows domain name for this security context</summary>
        /// <value>
        /// The Windows domain name for this security context
        /// </value>
        /// <remarks>
        /// <para>
        /// The default value for <see cref="DomainName"/> is the local machine name
        /// taken from the <see cref="Environment.MachineName"/> property.
        /// </para>
        /// <para>
        /// This property must be set if <see cref="Credentials"/>
        /// is set to <see cref="ImpersonationMode.User"/> (the default setting).
        /// </para>
        /// </remarks>
        public string DomainName
        {
            get { return this.m_domainName; }
            set { this.m_domainName = value; }
        }

        /// <summary>Sets the password for the Windows account specified by the <see cref="UserName"/> and <see cref="DomainName"/> properties.</summary>
        /// <value>
        /// The password for the Windows account specified by the <see cref="UserName"/> and <see cref="DomainName"/> properties.
        /// </value>
        /// <remarks>
        /// <para>
        /// This property must be set if <see cref="Credentials"/>
        /// is set to <see cref="ImpersonationMode.User"/> (the default setting).
        /// </para>
        /// </remarks>
        public string Password
        {
            set { this.m_password = value; }
        }

        /// <summary>Initialize the SecurityContext based on the options set.</summary>
        /// <remarks>
        /// <para>
        /// This is part of the <see cref="IOptionHandler"/> delayed object
        /// activation scheme. The <see cref="ActivateOptions"/> method must 
        /// be called on this object after the configuration properties have
        /// been set. Until <see cref="ActivateOptions"/> is called this
        /// object is in an undefined state and must not be used. 
        /// </para>
        /// <para>
        /// If any of the configuration properties are modified then 
        /// <see cref="ActivateOptions"/> must be called again.
        /// </para>
        /// <para>
        /// The security context will try to Logon the specified user account and
        /// capture a primary token for impersonation.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">The required <see cref="UserName" />, 
        /// <see cref="DomainName" /> or <see cref="Password" /> properties were not specified.</exception>
        public void ActivateOptions()
        {
            if (this.m_impersonationMode == ImpersonationMode.User)
            {
                if (this.m_userName == null)
                {
                    throw new ArgumentNullException("m_userName");
                }

                if (this.m_domainName == null)
                {
                    throw new ArgumentNullException("m_domainName");
                }

                if (this.m_password == null)
                {
                    throw new ArgumentNullException("m_password");
                }

                if (!OperatingSystem.IsWindows())
                {
                    // The Win32 logon APIs only exist on Windows. The context stays configured so
                    // that log4net configuration parsing never fails; Impersonate is then a
                    // documented no-op (it returns null, i.e. "no impersonation scope").
                    LogLog.Warn(declaringType, "WindowsSecurityContext: Windows impersonation is only available on Windows; this security context is inactive on the current platform.");
                    return;
                }

                this.m_identity = LogonUser(this.m_userName, this.m_domainName, this.m_password);
            }
        }

        /// <summary>Impersonate the Windows account specified by the <see cref="UserName"/> and <see cref="DomainName"/> properties.</summary>
        /// <param name="state">caller provided state</param>
        /// <returns>
        /// An <see cref="IDisposable"/> instance that will revoke the impersonation of this SecurityContext
        /// </returns>
        /// <remarks>
        /// <para>
        /// Depending on the <see cref="Credentials"/> property either
        /// impersonate a user using credentials supplied or revert 
        /// to the process credentials.
        /// </para>
        /// <para>
        /// <c>WindowsImpersonationContext</c> no longer exists on modern .NET. The scope based
        /// contract of this method (23 <c>using (securityContext.Impersonate(this))</c> call sites
        /// in this assembly) cannot be expressed with the callback based
        /// <see cref="WindowsIdentity.RunImpersonated(SafeAccessTokenHandle, Action)"/>, so the
        /// impersonation scope is established with the supported
        /// <c>ImpersonateLoggedOnUser</c>/<c>RevertToSelf</c> Win32 APIs, which is exactly what
        /// <c>RunImpersonated</c> itself uses internally.
        /// </para>
        /// </remarks>
        public override IDisposable Impersonate(object state)
        {
            if (!OperatingSystem.IsWindows())
            {
                // Documented no-op on non-Windows platforms: no impersonation is performed and
                // the caller's using(...) block simply operates on the current credentials.
                return null;
            }

            if (this.m_impersonationMode == ImpersonationMode.User)
            {
                if (this.m_identity != null)
                {
                    return new DisposableImpersonationContext(this.m_identity.AccessToken);
                }
            }
            else if (this.m_impersonationMode == ImpersonationMode.Process)
            {
                // A null token reverts to the process credentials, matching the legacy
                // WindowsIdentity.Impersonate(IntPtr.Zero) behaviour.
                return new DisposableImpersonationContext(null);
            }
            return null;
        }

        /// <summary>Create a <see cref="WindowsIdentity"/> given the userName, domainName and password.</summary>
        /// <param name="userName">the user name</param>
        /// <param name="domainName">the domain name</param>
        /// <param name="password">the password</param>
        /// <returns>the <see cref="WindowsIdentity"/> for the account specified</returns>
        /// <remarks>
        /// <para>
        /// Uses the Windows API call LogonUser to get a principal token for the account. This
        /// token is used to initialize the WindowsIdentity.
        /// </para>
        /// </remarks>
        private static WindowsIdentity LogonUser(string userName, string domainName, string password)
        {
            const int LOGON32_PROVIDER_DEFAULT = 0;
            //This parameter causes LogonUser to create a primary token.
            const int LOGON32_LOGON_INTERACTIVE = 2;

            // Call LogonUser to obtain a handle to an access token.
            IntPtr tokenHandle = IntPtr.Zero;
            if(!LogonUser(userName, domainName, password, LOGON32_LOGON_INTERACTIVE, LOGON32_PROVIDER_DEFAULT, ref tokenHandle))
            {
                NativeError error = NativeError.GetLastError();
                throw new Exception("Failed to LogonUser ["+userName+"] in Domain ["+domainName+"]. Error: "+ error.ToString());
            }

            const int SecurityImpersonation = 2;
            IntPtr dupeTokenHandle = IntPtr.Zero;
            if(!DuplicateToken(tokenHandle, SecurityImpersonation, ref dupeTokenHandle))
            {
                NativeError error = NativeError.GetLastError();
                if (tokenHandle != IntPtr.Zero)
                {
                    CloseHandle(tokenHandle);
                }
                throw new Exception("Failed to DuplicateToken after LogonUser. Error: " + error.ToString());
            }

            WindowsIdentity identity = new WindowsIdentity(dupeTokenHandle);

            // Free the tokens.
            if (dupeTokenHandle != IntPtr.Zero) 
            {
                CloseHandle(dupeTokenHandle);
            }
            if (tokenHandle != IntPtr.Zero)
            {
                CloseHandle(tokenHandle);
            }

            return identity;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool LogonUser(String lpszUsername, String lpszDomain, String lpszPassword, int dwLogonType, int dwLogonProvider, ref IntPtr phToken);

        [DllImport("kernel32.dll", CharSet=CharSet.Auto)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("advapi32.dll", CharSet=CharSet.Auto, SetLastError=true)]
        private static extern bool DuplicateToken(IntPtr ExistingTokenHandle, int SECURITY_IMPERSONATION_LEVEL, ref IntPtr DuplicateTokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool ImpersonateLoggedOnUser(SafeAccessTokenHandle hToken);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool RevertToSelf();

        /// <summary>Scope based Windows impersonation exposed through <see cref="IDisposable"/></summary>
        /// <remarks>
        /// <para>
        /// Establishes the impersonation in the constructor and restores whatever impersonation
        /// (if any) was in effect beforehand when disposed. This replaces the removed
        /// <c>WindowsImpersonationContext</c> type while preserving its exact semantics.
        /// </para>
        /// </remarks>
        [SupportedOSPlatform("windows")]
        private sealed class DisposableImpersonationContext : IDisposable
        {
            private readonly WindowsIdentity m_previousIdentity;
            private bool m_disposed;

            /// <summary>Constructor</summary>
            /// <param name="token">
            /// The access token to impersonate, or <c>null</c> to revert to the process credentials.
            /// </param>
            /// <remarks>
            /// <para>
            /// Constructor
            /// </para>
            /// </remarks>
            public DisposableImpersonationContext(SafeAccessTokenHandle token)
            {
                // Capture any impersonation currently in effect so Dispose can restore it.
                // Returns null when the thread is running as the process account.
                this.m_previousIdentity = WindowsIdentity.GetCurrent(true);

                if (token == null || token.IsInvalid)
                {
                    if (!RevertToSelf())
                    {
                        NativeError error = NativeError.GetLastError();
                        throw new Exception("Failed to RevertToSelf. Error: " + error.ToString());
                    }
                }
                else if (!ImpersonateLoggedOnUser(token))
                {
                    NativeError error = NativeError.GetLastError();
                    throw new Exception("Failed to ImpersonateLoggedOnUser. Error: " + error.ToString());
                }
            }

            /// <summary>Revert the impersonation</summary>
            /// <remarks>
            /// <para>
            /// Revert the impersonation
            /// </para>
            /// </remarks>
            public void Dispose()
            {
                if (this.m_disposed)
                {
                    return;
                }

                this.m_disposed = true;

                // Never throw from Dispose, but never fail silently either: a failed revert
                // would leave the thread impersonating, which is security relevant.
                if (!RevertToSelf())
                {
                    LogLog.Error(
                        declaringType,
                        "Failed to RevertToSelf while reverting impersonation. Error: " + NativeError.GetLastError().ToString());
                }

                if (this.m_previousIdentity != null)
                {
                    // Restore the impersonation that was in effect before this scope.
                    if (!this.m_previousIdentity.AccessToken.IsInvalid)
                    {
                        if (!ImpersonateLoggedOnUser(this.m_previousIdentity.AccessToken))
                        {
                            LogLog.Error(
                                declaringType,
                                "Failed to restore the previous impersonation context. Error: " + NativeError.GetLastError().ToString());
                        }
                    }

                    this.m_previousIdentity.Dispose();
                }
            }
        }
    }
}

