using System.Runtime.CompilerServices;

namespace Calculator.Helpers;

/// <summary>
/// Protects the recursive methods (the parser and the evaluator) from running out of stack.
/// pathological line (thousands of nested parentheses, or a very long "1 + 1 + 1 + ...") into a normal error.
/// </summary>
internal static class StackGuard
{
    // Call at the start of every method that calls itself, directly or through other methods.
    public static void EnsureSufficientStack()
    {
        // Returns false when the remaining stack is close to the limit.
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            throw new CalculatorException("Expression is too deeply nested");
        }
    }
}
