namespace Actions.Concerto.ApiModels
{
    /// <summary>
    /// One entry from a 'discover' call - an action Concerto is currently offering on an order.
    ///
    /// Note what you get back, and what you do with each part:
    ///
    ///   Id    - how you address the action on 'describe' and 'invoke', and nothing else. It is
    ///           specific to this instance. Never store one, never hardcode one.
    ///   Key   - a well-known key, where the client has one configured. Stable by design, so it is
    ///           the best thing to match on - but it is optional and may be absent.
    ///   Name  - what the action is called in the Supplier Portal. This is the documented way to
    ///           match an action, and the fallback when there is no Key.
    /// </summary>
    public class DiscoveredAction
    {
        public required string Id { get; set; }

        public string? Key { get; set; }

        public required string Name { get; set; }
    }
}
