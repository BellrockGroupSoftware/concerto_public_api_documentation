using System;
using System.Collections.Generic;
using System.Text;

namespace Actions.Concerto.ApiModels
{
    /// <summary>
    /// An Action description returned from the Concerto API by 'describe'.
    ///
    /// Describe always runs against a *specific* order, because the answer depends on it. The same
    /// action described against two different orders can come back with different inputs, and
    /// different metadata within those inputs.
    /// </summary>
    /// <remarks>
    /// The real model also carries a 'Fields' collection - read-only guidance about the action
    /// rather than data you send back. It is omitted here because nothing in this example needs it;
    /// the collection that matters when building a payload is Inputs.
    /// </remarks>
    public class ActionModel
    {
        public required string Id { get; set; }

        /// <summary>The action's well-known key, where the client has one configured.</summary>
        public string? Key { get; set; }

        public required string Name { get; set; }
        public required string Description { get; set; }
        public IEnumerable<InputModel> Inputs { get; set; } = Enumerable.Empty<InputModel>();
    }
}
