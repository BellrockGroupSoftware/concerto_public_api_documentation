using Actions.Concerto;
using Actions.Models;
using Actions.MyCafm;
using Microsoft.Extensions.Logging;
using Actions.Concerto.ApiModels;
using Actions.Viz;
using Newtonsoft.Json.Linq;

namespace Actions
{
    internal class Program
    {

        // First, some housekeeping - a logger for this example, and a place to store the request we create in response to an 'orderAssigned' event from Concerto.
        static ILoggerFactory loggerFactory = LoggerFactory
                .Create(builder => builder
                    .AddSimpleConsole(opt =>
                    {
                        opt.SingleLine = true;
                        opt.IncludeScopes = false;

                    }
                )
            );
        static ILogger log = loggerFactory.CreateLogger(string.Empty);


        // This is the request created in your system we keep here for convenience purposes
        static CafmRequest? createdRequest;

        // The IDs of events we have already handled. The same event can reach you more than once -
        // events are multicast to channels and your subscription may pick one up via several of
        // them - so de-duplicate before acting. A real integration would persist this, with a TTL,
        // rather than keeping it in memory.
        static HashSet<string> handledEventIds = new HashSet<string>();

        // Some secure storage for your concerto API credentials
        // Note: We're using 'direct auth' here for simplicity, but in reality you should be using OAuth2 and
        // storing the token you get back from the Hub, refreshing as needed.
        static (string BaseUrl, string AccessToken)[] ConcertoCredentials = [
            ("https://shop.bellrock.co.uk", "ACC-RETAIL-1234"),
            ("https://gov.bellrock.co.uk", "ACC-GOV-9876")
        ];

        // A map from your event to a concerto action.
        // Map is ('MyCafm Event', 'Concerto Tenant Key/null', 'Concerto Action Key/null', 'Concerto Action Name')
        //
        // Note what is matched, and what is not. Action IDs are per-instance and must never appear
        // in a map like this one. A well-known Key is the most robust thing to match on, but it is
        // optional per client - so we carry a Name too, and fall back to it.
        //
        // Entries can be scoped to a tenant, because the same event may need a different action
        // depending on whose order it is. A null tenant key means 'any client'.
        static (string MyCafmEvent, string? TenantKey, string? ConcertoActionKey, string ConcertoActionName)[] MyCafmToConcertoActionMap = [
            ("allocated", null, "ACCEPT", "Accept Job") // All allocations, across all clients, map to the ACCEPT action
        ];


        // A collection of 'resolver functions' - one per Concerto input type we know how to satisfy.
        //
        // This table is the heart of a dynamic integration. Rather than hardcoding "the accept action needs
        // an order number and an ETA", we handle each *input type* once and let 'describe' tell us which of
        // them any given action actually wants. When a client's action configuration changes, or Concerto
        // introduces a new input type, this table is the only thing that needs to grow, and only if it's a new
        // type of input that we don't already know how to resolve.
        //
        // Note the shape of a resolver: It does not return a value - it *writes into the payload* and reports
        // whether it managed to supply anything. That matters because:
        //   - an input type is a path, not a flat property ('Appointment.From' nests, see PayloadExtensions)
        //   - one described input can cause several properties to be written into the payload (see IsNoCharge below)
        //   - some inputs are arrays or objects, not scalars (see OrderActionFieldInputs below)
        //
        // Resolvers also get the InputModel itself, so they can read the metadata returned by 'describe' -
        // essential for inputs whose valid values are client-specific.
        static (string InputType, Func<CafmRequest, InputModel, JObject, bool> Resolve)[] InputResolvers = [

            // The Order this action applies to. We stored Concerto's order number as our 'their ref'.
            ("OrderId", (req, input, payload) =>
                payload.SetInput("OrderId", req.TheirRef)),

            // Some clients want a single ETA...
            ("EstimatedArrivalTime", (req, input, payload) =>
                payload.SetInput("EstimatedArrivalTime", req.ExpectedAttendDate)),

            // ...and others want an appointment window instead. Same MyCafm data, different Concerto inputs,
            // and these two land nested inside an 'Appointment' object in the payload.
            ("Appointment.From", (req, input, payload) =>
                payload.SetInput("Appointment.From", req.ExpectedAttendDate)),

            ("Appointment.To", (req, input, payload) =>
                payload.SetInput("Appointment.To", req.ExpectedAttendDate?.AddHours(2))),

            ("Appointment.Notes", (req, input, payload) =>
                payload.SetInput("Appointment.Notes", req.EngineerName == null ? null : $"Allocated to {req.EngineerName}")),

            ("OrderCompleteDate", (req, input, payload) =>
                payload.SetInput("OrderCompleteDate", req.CompleteDate)),

            // A nested cost input - { "ContractJobDetail": { "LabourCost": 50.00 } }
            ("ContractJobDetail.LabourCost", (req, input, payload) =>
                payload.SetInput("ContractJobDetail.LabourCost", req.SaleValue)),

            // IsNoCharge is a single input, but needs two properties adding to the payload. The guide notes NoChargeNote is submitted
            // alongside IsNoCharge, and describe will not necessarily mention it separately.
            ("IsNoCharge", (req, input, payload) =>
            {
                if (req.SaleValue == null)
                {
                    return false;
                }
                var isNoCharge = req.SaleValue == 0m;
                var wrote = payload.SetInput("IsNoCharge", isNoCharge);

                if (isNoCharge)
                {
                    payload.SetInput("NoChargeNote", "No chargeable work was carried out.");
                }

                return wrote;
            }),

            // An array shaped input whose contents are client-specific. We cannot know which custom fields
            // a client has configured, so we read them from the describe metadata and answer the ones we
            // recognise - leaving anything unfamiliar alone rather than guessing.
            ("OrderActionFieldInputs", (req, input, payload) =>
            {
                var answers = new JArray();

                foreach (var field in input.Meta.AllowedValues)
                {
                    if (field.DisplayText == "Client Mandated Auth Code")
                    {
                        answers.Add(new JObject
                        {
                            ["CustomFieldId"] = field.Value,
                            ["Input"] = "123456" // MyCafm would supply the real answer here
                        });
                    }
                }

                if (answers.Count == 0)
                {
                    // Nothing we recognise - let the caller decide whether that's fatal.
                    return false;
                }

                payload["OrderActionFieldInputs"] = answers;
                return true;
            })
        ];

        static void Main(string[] args)
        {
            // A banner and a short explanation of what is about to happen, for anyone who just ran this
            ConsoleViz.Welcome();

            // Register event handlers from both Concerto and your MyCafm system
            // In reality, these would be webhook listeners or queue consumers
            ConcertoPlatform.RegisterEventHandler("orderAssigned", OnConcerto_OrderAssigned);
            MyCafmSystem.RegisterEventHandler("allocated", OnMyCafmSystem_Allocated);

            // Let's start by finding a random order
            var orderToAssign = ConcertoPlatform.FindOrderForAssignment();
            
            if (orderToAssign == null)
            {
                log.LogError("No orders available for assignment. Exiting.");
                return;
            }

            // Now we have an order, we'll explain what we're going to do to it.
            // Below, we simulate an 'allocated' event from MyCafm - so show what the action that event
            // is mapped to would do to this order, before we go and do it.
            ConsoleViz.ConcertoOrderTransition(orderToAssign, MyCafmToConcertoActionMap.First(m => m.MyCafmEvent == "allocated").ConcertoActionName);

            ConsoleViz.PromptForContinue("Continue on to receive an 'OrderAssigned' event for this order");

            // The example only carries one event through to an invoke, but the mock has a good deal
            // more in it than that - see the note at the end of the run for what to try next.

            // Now let's kick things off - we'll simulate receiving an 'OrderAssigned' event from concerto...
            ConcertoPlatform.ProduceOrderAssignedEvent(orderToAssign); // This will fire 'orderAssigned' for the selected order

            // ...concerto will send an event to your 'order assigned' webhook
            // represented here by the 'OnConcerto_OrderAssigned' method.

            // In that handler you're creating a new request on your side, which we
            // stash in the 'createdRequest' variable for this example.


            // ------- SOME TIME LATER -------
            ConsoleViz.PromptForContinue("Continue on to allocate the request in MyCafm, which will fire an 'Allocated' event we need to let Concerto know about");
            
            // You've decided to allocate the job to an engineer. Allocating to an
            // engineer causes the OnMyCafmSystem_Allocated method to be called because
            // your system broadcasts an event.

            // So just pretend this is some activity that a user on your side performs to
            // generate an 'Allocated' event from your CAFM system.
            log.LogInformation("Simulating an 'Allocated' event from MyCafm...");
            createdRequest?.AllocateToEngineer("Estelle Engineer", DateTime.Now.AddMinutes(10));
        }

        /// <summary>
        /// An 'orderAssigned' event has been received from Concerto (via webhook, perhaps!)
        /// </summary>
        /// <param name="ev"></param>
        static void OnConcerto_OrderAssigned(ConcertoEvent ev)
        {
            log.LogInformation($"Received an 'orderAssigned' event from Concerto for order '{ev.Payload["OrderId"]}'");

            // Have we seen this one already? Concerto multicasts events to channels, so the same
            // event can be delivered to you more than once. Acting twice would raise two requests
            // for one order, so check the event ID before doing anything else.
            if (!handledEventIds.Add(ev.Id))
            {
                log.LogWarning($"Already handled event '{ev.Id}'. Discarding the duplicate.");
                return;
            }

            var data = ev.Payload.ToObject<OrderAssignedPayload>();

            if (data == null)
            {
                log.LogError($"Failed to deserialize payload for 'orderAssigned' event from Concerto. Payload: {ev.Payload.ToString(Newtonsoft.Json.Formatting.None)}");
                throw new InvalidOperationException($"Failed to deserialize payload for 'orderAssigned' event from Concerto. Payload: {ev.Payload.ToString(Newtonsoft.Json.Formatting.None)}");
            }

            // Raise a new request in the MyCafm system.
            // Store two things from the event against it: the Order *ID* (not the human readable
            // ref - that is for display) and the Tenant Key, which tells you which Concerto
            // instance to send later API calls to. We keep the tenant key in the request's
            // 'ExternalSystemRef' property here.
            // Don't have a field for that? Why not prefix the 'their ref' value with the value instead! (eg. {tenantKey}:{orderId})
            createdRequest = MyCafmSystem.CreateRequest(data.OrderId, data.PortfolioCode);

            log.LogInformation($"Created a new request in MyCafm with their ref '{createdRequest.TheirRef}' and external system ref '{createdRequest.ExternalSystemRef}'");
        }

        /// <summary>
        /// An 'Allocated' event has been received from your CAFM system
        /// </summary>
        /// <param name="ev"></param>
        static void OnMyCafmSystem_Allocated(CafmEvent ev)
        {
            log.LogInformation($"Received an 'Allocated' event from MyCafm for request '{ev.Request.TheirRef}' with external system ref '{ev.Request.ExternalSystemRef}'");

            // This method is huge - it's laid out this way so you can follow the steps in some sort of happy-path order.
            // In reality, you should split this up into multiple stages in a pipeline!


            // An engineer was allocated to a Request in MyCafm.
            // Was this a request we need to let Concerto know about?
            if (string.IsNullOrEmpty(ev.Request.ExternalSystemRef))
            {
                // Nope. Discard the event
                log.LogWarning($"The request '{ev.Request.TheirRef}' does not have an external system ref. Discarding event.");
                return;
            }

            // Check the action map to see if we need to let Concerto know, giving priority to a mapping that explicitly specifies a
            // tenant, over a generic one
            var mappedAction = MyCafmToConcertoActionMap.FirstOrDefault(m => m.MyCafmEvent == ev.MyCafmEventType && m.TenantKey == ev.Request.ExternalSystemRef);
            if (mappedAction == default)
            {
                mappedAction = MyCafmToConcertoActionMap.FirstOrDefault(m => m.MyCafmEvent == ev.MyCafmEventType && string.IsNullOrEmpty(m.TenantKey));
            }

            // Did we match?
            if (mappedAction == default)
            {
                // Nope. Discard the event
                log.LogWarning($"No mapping found for event '{ev.MyCafmEventType}' with external system ref '{ev.Request.ExternalSystemRef}'. Discarding event.");
                return;
            }

            // We matched an action key and name (not an ID!) from the map
            var selectedConcertoAction = mappedAction.ConcertoActionName;
            log.LogInformation($"Mapped event '{ev.MyCafmEventType}' with external system ref '{ev.Request.ExternalSystemRef}' to concerto action '{selectedConcertoAction}'");

            ConsoleViz.PromptForContinue($"We've looked at our 'MyCafm -> Concerto' action mapping, and have decided that the action we're after from Concerto is '{selectedConcertoAction}'. " +
                $"Continue on to call the Concerto API and see if we can find this action.");

            // Now we need to make a Concerto API call to see if the action we're after is available.
            // Which concerto instance are we making a call against? Use the routing API to find out
            // by taking the tenant key (which we stored in the ExternalSystemRef property of the request)
            // and resolving it.

            // Ideally, you'd be caching this somewhere rather than hitting the routing API each time
            var concertoBaseUrl = ConcertoPlatform.RoutingApi_ResolveTenant(ev.Request.ExternalSystemRef);
            log.LogInformation($"Resolved concerto base URL '{concertoBaseUrl}' for external system ref '{ev.Request.ExternalSystemRef}'");


            // Now retrieve your credentials from storage
            var concertoAccessToken = ConcertoCredentials.FirstOrDefault(c => c.BaseUrl == concertoBaseUrl).AccessToken;

            if (string.IsNullOrEmpty(concertoAccessToken))
            {
                log.LogError($"No credentials found for concerto base URL '{concertoBaseUrl}'. Cannot continue.");
                throw new InvalidOperationException($"No credentials found for concerto base URL '{concertoBaseUrl}'. Cannot continue.");
            }


            // And obtain an API client
            var concertoApi = ConcertoPlatform.Connect(concertoBaseUrl);
            log.LogInformation($"Obtained Concerto API client for base URL '{concertoBaseUrl}'");

            // Now we can check if the action is available
            var availableActions = concertoApi.Discover(concertoAccessToken, ev.Request.TheirRef);

            ConsoleViz.ConcertoDiscoveredActions(availableActions, ev.Request.TheirRef, selectedConcertoAction);

            // See if the specific action we want is among them. Match on the well-known key where
            // the client has configured one, and fall back to the name where it has not. Never
            // match on the ID - it is specific to this instance.
            var candidates = availableActions
                .Where(a => a.Key != null && a.Key == mappedAction.ConcertoActionKey)
                .ToList();

            if (candidates.Count == 0)
            {
                candidates = availableActions.Where(a => a.Name == selectedConcertoAction).ToList();
            }

            if (candidates.Count == 0)
            {
                // The action we want to perform isn't available. That's an error!
                log.LogError($"The action '{selectedConcertoAction}' is not available for request '{ev.Request.TheirRef}' in Concerto.");
                throw new InvalidOperationException($"The action '{selectedConcertoAction}' is not available for request '{ev.Request.TheirRef}' in Concerto.");
            }

            if (candidates.Count > 1)
            {
                // Names are not guaranteed unique - a client can configure two actions called
                // 'Complete Job', scoped to different types of order. Usually only one of them is
                // available at a time, so discover returns one; if more than one comes back, that
                // is a mapping question for a human rather than something to guess at.
                log.LogError($"'{selectedConcertoAction}' matched {candidates.Count} actions on request '{ev.Request.TheirRef}'. Refine the mapping to pick between them.");
                throw new InvalidOperationException($"'{selectedConcertoAction}' matched {candidates.Count} actions on request '{ev.Request.TheirRef}'.");
            }
            var targetActionId = candidates[0].Id;

            ConsoleViz.PromptForContinue($"Looking at the list of actions returned from Concerto we've found the one we're looking for, with ID '{targetActionId}'. " +
                $"Next we'll ask Concerto to Describe the action so we know what inputs we need to provide");

            // We now have an action we can perform. Let's see what inputs the action needs.
            var targetAction = concertoApi.Describe(concertoAccessToken, targetActionId, ev.Request.TheirRef);

            // Now we have an action, we should try and resolve the inputs it needs, and build up a payload to
            // send to the 'invoke' endpoint.
            //
            // Resolve everything first and record how each input went, so we can show the complete
            // picture before failing - a mapping gap is much easier to fix when you can see the
            // whole input list, not just the first one that blew up.
            var payload = new JObject();
            var resolutions = new List<InputResolution>();

            foreach (var input in targetAction.Inputs)
            {
                // Do we have a resolver for this type of input?
                var resolver = InputResolvers.FirstOrDefault(r => r.InputType == input.Type);
                if (resolver == default)
                {
                    // No resolver. This is where you end up when a client turns on an input you've never
                    // seen before - treat it as a mapping issue for a human to fix, not a crash.
                    resolutions.Add(new InputResolution(input.Type, input.IsRequired, InputOutcome.NoResolver, []));
                    continue;
                }

                // Let the resolver write whatever it needs to into the payload. It only reports a
                // bool, so snapshot the payload either side of the call to find out what it wrote.
                var before = (JObject)payload.DeepClone();
                var wasResolved = resolver.Resolve(ev.Request, input, payload);

                resolutions.Add(new InputResolution(
                    input.Type,
                    input.IsRequired,
                    wasResolved ? InputOutcome.Resolved : InputOutcome.NoValue,
                    wasResolved ? PayloadInspector.WrittenValues(before, payload) : []));
            }

            ConsoleViz.ConcertoActionInputs(targetActionId, resolutions);

            // A required input we could not supply means the invoke would be rejected - stop here.
            //
            // Note that we stop, rather than throw. This is a mapping gap, not a fault: a client has
            // turned on an input we have no answer for, and the fix is for someone to teach the
            // integration about it. Treat it the way you would any other BAU mapping issue - record
            // it against the request, show it to a human, and let them retry once it is resolved.
            // Throwing out of an event handler just loses the context the table above gives you.
            var unsatisfied = resolutions.Where(r => r.IsFatal).ToList();
            if (unsatisfied.Count > 0)
            {
                foreach (var input in unsatisfied)
                {
                    var reason = input.Outcome == InputOutcome.NoResolver
                        ? "no resolver is available to provide a value"
                        : "the resolver did not provide a value";

                    log.LogError($"Action '{targetActionId}' requires an input of type '{input.Type}' but {reason}.");
                }

                log.LogError($"Not invoking '{targetActionId}' for request '{ev.Request.TheirRef}'. Close the mapping gap above and retry.");
                return;
            }

            // Note the shape of what we built - nested and array inputs are not flat properties.
            ConsoleViz.Json("Invoke payload", payload);

            ConsoleViz.PromptForContinue("We've built a payload by running the resolvers against our MyCafm system to generate values, " +
                "then packaged everythign up into a payload. The next (optional, but reccomended!) step is to validate the payload with Cocnerto");

            // Optional, but worth doing while you are building an integration: ask Concerto whether
            // this payload would be accepted, without actually applying it. 'Invoke' runs the same
            // checks itself, so this is a diagnostic rather than a required step - but it lets you
            // find a payload problem without having moved the order anywhere.
            var validation = concertoApi.Validate(concertoAccessToken, targetActionId, payload);
            ConsoleViz.ConcertoValidation(validation);

            if (!validation.IsValid)
            {
                // Concerto will reject this payload. Stop here and let a human see why rather than
                // sending it anyway.
                log.LogError($"Concerto rejected the payload for action '{targetActionId}': {string.Join(" ", validation.Errors)}");
                return;
            }

            ConsoleViz.PromptForContinue($"Concerto reports that the payload is valid. Next we'll invoke the action and see what happens to the order");

            var orderBeforeAction = concertoApi.GetOrder(concertoAccessToken, ev.Request.TheirRef); // grab the current state of the order

            // We now have a payload for the invoke endpoint. Let's call it!
            log.LogInformation($"Invoking action '{targetActionId}' with generated payload");
            var result = concertoApi.Invoke(concertoAccessToken, targetActionId, payload);

            // A rejected payload comes back as an unsuccessful result (HTTP 400 against the real
            // API), not as an exception. It is a normal outcome, and one you should expect to see
            // when a client changes their action configuration - surface it for someone to fix.
            if (!result.WasSuccess)
            {
                log.LogError($"Action '{targetActionId}' was not invoked: {result.ErrorMessage}");
                return;
            }

            log.LogInformation($"Action '{targetActionId}' invoked successfully");

            // Now let's see what has changed on that order
            var orderAfterAction = concertoApi.GetOrder(concertoAccessToken, ev.Request.TheirRef); // grab the new order version

            // Obviously, don't do this in a real implementation. Instead you should wait for an 'OrderAccepted' or other event to indicate your action has completed.
            // This only works here because everything in this example is synchronous and immediate, which is not how Concerto works in reality.

            ConsoleViz.ConcertoOrderDiff(orderBeforeAction, orderAfterAction);

            ConsoleViz.PromptForContinue($"We've invoked the action '{targetActionId}' and seen the order change. " +
                $"In a real integration, you would not be able to see the order change immediately like this - instead you would wait for an event from Concerto to tell you what happened. " +
                $"Next we'll look at what to try next in this example.");

            // That is one action, on one order. The mock has rather more in it - see the closing note.
            ConsoleViz.WhatToTryNext();
        }
    }
}