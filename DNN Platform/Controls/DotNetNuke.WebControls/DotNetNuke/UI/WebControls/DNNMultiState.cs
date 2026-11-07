// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System.ComponentModel;

    /// <summary>
    /// One of the states a <see cref="DNNMultiStateBox"/> can cycle through.
    /// </summary>
    /// <remarks>
    /// The <c>PersistChildren</c> / <c>ParseChildren</c> annotations of the original were
    /// <c>System.Web.UI</c> markup-parsing directives for the ASPX parser and have no .NET 10
    /// equivalent; the type itself is a plain data holder and is carried over unchanged.
    /// </remarks>
    public class DNNMultiState
    {
        private string key;
        private string text;
        private string imageUrl;
        private string disabledImageUrl;
        private string toolTip;

        /// <summary>Initializes a new instance of the <see cref="DNNMultiState"/> class.</summary>
        public DNNMultiState()
        {
            this.key = string.Empty;
            this.text = string.Empty;
            this.imageUrl = string.Empty;
            this.disabledImageUrl = string.Empty;
            this.toolTip = string.Empty;
        }

        /// <summary>Initializes a new instance of the <see cref="DNNMultiState"/> class.</summary>
        /// <param name="key">The key identifying the state.</param>
        /// <param name="text">The text of the state.</param>
        /// <param name="imageUrl">The image of the state.</param>
        /// <param name="disabledImageUrl">The image of the state while it is disabled.</param>
        /// <param name="toolTip">The tooltip of the state.</param>
        public DNNMultiState(string key, string text, string imageUrl, string disabledImageUrl, string toolTip)
            : this()
        {
            this.key = key;
            this.text = text;
            this.imageUrl = imageUrl;
            this.disabledImageUrl = disabledImageUrl;
            this.toolTip = toolTip;
        }

        /// <summary>Gets or sets the key identifying the state.</summary>
        [NotifyParentProperty(true)]
        public string Key
        {
            get
            {
                return this.key;
            }

            set
            {
                this.key = value;
                this.NotifyDesigner();
            }
        }

        /// <summary>Gets or sets the text of the state.</summary>
        [DefaultValue("")]
        [Category("Appearance")]
        [NotifyParentProperty(true)]
        public string Text
        {
            get
            {
                return this.text;
            }

            set
            {
                this.text = value;
                this.NotifyDesigner();
            }
        }

        /// <summary>Gets or sets the tooltip of the state.</summary>
        [DefaultValue("")]
        [NotifyParentProperty(true)]
        [Category("Appearance")]
        public string ToolTip
        {
            get
            {
                return this.toolTip;
            }

            set
            {
                this.toolTip = value;
                this.NotifyDesigner();
            }
        }

        /// <summary>Gets or sets the image of the state.</summary>
        [DefaultValue("")]
        [NotifyParentProperty(true)]
        [Category("Appearance")]
        public string ImageUrl
        {
            get
            {
                return this.imageUrl;
            }

            set
            {
                this.imageUrl = value;
                this.NotifyDesigner();
            }
        }

        /// <summary>Gets or sets the image of the state while it is disabled.</summary>
        [DefaultValue("")]
        [Category("Appearance")]
        [NotifyParentProperty(true)]
        public string DisabledImageUrl
        {
            get
            {
                return this.disabledImageUrl;
            }

            set
            {
                this.disabledImageUrl = value;
                this.NotifyDesigner();
            }
        }

        /// <summary>Gets or sets the box this state belongs to.</summary>
        internal DNNMultiStateBox Parent { get; set; }

        /// <summary>Tells the designer of the owning box that this state changed.</summary>
        internal void NotifyDesigner()
        {
            if (this.Parent is not null)
            {
                this.Parent.NotifyDesigner();
            }
        }
    }
}
