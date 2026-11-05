// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Framework
{
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Text;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Entities.Portals;

    /// <summary>DiskPageStatePersister provides a disk (stream) based page state persistence mechanism.</summary>
    /// <remarks>
    /// The state file name used to be scoped by <c>Session.SessionID</c>; it is now scoped by
    /// <see cref="PageBase.PageStateId"/>, the identity of the page component instance. The state is
    /// written with <see cref="PageStatePersister.StateFormatter"/> (JSON) rather than
    /// <c>LosFormatter</c>, which no longer exists.
    /// </remarks>
    public class DiskPageStatePersister : PageStatePersister
    {
        /// <summary>Initializes a new instance of the <see cref="DiskPageStatePersister"/> class.</summary>
        /// <param name="page">The <see cref="PageBase"/> that the state persistence mechanism is created for.</param>
        public DiskPageStatePersister(PageBase page)
            : this(page, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DiskPageStatePersister"/> class.</summary>
        /// <param name="page">The <see cref="PageBase"/> that the state persistence mechanism is created for.</param>
        /// <param name="stateFormatter">The state formatter, or <see langword="null"/> for the default.</param>
        public DiskPageStatePersister(PageBase page, IPageStateFormatter stateFormatter)
            : base(page, stateFormatter)
        {
        }

        /// <summary>
        /// Gets the CacheDirectory property is used to return the location of the "Cache"
        /// Directory for the Portal.
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public string CacheDirectory => PortalController.Instance.GetCurrentSettings().HomeSystemDirectoryMapPath + "Cache";

        /// <summary>Gets the StateFileName property is used to store the FileName for the State.</summary>
        public string StateFileName
        {
            get
            {
                var key = new StringBuilder();
                {
                    key.Append("VIEWSTATE_");
                    key.Append(this.Page.PageStateId);
                    key.Append('_');
                    key.Append(this.Page.Request?.GetRawUrl());
                }

                return Path.Combine(this.CacheDirectory, $"{Globals.CleanFileName(key.ToString())}.txt");
            }
        }

        /// <summary>Loads the Page State from disk.</summary>
        public override void Load()
        {
            // Read the state string, using the state formatter.
            using (var reader = new StreamReader(this.StateFileName))
            {
                var serializedStatePair = reader.ReadToEnd();

                // Deserialize returns the PageState object that is serialized in the Save method.
                var statePair = this.StateFormatter.Deserialize(serializedStatePair);
                this.ViewState = statePair.ViewState;
                this.ControlState = statePair.ControlState;
            }
        }

        /// <summary>Saves the Page State to disk.</summary>
        public override void Save()
        {
            // No processing needed if no states available
            if (this.ViewState == null && this.ControlState == null)
            {
                return;
            }

            if (!Directory.Exists(this.CacheDirectory))
            {
                Directory.CreateDirectory(this.CacheDirectory);
            }

            // Write a state string, using the state formatter.
            using (var writer = new StreamWriter(this.StateFileName, false))
            {
                var statePair = new PageState { ViewState = this.ViewState, ControlState = this.ControlState };
                var serializedState = this.StateFormatter.Serialize(statePair);
                writer.Write(serializedState);
            }
        }
    }
}
