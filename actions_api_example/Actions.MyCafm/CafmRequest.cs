using System;
using System.Collections.Generic;
using System.Text;

namespace Actions.MyCafm
{
    /// <summary>
    /// A Request in your CAFM system
    /// </summary>
    public class CafmRequest
    {
        /// <summary>
        /// Our internal reference for the Request
        /// </summary>
        public string OurRef { get; internal set; }

        /// <summary>
        /// The Requestors reference
        /// </summary>
        public string TheirRef { get; internal set; }

        /// <summary>
        /// An external system ref
        /// </summary>
        public string ExternalSystemRef { get; internal set; }

        /// <summary>
        /// The name of the engineer this request is assigned to, if any
        /// </summary>
        public string? EngineerName { get; internal set; }

        /// <summary>
        /// The expected date the engineer will attend the request, if any
        /// </summary>
        public DateTime? ExpectedAttendDate { get; internal set; }
        
        /// <summary>
        /// The sale value of this request
        /// </summary>
        public decimal? SaleValue { get; set; }
        
        /// <summary>
        /// The Complete date of this request
        /// </summary>
        public DateTime? CompleteDate { get; set; }

        internal CafmRequest(string ourRef, string theirRef, string externalSystemRef)
        {
            OurRef = ourRef;
            TheirRef = theirRef;
            ExternalSystemRef = externalSystemRef;
        }

        /// <summary>
        /// The engineer has finished. Records what the work came to and when it was done, then
        /// broadcasts a 'completed' event.
        /// </summary>
        /// <remarks>
        /// Nothing in the example calls this - it is here so that the 'try this next' suggestions
        /// have data to work with. A Concerto 'Complete Job' action asks for a cost and a
        /// completion date, and a resolver can only supply what the request actually holds.
        /// </remarks>
        public void CompleteWork(decimal saleValue, DateTime completeDate)
        {
            SaleValue = saleValue;
            CompleteDate = completeDate;

            MyCafmSystem.RaiseEvent(new CafmEvent
            {
                MyCafmEventType = "completed",
                Request = this
            });
        }

        public void AllocateToEngineer(string engineerName, DateTime expectedAttendDate)
        {
            // Logic to allocate the request to an engineer
            EngineerName = engineerName;
            ExpectedAttendDate = expectedAttendDate;
            MyCafmSystem.RaiseEvent(new CafmEvent
            {
                MyCafmEventType = "allocated",
                Request = this
            });
        }
    }
}
