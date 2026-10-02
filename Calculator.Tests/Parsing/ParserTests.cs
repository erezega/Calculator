using Calculator.Lexing;
using Calculator.Parsing;

namespace Calculator.Tests.Parsing;

public class ParserTests
{
    private static Statement Parse(string line) => Parser.Parse(Lexer.Tokenize(line));

    // Parses "r = <expression>" and returns just the expression tree.
    private static Expression ParseExpression(string expression) => ((AssignmentStatement)Parse("r = " + expression)).Value;

    // Short helpers to build expected trees.
    private static NumberExpression Num(int value) => new(value);
    private static VariableExpression Var(string name) => new(name);
    private static BinaryExpression Bin(Expression left, BinaryOperator op, Expression right) => new(left, op, right);
    private static UnaryExpression Neg(Expression operand) => new(UnaryOperator.Negate, operand);
    private static IncrementExpression PreInc(string name) => new(name, IncrementOperator.Increment, IsPrefix: true);
    private static IncrementExpression PostInc(string name) => new(name, IncrementOperator.Increment, IsPrefix: false);

    // ---------- statements ----------

    [Fact]
    public void SimpleAssignment()
    {
        Assert.Equal(new AssignmentStatement("x", null, Num(5)), Parse("x = 5"));
    }

    [Theory]
    [InlineData("x += 1", BinaryOperator.Add)]
    [InlineData("x -= 1", BinaryOperator.Subtract)]
    [InlineData("x *= 1", BinaryOperator.Multiply)]
    [InlineData("x /= 1", BinaryOperator.Divide)]
    [InlineData("x %= 1", BinaryOperator.Remainder)]
    public void CompoundAssignment(string line, BinaryOperator expected)
    {
        Assert.Equal(new AssignmentStatement("x", expected, Num(1)), Parse(line));
    }

    [Theory]
    [InlineData("i++", IncrementOperator.Increment, false)]
    [InlineData("i--", IncrementOperator.Decrement, false)]
    [InlineData("++i", IncrementOperator.Increment, true)]
    [InlineData("--i", IncrementOperator.Decrement, true)]
    [InlineData("(i)++", IncrementOperator.Increment, false)]
    public void StandaloneIncrement(string line, IncrementOperator op, bool isPrefix)
    {
        Assert.Equal(new IncrementStatement(new IncrementExpression("i", op, isPrefix)), Parse(line));
    }

    [Fact]
    public void AssignmentExample_FromTheAssignment()
    {
        Assert.Equal(new AssignmentStatement("i", null, Num(0)), Parse("i = 0"));
        Assert.Equal(new AssignmentStatement("j", null, PreInc("i")), Parse("j = ++i"));
        Assert.Equal(new AssignmentStatement("x", null, Bin(PostInc("i"), BinaryOperator.Add, Num(5))), Parse("x = i++ + 5"));
        Assert.Equal(
            new AssignmentStatement("y", null, Bin(Bin(Num(5), BinaryOperator.Add, Num(3)), BinaryOperator.Multiply, Num(10))),
            Parse("y = (5 + 3) * 10"));
        Assert.Equal(new AssignmentStatement("i", BinaryOperator.Add, Var("y")), Parse("i += y"));
    }

    [Fact]
    public void PostfixIncrementPlusParenthesizedProduct()
    {
        // The line from the parser step-through: x = (i++) + (5 * 2)
        //
        // AssignmentStatement (x)
        // └── BinaryExpression (+)
        //     ├── IncrementExpression (i, ++, postfix)
        //     └── BinaryExpression (*)
        //         ├── NumberExpression 5
        //         └── NumberExpression 2
        var expected = new AssignmentStatement(
            "x",
            null,
            Bin(PostInc("i"), BinaryOperator.Add, Bin(Num(5), BinaryOperator.Multiply, Num(2))));

        Assert.Equal(expected, Parse("x = i++ + (5 * 2)"));
    }

    // ---------- precedence and associativity ----------

    [Fact]
    public void MultiplicationBindsTighterThanAddition()
    {
        // 1 + (2 * 3)
        Assert.Equal(
            Bin(Num(1), BinaryOperator.Add, Bin(Num(2), BinaryOperator.Multiply, Num(3))),
            ParseExpression("1 + 2 * 3"));
    }

    [Fact]
    public void ParenthesesOverridePrecedence()
    {
        Assert.Equal(
            Bin(Bin(Num(1), BinaryOperator.Add, Num(2)), BinaryOperator.Multiply, Num(3)),
            ParseExpression("(1 + 2) * 3"));
    }

    [Fact]
    public void SubtractionIsLeftAssociative()
    {
        // (10 - 3) - 2, not 10 - (3 - 2)
        Assert.Equal(
            Bin(Bin(Num(10), BinaryOperator.Subtract, Num(3)), BinaryOperator.Subtract, Num(2)),
            ParseExpression("10 - 3 - 2"));
    }

    [Fact]
    public void MultiplicativeOperatorsAreLeftAssociative()
    {
        // ((8 / 4) * 3) % 5
        Assert.Equal(
            Bin(Bin(Bin(Num(8), BinaryOperator.Divide, Num(4)), BinaryOperator.Multiply, Num(3)), BinaryOperator.Remainder, Num(5)),
            ParseExpression("8 / 4 * 3 % 5"));
    }

    [Fact]
    public void NestedParentheses()
    {
        Assert.Equal(Num(1), ParseExpression("((1))"));
        Assert.Equal(
            Bin(Bin(Num(1), BinaryOperator.Add, Num(2)), BinaryOperator.Multiply, Bin(Num(3), BinaryOperator.Subtract, Num(4))),
            ParseExpression("((1 + 2) * (3 - 4))"));
    }

    // ---------- unary, prefix and postfix ----------

    [Fact]
    public void UnaryOperators()
    {
        Assert.Equal(Neg(Num(5)), ParseExpression("-5"));
        Assert.Equal(new UnaryExpression(UnaryOperator.Plus, Num(5)), ParseExpression("+5"));
        Assert.Equal(Neg(Neg(Num(5))), ParseExpression("- -5"));
        Assert.Equal(Neg(Neg(Num(5))), ParseExpression("-(-5)"));
    }

    [Fact]
    public void UnaryMinusBindsTighterThanMultiplication()
    {
        Assert.Equal(Bin(Neg(Num(2)), BinaryOperator.Multiply, Num(3)), ParseExpression("-2 * 3"));
        Assert.Equal(Bin(Num(2), BinaryOperator.Multiply, Neg(Num(3))), ParseExpression("2 * -3"));
    }

    [Fact]
    public void PrefixAndPostfix()
    {
        Assert.Equal(PreInc("i"), ParseExpression("++i"));
        Assert.Equal(PostInc("i"), ParseExpression("i++"));
        Assert.Equal(new IncrementExpression("i", IncrementOperator.Decrement, IsPrefix: true), ParseExpression("--i"));
        Assert.Equal(new IncrementExpression("i", IncrementOperator.Decrement, IsPrefix: false), ParseExpression("i--"));
        Assert.Equal(PreInc("i"), ParseExpression("++(i)"));
        Assert.Equal(Neg(PostInc("i")), ParseExpression("-i++"));
    }

    [Fact]
    public void MaximalMunchResult_IsParsedAsPostfixThenAdd()
    {
        // The lexer turns "i+++j" into i ++ + j.
        Assert.Equal(Bin(PostInc("i"), BinaryOperator.Add, Var("j")), ParseExpression("i+++j"));
        Assert.Equal(Bin(Var("i"), BinaryOperator.Add, PreInc("j")), ParseExpression("i + ++j"));
    }

    // ---------- errors ----------

    [Theory]
    [InlineData("x = ", "Expected a number, a variable or '(' but found end of line at column 5")]
    [InlineData("x = 1 +", "Expected a number, a variable or '(' but found end of line at column 8")]
    [InlineData("x = )", "Expected a number, a variable or '(' but found ')' at column 5")]
    [InlineData("x = * 2", "Expected a number, a variable or '(' but found '*' at column 5")]
    [InlineData("= 5", "Expected a number, a variable or '(' but found '=' at column 1")]
    [InlineData("x = (1 + 2", "Expected ')' but found end of line at column 11")]
    [InlineData("x = 1 2", "Expected end of line but found '2' at column 7")]
    [InlineData("x = (1))", "Expected end of line but found ')' at column 8")]
    [InlineData("x = y = 5", "Expected end of line but found '=' at column 7")]
    [InlineData("x = ++5", "Operator '++' at column 5 can only be applied to a variable")]
    [InlineData("x = --5", "Operator '--' at column 5 can only be applied to a variable")]
    [InlineData("x = 5++", "Operator '++' at column 6 can only be applied to a variable")]
    [InlineData("x = (i + 1)++", "Operator '++' at column 12 can only be applied to a variable")]
    [InlineData("x = i++++", "Operator '++' at column 8 can only be applied to a variable")]
    [InlineData("x = ++i++", "Operator '++' at column 5 can only be applied to a variable")]
    [InlineData("x = ++-i", "Operator '++' at column 5 can only be applied to a variable")]
    [InlineData("x", "Invalid statement: expected an assignment (x = 1) or an increment (x++, --x)")]
    [InlineData("5", "Invalid statement: expected an assignment (x = 1) or an increment (x++, --x)")]
    [InlineData("i + 1", "Invalid statement: expected an assignment (x = 1) or an increment (x++, --x)")]
    [InlineData("-i++", "Invalid statement: expected an assignment (x = 1) or an increment (x++, --x)")]
    [InlineData("5 = 3", "Invalid statement: expected an assignment (x = 1) or an increment (x++, --x)")]
    public void InvalidInput_Throws(string line, string expectedMessage)
    {
        var ex = Assert.Throws<CalculatorException>(() => Parse(line));

        Assert.Equal(expectedMessage, ex.Message);
        Assert.Null(ex.LineNumber);
    }
}
