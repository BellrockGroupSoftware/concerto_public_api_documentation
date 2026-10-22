using Actions.Concerto.ApiModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Actions.Concerto.Actions
{
    internal class ConcertoAction
    {
        /// <summary>
        /// The action's ID on this instance. IDs are per-instance and must never be hardcoded by an
        /// integration - the same logical action has a different ID on every client.
        /// </summary>
        internal required string Id { get; set; }

        /// <summary>
        /// A well-known key for the action, where the client has one configured. Keys are stable by
        /// design and are the most robust thing to match on - but they are optional, so an
        /// integration needs the name as a fallback.
        /// </summary>
        internal string? Key { get; set; }

        internal required string Name { get; set; }
        internal required string Description { get; set; }

        internal IEnumerable<Input> Inputs { get; set; } = Enumerable.Empty<Input>();

        internal Action<Order>? OnApply;

        internal string[] ScopeOrderTypes = Array.Empty<string>();

        internal string[] ScopeStates = Array.Empty<string>();


        internal bool CheckIsAvailable(Order order)
        {
            if (ScopeOrderTypes?.Length > 0)
            {
                if (ScopeOrderTypes.Contains(order.Type) == false)
                {
                    // Wrong order type
                    return false;
                }
            }

            if (ScopeStates?.Length > 0)
            {
                if (ScopeStates.Contains(order.Status) == false)
                {
                    // Wrong order state
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Runs every check 'invoke' would run, without applying anything. This is what sits behind
        /// both the 'validate' and the 'invoke' endpoints - validate reports the answer and stops,
        /// invoke goes on to apply the payload when there is nothing to report.
        /// </summary>
        /// <param name="instance">The instance the order lives on.</param>
        /// <param name="inputs">The payload as it would be POSTed to 'invoke'.</param>
        /// <param name="order">The order the payload names, when it could be resolved.</param>
        /// <returns>One entry per problem found. Empty means the payload would be accepted.</returns>
        internal IReadOnlyList<string> Validate(ConcertoInstance instance, JObject inputs, out Order? order)
        {
            order = null;
            var errors = new List<string>();

            // Every Order action carries an OrderId input - that's how we know what we're acting on.
            var orderIdInput = inputs.SelectToken("OrderId")?.Value<string>();
            if (string.IsNullOrEmpty(orderIdInput))
            {
                errors.Add($"OrderId: required to identify the order '{Name}' should be applied to");
                return errors;
            }

            // An order we have never heard of is a 404 rather than a validation failure - the
            // request names something that does not exist, so there is nothing to validate against.
            order = instance.Orders.FirstOrDefault(o => o.OrderNumber == orderIdInput)
                ?? throw new MockHttpException(404, $"Order '{orderIdInput}' not found");

            // Scope is re-checked at invoke time, not just at discover time. An action you
            // discovered a moment ago may have become unavailable since - someone else may have
            // moved the order on in the meantime.
            if (CheckIsAvailable(order) == false)
            {
                errors.Add($"Action '{Name}' is not available for order '{order.OrderNumber}' in status '{order.Status}'");
                return errors;
            }

            // Note we resolve each value by *path*, not by property name - an input type of
            // 'Appointment.From' expects { "Appointment": { "From": ... } } in the payload. A caller
            // who writes a flat property literally named "Appointment.From" will be told it is
            // missing here, just as they would be by the real API.
            foreach (var input in Inputs)
            {
                if (input.IsRequired && IsMissing(inputs.SelectToken(input.Type)))
                {
                    errors.Add($"{input.Type}: required");
                }
            }

            return errors;
        }

        /// <summary>
        /// Applies a payload that has already been found valid.
        /// </summary>
        internal void Apply(ConcertoInstance instance, Order order, JObject inputs)
        {
            // We're simulating here, and passing stuff by ref, so clone the order so we don't modify the original
            // ..allows us to do a meaningful difference check after the action is applied. Nasty!
            instance.Orders.Remove(order);
            order = order.Clone();
            instance.Orders.Add(order);

            // Unpack and apply each input, again resolving the value by path.
            foreach (var input in Inputs)
            {
                var value = inputs.SelectToken(input.Type);

                if (IsMissing(value))
                {
                    continue;
                }

                input.OnApply(value!, order);
            }

            // Apply any state change etc
            if (OnApply != null)
            {
                OnApply(order);
            }
        }

        /// <summary>
        /// An input value is 'missing' if it isn't present, is explicitly null, or is an empty string.
        /// Note that 'false' and '0' are perfectly good values and must not be treated as missing.
        /// </summary>
        private static bool IsMissing(JToken? value) =>
            value == null
            || value.Type == JTokenType.Null
            || (value.Type == JTokenType.String && string.IsNullOrEmpty(value.Value<string>()));

        internal ActionModel ToApiModel(Order order) => new ActionModel()
        {
            Id = this.Id,
            Key = this.Key,
            Name = this.Name,
            Description = this.Description,
            Inputs = this.Inputs.Select(i => i.ToApiModel(order))
        };


    }
}
