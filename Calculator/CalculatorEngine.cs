using Calculator.Evaluation;
using Calculator.Formatting;
using Calculator.Lexing;
using Calculator.Parsing;

namespace Calculator;

/// <summary>
/// Runs a whole input through the pipeline and returns the output line:
/// each line goes lexer → parser → evaluator (all lines share one variable store), then the formatter prints the result.
/// This is the library's public entry point; the CLI only calls <see cref="Run"/>.
/// </summary>
public static class CalculatorEngine
{
    // Handles: all the lines of the input, in order.
    // Example: ["i = 0", "j = ++i", "x = i++ + 5", "y = (5 + 3) * 10", "i += y"] → "(i=82,j=1,x=6,y=80)"
    // Note:    stops at the first error and rethrows it with the line number ("Line 3: Division by zero").
    public static string Run(IEnumerable<string> lines)
    {
        var evaluator = new Evaluator();
        var lineNumber = 0;

        // The lines are read one at a time (lazily): with File.ReadLines, a huge file is never loaded into memory.
        foreach (var line in lines)
        {
            lineNumber++; // counts blank lines too, so the number matches the line in the file

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var tokens = Lexer.Tokenize(line);
                var statement = Parser.Parse(tokens);
                evaluator.Execute(statement);
            }
            catch (CalculatorException ex)
            {
                // The lexer, parser and evaluator only see one line, so they can't know its number; add it here.
                // Only CalculatorException is caught: any other exception would be a bug and should not be hidden.
                throw ex.AtLine(lineNumber);
            }
        }

        return Formatter.Format(evaluator.Variables);
    }
}
