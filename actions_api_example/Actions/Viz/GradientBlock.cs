using Spectre.Console;
using Spectre.Console.Rendering;

namespace Actions.Viz
{
    /// <summary>
    /// One line of a <see cref="GradientBlock"/>: the text, and the markup style to draw it in.
    /// </summary>
    /// <param name="Text">
    /// The line, already laid out. Lines are padded, never wrapped, so whatever you pass is what
    /// gets drawn - see <see cref="GradientBlock.Render"/>.
    /// </param>
    /// <param name="Style">Foreground style for the whole line, eg. 'bold #ffffff'.</param>
    public readonly record struct GradientLine(string Text, string Style);

    /// <summary>
    /// Draws a block of lines over a 45 degree colour gradient, by colouring the background of
    /// every cell it occupies.
    /// </summary>
    /// <remarks>
    /// There is no 'background' in console rendering the way there is in CSS - a background is just
    /// the colour behind each character, so the whole block has to be painted cell by cell. The
    /// runs are merged and the gradient is quantised to keep the escape sequences to a sane size.
    /// </remarks>
    public static class GradientBlock
    {
        /// <summary>
        /// How many distinct colours the gradient is allowed. Enough to look continuous, few enough
        /// that neighbouring cells usually share one and can be written as a single run.
        /// </summary>
        private const int Steps = 48;

        /// <summary>
        /// Console cells are roughly twice as tall as they are wide, so a diagonal that advances one
        /// column per row looks far steeper than 45 degrees. Advancing two columns per row looks right.
        /// </summary>
        private const int CellAspect = 2;

        /// <summary>
        /// Renders the lines over the gradient, padded out to the full width.
        /// </summary>
        /// <param name="lines">The block's contents. Anything longer than <paramref name="width"/> is cut.</param>
        /// <param name="from">Colour at the top left.</param>
        /// <param name="to">Colour at the bottom right.</param>
        /// <param name="width">Width of the block - normally the full console width.</param>
        public static IRenderable Render(IEnumerable<GradientLine> lines, Color from, Color to, int width)
        {
            var rows = lines.ToList();
            var height = Math.Max(rows.Count, 1);

            // The gradient runs corner to corner, so the furthest cell is the one at the bottom right.
            var longest = width + (height * CellAspect);

            return new Rows(rows.Select((line, y) => Paint(line, y, width, longest, from, to)));
        }

        /// <summary>
        /// Paints one line: pad it to the block width, then walk it cell by cell, emitting a markup
        /// run each time the background colour changes.
        /// </summary>
        private static IRenderable Paint(GradientLine line, int y, int width, int longest, Color from, Color to)
        {
            var text = line.Text.Length > width ? line.Text[..width] : line.Text.PadRight(width);
            var markup = new System.Text.StringBuilder();

            var runStart = 0;
            var runColour = ColourAt(0, y, longest, from, to);

            for (var x = 1; x <= text.Length; x++)
            {
                var colour = x < text.Length ? ColourAt(x, y, longest, from, to) : string.Empty;

                if (colour == runColour)
                {
                    continue;
                }

                // Colour changed (or we ran out of line) - flush what we have as one run.
                markup.Append($"[{line.Style} on {runColour}]{VizStyle.Escape(text[runStart..x])}[/]");

                runStart = x;
                runColour = colour;
            }

            return new Markup(markup.ToString());
        }

        /// <summary>
        /// The gradient colour at a cell, as a hex string ready to drop into markup.
        /// </summary>
        private static string ColourAt(int x, int y, int longest, Color from, Color to)
        {
            var distance = x + (y * CellAspect);

            // Quantised, so a run of cells shares a colour rather than each getting its own escape code.
            var step = (double)Math.Clamp(distance * Steps / longest, 0, Steps - 1) / (Steps - 1);

            return $"#{Mix(from.R, to.R, step):x2}{Mix(from.G, to.G, step):x2}{Mix(from.B, to.B, step):x2}";
        }

        /// <summary>Linear blend of one channel, <paramref name="step"/> of the way from 'from' to 'to'.</summary>
        private static int Mix(byte from, byte to, double step) => (int)Math.Round(from + ((to - from) * step));
    }
}
