// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System.Collections;

    /// <summary>The <see cref="DNNMultiStateCollection"/> class.</summary>
    public class DNNMultiStateCollection : CollectionBase
    {
        private readonly DNNMultiStateBox ctlValue;

        /// <summary>Initializes a new instance of the <see cref="DNNMultiStateCollection"/> class.</summary>
        /// <param name="ctl">The ctl.</param>
        public DNNMultiStateCollection(DNNMultiStateBox ctl)
        {
            this.ctlValue = ctl;
        }

        /// <summary>Gets or sets the item at the given position.</summary>
        /// <param name="index">The index.</param>
        /// <returns>The item at the given position.</returns>
        public DNNMultiState this[int index]
        {
            get { return this.InnerList[index] as DNNMultiState; }
            set { this.InnerList[index] = value; }
        }

        /// <summary>Performs the <c>Add</c> operation.</summary>
        /// <param name="item">The item.</param>
        public void Add(DNNMultiState item)
        {
            this.InnerList.Add(item);
        }

        /// <summary>Performs the <c>AddAt</c> operation.</summary>
        /// <param name="index">The index.</param>
        /// <param name="item">The item.</param>
        public void AddAt(int index, DNNMultiState item)
        {
            this.InnerList.Insert(index, item);
        }

        /// <summary>Performs the <c>OnInsert</c> operation.</summary>
        /// <param name="index">The index.</param>
        /// <param name="value">The value.</param>
        protected override void OnInsert(int index, object value)
        {
            var state = value as DNNMultiState;
            if (state != null)
            {
                state.Parent = this.ctlValue;
            }

            this.ctlValue.NotifyDesigner();
        }
    }
}
