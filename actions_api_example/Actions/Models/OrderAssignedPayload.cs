using System;
using System.Collections.Generic;
using System.Text;

namespace Actions.Models
{
    /// <summary>
    /// Model in your middleware representing the payload from an 'Order Assigned' event from Concerto
    /// </summary>
    public class OrderAssignedPayload
    {
        /// <summary>
        /// The Order's ID. This is the value you store, and the value you pass to the Actions API
        /// as a target entity and as the OrderId input.
        /// </summary>
        public required string OrderId;

        /// <summary>
        /// The Order's human readable ref, as shown in the Supplier Portal. Useful to display to a
        /// user or to quote in a support call - but do not key your records on it.
        /// </summary>
        public required string OrderRef;

        /// <summary>
        /// The tenant key for the portfolio this order belongs to. Store it alongside the Order ID:
        /// without it you will not know which Concerto instance to send your API calls to.
        /// </summary>
        public required string PortfolioCode;

        public DateTime Timestamp;
    }
}
