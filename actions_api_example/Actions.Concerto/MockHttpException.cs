using System;
using System.Collections.Generic;
using System.Text;

namespace Actions.Concerto
{
    /// <summary>
    /// A pretend HTTP error response
    /// </summary>
    public class MockHttpException : Exception
    {
        public int ResponseCode { get; set; }
        internal MockHttpException()
        {

        }

        internal MockHttpException(int responseCode, string message) : base(message)
        {
            ResponseCode = responseCode;
        }
    }
}
