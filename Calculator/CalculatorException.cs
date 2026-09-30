namespace Calculator;

/// <summary>
/// Any error detected while lexing, parsing or evaluating the input
/// (syntax error, undefined variable, division by zero, ...).
/// The class is sealed (cannot be inherited): it is not designed for subclassing, and
/// <see cref="AtLine"/> always creates a <see cref="CalculatorException"/>, so a subclass would lose its type.
/// </summary>
public sealed class CalculatorException : Exception
{
    /// <summary>
    /// 1-based line number where the error occurred, or null when not known yet.
    /// The lexer, parser and evaluator work on a single line, so they throw without a line number;
    /// the component that loops over the lines adds it with <see cref="AtLine"/>.
    /// </summary>
    public int? LineNumber { get; }

    public CalculatorException(string message) : base(message)
    {
    }

    private CalculatorException(string message, int lineNumber, Exception innerException)
        : base($"Line {lineNumber}: {message}", innerException)
    {
        LineNumber = lineNumber;
    }

    /// <summary>Returns a copy of this error that also reports the given line number.</summary>
    public CalculatorException AtLine(int lineNumber) => new(Message, lineNumber, this);
}
