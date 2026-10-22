using Spectre.Console;
using Spectre.Console.Rendering;

namespace Actions.Viz
{
    /// <summary>
    /// The shared look of every console visualization - borders, colours and text escaping.
    ///
    /// Visualizations should go through here rather than newing up their own <see cref="Table"/>,
    /// so that a visualization added next week sits happily next to the ones added today.
    /// </summary>
    public static class VizStyle
    {
        /// <summary>Colour used for a value that has been removed, or that we failed to supply.</summary>
        public const string Removed = "red";

        /// <summary>Colour used for a value that has been added or newly supplied.</summary>
        public const string Added = "green";

        /// <summary>Style for anything present but not interesting - unchanged or skipped.</summary>
        public const string Muted = "dim";

        /// <summary>Style for the thing the reader is meant to notice.</summary>
        public const string Highlight = "bold";

        /// <summary>
        /// Style for a table or frame heading. Deliberately loud - these sit in amongst ordinary
        /// log output, and a dim title is a title nobody reads.
        /// </summary>
        public const string Title = "bold aqua";

        /// <summary>
        /// Style for a prompt the run is waiting on. Reversed out rather than merely coloured, so
        /// that it reads as a stop rather than as one more line of narration.
        /// </summary>
        public const string Prompt = "black on aqua";

        /// <summary>
        /// A table with the house style applied, and the given headers.
        /// </summary>
        public static Table Table(string title, params string[] headers)
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey)
                .Title(Escape(title), Style.Parse(Title));

            foreach (var header in headers)
            {
                table.AddColumn(new TableColumn($"[{Highlight}]{Escape(header)}[/]"));
            }

            return table;
        }

        /// <summary>
        /// A framed box around a renderable - used when the content draws itself (JSON, say) and
        /// only needs a titled border around it.
        /// </summary>
        public static Panel Frame(string title, IRenderable content) =>
            new Panel(content)
                .Header($"[{Title}] {Escape(title)} [/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Grey);

        /// <summary>
        /// Wraps text in a style, ready to drop into a table cell.
        /// </summary>
        /// <remarks>
        /// Always build cells through here. Values come from an external system, and a description
        /// containing '[' would otherwise be read as Spectre markup and throw at render time.
        /// </remarks>
        public static IRenderable Cell(string text, string style) => new Markup($"[{style}]{Escape(text)}[/]");

        /// <summary>
        /// Writes a visualization to the console, preceded by a blank line.
        ///
        /// Go through here rather than calling AnsiConsole directly - the spacing is what stops a
        /// table from being swallowed by the log lines either side of it.
        /// </summary>
        public static void Write(IRenderable renderable)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(renderable);
        }

        /// <summary>
        /// Makes arbitrary text safe to use inside Spectre markup.
        /// </summary>
        public static string Escape(string text) => Markup.Escape(text);
    }
}
