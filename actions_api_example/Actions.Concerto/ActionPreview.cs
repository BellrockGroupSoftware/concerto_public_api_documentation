using Actions.Concerto.ApiModels;

namespace Actions.Concerto
{
    /// <summary>
    /// A look ahead at what invoking an action would do to an order - the state it is in now, the
    /// state it would end up in, and the inputs the action would ask for along the way.
    /// </summary>
    /// <remarks>
    /// This is a convenience of the mock, not of Concerto. The real platform has no 'what would
    /// happen if' endpoint: you discover, describe, invoke, and then observe the result. It exists
    /// here purely so the example can narrate what it is about to do before it does it.
    /// </remarks>
    public class ActionPreview
    {
        /// <summary>The order the action would be invoked against.</summary>
        public required string OrderNumber { get; init; }

        /// <summary>The action's ID on this instance. IDs are client specific - never hardcode one.</summary>
        public required string ActionId { get; init; }

        /// <summary>The action's name. This is what an integration matches on.</summary>
        public required string ActionName { get; init; }

        /// <summary>Concerto's own description of what the action does.</summary>
        public required string ActionDescription { get; init; }

        /// <summary>The status the order is in now, and so the status that made this action available.</summary>
        public required string CurrentStatus { get; init; }

        /// <summary>The status the order would be in once the action had been invoked.</summary>
        public required string ResultingStatus { get; init; }

        /// <summary>The inputs 'describe' would return for this action, against this order.</summary>
        public IEnumerable<InputModel> Inputs { get; init; } = Enumerable.Empty<InputModel>();

        /// <summary>True when the action leaves the order where it is - a notification, rather than a move.</summary>
        public bool MovesStatus => CurrentStatus != ResultingStatus;
    }
}
