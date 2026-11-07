// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections;
    using System.Xml;

    /// <summary>The <see cref="DNNNodeEnumerator"/> class.</summary>
    internal sealed class DNNNodeEnumerator : IEnumerator
    {
        private readonly XmlNode xmlNode;
        private int cursor;

        /// <summary>Initializes a new instance of the <see cref="DNNNodeEnumerator"/> class.</summary>
        /// <param name="objRoot">The obj Root.</param>
        public DNNNodeEnumerator(XmlNode objRoot)
        {
            this.xmlNode = objRoot;
            this.cursor = -1;
        }

        /// <summary>Gets the current.</summary>
        public object Current
        {
            get
            {
                if (this.cursor < 0 || this.cursor == this.xmlNode.ChildNodes.Count)
                {
                    throw new InvalidOperationException();
                }

                return new DNNNode(this.xmlNode.ChildNodes[this.cursor]);
            }
        }

        /// <summary>Gets the <c>MoveNext</c> operation.</summary>
        /// <returns>The result of the operation.</returns>
        public bool MoveNext()
        {
            if (this.cursor < this.xmlNode.ChildNodes.Count)
            {
                this.cursor += 1;
            }

            return this.cursor != this.xmlNode.ChildNodes.Count;
        }

        /// <summary>Performs the <c>Reset</c> operation.</summary>
        public void Reset()
        {
            this.cursor = -1;
        }
    }
}
