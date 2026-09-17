using System.Text;

namespace TrafficLens.App.Services;

/// <summary>
/// Builds the plain-text diagnostics block surfaced on the About page and sent
/// to the clipboard with the "Copy diagnostics" action. Pure and testable.
/// </summary>
public static class DiagnosticsInfo
{
    public static string Build(IEnumerable<(string Label, string Value)> entries)
    {
        var builder = new StringBuilder();
        foreach (var (label, value) in entries)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(label).Append(": ").Append(value);
        }

        return builder.ToString();
    }
}