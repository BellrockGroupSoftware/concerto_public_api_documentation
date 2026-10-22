using Actions.Concerto.Actions;
using Actions.Concerto.ApiModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Actions.Concerto
{
    internal class ConcertoInstance
    {
        public required string Name { get; set; }
        public required string BaseUrl { get; set; }
        internal required string ExpectedAccessToken { get; set; }

        internal Client[] Clients = Array.Empty<Client>();

        internal string[] OrderStatuses = Array.Empty<string>();

        internal ConcertoAction[] Actions = Array.Empty<ConcertoAction>();

        internal List<Order> Orders = new List<Order>();

        internal ConcertoApiClient GetApiClient()
        {
            return new ConcertoApiClient(
                Describe,
                Discover,
                GetOrder,
                GetOrders,
                Validate,
                Invoke
            );
        }

        private void CheckAccessToken(string accessToken)
        {
            if (accessToken != ExpectedAccessToken)
            {
                throw new MockHttpException(401, "Invalid access token");
            }
        }

        internal Order GetOrder(string accessToken, string orderNumber)
        {
            CheckAccessToken(accessToken);
            var order = Orders.FirstOrDefault(o => o.OrderNumber == orderNumber);
            if (order == null)
            {
                throw new MockHttpException(404, $"Order with number '{orderNumber}' not found");
            }
            return order;
        }

        internal IEnumerable<Order> GetOrders(string accessToken, string? clientCode = null, string? status = null, string? type = null)
        {
            CheckAccessToken(accessToken);
            IEnumerable<Order> results = Orders;

            if (!string.IsNullOrEmpty(clientCode))
            {
                results = results.Where(o => o.TenantKey == clientCode);
            }

            if (!string.IsNullOrEmpty(status))
            {
                results = results.Where(o => o.Status == status);
            }

            if (!string.IsNullOrEmpty(type))
            {
                results = results.Where(o => o.Type == type);
            }

            return results;
        }

        /// <summary>
        /// Discovers the Actions currently available on a given Order.
        ///
        /// 'Currently' is the important word: availability depends on the order's status, its type,
        /// and who is asking. The same order will offer a different set of actions an hour later.
        /// </summary>
        internal IEnumerable<DiscoveredAction> Discover(string accessToken, string orderNumber)
        {
            CheckAccessToken(accessToken);
            var order = Orders.FirstOrDefault(o => o.OrderNumber == orderNumber);
            if (order == null)
            {
                throw new MockHttpException(404, $"Order with number '{orderNumber}' not found");
            }

            return Actions
                .Where(a => a.CheckIsAvailable(order))
                .Select(a => new DiscoveredAction { Id = a.Id, Key = a.Key, Name = a.Name });
        }

        internal ActionModel Describe(string accessToken, string actionId, string orderNumber)
        {
            CheckAccessToken(accessToken);

            var order = Orders.FirstOrDefault(o => o.OrderNumber == orderNumber);
            if (order == null)
            {
                throw new MockHttpException(404, $"Order with number '{orderNumber}' not found");
            }

            var action = Actions.FirstOrDefault(a => a.Id == actionId);
            if (action == null)
            {
                throw new MockHttpException(404, $"Action with id '{actionId}' not found");
            }

            if (!action.CheckIsAvailable(order))
            {
                throw new MockHttpException(400, $"Action with id '{actionId}' is not available for the given order");
            }


            return action.ToApiModel(order);
        }

        internal PayloadValidationResult Validate(string accessToken, string actionId, JObject inputs)
        {
            CheckAccessToken(accessToken);

            var action = Actions.FirstOrDefault(a => a.Id == actionId)
                ?? throw new MockHttpException(404, $"Action with id '{actionId}' not found");

            var errors = action.Validate(this, inputs, out _);

            return new PayloadValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors
            };
        }

        internal ActionResult Invoke(string accessToken, string actionId, JObject inputs)
        {
            CheckAccessToken(accessToken);
            
            // Fetch the action
            var action = Actions.FirstOrDefault(a => a.Id == actionId)
                ?? throw new MockHttpException(404, $"Action with id '{actionId}' not found");

            // Invoke validates the payload itself - calling 'validate' first is optional, not a
            // precondition. A payload that fails here comes back as an unsuccessful result (the
            // real API answers HTTP 400), not as an exception.
            var errors = action.Validate(this, inputs, out var order);

            if (errors.Count > 0 || order == null)
            {
                return new ActionResult
                {
                    WasSuccess = false,
                    ErrorMessage = "One or more validation errors occurred processing your payload: "
                        + string.Join(" ", errors),
                    Errors = errors
                };
            }

            action.Apply(this, order, inputs);

            return new ActionResult
            {
                WasSuccess = true
            };
        }

    }
}
