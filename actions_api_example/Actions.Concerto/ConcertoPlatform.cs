using Actions.Concerto.Actions;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Actions.Concerto
{
    public static class ConcertoPlatform
    {
        private static ConcertoInstance[] instances = Array.Empty<ConcertoInstance>();
        private static Dictionary<string, Action<ConcertoEvent>> eventHandlers = new Dictionary<string, Action<ConcertoEvent>>();

        /// <summary>
        /// Registers a handler for particular types of events
        /// </summary>
        /// <param name="cafmEvent"></param>
        /// <param name="handler"></param>
        public static void RegisterEventHandler(string cafmEvent, Action<ConcertoEvent> handler)
        {
            eventHandlers.Add(cafmEvent, handler);
        }

        internal static void RaiseEvent(ConcertoEvent ev)
        {
            if (eventHandlers.ContainsKey(ev.Type))
            {
                eventHandlers[ev.Type](ev);
            }
        }


        static ConcertoPlatform()
        {
            Reset();
        }

        /// <summary>
        /// Find a random order in a sensible status for assignment, or null if none found
        /// </summary>
        /// <returns></returns>
        public static Order? FindOrderForAssignment() => instances
                .SelectMany(i => i.Orders)
                .Where(o => o.Status == "waiting-for-accept" || o.Status == "waiting-for-ack")
                .OrderBy(o => Guid.NewGuid())
                .FirstOrDefault();

        /// <summary>
        /// Produce an OrderAssigned event for a random order in a sensible status
        /// </summary>
        /// <returns></returns>
        public static void ProduceOrderAssignedEvent(Order order)
        {
            var ev = new ConcertoEvent
            {
                Id = Guid.NewGuid().ToString(),
                Type = "orderAssigned",
                Payload = JObject.FromObject(new
                {
                    // The order's ID - what you address every subsequent API call with. Real IDs
                    // are GUIDs; readable strings are used throughout this example instead.
                    OrderId = order.OrderNumber,

                    // The human readable ref, as seen in the Supplier Portal. Handy to show a user,
                    // but it is OrderId you store and integrate against.
                    OrderRef = order.OrderNumber,

                    // The tenant key - which Concerto instance this order lives on. Resolve it
                    // through the routing API to find the base URL for your subsequent calls.
                    PortfolioCode = order.TenantKey,

                    Timestamp = DateTime.Now
                })
            };

            RaiseEvent(ev);

            // Events are multicast to channels, and your subscription can pick the same one up via
            // more than one of them. Here is that happening - the integration is expected to notice
            // it has already handled this event ID and do nothing the second time.
            RaiseEvent(ev);
        }


        /// <summary>
        /// This is a mock of the global 'routing' API, where you can exchange a tenant key for the base url of its concerto instance
        /// </summary>
        /// <param name="tenantKey"></param>
        /// <returns></returns>
        /// <exception cref="MockHttpException"></exception>
        public static string RoutingApi_ResolveTenant(string tenantKey)
        {
            var instance = instances.FirstOrDefault(i => i.Clients.Any(c => c.ClientCode == tenantKey));
            if (instance == null)
            {
                throw new MockHttpException(404, $"No ConcertoInstance found for TenantKey '{tenantKey}'.");
            }

            return instance.BaseUrl;
        }

        /// <summary>
        /// Gets an API client for the Concerto instance with the specified base url
        /// </summary>
        /// <param name="baseUrl"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static ConcertoApiClient Connect(string baseUrl) => instances.FirstOrDefault(c => c.BaseUrl.ToLower() == baseUrl.ToLower())?.GetApiClient()
                ?? throw new InvalidOperationException($"No ConcertoInstance found for BaseUrl '{baseUrl}'.");



        /// <summary>
        /// Works out what invoking a named action against an order would do to it, without invoking
        /// anything - the current status, the resulting status, and the inputs 'describe' would ask for.
        /// </summary>
        /// <param name="order">The order the action would be performed against.</param>
        /// <param name="actionName">
        /// The action's name, as an integration would match it. Names are stable across clients; IDs are not.
        /// </param>
        /// <returns>
        /// The preview, or null if this order is unknown or no action of that name is currently
        /// available on it.
        /// </returns>
        /// <remarks>
        /// Convenience of the mock only - see <see cref="ActionPreview"/>. A real integration cannot ask
        /// Concerto what an action would do; it discovers, describes, invokes, and then reads the order back.
        /// </remarks>
        public static ActionPreview? PreviewAction(Order order, string actionName)
        {
            var instance = instances.FirstOrDefault(i => i.Orders.Any(o => o.OrderNumber == order.OrderNumber));
            if (instance == null)
            {
                return null;
            }

            // Only actions Concerto would actually offer on this order - same availability rules 'discover' uses.
            var action = instance.Actions.FirstOrDefault(a => a.Name == actionName && a.CheckIsAvailable(order));
            if (action == null)
            {
                return null;
            }

            // Apply the action to a throwaway copy to find out where the order would land. Inputs are
            // not supplied, so only the action's own state change is reflected here.
            var outcome = order.Clone();
            action.OnApply?.Invoke(outcome);

            return new ActionPreview
            {
                OrderNumber = order.OrderNumber,
                ActionId = action.Id,
                ActionName = action.Name,
                ActionDescription = action.Description,
                CurrentStatus = order.Status,
                ResultingStatus = outcome.Status,
                Inputs = action.ToApiModel(order).Inputs.ToList()
            };
        }

        public static void Reset() { 
            ConcertoPlatform.instances = [
                new ConcertoInstance
                {
                    Name = "Retail Instance",
                    BaseUrl = "https://shop.bellrock.co.uk",
                    ExpectedAccessToken = "ACC-RETAIL-1234",
                    Clients = [
                        Client.Coffee,
                        Client.Food
                    ],
                    OrderStatuses =
                    [
                        "waiting-for-accept",
                        "waiting-for-appointment",
                        "in-progress",
                        "complete"
                    ],
                    Actions =
                    [
                        new ConcertoAction()
                        {
                            Id = "acc-01",

                            // The same well-known key as the local authority instance's 'Accept Job'
                            // below, even though the IDs have nothing in common. Keys are stable
                            // where a client configures them; IDs never are.
                            Key = "ACCEPT",
                            Name = "Accept Job",
                            Description = "Accept a job and move it to the next status",
                            ScopeStates = [ "waiting-for-accept" ],
                            Inputs = [
                                Inputs.OrderId(),

                                // This client captures attendance as an appointment window, so these two
                                // inputs are nested under 'Appointment' in the invoke payload.
                                // They're required!
                                Inputs.AppointmentFrom(),
                                Inputs.AppointmentTo()
                                ],
                            OnApply = o => o.Status = "waiting-for-appointment",
                        },

                        new ConcertoAction()
                        {
                            Id = "schedule-appointment",
                            Name = "Schedule Appointment",
                            Description = "Schedule an appointment for a job and move it to the next status",
                            ScopeStates = [ "waiting-for-appointment" ],
                            Inputs = [
                                Inputs.OrderId(),
                                Inputs.AppointmentFrom(),
                                Inputs.AppointmentTo(),
                                Inputs.AppointmentNotes().IsOptional()
                            ],
                            OnApply = o => o.Status = "in-progress",
                        },

                        new ConcertoAction() {
                            Id = "complete-reactive-job",
                            Name = "Complete Job",
                            Description = "Complete a reactive job and move it to the next status",
                            ScopeStates = [ "in-progress" ],
                            ScopeOrderTypes = [ "reactive" ],
                            Inputs = [
                                Inputs.OrderId(),
                                Inputs.LabourCost(),
                                Inputs.OrderCompleteDate(),
                                Inputs.IsNoCharge().IsOptional()
                             ],
                            OnApply = o => o.Status = "complete",
                        },

                        new ConcertoAction() {
                            Id = "complete-ppm-job",
                            Name = "Complete Job",
                            Description = "Complete a PPM job and move it to the next status",
                            ScopeStates = [ "in-progress" ],
                            ScopeOrderTypes = [ "ppm" ],
                            Inputs = [
                                Inputs.OrderId(),
                                Inputs.LabourCost().IsOptional(),
                                Inputs.OrderCompleteDate(),
                                // PPM compliance is a client-configured custom field on this instance.
                                // The integrator can only discover it by reading the describe metadata.
                                Inputs.OrderActionFieldInputs("client-auth-code", "Client Mandated Auth Code")
                             ],
                            OnApply = o => o.Status = "complete",
                        }
                    ],
                    Orders = [

                        // Coffee client orders
                        new Order()
                        {
                            Type = "reactive",
                            Status = "waiting-for-accept",
                            TenantKey = Client.Coffee.ClientCode,
                            OrderNumber = "COFFEE-1",
                            Description = "Coffee machine is broken",
                            Value = 50.00m
                        },
                        new Order()
                        {
                            Type = "reactive",
                            Status = "in-progress",
                            TenantKey = Client.Coffee.ClientCode,
                            OrderNumber = "COFFEE-2",
                            Description = "Fridge is not cooling",
                            Value = 100.00m,
                        },
                        new Order()
                        {
                            Type = "reactive",
                            Status = "complete",
                            TenantKey = Client.Coffee.ClientCode,
                            OrderNumber = "COFFEE-3",
                            Description = "Store door is stuck",
                            Value = 0.00m,
                            CompleteDate = DateTime.Now.AddDays(-1)
                        },

                        // Food client orders
                        new Order()
                        {
                            Type = "reactive",
                            Status = "waiting-for-accept",
                            TenantKey = Client.Food.ClientCode,
                            OrderNumber = "FOOD-1",
                            Description = "Sandwich maker is producing incorrect sandwiches",
                            Value = 50.00m
                        },

                        new Order()
                        {
                            Type = "reactive",
                            Status = "waiting-for-accept",
                            TenantKey = Client.Food.ClientCode,
                            OrderNumber = "FOOD-2",
                            Description = "Brioche is not brioching",
                            Value = 50.00m
                        },

                        new Order()
                        {
                            Type = "ppm",
                            Status = "in-progress",
                            TenantKey = Client.Food.ClientCode,
                            OrderNumber = "FOOD-3",
                            Description = "5yr Fixed Wire Inspection",
                            Value = 0.00m
                        }
                    ]
                },

                new ConcertoInstance
                {
                    Name = "Local Government Instance",
                    BaseUrl = "https://gov.bellrock.co.uk",
                    ExpectedAccessToken = "ACC-GOV-9876",
                    Clients = [ 
                        Client.LocalAuthority1,
                        Client.LocalAuthority2
                    ],
                    OrderStatuses = [
                        "waiting-for-ack",
                        "acknowledged",
                        "en-route",
                        "on-site",
                        "complete"
                    ],

                    Actions =
                    [
                        new ConcertoAction()
                        {
                            Id = "ack_job_v7",
                            Key = "ACCEPT",
                            Name = "Accept Job",
                            Description = "Supplier Acknowledges Job",
                            ScopeStates = [ "waiting-for-ack" ],
                            Inputs = [
                                Inputs.OrderId()
                            ],
                            OnApply = o => o.Status = "acknowledged",
                        },

                        new ConcertoAction()
                        {
                            Id = "notify-en-route",
                            Name = "Engineer on the way",
                            Description = "Supplier notifies that the engineer is on the way",
                            ScopeStates = [ "acknowledged" ],
                            Inputs = [
                                Inputs.OrderId(),
                                Inputs.EstimatedArrivalTime()
                            ],
                            OnApply = o => o.Status = "en-route",
                        },

                        new ConcertoAction()
                        {
                            Id = "notify-delayed",
                            Name = "Engineer is running late",
                            Description = "Supplier notifies that the engineer is running late",
                            ScopeStates = [ "en-route" ],
                            Inputs = [
                                Inputs.OrderId(),
                                Inputs.EstimatedArrivalTime()
                            ],
                            OnApply = o => o.Status = "en-route",
                        },

                        new ConcertoAction() {
                            Id = "notify-arrived",
                            Name = "Engineer has arrived",
                            Description = "Supplier notifies that the engineer has arrived on site",
                            ScopeStates = [ "en-route" ],
                            Inputs = [
                                Inputs.OrderId()
                            ],
                            OnApply = o => o.Status = "on-site",
                        },


                        new ConcertoAction() {
                            Id = "notify-resolved",
                            Name = "Issue Resolved",
                            Description = "Supplier notifies that the issue has been resolved",
                            ScopeStates = [ "on-site" ],
                            Inputs = [
                                Inputs.OrderId(),
                                Inputs.OrderCompleteDate()
                            ],
                            OnApply = o => o.Status = "complete",
                        },

                        new ConcertoAction() {
                            Id = "notify-unresolved",
                            Name = "Issue Unresolved - revisit required",
                            Description = "Supplier notifies that the issue has not been resolved and a revisit is required",
                            ScopeStates = [ "on-site" ],
                            Inputs = [
                                Inputs.OrderId(),
                                Inputs.EstimatedArrivalTime()
                            ],
                            OnApply = o => o.Status = "acknowledged",
                        },
                    ],
                    Orders = [
                        new Order()
                        {
                            Type = "reactive",
                            Status = "waiting-for-ack",
                            TenantKey = Client.LocalAuthority1.ClientCode,
                            OrderNumber = "NORTH-1",
                            Description = "Sink blocked",
                            Value = 50.00m
                        },
                        new Order()
                        {
                            Type = "reactive",
                            Status = "waiting-for-ack",
                            TenantKey = Client.LocalAuthority2.ClientCode,
                            OrderNumber = "SOUTH-1",
                            Description = "Door hinge squeaky",
                            Value = 50.00m
                        }
                    ]
                }
            ];
        }
    }
}
