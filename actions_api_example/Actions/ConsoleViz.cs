using Actions.Concerto;
using Actions.Concerto.Actions;
using Actions.Concerto.ApiModels;
using Actions.Viz;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Spectre.Console;
using Spectre.Console.Json;

namespace Actions
{
    /// <summary>
    /// This helper is used to render helpful visualizations in the console.
    ///
    /// Each method here should only decide *what* to show - the table drawing, colouring and
    /// value formatting live in <see cref="DiffTable"/> so that every visualization we add
    /// looks and behaves the same.
    /// </summary>
    public static class ConsoleViz
    {
        /// <summary>
        /// The opening banner for the demo - a title, and enough scene setting that someone who
        /// just ran the app knows what they are about to watch happen.
        /// </summary>
        /// <remarks>
        /// Call this first, before any logging. The wording lives here rather than in Program.cs so
        /// that the program reads as the integration it is meant to demonstrate.
        /// </remarks>
        public static void Welcome()
        {
            HeroBanner.Render(
                title: "Concerto Actions Demo",
                strapline: "Discover, Describe, Validate, Invoke",
                intro:
                [
                    "This console app walks through a complete round trip between a CAFM system and the "
                    + "Concerto platform, using a mock Concerto that behaves like the real one: two instances, "
                    + "several clients, and actions whose IDs, inputs and availability differ from client to client.",

                    "Nothing below is hardcoded to a particular client. The integration asks Concerto which "
                    + "actions are available on an order, asks what inputs the one it wants requires, resolves "
                    + "those inputs from its own data, and only then invokes - which is what makes the same code "
                    + "work for a retail client and a local authority alike.",

                    "It is a starting point rather than a finished integration. One event is carried through to "
                    + "one action, and the mock holds plenty more than that - the run ends with some suggestions "
                    + "for what to change and re-run.",
                ],
                points:
                [
                    "An order is picked at random, so each run takes a slightly different path",
                    "Every Concerto call is narrated as a table, so you can see what came back",
                    "The order is shown before and after, so you can see what the action moved",
                    "Read it alongside the Actions API section of the IAC Concerto API Guide"
                ]);
        }

        /// <summary>
        /// Pauses the run until the reader presses Enter.
        ///
        /// The example moves fast and prints a lot. Drop one of these between steps when you want
        /// someone to actually read a table before the next one scrolls it away.
        /// </summary>
        /// <param name="prompt">
        /// What the reader should look at, or what happens next - eg. 'That is the payload we
        /// built. Next we ask Concerto whether it would accept it.' Shown above the prompt in its
        /// own right. Omit it for a bare 'Press Enter to continue'.
        /// </param>
        /// <remarks>
        /// Does nothing when console input has been redirected (piped, or running under a test
        /// harness), because there is no one there to press anything and we would block forever.
        /// </remarks>
        public static void PromptForContinue(string? prompt = null)
        {
            if (Console.IsInputRedirected)
            {
                return;
            }

            var keypress = new Markup($"[{VizStyle.Prompt}] Press Enter to continue [/]");

            if (prompt == null)
            {
                VizStyle.Write(keypress);
            }
            else
            {
                // The message leads, in the same loud style a table title gets, with the keypress
                // sitting directly beneath it - the pair should read as one interruption.
                VizStyle.Write(new Markup($"[{VizStyle.Title}]{VizStyle.Escape(prompt)}[/]"));
                AnsiConsole.WriteLine();
                AnsiConsole.Write(keypress);
            }

            AnsiConsole.WriteLine();

            Console.ReadLine();
        }

        /// <summary>
        /// Shows what will happen to an order when we eventually invoke an action on it - the state it
        /// is in now, the action we intend to run, the inputs that action will ask us for, and the state
        /// the order will be left in.
        ///
        /// Use this to set the scene *before* the discover/describe/invoke dance below, so the reader
        /// knows what they're watching the integration work towards.
        /// </summary>
        /// <param name="order">The order we're about to put through its paces.</param>
        /// <param name="actionName">
        /// The name of the action we intend to invoke - the same name an integration would map its own
        /// event to. Names are matched, not IDs: IDs are client specific.
        /// </param>
        /// <remarks>
        /// The Concerto lookups live in here deliberately. This is a documentation aid, and none of the
        /// 'what would happen if' guesswork it does belongs in the integration itself - a real one
        /// discovers, describes, invokes and then reads the order back to see what moved.
        /// </remarks>
        public static void ConcertoOrderTransition(Order order, string actionName)
        {
            var preview = ConcertoPlatform.PreviewAction(order, actionName);

            if (preview == null)
            {
                // Either the order is a stranger to us, or the action isn't offered in its current
                // state. Both are worth saying out loud rather than drawing an empty flow.
                VizStyle.Write(VizStyle.Frame(
                    $"Invalid Order {order.OrderNumber}",
                    VizStyle.Cell(
                        $"'{actionName}' is not available on this order while it is '{order.Status}'",
                        VizStyle.Removed)));

                return;
            }

            TransitionFlow.Render(
                title: "We've selected a random order",
                subtitle: $"Order Number: {preview.OrderNumber}   ({order.Type} - {order.Description})",
                fromState: preview.CurrentStatus,
                actionName: preview.ActionName,
                actionDetail: preview.ActionId,
                inputs: preview.Inputs.Select(i => new FlowInput(i.Type, i.IsRequired)),
                toState: preview.MovesStatus ? preview.ResultingStatus : $"{preview.ResultingStatus} (unchanged)");
        }

        /// <summary>
        /// Shows what invoking an action did to a Concerto order, by comparing the order as it was
        /// before the call with the order as it was afterwards.
        /// </summary>
        /// <param name="from">The order before the action was invoked.</param>
        /// <param name="to">The order after the action was invoked.</param>
        /// <param name="changedOnly">
        /// When true (the default) only fields that actually moved are listed. Pass false to show
        /// the whole order, which is handy when you're trying to work out why nothing changed.
        /// </param>
        public static void ConcertoOrderDiff(Order from, Order to, bool changedOnly = true)
        {
            DiffRow[] rows =
            [
                DiffTable.Compare("Status", from.Status, to.Status),
                DiffTable.Compare("Type", from.Type, to.Type),
                DiffTable.Compare("Description", from.Description, to.Description),
                DiffTable.Compare("ETA", from.Eta, to.Eta),
                DiffTable.Compare("Value", from.Value, to.Value, v => v.ToString("C")),
                DiffTable.Compare("Complete date", from.CompleteDate, to.CompleteDate),
                DiffTable.Compare("Client auth code", from.ClientAuthCode, to.ClientAuthCode)
            ];

            DiffTable.Render($"Concerto order {to.OrderNumber}", rows, changedOnly);
        }

        /// <summary>
        /// Lists the actions Concerto says are currently available on an order, highlighting the
        /// one we intend to invoke.
        /// </summary>
        /// <param name="actions">The result of a 'discover' call.</param>
        /// <param name="orderNumber">The order the actions belong to.</param>
        /// <param name="wantedActionName">
        /// The action name we mapped our own event to, so the reader can see at a glance whether
        /// it turned up. Actions are matched on name, not ID - IDs are client specific.
        /// </param>
        public static void ConcertoDiscoveredActions(
            IEnumerable<DiscoveredAction> actions,
            string orderNumber,
            string? wantedActionName = null)
        {
            var discovered = actions.ToList();

            if (discovered.Count == 0)
            {
                VizStyle.Write(VizStyle.Frame(
                    $"Actions available on order {orderNumber}",
                    VizStyle.Cell("No actions available", VizStyle.Muted)));

                return;
            }

            var table = VizStyle.Table($"Actions available on order {orderNumber}", "Action ID", "Key", "Name");

            foreach (var action in discovered)
            {
                // The action we're here to invoke is picked out in colour; the rest stay quiet.
                var isWanted = action.Name == wantedActionName;

                table.AddRow(
                    VizStyle.Cell(action.Id, isWanted ? VizStyle.Added : VizStyle.Muted),
                    VizStyle.Cell(action.Key ?? "(none)", isWanted && action.Key != null ? VizStyle.Added : VizStyle.Muted),
                    VizStyle.Cell(action.Name, isWanted ? VizStyle.Highlight : VizStyle.Muted));
            }

            VizStyle.Write(table);

            // Say so explicitly - an absent action is the most common reason an integration stalls.
            if (wantedActionName != null && !discovered.Any(a => a.Name == wantedActionName))
            {
                VizStyle.Write(VizStyle.Cell($"'{wantedActionName}' is not available on this order", VizStyle.Removed));
            }
        }

        /// <summary>
        /// Shows what the 'validate' endpoint made of a payload - either that it would be accepted,
        /// or the list of reasons it would not.
        /// </summary>
        public static void ConcertoValidation(PayloadValidationResult validation)
        {
            if (validation.IsValid)
            {
                VizStyle.Write(VizStyle.Frame(
                    "Validate payload",
                    VizStyle.Cell("ok - Concerto would accept this payload", VizStyle.Added)));

                return;
            }

            var table = VizStyle.Table("Validate payload", "Concerto rejected this payload because");

            foreach (var error in validation.Errors)
            {
                table.AddRow(VizStyle.Cell(error, VizStyle.Removed));
            }

            VizStyle.Write(table);
        }

        /// <summary>
        /// The closing note. The example deliberately carries a single event through to a single
        /// invoke; the mock holds a good deal more than that, and the point of shipping it as
        /// source is that an integrator goes and pokes at the rest.
        /// </summary>
        public static void WhatToTryNext()
        {
            HeroBanner.Render(
                title: "Try this next",
                strapline: null,
                intro:
                [
                    "One event has been carried through to one invoke, which is enough to show the shape of "
                    + "the thing: discover, describe, resolve, validate, invoke. The mock Concerto holds "
                    + "several more actions than the one that just ran, with inputs this run never touched - "
                    + "nested cost objects, array shaped custom field answers, and an input that writes two "
                    + "properties into the payload rather than one.",

                    "Those are the inputs worth your time, because they are the ones that do not behave like "
                    + "a flat property. Edit the example and go and meet them.",
                ],
                points:
                [
                    "Handle 'completed' as well as 'allocated', map it to 'Complete Job', and call "
                    + "CafmRequest.CompleteWork(..) - then relax the filter in FindOrderForAssignment so it "
                    + "picks an order that is already in-progress",

                    "'Complete Job' differs by order type - a reactive order and a PPM order describe "
                    + "different inputs, from two separate actions that share one name",

                    "The PPM one asks for a client-configured custom field. You cannot know it exists "
                    + "without reading the metadata that 'describe' returns",

                    "Delete a resolver from the InputResolvers table, or skip CompleteWork, to see a "
                    + "mapping gap reported rather than guessed at",

                    "Add an action, or an input, to ConcertoPlatform.Reset() and watch the integration cope "
                    + "- not having to change is the whole point"
                ]);
        }

        /// <summary>
        /// Shows the inputs an action described, alongside what we managed to resolve for each -
        /// the two halves of the same story, so you can see a gap without cross referencing lists.
        /// </summary>
        /// <param name="actionId">The action whose inputs these are.</param>
        /// <param name="resolutions">One entry per described input.</param>
        public static void ConcertoActionInputs(string actionId, IEnumerable<InputResolution> resolutions)
        {
            var rows = resolutions.ToList();

            if (rows.Count == 0)
            {
                VizStyle.Write(VizStyle.Frame(
                    $"Inputs for action {actionId}",
                    VizStyle.Cell("This action takes no inputs", VizStyle.Muted)));

                return;
            }

            var table = VizStyle.Table($"Inputs for action {actionId}", "Input", "Required", "Resolved Value");

            foreach (var row in rows)
            {
                table.AddRow(
                    VizStyle.Cell(row.Type, row.IsFatal ? VizStyle.Removed : VizStyle.Highlight),
                    VizStyle.Cell(row.IsRequired ? "yes" : "no", row.IsRequired ? VizStyle.Highlight : VizStyle.Muted),
                    VizStyle.Cell(ResolvedValueText(row), ResolvedValueStyle(row)));
            }

            VizStyle.Write(table);
        }

        /// <summary>
        /// Shows a JSON body in a frame, syntax highlighted - handy for eyeballing the nesting,
        /// which is where most invoke payloads go wrong.
        /// </summary>
        /// <param name="title">Heading for the frame, eg. 'Invoke payload'.</param>
        /// <param name="json">The body to show.</param>
        public static void Json(string title, JObject json) =>
            VizStyle.Write(VizStyle.Frame(title, new JsonText(json.ToString(Formatting.Indented))));

        /// <summary>
        /// What to print in the 'Resolved Value' column - the values written, or why there aren't any.
        /// </summary>
        private static string ResolvedValueText(InputResolution row) => row.Outcome switch
        {
            InputOutcome.Resolved when row.WrittenValues.Count > 0 => string.Join(Environment.NewLine, row.WrittenValues),
            InputOutcome.Resolved => "(resolved)",
            InputOutcome.NoResolver => "(no resolver for this input type)",
            _ => "(resolver had no value to supply)"
        };

        /// <summary>
        /// Green for a supplied value, red when a missing one will fail the invoke, dim when it's
        /// an optional input we simply had nothing for.
        /// </summary>
        private static string ResolvedValueStyle(InputResolution row) => row.Outcome switch
        {
            InputOutcome.Resolved => VizStyle.Added,
            _ when row.IsRequired => VizStyle.Removed,
            _ => VizStyle.Muted
        };
    }
}
