using Spectre.Console;
using Spectre.Console.Rendering;

namespace Actions.Viz
{
    /// <summary>
    /// One input an action is going to ask for, as shown beneath the action in a transition flow.
    /// </summary>
    /// <param name="Type">The Concerto input type, eg. 'Appointment.From'.</param>
    /// <param name="IsRequired">Whether the invoke would be rejected without it.</param>
    public readonly record struct FlowInput(string Type, bool IsRequired);

    /// <summary>
    /// Draws a 'this state, via this action, becomes that state' flow.
    ///
    /// Like <see cref="DiffTable"/>, this owns the borders, colours and spacing so that callers in
    /// <see cref="ConsoleViz"/> only have to say what the three boxes contain.
    /// </summary>
    public static class TransitionFlow
    {
        /// <summary>Text of the connector drawn between boxes when they sit side by side.</summary>
        private const string RightArrow = "-->";

        /// <summary>Width one horizontal connector occupies, including the space either side of it.</summary>
        private const int ArrowWidth = 5;

        /// <summary>
        /// Renders the flow.
        /// </summary>
        /// <param name="title">Heading for the whole thing.</param>
        /// <param name="subtitle">A line above the flow - the order number and anything else worth knowing.</param>
        /// <param name="fromState">The state the subject is in now.</param>
        /// <param name="actionName">The action that would be invoked.</param>
        /// <param name="actionDetail">A quieter second line under the action - its ID, say.</param>
        /// <param name="inputs">The inputs that action would ask for. Listed under it, in describe order.</param>
        /// <param name="toState">The state the subject would end up in.</param>
        public static void Render(
            string title,
            string subtitle,
            string fromState,
            string actionName,
            string actionDetail,
            IEnumerable<FlowInput> inputs,
            string toState)
        {
            // An action that doesn't move the order is worth noticing rather than glossing over -
            // notification style actions are a common source of 'why did nothing happen?'.
            var movesState = fromState != toState;

            var boxes = new[]
            {
                new Box("Current state", [(fromState, VizStyle.Removed)]),
                new Box("Action", ActionLines(actionName, actionDetail, inputs)),
                new Box("Resulting state", [(toState, movesState ? VizStyle.Added : VizStyle.Muted)])
            };

            // Side by side reads best, but only while the boxes actually fit - squeezed into a narrow
            // console the grid hyphenates status names and even breaks the arrows in half.
            var needed = boxes.Sum(b => b.Width) + (ArrowWidth * (boxes.Length - 1));
            var flow = needed <= AnsiConsole.Profile.Width
                ? SideBySide(boxes)
                : Stacked(boxes);

            VizStyle.Write(new Rows(
                new Rule($"[{VizStyle.Title}]{VizStyle.Escape(title)}[/]").LeftJustified(),
                VizStyle.Cell(subtitle, VizStyle.Highlight),
                Blank(),
                flow));
        }

        /// <summary>
        /// The action box's contents: what we'd invoke, and - the part that's easy to forget -
        /// everything it is going to want from us before it will run.
        /// </summary>
        private static List<(string Text, string Style)> ActionLines(
            string actionName,
            string actionDetail,
            IEnumerable<FlowInput> inputs)
        {
            var lines = new List<(string, string)>
            {
                (actionName, VizStyle.Highlight),
                (actionDetail, VizStyle.Muted)
            };

            var listed = inputs.ToList();

            if (listed.Count == 0)
            {
                lines.Add(("no inputs", VizStyle.Muted));
            }

            foreach (var input in listed)
            {
                // Required inputs are the ones that will stop the invoke dead, so they keep the colour.
                lines.Add((
                    $"+ {input.Type} ({(input.IsRequired ? "required" : "optional")})",
                    input.IsRequired ? VizStyle.Highlight : VizStyle.Muted));
            }

            return lines;
        }

        /// <summary>
        /// Boxes left to right, joined by arrows. Columns are pinned to the width each box needs, so
        /// the grid lays them out rather than sharing the console width equally between them.
        /// </summary>
        private static IRenderable SideBySide(IReadOnlyList<Box> boxes)
        {
            var grid = new Grid();

            for (var i = 0; i < boxes.Count; i++)
            {
                if (i > 0)
                {
                    grid.AddColumn(new GridColumn().Width(ArrowWidth).NoWrap().PadLeft(0).PadRight(0));
                }

                // Padding off: the widths above are the whole story, and a grid that quietly adds a
                // column of its own is a grid that overflows and starts shrinking the boxes again.
                grid.AddColumn(new GridColumn().Width(boxes[i].Width).NoWrap().PadLeft(0).PadRight(0));
            }

            var cells = new List<IRenderable>();

            for (var i = 0; i < boxes.Count; i++)
            {
                if (i > 0)
                {
                    // The blank line drops the arrow onto the boxes' first content line, rather than
                    // leaving it level with their top border.
                    cells.Add(new Rows(
                        VizStyle.Cell(" ", VizStyle.Muted),
                        VizStyle.Cell($" {RightArrow} ", VizStyle.Muted)));
                }

                cells.Add(boxes[i].Render());
            }

            grid.AddRow(cells.ToArray());

            return grid;
        }

        /// <summary>
        /// The narrow console fallback - the same boxes, top to bottom. Less pretty, but it never
        /// breaks a word in half, and the order of events is still obvious.
        /// </summary>
        private static IRenderable Stacked(IReadOnlyList<Box> boxes)
        {
            var rows = new List<IRenderable>();

            foreach (var box in boxes)
            {
                if (rows.Count > 0)
                {
                    rows.Add(VizStyle.Cell("  |", VizStyle.Muted));
                    rows.Add(VizStyle.Cell("  v", VizStyle.Muted));
                }

                rows.Add(box.Render());
            }

            return new Rows(rows);
        }

        /// <summary>An empty line, for breathing room.</summary>
        private static IRenderable Blank() => new Markup(string.Empty);

        /// <summary>
        /// One titled box in the flow, and the width it needs to draw itself without wrapping.
        /// </summary>
        private sealed class Box(string heading, List<(string Text, string Style)> lines)
        {
            /// <summary>Border and padding a panel adds either side of its content.</summary>
            private const int Chrome = 4;

            /// <summary>Space a panel wants around a heading, on top of the heading itself.</summary>
            private const int HeadingChrome = 4;

            /// <summary>
            /// Wide enough for the longest line, and never narrower than the heading - a panel narrower
            /// than its own header gets that header truncated: 'Resulting state' becomes 'Resulting...'.
            /// </summary>
            public int Width => Math.Max(heading.Length + HeadingChrome, lines.Max(l => l.Text.Length)) + Chrome;

            /// <remarks>
            /// Lines are padded out to the box width so the panel really is <see cref="Width"/> wide.
            /// Left to itself a panel shrinks to its content, and then the heading is what gets cut.
            /// </remarks>
            public IRenderable Render() => VizStyle.Frame(
                heading,
                new Rows(lines.Select(l => VizStyle.Cell(l.Text.PadRight(Width - Chrome), l.Style))));
        }
    }
}
