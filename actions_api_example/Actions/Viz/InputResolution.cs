namespace Actions.Viz
{
    /// <summary>
    /// How an attempt to satisfy one described input ended up.
    /// </summary>
    public enum InputOutcome
    {
        /// <summary>A resolver ran and wrote something into the payload.</summary>
        Resolved,

        /// <summary>We have never seen this input type - a mapping gap for a human to close.</summary>
        NoResolver,

        /// <summary>A resolver exists, but had no data to supply for this request.</summary>
        NoValue
    }

    /// <summary>
    /// One row of the 'what did describe ask for, and what did we come up with' story: the input
    /// Concerto described, joined to the result of trying to resolve it.
    /// </summary>
    /// <param name="Type">The Concerto input type, eg. 'Appointment.From'.</param>
    /// <param name="IsRequired">Whether Concerto said the action cannot be invoked without it.</param>
    /// <param name="Outcome">How the resolve attempt ended.</param>
    /// <param name="WrittenValues">
    /// The 'path = value' entries this input contributed to the payload. Usually one, but can be
    /// several - see <see cref="PayloadInspector"/>.
    /// </param>
    public readonly record struct InputResolution(
        string Type,
        bool IsRequired,
        InputOutcome Outcome,
        IReadOnlyList<string> WrittenValues)
    {
        /// <summary>
        /// True when this input is required but we could not supply it - ie. the invoke will fail.
        /// </summary>
        public bool IsFatal => IsRequired && Outcome != InputOutcome.Resolved;
    }
}
