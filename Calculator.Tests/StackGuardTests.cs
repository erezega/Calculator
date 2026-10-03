using Calculator.Evaluation;
using Calculator.Lexing;
using Calculator.Parsing;

namespace Calculator.Tests;

// Pathological lines must give a normal error instead of crashing the process with a stack overflow.
public class StackGuardTests
{
    private const string TooDeep = "Expression is too deeply nested";

    private static Statement Parse(string line) => Parser.Parse(Lexer.Tokenize(line));

    private static int Run(string line)
    {
        var evaluator = new Evaluator();
        evaluator.Execute(Parse(line));
        return evaluator.Variables["x"];
    }

    [Fact]
    public void DeeplyNestedParentheses_Throws()
    {
        var line = "x = " + new string('(', 100_000) + "1" + new string(')', 100_000);

        var ex = Assert.Throws<CalculatorException>(() => Parse(line));
        Assert.Equal(TooDeep, ex.Message);
    }

    [Fact]
    public void LongUnaryChain_Throws()
    {
        // "- - - ... 1" (with spaces; "--" would be the decrement operator)
        var line = "x = " + string.Concat(Enumerable.Repeat("- ", 100_000)) + "1";

        var ex = Assert.Throws<CalculatorException>(() => Parse(line));
        Assert.Equal(TooDeep, ex.Message);
    }

    [Fact]
    public void VeryLongSum_ParsesButEvaluationThrows()
    {
        // The parser builds "1 + 1 + ..." with a loop (no recursion), but the tree is 200,000 levels deep,
        // and the evaluator walks it recursively.
        var line = "x = 1" + string.Concat(Enumerable.Repeat(" + 1", 200_000));
        var statement = Parse(line);

        var ex = Assert.Throws<CalculatorException>(() => new Evaluator().Execute(statement));
        Assert.Equal(TooDeep, ex.Message);
    }

    [Fact]
    public void ReasonableNesting_StillWorks()
    {
        Assert.Equal(1, Run("x = " + new string('(', 100) + "1" + new string(')', 100)));
        Assert.Equal(1001, Run("x = 1" + string.Concat(Enumerable.Repeat(" + 1", 1000))));
        Assert.Equal(1, Run("x = " + string.Concat(Enumerable.Repeat("- ", 100)) + "1"));
    }
}
