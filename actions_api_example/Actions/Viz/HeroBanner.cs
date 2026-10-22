using Spectre.Console;

namespace Actions.Viz
{
    /// <summary>
    /// The opening splash an app shows before it starts logging at you - the console's answer to a
    /// hero section: full width, its own colour, and the only place here that sets out to be pretty.
    ///
    /// As with <see cref="DiffTable"/> and <see cref="TransitionFlow"/>, callers in
    /// <see cref="ConsoleViz"/> say what goes in it; the colours, wrapping and spacing live here.
    /// </summary>
    public static class HeroBanner
    {
        /// <summary>Colour at the top left of the hero.</summary>
        private static readonly Color GradientFrom = new(0x50, 0x21, 0x91);

        /// <summary>Colour at the bottom right of the hero.</summary>
        private static readonly Color GradientTo = new(0x2B, 0x27, 0x8A);

        /// <summary>The title. Brightest thing on the screen, as a title should be.</summary>
        private const string TitleStyle = "bold #ffffff";

        /// <summary>The line under the title - present, but not competing with it.</summary>
        private const string StraplineStyle = "#d9cdfa";

        /// <summary>Running text. Light enough to read comfortably against the gradient.</summary>
        private const string ProseStyle = "#ece6ff";

        /// <summary>The 'what you'll see' points, picked out slightly from the prose.</summary>
        private const string PointStyle = "#ffffff";

        /// <summary>Blank columns kept clear at either edge, so nothing sits against the screen edge.</summary>
        private const int Margin = 1;

        /// <summary>The widest running text is allowed to get, measure being what makes prose readable.</summary>
        private const int MaximumProseWidth = 88;

        /// <summary>Indent of a wrapped bullet, so it lines up under its own text and not the dash.</summary>
        private const string BulletHangingIndent = "   ";

        /// <summary>
        /// Renders the banner.
        /// </summary>
        /// <param name="title">The headline, in bright white.</param>
        /// <param name="strapline">A single line under the title.</param>
        /// <param name="intro">A paragraph or two of scene setting. One entry per paragraph.</param>
        /// <param name="points">Optional bullets under the intro - the 'what you'll see' list.</param>
        public static void Render(
            string title,
            string? strapline,
            IEnumerable<string> intro,
            IEnumerable<string> points)
        {
            var width = AnsiConsole.Profile.Width;
            var textWidth = Math.Min(width - (Margin * 2), MaximumProseWidth);

            var lines = new List<GradientLine> { Blank() };

            lines.Add(Line(string.Empty.PadLeft(title.Length, '-'), TitleStyle));
            lines.Add(Line(title, TitleStyle));
            lines.Add(Line(string.Empty.PadLeft(title.Length, '-'), TitleStyle));
            if (!string.IsNullOrWhiteSpace(strapline))
            {
                lines.Add(Line(strapline, StraplineStyle));
            }
            lines.Add(Blank());

            foreach (var paragraph in intro)
            {
                lines.AddRange(Wrap(paragraph, textWidth).Select(l => Line(l, ProseStyle)));
                lines.Add(Blank());
            }

            foreach (var point in points)
            {
                // Bulleted so the eye can skip the prose and still come away knowing what's coming.
                // Wrapped by hand because the hanging indent has to survive into the painted line.
                var wrapped = Wrap(point, textWidth - BulletHangingIndent.Length).ToList();

                lines.Add(Line($" - {wrapped[0]}", PointStyle));
                lines.AddRange(wrapped.Skip(1).Select(l => Line(BulletHangingIndent + l, PointStyle)));
            }

            lines.Add(Blank());

            VizStyle.Write(GradientBlock.Render(lines, GradientFrom, GradientTo, width));
        }

        /// <summary>
        /// A line of the hero, indented by the margin so the gradient shows either side of it.
        /// </summary>
        private static GradientLine Line(string text, string style) =>
            new(new string(' ', Margin) + text, style);

        /// <summary>An empty line - still painted, so the gradient runs through it.</summary>
        private static GradientLine Blank() => new(string.Empty, ProseStyle);

        /// <summary>
        /// Greedy word wrap. The hero paints its own lines cell by cell, so it has to know where
        /// they break rather than leaving that to the renderer.
        /// </summary>
        private static IEnumerable<string> Wrap(string text, int width)
        {
            var line = new System.Text.StringBuilder();

            foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Length > 0 && line.Length + 1 + word.Length > width)
                {
                    yield return line.ToString();
                    line.Clear();
                }

                line.Append(line.Length > 0 ? " " : string.Empty).Append(word);
            }

            yield return line.ToString();
        }
    }
}
