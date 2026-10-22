using Actions.Concerto.Actions;
using Actions.Concerto.ApiModels;
using Newtonsoft.Json.Linq;

namespace Actions.Concerto
{
    /// <summary>
    /// A stand-in for the HTTP client you would write against a single Concerto instance.
    ///
    /// The four Actions API calls are the whole of the dance: Discover what is available, Describe
    /// the one you want, optionally Validate the payload you built, then Invoke.
    /// </summary>
    public class ConcertoApiClient
    {
        private Func<string, string, string, ActionModel> onDescribe;
        private Func<string, string, IEnumerable<DiscoveredAction>> onDiscover;
        private Func<string, string?, string?, string?, IEnumerable<Order>> onGetOrders;
        private Func<string, string, JObject, PayloadValidationResult> onValidate;
        private Func<string, string, JObject, ActionResult> onInvoke;

        private Func<string, string, Order> onGetOrder;

        internal ConcertoApiClient(
            Func<string, string, string, ActionModel> onDescribe,
            Func<string, string, IEnumerable<DiscoveredAction>> onDiscover,
            Func<string, string, Order> onGetOrder,
            Func<string, string?, string?, string?, IEnumerable<Order>> onGetOrders,
            Func<string, string, JObject, PayloadValidationResult> onValidate,
            Func<string, string, JObject, ActionResult> onInvoke)
        {
            this.onDescribe = onDescribe;
            this.onDiscover = onDiscover;
            this.onGetOrder = onGetOrder;
            this.onGetOrders = onGetOrders;
            this.onValidate = onValidate;
            this.onInvoke = onInvoke;
        }

        public IEnumerable<Order> GetOrders(string accessToken, string? tenantKey = null, string? status = null, string? type = null) => onGetOrders(accessToken, tenantKey, status, type);

        /// <summary>GET /api/action/v1/action/{actionId}/describe?targetEntityId={orderNumber}</summary>
        public ActionModel Describe(string accessToken, string actionId, string orderNumber) => onDescribe(accessToken, actionId, orderNumber);

        /// <summary>GET /api/action/v1/action/discover?targetEntityId={orderNumber}</summary>
        public IEnumerable<DiscoveredAction> Discover(string accessToken, string orderNumber) => onDiscover(accessToken, orderNumber);

        /// <summary>POST /api/action/v1/action/{actionId}/validate</summary>
        public PayloadValidationResult Validate(string accessToken, string actionId, JObject inputs) => onValidate(accessToken, actionId, inputs);

        /// <summary>POST /api/action/v1/action/{actionId}/invoke</summary>
        public ActionResult Invoke(string accessToken, string actionId, JObject inputs) => onInvoke(accessToken, actionId, inputs);

        public Order GetOrder(string concertoAccessToken, string theirRef) => onGetOrder(concertoAccessToken, theirRef);
    }
}
