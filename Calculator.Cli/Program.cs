using Calculator;

// Command-line entry point. No calculator logic here: read the input, call the engine, print the result.
//
// Usage:
//   Calculator.Cli <file>     read the lines from a file
//   Calculator.Cli            read the lines from standard input (a pipe, or typed in; end with Ctrl+D)
//
// Exit codes: 0 = success, 1 = calculation error (syntax, undefined variable, division by zero),
//             2 = usage or file error.

const int Success = 0;
const int CalculationError = 1;
const int UsageOrFileError = 2;

if (args.Length > 1)
{
    Console.Error.WriteLine("Usage: Calculator.Cli [file]   (without a file, reads from standard input)");
    return UsageOrFileError;
}

try
{
    // Both sources are read lazily, one line at a time, so a huge input is never loaded into memory at once.
    var lines = args.Length == 1 ? File.ReadLines(args[0]) : ReadStandardInput();

    Console.WriteLine(CalculatorEngine.Run(lines));
    return Success;
}
catch (CalculatorException ex)
{
    // Already includes the line number: "Line 3: Division by zero".
    Console.Error.WriteLine($"Error: {ex.Message}");
    return CalculationError;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
{
    // FileNotFoundException and DirectoryNotFoundException are IOExceptions.
    var source = args.Length == 1 ? $"'{args[0]}'" : "standard input";
    Console.Error.WriteLine($"Error: cannot read {source}: {ex.Message}");
    return UsageOrFileError;
}

static IEnumerable<string> ReadStandardInput()
{
    // When a person types the input (not a pipe or a redirected file), explain how to finish,
    // otherwise the program looks stuck. Written to stderr so it never mixes with the result.
    if (!Console.IsInputRedirected)
    {
        Console.Error.WriteLine("Enter one assignment per line. Finish with Ctrl+D (Ctrl+Z then Enter on Windows).");
    }

    while (Console.In.ReadLine() is { } line)
    {
        yield return line;
    }
}
