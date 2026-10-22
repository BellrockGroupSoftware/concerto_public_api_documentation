
using Spectre.Console;

namespace Actions.Viz
{
    /// <summary>
    /// A single 'what did this field look like before, and after' line in a diff table.
    /// </summary>
    /// <param name="Label">The field name, as shown in the left hand column.</param>
    /// <param name="Before">The formatted value prior to the change.</param>
    /// <param name="After">The formatted value after the change.</param>
    /// <param name="HasChanged">True when Before and After differ.</param>
    public readonly record struct DiffRow(string Label, string Before, string After, bool HasChanged);

    /// <summary>
    /// Rendering helpers shared by every visualization in <see cref="ConsoleViz"/>.
    ///
    /// Visualizations should describe *what* they want to show (a set of <see cref="DiffRow"/>s, say)
    /// and leave the borders, colours and column widths to this class, so that a second or third
    /// visualization automatically looks like the first one.
    /// </summary>
    public static class DiffTable
    {
        /// <summary>
        /// Builds a <see cref="DiffRow"/> by comparing two values of the same field.
        /// </summary>
        /// <param name="label">The field name to show in the table.</param>
        /// <param name="before">The value before the change.</param>
        /// <param name="after">The value after the change.</param>
        /// <param name="format">
        /// Optional formatter, for when the default (see <see cref="Format{T}"/>) isn't good enough -
        /// a currency value, for instance.
        /// </param>
        public static DiffRow Compare<T>(string label, T before, T after, Func<T, string>? format = null)
        {
            format ??= Format;

            return new DiffRow(
                Label: label,
                Before: format(before),
                After: format(after),
                HasChanged: !EqualityComparer<T>.Default.Equals(before, after));
        }

        /// <summary>
        /// The default way of turning a value into something readable - nulls become a visible
        /// placeholder rather than an empty cell, and dates get a consistent, sortable format.
        /// </summary>
        public static string Format<T>(T value) => value switch
        {
            null => "(none)",
            bool b => b ? "yes" : "no",
            DateTime d => d.ToString("yyyy-MM-dd HH:mm"),
            DateTimeOffset d => d.ToString("yyyy-MM-dd HH:mm"),
            decimal m => m.ToString("N2"),
            _ => value.ToString() ?? "(none)"
        };

        /// <summary>
        /// Renders a set of rows as a before/after table.
        /// </summary>
        /// <param name="title">Heading for the table - typically names the thing that changed.</param>
        /// <param name="rows">The fields to show.</param>
        /// <param name="changedOnly">
        /// When true (the default) unchanged fields are hidden, so the table shows the effect of an
        /// action rather than a full dump of the record.
        /// </param>
        public static void Render(string title, IEnumerable<DiffRow> rows, bool changedOnly = true)
        {
            var visible = rows.Where(r => r.HasChanged || !changedOnly).ToList();

            if (visible.Count == 0)
            {
                VizStyle.Write(VizStyle.Frame(title, VizStyle.Cell("No changes", VizStyle.Muted)));
                return;
            }

            var table = VizStyle.Table(title, "Field", "Before", string.Empty, "After");

            foreach (var row in visible)
            {
                table.AddRow(
                    VizStyle.Cell(row.Label, row.HasChanged ? VizStyle.Highlight : VizStyle.Muted),
                    VizStyle.Cell(row.Before, row.HasChanged ? VizStyle.Removed : VizStyle.Muted),
                    VizStyle.Cell(row.HasChanged ? "->" : string.Empty, VizStyle.Muted),
                    VizStyle.Cell(row.After, row.HasChanged ? VizStyle.Added : VizStyle.Muted));
            }

            VizStyle.Write(table);
        }
    }
}
