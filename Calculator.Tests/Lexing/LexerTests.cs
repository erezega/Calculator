using Calculator.Lexing;

namespace Calculator.Tests.Lexing;

public class LexerTests
{
    // Token types of a line, without the trailing End token, to keep assertions short.
    private static TokenType[] Types(string line) =>
        Lexer.Tokenize(line).Select(t => t.Type).SkipLast(1).ToArray();

    [Fact]
    public void EmptyLine_ReturnsOnlyEnd()
    {
        var tokens = Lexer.Tokenize("");

        Assert.Equal([new Token(TokenType.End, "", 0)], tokens);
    }

    [Fact]
    public void WhitespaceOnly_ReturnsOnlyEnd()
    {
        var tokens = Lexer.Tokenize("  \t ");

        Assert.Equal([new Token(TokenType.End, "", 4)], tokens);
    }

    [Fact]
    public void AlwaysEndsWithEndToken()
    {
        var tokens = Lexer.Tokenize("x = 1");

        Assert.Equal(new Token(TokenType.End, "", 5), tokens[^1]);
    }

    [Theory]
    [InlineData("+", TokenType.Plus)]
    [InlineData("-", TokenType.Minus)]
    [InlineData("*", TokenType.Star)]
    [InlineData("/", TokenType.Slash)]
    [InlineData("%", TokenType.Percent)]
    [InlineData("++", TokenType.PlusPlus)]
    [InlineData("--", TokenType.MinusMinus)]
    [InlineData("=", TokenType.Assign)]
    [InlineData("+=", TokenType.PlusAssign)]
    [InlineData("-=", TokenType.MinusAssign)]
    [InlineData("*=", TokenType.StarAssign)]
    [InlineData("/=", TokenType.SlashAssign)]
    [InlineData("%=", TokenType.PercentAssign)]
    [InlineData("(", TokenType.LeftParen)]
    [InlineData(")", TokenType.RightParen)]
    public void SingleOperator(string text, TokenType expected)
    {
        var tokens = Lexer.Tokenize(text);

        Assert.Equal(new Token(expected, text, 0), tokens[0]);
        Assert.Equal(TokenType.End, tokens[1].Type);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("7", 7)]
    [InlineData("42", 42)]
    [InlineData("2147483647", int.MaxValue)]
    public void Number_HasParsedValue(string text, int expected)
    {
        var token = Lexer.Tokenize(text)[0];

        Assert.Equal(new Token(TokenType.Number, text, 0, expected), token);
    }

    [Theory]
    [InlineData("x")]
    [InlineData("count")]
    [InlineData("_tmp")]
    [InlineData("x1")]
    [InlineData("camelCase_2")]
    public void Identifier(string text)
    {
        var token = Lexer.Tokenize(text)[0];

        Assert.Equal(new Token(TokenType.Identifier, text, 0), token);
    }

    [Fact]
    public void FullStatement_TypesTextAndPositions()
    {
        var tokens = Lexer.Tokenize("x = i++ + 5");

        Assert.Equal(
        [
            new Token(TokenType.Identifier, "x", 0),
            new Token(TokenType.Assign, "=", 2),
            new Token(TokenType.Identifier, "i", 4),
            new Token(TokenType.PlusPlus, "++", 5),
            new Token(TokenType.Plus, "+", 8),
            new Token(TokenType.Number, "5", 10, 5),
            new Token(TokenType.End, "", 11),
        ], tokens);
    }

    [Fact]
    public void WhitespaceIsOptional()
    {
        Assert.Equal(Types("y = (5 + 3) * 10"), Types("y=(5+3)*10"));
    }

    [Theory]
    [InlineData("i+++j", new[] { TokenType.Identifier, TokenType.PlusPlus, TokenType.Plus, TokenType.Identifier })]
    [InlineData("i---j", new[] { TokenType.Identifier, TokenType.MinusMinus, TokenType.Minus, TokenType.Identifier })]
    [InlineData("i++++j", new[] { TokenType.Identifier, TokenType.PlusPlus, TokenType.PlusPlus, TokenType.Identifier })]
    [InlineData("i+=+j", new[] { TokenType.Identifier, TokenType.PlusAssign, TokenType.Plus, TokenType.Identifier })]
    [InlineData("i + ++j", new[] { TokenType.Identifier, TokenType.Plus, TokenType.PlusPlus, TokenType.Identifier })]
    [InlineData("--5", new[] { TokenType.MinusMinus, TokenType.Number })]
    [InlineData("- -5", new[] { TokenType.Minus, TokenType.Minus, TokenType.Number })]
    public void MaximalMunch(string line, TokenType[] expected)
    {
        Assert.Equal(expected, Types(line));
    }

    [Fact]
    public void Lexer_DoesNotCheckGrammar()
    {
        // Rejecting a wrong token order is the parser's job, not the lexer's.
        Assert.Equal(
            [TokenType.Assign, TokenType.Assign, TokenType.Number, TokenType.Identifier, TokenType.Plus],
            Types("= = 5 x +"));
    }

    [Theory]
    [InlineData("x = 1 # 2", "Unexpected character '#' at column 7")]
    [InlineData("x = a.b", "Unexpected character '.' at column 6")]
    [InlineData("x = 1 & 2", "Unexpected character '&' at column 7")]
    [InlineData("x$ = 1", "Unexpected character '$' at column 2")]
    [InlineData("x = 12abc", "Invalid number '12a' at column 5")]
    [InlineData("x = 010", "Numbers with a leading zero are not supported: '010' at column 5")]
    [InlineData("x = 00", "Numbers with a leading zero are not supported: '00' at column 5")]
    [InlineData("x = 2147483648", "Integer literal out of range: '2147483648' at column 5")]
    [InlineData("x = 99999999999999999999", "Integer literal out of range: '99999999999999999999' at column 5")]
    public void InvalidInput_Throws(string line, string expectedMessage)
    {
        var ex = Assert.Throws<CalculatorException>(() => Lexer.Tokenize(line));

        Assert.Equal(expectedMessage, ex.Message);
        Assert.Null(ex.LineNumber);
    }
}
