using System;
using System.Collections.Generic;
using System.Text;

namespace Actions.Concerto
{
    public class Order
    {
        public required string OrderNumber { get; set; }
        public required string TenantKey { get; set; }
        public required string Status { get; set; }
        public required string Type { get; set; }

        public required string Description { get; set; }

        public DateTime? Eta { get; set; }
        public decimal Value { get; set; } = 0.0m;
        public DateTime? CompleteDate { get; set; }
        /// <summary>
        /// The answer to this client's 'Client Mandated Auth Code' custom field, where it has one
        /// configured. Custom fields are per-client, which is why they arrive as describe metadata
        /// rather than as a fixed property on the API model.
        /// </summary>
        public string? ClientAuthCode { get; set; }

        internal Order Clone() => new Order()
        {
            OrderNumber = this.OrderNumber,
            TenantKey = this.TenantKey,
            Status = this.Status,
            Type = this.Type,
            Description = this.Description,
            Eta = this.Eta,
            Value = this.Value,
            CompleteDate = this.CompleteDate,
            ClientAuthCode = this.ClientAuthCode
        };

    }
}
