using System.Globalization;

namespace Calculator.Formatting;

/// <summary>
/// Turns the final variable store into the output line: { i: 82, j: 1, x: 6, y: 80 } → "(i=82,j=1,x=6,y=80)".
/// </summary>
public static class Formatter
{
    // Handles: the whole store, after the last line.
    // Example: { y: 80, i: 82 } → "(i=82,y=80)";  { } → "()"
    // Note:    the output is the same on every machine; it does not depend on the regional settings (see below).
    public static string Format(IReadOnlyDictionary<string, int> variables)
    {
        // Ordinal = compare character codes: "A" < "B" < "_x" < "a" < "b", and "x10" < "x2".
        // A culture-aware sort would give a different order on different machines.
        var pairs = variables
            .OrderBy(variable => variable.Key, StringComparer.Ordinal)
            .Select(variable => variable.Key + "=" + variable.Value.ToString(CultureInfo.InvariantCulture));

        // InvariantCulture: some regional settings print -9 with a different minus sign character (U+2212)
        // or add an invisible direction mark; the invariant culture always prints a plain "-9".
        return "(" + string.Join(",", pairs) + ")";
    }
}
