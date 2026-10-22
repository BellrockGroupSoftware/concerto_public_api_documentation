using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Actions.Concerto.Actions
{
    /// <summary>
    /// Factories for the Action Inputs this mock supports.
    ///
    /// Input type names mirror those listed in Appendix A of the Concerto API Integration Guide.
    /// Note that an input type is also a *path* into the invoke payload, and several of them are
    /// dotted - meaning the value belongs in a nested object rather than a top level property:
    ///
    ///     "OrderId"                       =&gt; { "OrderId": "..." }
    ///     "Appointment.From"              =&gt; { "Appointment": { "From": "..." } }
    ///     "ContractJobDetail.LabourCost"  =&gt; { "ContractJobDetail": { "LabourCost": 120.00 } }
    ///
    /// ...and some are array shaped. Never assume an input type maps to a flat property.
    /// </summary>
    internal static class Inputs
    {
        /// <summary>
        /// (*) OrderId - the Order the action is being performed against. Present on every Order action.
        /// </summary>
        internal static Input OrderId() => new Input
        {
            Type = "OrderId",
            IsRequired = true,
        };

        /// <summary>
        /// (*) EstimatedArrivalTime - the ETA for the supplier to attend the Order.
        /// </summary>
        internal static Input EstimatedArrivalTime() => new Input
        {
            Type = "EstimatedArrivalTime",
            OnConfigure = (o, i) =>
            {
                if (o.Eta.HasValue)
                {
                    i.Meta.CurrentValue = o.Eta.Value.ToString("yyyy-MM-ddTHH:mm:ssZ");
                }
            },
            OnApply = (v, o) => o.Eta = v.Value<DateTime>()
        };

        /// <summary>
        /// (*) Appointment.From - start of the appointment window. Nested under 'Appointment' in the payload.
        /// Some clients capture attendance as an appointment window rather than a single ETA - this is exactly
        /// the sort of per-client variation that makes 'describe before invoke' necessary.
        /// </summary>
        internal static Input AppointmentFrom() => new Input
        {
            Type = "Appointment.From",
            OnConfigure = (o, i) =>
            {
                if (o.Eta.HasValue)
                {
                    i.Meta.CurrentValue = o.Eta.Value.ToString("yyyy-MM-ddTHH:mm:ssZ");
                }
            },
            OnApply = (v, o) => o.Eta = v.Value<DateTime>()
        };

        /// <summary>
        /// (*) Appointment.To - end of the appointment window. Nested under 'Appointment' in the payload.
        /// </summary>
        internal static Input AppointmentTo() => new Input
        {
            Type = "Appointment.To"
        };

        /// <summary>
        /// (*) Appointment.Notes - free text to accompany the appointment booking.
        /// </summary>
        internal static Input AppointmentNotes() => new Input
        {
            Type = "Appointment.Notes"
        };

        /// <summary>
        /// (*) OrderCompleteDate - the date and time work on the Order was completed.
        /// </summary>
        internal static Input OrderCompleteDate() => new Input
        {
            Type = "OrderCompleteDate",
            OnApply = (v, o) => o.CompleteDate = v.Value<DateTime>()
        };

        /// <summary>
        /// ContractJobDetail.LabourCost - labour cost for the job. Nested under 'ContractJobDetail'.
        /// </summary>
        internal static Input LabourCost() => new Input
        {
            Type = "ContractJobDetail.LabourCost",
            OnConfigure = (o, i) =>
            {
                i.Meta.CurrentValue = o.Value.ToString();
                i.Meta.IntMin = (int)o.Value;
            },
            OnApply = (v, o) => o.Value = v.Value<decimal>()
        };

        /// <summary>
        /// (*) IsNoCharge - close the Order with no charge to the client.
        /// The guide notes that NoChargeNote is submitted *alongside* IsNoCharge, so a single described
        /// input can require you to write more than one property into the invoke payload.
        /// </summary>
        internal static Input IsNoCharge() => new Input
        {
            Type = "IsNoCharge",
            OnApply = (v, o) =>
            {
                if (v.Value<bool>())
                {
                    o.Value = 0.00m;
                }
            }
        };

        /// <summary>
        /// (*) OrderActionFieldInputs - answers to the custom fields configured against this action.
        /// Array shaped: [ { "CustomFieldId": "...", "Input": ... } ].
        ///
        /// Custom fields are client-specific, so the only way to know which ones to answer is to read the
        /// metadata returned by 'describe'. We expose the available fields via Meta.AllowedValues here
        /// (the real API returns a richer CustomFieldMeta).
        /// </summary>
        internal static Input OrderActionFieldInputs(string customFieldId, string label) => new Input
        {
            Type = "OrderActionFieldInputs",
            Meta =
            {
                AllowedValues = [ new MetaItem { Value = customFieldId, DisplayText = label } ]
            },
            OnApply = (v, o) =>
            {
                // The value is an array, so pick out the entry answering the field we care about.
                // Anything the caller sent for fields we do not recognise is simply left alone.
                var answer = v.FirstOrDefault(a => a.Value<string>("CustomFieldId") == customFieldId);
                if (answer != null)
                {
                    o.ClientAuthCode = answer.Value<string>("Input");
                }
            }
        };

        internal static Input IsOptional(this Input input)
        {
            input.IsRequired = false;
            return input;
        }

        internal static Input HasMeta(this Input input, Action<Order, InputMeta> configure)
        {
            input.Meta.OnConfigure = configure;
            return input;
        }
    }
}
