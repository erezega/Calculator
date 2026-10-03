using Calculator.Evaluation;
using Calculator.Lexing;
using Calculator.Parsing;

namespace Calculator.Tests.Evaluation;

public class EvaluatorTests
{
    private static void Execute(Evaluator evaluator, string line) => evaluator.Execute(Parser.Parse(Lexer.Tokenize(line)));

    // Runs the lines one after another on the same evaluator, the way the program does.
    private static Evaluator Run(params string[] lines)
    {
        var evaluator = new Evaluator();
        foreach (var line in lines)
        {
            Execute(evaluator, line);
        }

        return evaluator;
    }

    // Calculates one expression by running "r = <expression>" and returning r.
    private static int Calculate(string expression) => Run("r = " + expression).Variables["r"];

    // Compares the whole store, ignoring order.
    private static void AssertVariables(Evaluator evaluator, params (string Name, int Value)[] expected)
    {
        var actual = evaluator.Variables.Select(v => (v.Key, v.Value)).OrderBy(v => v.Key, StringComparer.Ordinal);
        Assert.Equal(expected.OrderBy(v => v.Name, StringComparer.Ordinal), actual);
    }

    // ---------- the assignment's example ----------

    [Fact]
    public void AssignmentExample_StoreAfterEachLine()
    {
        var evaluator = new Evaluator();

        Execute(evaluator, "i = 0");
        AssertVariables(evaluator, ("i", 0));

        Execute(evaluator, "j = ++i");
        AssertVariables(evaluator, ("i", 1), ("j", 1));

        Execute(evaluator, "x = i++ + 5");
        AssertVariables(evaluator, ("i", 2), ("j", 1), ("x", 6));

        Execute(evaluator, "y = (5 + 3) * 10");
        AssertVariables(evaluator, ("i", 2), ("j", 1), ("x", 6), ("y", 80));

        Execute(evaluator, "i += y");
        AssertVariables(evaluator, ("i", 82), ("j", 1), ("x", 6), ("y", 80));
    }

    [Fact]
    public void StepThroughLine()
    {
        // x = i++ + (5 * 2) with i = 1: i++ gives 1 (i becomes 2), 5 * 2 is 10.
        AssertVariables(Run("i = 1", "x = i++ + (5 * 2)"), ("i", 2), ("x", 11));
    }

    // ---------- assignment ----------

    [Fact]
    public void Assignment_StoresAndOverwrites()
    {
        AssertVariables(Run("x = 5"), ("x", 5));
        AssertVariables(Run("x = 5", "x = 7"), ("x", 7));
        AssertVariables(Run("x = 5", "y = x"), ("x", 5), ("y", 5));
    }

    [Theory]
    [InlineData("x += 3", 13)]
    [InlineData("x -= 3", 7)]
    [InlineData("x *= 3", 30)]
    [InlineData("x /= 3", 3)]
    [InlineData("x %= 3", 1)]
    [InlineData("x += x * 2", 30)]
    public void CompoundAssignment(string line, int expected)
    {
        AssertVariables(Run("x = 10", line), ("x", expected));
    }

    [Fact]
    public void CompoundAssignment_ReadsVariableBeforeRightSide()
    {
        // x is read (1) before x++ runs, so x = 1 + 1. Reading it afterwards would give 2 + 1 = 3.
        AssertVariables(Run("x = 1", "x += x++"), ("x", 2));
        // x is read (1), then ++x makes x 2 and returns 2, so x = 1 + 2.
        AssertVariables(Run("x = 1", "x += ++x"), ("x", 3));
    }

    // ---------- arithmetic ----------

    [Theory]
    [InlineData("1 + 2", 3)]
    [InlineData("7 - 10", -3)]
    [InlineData("6 * 7", 42)]
    [InlineData("1 + 2 * 3", 7)]
    [InlineData("(1 + 2) * 3", 9)]
    [InlineData("10 - 3 - 2", 5)]
    [InlineData("8 / 4 * 2", 4)]
    [InlineData("-5", -5)]
    [InlineData("+5", 5)]
    [InlineData("- -5", 5)]
    [InlineData("-(2 + 3)", -5)]
    [InlineData("2 * -3", -6)]
    public void Arithmetic(string expression, int expected)
    {
        Assert.Equal(expected, Calculate(expression));
    }

    [Theory]
    [InlineData("7 / 2", 3)]
    [InlineData("-7 / 2", -3)]
    [InlineData("7 / -2", -3)]
    [InlineData("-7 / -2", 3)]
    [InlineData("7 % 3", 1)]
    [InlineData("-7 % 3", -1)]
    [InlineData("7 % -3", 1)]
    [InlineData("-7 % -3", -1)]
    public void IntegerDivision_TruncatesTowardZero_RemainderTakesSignOfLeft(string expression, int expected)
    {
        Assert.Equal(expected, Calculate(expression));
    }

    [Theory]
    [InlineData("2147483647 + 1", int.MinValue)]
    [InlineData("-2147483647 - 2", int.MaxValue)]
    [InlineData("2147483647 * 2", -2)]
    [InlineData("65536 * 65536", 0)]
    public void Overflow_WrapsAround(string expression, int expected)
    {
        Assert.Equal(expected, Calculate(expression));
    }

    [Fact]
    public void MinValueEdgeCases_WrapAroundInsteadOfThrowing()
    {
        // m = int.MinValue (the literal 2147483648 is out of range, so build it with a subtraction).
        var evaluator = Run(
            "m = -2147483647 - 1",
            "negated = -m",
            "divided = m / -1",
            "remainder = m % -1",
            "below = m - 1");

        AssertVariables(evaluator,
            ("m", int.MinValue),
            ("negated", int.MinValue),
            ("divided", int.MinValue),
            ("remainder", 0),
            ("below", int.MaxValue));
    }

    [Fact]
    public void Increment_WrapsAround()
    {
        AssertVariables(Run("i = 2147483647", "i++"), ("i", int.MinValue));
        AssertVariables(Run("i = -2147483647 - 1", "i--"), ("i", int.MaxValue));
    }

    // ---------- ++ and -- ----------

    [Theory]
    [InlineData("j = ++i", 6, 6)]
    [InlineData("j = i++", 6, 5)]
    [InlineData("j = --i", 4, 4)]
    [InlineData("j = i--", 4, 5)]
    public void PrefixReturnsNewValue_PostfixReturnsOldValue(string line, int expectedI, int expectedJ)
    {
        AssertVariables(Run("i = 5", line), ("i", expectedI), ("j", expectedJ));
    }

    [Theory]
    [InlineData("i++", 6)]
    [InlineData("++i", 6)]
    [InlineData("i--", 4)]
    [InlineData("--i", 4)]
    [InlineData("(i)++", 6)]
    public void StandaloneIncrement(string line, int expectedI)
    {
        AssertVariables(Run("i = 5", line), ("i", expectedI));
    }

    [Fact]
    public void LeftOperandIsCalculatedFirst()
    {
        // i++ runs first: its value is 1 and i becomes 2, then the right side reads 2.
        AssertVariables(Run("i = 1", "x = i++ + i"), ("i", 2), ("x", 3));
        // i is read first (1), then i++ gives 1 and makes i 2.
        AssertVariables(Run("i = 1", "x = i + i++"), ("i", 2), ("x", 2));
    }

    [Fact]
    public void AssigningPostfixToItself_LeavesValueUnchanged()
    {
        // i++ gives 5 and makes i 6, then the assignment writes the 5 back.
        AssertVariables(Run("i = 5", "i = i++"), ("i", 5));
    }

    // ---------- errors ----------

    [Theory]
    [InlineData("x = y", "Undefined variable 'y'")]
    [InlineData("x = x + 1", "Undefined variable 'x'")]
    [InlineData("x += 1", "Undefined variable 'x'")]
    [InlineData("x++", "Undefined variable 'x'")]
    [InlineData("x = --x", "Undefined variable 'x'")]
    [InlineData("x = 1 / 0", "Division by zero")]
    [InlineData("x = 1 % 0", "Division by zero")]
    [InlineData("x = 5 / (2 - 2)", "Division by zero")]
    public void InvalidInput_Throws(string line, string expectedMessage)
    {
        var ex = Assert.Throws<CalculatorException>(() => Run(line));

        Assert.Equal(expectedMessage, ex.Message);
        Assert.Null(ex.LineNumber);
    }

    [Fact]
    public void CompoundDivisionByZero_Throws()
    {
        var ex = Assert.Throws<CalculatorException>(() => Run("x = 10", "x /= 0"));
        Assert.Equal("Division by zero", ex.Message);
    }

    [Fact]
    public void FailedAssignment_DoesNotChangeTheVariable()
    {
        var evaluator = Run("x = 1");

        Assert.Throws<CalculatorException>(() => Execute(evaluator, "x = 5 / 0"));

        AssertVariables(evaluator, ("x", 1));
    }
}
