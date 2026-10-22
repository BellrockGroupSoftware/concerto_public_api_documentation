using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Actions.Concerto
{
    /// <summary>
    /// An event delivered to your webhook by Concerto.
    ///
    /// The real envelope carries a little more than this (a channel, and the UTC time the event was
    /// generated) but the three properties here are the ones an integration actually acts on:
    /// an Id to de-duplicate by, a Type to dispatch on, and the data for the thing that happened.
    /// </summary>
    public class ConcertoEvent
    {
        /// <summary>
        /// A unique ID for this event.
        ///
        /// The same event can reach you more than once - events are multicast to channels, and your
        /// subscription may pick one up via several of them. De-duplicate on this before you act.
        /// </summary>
        public required string Id { get; set; }

        /// <summary>
        /// The type of event, eg. 'orderAssigned'. New types are added over time, so an integration
        /// must tolerate receiving types it does not recognise rather than failing on them.
        /// </summary>
        public required string Type { get; set; }

        /// <summary>
        /// Data about the thing that happened. The shape depends on <see cref="Type"/> - for order
        /// events it carries the order's ID, its human readable ref, and the tenant key of the
        /// portfolio the order belongs to.
        /// </summary>
        public required JObject Payload { get; set; }
    }
}
