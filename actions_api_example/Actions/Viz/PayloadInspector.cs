using Newtonsoft.Json.Linq;

namespace Actions.Viz
{
    /// <summary>
    /// Works out what a resolver actually wrote into an invoke payload.
    ///
    /// A resolver only reports a bool, and one input can legitimately write several properties
    /// (see the IsNoCharge resolver) or a whole array (see OrderActionFieldInputs). The only
    /// reliable way to report 'resolved value' is therefore to compare the payload before the
    /// resolver ran with the payload afterwards.
    /// </summary>
    public static class PayloadInspector
    {
        /// <summary>
        /// Returns the leaf values present in <paramref name="after"/> but not in
        /// <paramref name="before"/>, formatted as 'path = value'.
        /// </summary>
        /// <remarks>
        /// Paths come from Newtonsoft, so nesting and array indices read the way they appear in the
        /// JSON - 'Appointment.From', 'OrderActionFieldInputs[0].CustomFieldId'.
        /// </remarks>
        public static IReadOnlyList<string> WrittenValues(JObject before, JObject after)
        {
            var prior = Leaves(before).ToDictionary(leaf => leaf.Path, leaf => leaf.Value);

            return Leaves(after)
                .Where(leaf => !prior.TryGetValue(leaf.Path, out var old) || old != leaf.Value)
                .Select(leaf => $"{leaf.Path} = {leaf.Value}")
                .ToList();
        }

        private static IEnumerable<(string Path, string Value)> Leaves(JContainer token) =>
            token.Descendants()
                 .OfType<JValue>()
                 .Select(value => (value.Path, value.ToString()));
    }
}
