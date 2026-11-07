// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Installer.Installers
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;

    using DotNetNuke.Common;
    using DotNetNuke.Data;

    /// <summary>The AssemblyInstaller installs Assembly Components to a DotNetNuke site.</summary>
    public class AssemblyInstaller : FileInstaller
    {
        /// <inheritdoc />
        public override string AllowableFiles => "dll,pdb";

        /// <summary>Gets the name of the Collection Node (<c>assemblies</c>).</summary>
        protected override string CollectionNodeName => "assemblies";

        /// <summary>Gets the default Path for the file - if not present in the manifest.</summary>
        protected override string DefaultPath => "bin" + System.IO.Path.DirectorySeparatorChar;

        /// <summary>Gets the name of the Item Node (<c>assembly</c>).</summary>
        protected override string ItemNodeName => "assembly";

        /// <summary>Gets the PhysicalBasePath for the assemblies.</summary>
        protected override string PhysicalBasePath => this.PhysicalSitePath + Path.DirectorySeparatorChar;

        /// <inheritdoc />
        [SuppressMessage("Microsoft.Naming", "CA1725:ParameterNamesShouldMatchBaseDeclaration", Justification = "Breaking change")]
        protected override void DeleteFile(InstallFile file)
        {
            // Attempt to unregister assembly
            // this will only log if the assembly is used by another package and cannot be deleted
            // if it is not being used it will be deleted
            if (DataProvider.Instance().UnRegisterAssembly(this.Package.PackageID, file.Name))
            {
                this.Log.AddInfo(Util.ASSEMBLY_UnRegistered + " - " + file.FullName);

                this.RemoveBindingRedirect(file);

                // Call base class version to deleteFile file from \bin
                base.DeleteFile(file);
            }
            else
            {
                this.Log.AddInfo(Util.ASSEMBLY_InUse + " - " + file.FullName);
            }
        }

        /// <inheritdoc />
        protected override bool IsCorrectType(InstallFileType type)
        {
            return type == InstallFileType.Assembly;
        }

        /// <inheritdoc />
        [SuppressMessage("Microsoft.Naming", "CA1725:ParameterNamesShouldMatchBaseDeclaration", Justification = "Breaking change")]
        protected override bool InstallFile(InstallFile file)
        {
            bool bSuccess = true;
            if (file.Action == "UnRegister")
            {
                var prevDeleteFiles = this.DeleteFiles;
                try
                {
                    this.DeleteFiles = true;
                    this.DeleteFile(file);
                }
                finally
                {
                    this.DeleteFiles = prevDeleteFiles;
                }
            }
            else
            {
                // Attempt to register assembly this will return False if the assembly exists and true if it does not or is older
                int returnCode = DataProvider.Instance().RegisterAssembly(this.Package.PackageID, file.Name, file.Version.ToString(3));
                switch (returnCode)
                {
                    case 0:
                        // Assembly Does Not Exist
                        this.Log.AddInfo(Util.ASSEMBLY_Added + " - " + file.FullName);
                        break;
                    case 1:
                        // Older version of Assembly Exists
                        this.Log.AddInfo(Util.ASSEMBLY_Updated + " - " + file.FullName);
                        break;
                    case 2:
                    case 3:
                        // Assembly already Registered
                        this.Log.AddInfo(Util.ASSEMBLY_Registered + " - " + file.FullName);
                        break;
                }

                // If assembly not registered, is newer (or is the same version and we are in repair mode)
                if (returnCode < 2 || (returnCode == 2 && file.InstallerInfo.RepairInstall))
                {
                    // Call base class version to copy file to \bin
                    bSuccess = base.InstallFile(file);
                    this.AddOrUpdateBindingRedirect(file);
                }
            }

            return bSuccess;
        }

        /// <summary>Adds or updates the binding redirect for the assembly file, if the assembly file it strong-named.</summary>
        /// <param name="file">The assembly file.</param>
        /// <remarks>
        /// No-op on net10.0. Assembly binding redirects were a .NET Framework CLR loader feature
        /// configured through <c>&lt;runtime&gt;&lt;assemblyBinding&gt;</c> in <c>web.config</c>.
        /// Modern .NET resolves assemblies through <c>AssemblyLoadContext</c> and the
        /// <c>.deps.json</c> dependency graph, where the newest compatible version always wins and
        /// a redirect has nothing to redirect. Applying
        /// <c>Install/Config/BindingRedirect.config</c> would write a <c>&lt;runtime&gt;</c>
        /// section that nothing reads. The merge scripts are kept on disk verbatim so that a
        /// rollback to the WebForms build can still use them; see migration-summary.md.
        /// </remarks>
        private void AddOrUpdateBindingRedirect(InstallFile file)
        {
            this.Log.AddInfo(
                $"Skipped the assembly binding redirect for {file.FullName}: binding redirects have no effect on .NET 10.");
        }

        /// <summary>Removes the binding redirect for the assembly file, if the assembly is strong-named.</summary>
        /// <param name="file">The assembly file.</param>
        /// <remarks>No-op on net10.0, for the reason given on <see cref="AddOrUpdateBindingRedirect"/>.</remarks>
        private void RemoveBindingRedirect(InstallFile file)
        {
            this.Log.AddInfo(
                $"Skipped removing the assembly binding redirect for {file.FullName}: binding redirects have no effect on .NET 10.");
        }
    }
}
