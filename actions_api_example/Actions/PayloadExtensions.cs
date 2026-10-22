using Newtonsoft.Json.Linq;

namespace Actions
{
    /// <summary>
    /// Helpers for building the JSON body you POST to the Actions 'invoke' endpoint.
    /// </summary>
    internal static class PayloadExtensions
    {
        /// <summary>
        /// Writes a value into the invoke payload at the path implied by an input type.
        ///
        /// Input types are paths, not flat property names. 'OrderId' is a top level property, but
        /// 'Appointment.From' means the value belongs inside an 'Appointment' object:
        ///
        ///     payload.SetInput("OrderId", "ABC123");
        ///     payload.SetInput("Appointment.From", someDate);
        ///
        ///     =&gt; { "OrderId": "ABC123", "Appointment": { "From": "2026-10-20T19:30:00Z" } }
        ///
        /// Writing these as flat properties is the single most common way to get a 400 back from
        /// the Actions API, so build the payload through a helper like this one rather than by
        /// calling payload.Add(inputType, value) directly.
        /// </summary>
        /// <returns>
        /// True if a value was written, false if there was nothing to write. The caller uses this
        /// to decide whether a required input has gone unsatisfied.
        /// </returns>
        internal static bool SetInput(this JObject payload, string inputType, object? value)
        {
            if (value is null)
            {
                return false;
            }

            if (value is string s && string.IsNullOrEmpty(s))
            {
                return false;
            }

            // Concerto expects ISO 8601 / UTC for dates - see the Appendix A examples in the API guide.
            JToken token = value is DateTime date
                ? new JValue(date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"))
                : JToken.FromObject(value);

            // Walk (and create) the nested objects named by all but the last path segment.
            var segments = inputType.Split('.');
            var target = payload;

            for (var i = 0; i < segments.Length - 1; i++)
            {
                if (target[segments[i]] is not JObject child)
                {
                    child = new JObject();
                    target[segments[i]] = child;
                }

                target = child;
            }

            target[segments[^1]] = token;
            return true;
        }
    }
}
