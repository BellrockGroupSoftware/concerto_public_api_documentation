using System;
using System.Collections.Generic;
using System.Text;

namespace Actions.Concerto.Actions
{
    /// <summary>
    /// The outcome of a call to the 'invoke' endpoint.
    ///
    /// A rejected payload is a normal, expected answer - not an exception. The real API answers
    /// HTTP 400 with the validation errors in the body, and an integration should surface those
    /// for a human to act on rather than treating them as a crash. (A genuinely unknown action or
    /// order is a 404, which this mock still raises as <see cref="MockHttpException"/>.)
    /// </summary>
    public class ActionResult
    {
        public bool WasSuccess { get; set; }

        /// <summary>A single summary message, suitable for logging.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>The individual validation failures, one per problem found with the payload.</summary>
        public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// The outcome of a call to the 'validate' endpoint - the same checks 'invoke' runs, without
    /// applying anything. Use it when you want to find out whether a payload would be accepted
    /// before you commit to changing the order.
    /// </summary>
    public class PayloadValidationResult
    {
        public bool IsValid { get; set; }

        public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
    }
}
