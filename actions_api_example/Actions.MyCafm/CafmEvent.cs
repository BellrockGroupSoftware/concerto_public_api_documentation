using System;
using System.Collections.Generic;
using System.Text;

namespace Actions.MyCafm
{
    /// <summary>
    /// An event from your CAFM system
    /// </summary>
    public class CafmEvent
    {
        /// <summary>
        /// The type of event
        /// </summary>
        public required string MyCafmEventType { get; set; }

        /// <summary>
        /// The Request the event relates to
        /// </summary>
        public required CafmRequest Request { get; set; }
    }
}
