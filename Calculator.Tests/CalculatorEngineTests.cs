namespace Calculator.Tests;

// End-to-end tests: text lines in, output line (or an error with its line number) out.
public class CalculatorEngineTests
{
    [Fact]
    public void AssignmentExample()
    {
        string[] lines =
        [
            "i = 0",
            "j = ++i",
            "x = i++ + 5",
            "y = (5 + 3) * 10",
            "i += y",
        ];

        Assert.Equal("(i=82,j=1,x=6,y=80)", CalculatorEngine.Run(lines));
    }

    [Fact]
    public void EmptyInput_PrintsEmptyParentheses()
    {
        Assert.Equal("()", CalculatorEngine.Run([]));
    }

    [Fact]
    public void BlankAndWhitespaceLines_AreSkipped()
    {
        string[] lines = ["", "i = 1", "   ", "\t", "j = i + 1", ""];

        Assert.Equal("(i=1,j=2)", CalculatorEngine.Run(lines));
    }

    [Fact]
    public void WindowsLineEnding_IsIgnored()
    {
        // A '\r' left at the end of a line is whitespace for the lexer.
        Assert.Equal("(x=5)", CalculatorEngine.Run(["x = 5\r"]));
    }

    [Fact]
    public void ByteOrderMark_AtTheStartOfTheInput_IsIgnored()
    {
        // A file saved with a BOM starts with U+FEFF; standard input does not remove it.
        Assert.Equal("(i=1,j=2)", CalculatorEngine.Run(["﻿i = 1", "j = i + 1"]));
    }

    [Fact]
    public void ByteOrderMark_InsideTheInput_IsAnError()
    {
        // Only the very start of the input can carry a BOM.
        var ex = Assert.Throws<CalculatorException>(() => CalculatorEngine.Run(["i = 1", "﻿j = 2"]));

        Assert.Equal("Line 2: Unexpected character U+FEFF at column 1", ex.Message);
    }

    [Fact]
    public void AssignmentExample_WithSemicolons()
    {
        string[] lines = ["i = 0;", "j = ++i;", "x = i++ + 5;", "y = (5 + 3) * 10;", "i += y;"];

        Assert.Equal("(i=82,j=1,x=6,y=80)", CalculatorEngine.Run(lines));
    }

    [Fact]
    public void VariablesAreSharedBetweenLines()
    {
        Assert.Equal("(i=2,x=11)", CalculatorEngine.Run(["i = 1", "x = i++ + (5 * 2)"]));
    }

    // ---------- errors get the line number ----------

    [Theory]
    [InlineData("x = 1 # 2", "Line 2: Unexpected character '#' at column 7")]                           // lexer
    [InlineData("x = (1 + 2", "Line 2: Expected ')' but found end of line at column 11")]              // parser
    [InlineData("x = i / 0", "Line 2: Division by zero")]                                             // evaluator
    [InlineData("x = y", "Line 2: Undefined variable 'y'")]                                           // evaluator
    public void ErrorFromAnyStage_HasLineNumber(string secondLine, string expectedMessage)
    {
        var ex = Assert.Throws<CalculatorException>(() => CalculatorEngine.Run(["i = 1", secondLine]));

        Assert.Equal(expectedMessage, ex.Message);
        Assert.Equal(2, ex.LineNumber);
    }

    [Fact]
    public void LineNumber_CountsBlankLines()
    {
        // The error is on the 4th line of the file, even though only 2 lines contain code.
        var ex = Assert.Throws<CalculatorException>(() => CalculatorEngine.Run(["i = 0", "", "   ", "x = y"]));

        Assert.Equal("Line 4: Undefined variable 'y'", ex.Message);
        Assert.Equal(4, ex.LineNumber);
    }

    [Fact]
    public void Error_KeepsTheOriginalErrorAsInnerException()
    {
        var ex = Assert.Throws<CalculatorException>(() => CalculatorEngine.Run(["x = 1 / 0"]));

        var inner = Assert.IsType<CalculatorException>(ex.InnerException);
        Assert.Equal("Division by zero", inner.Message);
        Assert.Null(inner.LineNumber);
    }

    [Fact]
    public void StopsAtFirstError_AndDoesNotReadTheRestOfTheInput()
    {
        // The input is read lazily: if the engine asked for a line after the error, this would throw instead.
        static IEnumerable<string> Lines()
        {
            yield return "x = 1";
            yield return "y = x / 0";
            throw new InvalidOperationException("The engine read past the first error");
        }

        var ex = Assert.Throws<CalculatorException>(() => CalculatorEngine.Run(Lines()));
        Assert.Equal("Line 2: Division by zero", ex.Message);
    }

    [Fact]
    public void DeepNestingError_HasLineNumber()
    {
        var deep = "x = " + new string('(', 100_000) + "1" + new string(')', 100_000);

        var ex = Assert.Throws<CalculatorException>(() => CalculatorEngine.Run(["i = 1", deep]));

        Assert.Equal("Line 2: Expression is too deeply nested", ex.Message);
    }
}
