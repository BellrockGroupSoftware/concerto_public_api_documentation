using System;
using System.Collections.Generic;
using System.Text;

namespace Actions.MyCafm
{
    /// <summary>
    /// Your CAFM system
    /// </summary>
    public static class MyCafmSystem
    {
        private static int requestSeed = (int)DateTime.Now.TimeOfDay.TotalSeconds;
        private static List<CafmRequest> requests = new List<CafmRequest>();

        private static Dictionary<string, Action<CafmEvent>> eventHandlers = new Dictionary<string, Action<CafmEvent>>();

        /// <summary>
        /// Registers a handler for particular types of events
        /// </summary>
        /// <param name="cafmEvent"></param>
        /// <param name="handler"></param>
        public static void RegisterEventHandler(string cafmEvent, Action<CafmEvent> handler)
        {
            eventHandlers.Add(cafmEvent, handler);
        }

        internal static void RaiseEvent(CafmEvent ev)
        {
            if (eventHandlers.ContainsKey(ev.MyCafmEventType))
            {
                eventHandlers[ev.MyCafmEventType](ev);
            }
        }

        /// <summary>
        /// Creates a new CAFM request
        /// </summary>
        /// <param name="theirRef"></param>
        /// <param name="externalSystemRef"></param>
        /// <returns></returns>
        public static CafmRequest CreateRequest(string theirRef, string externalSystemRef)
        {
            var req = new CafmRequest(
                ourRef: $"R{requestSeed++}",
                theirRef: theirRef,
                externalSystemRef: externalSystemRef);

            requests.Add(req);
            return req;
        }

       
    }
}
