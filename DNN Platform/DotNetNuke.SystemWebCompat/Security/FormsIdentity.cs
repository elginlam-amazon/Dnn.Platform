// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.Security
{
    using System.Security.Claims;

    /// <summary>Replacement for the System.Web forms authentication identity on .NET 10.</summary>
    [Serializable]
    public class FormsIdentity : ClaimsIdentity
    {
        /// <summary>Initializes a new instance of the <see cref="FormsIdentity"/> class.</summary>
        /// <param name="ticket">The forms authentication ticket.</param>
        public FormsIdentity(FormsAuthenticationTicket ticket)
            : base(CreateClaims(ticket), "Forms")
        {
            this.Ticket = ticket;
        }

        /// <summary>Gets the forms authentication ticket.</summary>
        public FormsAuthenticationTicket Ticket { get; }

        /// <inheritdoc />
        public override string AuthenticationType => "Forms";

        /// <inheritdoc />
        public override bool IsAuthenticated => true;

        /// <inheritdoc />
        public override string Name => this.Ticket.Name;

        private static Claim[] CreateClaims(FormsAuthenticationTicket ticket)
        {
            if (ticket == null)
            {
                throw new ArgumentNullException(nameof(ticket));
            }

            return [new Claim(ClaimTypes.Name, ticket.Name)];
        }
    }
}
