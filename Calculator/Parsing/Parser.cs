using Calculator.Helpers;
using Calculator.Lexing;

namespace Calculator.Parsing;

/// <summary>
/// Turns the tokens of one line into a syntax tree (<see cref="Statement"/>, see Ast.cs).
///
/// The idea: each method handles one group of operators. To get its operands, it calls the method
/// below it. Operators handled lower down bind tighter, so they end up deeper in the tree and are
/// calculated first.
///
/// Example, "1 + 2 * 3":
///   ParseExpression handles '+'. It asks ParseTerm for the left side and gets 1.
///   It asks ParseTerm for the right side, and ParseTerm reads all of "2 * 3".
///   Result: 1 + (2 * 3).
///
/// The methods, top to bottom (each one calls the one below it):
/// <code>
/// ParseStatement    the whole line: "x = ...", "x += ...", or just "i++" / "--i"
/// ParseExpression   +  -                  read left to right: "a - b - c" is (a - b) - c
/// ParseTerm         *  /  %               read left to right: "a / b * c" is (a / b) * c
/// ParseUnary        -a  +a  ++a  --a      can repeat: "- -5"
/// ParsePostfix      a++  a--
/// ParsePrimary      a number, a variable, or "( ... )", which starts again from ParseExpression
/// </code>
///
/// '++' and '--' only work on a variable: "i++", "(i)++" and "++(i)" are fine; "5++" and "(i + 1)++" are errors.
///
/// Formal grammar, the same rules in standard notation ('|' = or, '*' = zero or more times, '?' = optional):
/// <code>
/// line       := statement ';'?                                ('?' = optional)
/// statement  := IDENT assignOp expression | expression        (expression must be ++/-- on a variable)
/// assignOp   := '=' | '+=' | '-=' | '*=' | '/=' | '%='
/// expression := term (('+' | '-') term)*
/// term       := unary (('*' | '/' | '%') unary)*
/// unary      := ('+' | '-' | '++' | '--') unary | postfix
/// postfix    := primary ('++' | '--')*
/// primary    := NUMBER | IDENT | '(' expression ')'
/// </code>
/// </summary>
public sealed class Parser
{
    private readonly IReadOnlyList<Token> _tokens;
    private int _pos;
    private Token CurrentToken => _tokens[_pos];

    private Parser(IReadOnlyList<Token> tokens)
    {
        _tokens = tokens;
    }

    /// <summary>
    /// The parser's single public entry point: takes one line's tokens, returns that line's syntax tree (AST),
    /// and throws if any tokens are left over. One optional ';' may end the line: "x = 5;" is the same as "x = 5".
    /// </summary>
    /// <param name="tokens">Tokens of one line, ending with <see cref="TokenType.End"/> (as produced by the lexer).</param>
    public static Statement Parse(IReadOnlyList<Token> tokens)
    {
        var parser = new Parser(tokens);
        var statement = parser.ParseStatement();

        // At most one ';', and only at the very end: "x = 5;;" and "x = 1; y = 2" fail on the Expect below.
        if (parser.CurrentToken.Type == TokenType.Semicolon)
        {
            parser.TakeCurrentTokenAndIterateNext();
        }

        parser.Expect(TokenType.End, "end of line");
        return statement;
    }

    // Returns the current token and moves to the next one.
    // Never moves past the End token, so CurrentToken is always valid.
    private Token TakeCurrentTokenAndIterateNext()
    {
        var token = CurrentToken;
        if (token.Type != TokenType.End)
        {
            _pos++;
        }

        return token;
    }

    // Handles: the whole line (the root of the tree): an assignment ('=', '+=', '-=', '*=', '/=', '%=') or a standalone '++' / '--'.
    // Example: "x += 1" → assignment (x, Add, 1);  "i++" → increment statement
    // Note:    any other line ("x", "i + 1", "5 = 3") is an error; the right side of an assignment comes from ParseExpression.
    private Statement ParseStatement()
    {
        // Math.Min: on an empty line the current token is already End, so "nextToken" stays on End instead of going out of range.
        var nextToken = _tokens[Math.Min(_pos + 1, _tokens.Count - 1)];

        // An assignment starts with a name followed by an assignment operator: "x = ...", "x += ...".
        // TryGetAssignmentOperator checks that the next token is an assignment operator and maps it to its BinaryOperator:
        // '=' → null (plain assignment), '+=' → Add, '-=' → Subtract, ... (x += 1 means x = x + 1).
        if (CurrentToken.Type == TokenType.Identifier && TryGetAssignmentOperator(nextToken.Type, out var compound))
        {
            var name = TakeCurrentTokenAndIterateNext().Text; // x
            TakeCurrentTokenAndIterateNext(); // =
            return new AssignmentStatement(name, compound, ParseExpression());
        }

        // Otherwise the only valid statement is a standalone increment: "i++", "--i".
        if (ParseExpression() is IncrementExpression increment)
        {
            return new IncrementStatement(increment);
        }

        throw new CalculatorException("Invalid statement: expected an assignment (x = 1) or an increment (x++, --x)");
    }

    // Handles: '+' and '-' (the lowest precedence).
    // Example: "1 + 2 - 3" → (1 + 2) - 3
    // Note:    operands come from ParseTerm; the loop reads left to right, so "10 - 3 - 2" is (10 - 3) - 2.
    private Expression ParseExpression()
    {
        StackGuard.EnsureSufficientStack(); // each '(' comes back here, so deep nesting recurses through this method

        // "left" = everything parsed so far; it becomes the left side of the next '+' or '-'.
        // "x = 5": left is 5 and the loop never runs. "x = 5 + 3": left is 5, then (5 + 3).
        var left = ParseTerm();
        while (CurrentToken.Type is TokenType.Plus or TokenType.Minus)
        {
            var op = TakeCurrentTokenAndIterateNext().Type == TokenType.Plus ? BinaryOperator.Add : BinaryOperator.Subtract;
            left = new BinaryExpression(left, op, ParseTerm());
        }

        return left;
    }

    // Handles: '*', '/' and '%'.
    // Example: "8 / 4 * 2" → (8 / 4) * 2
    // Note:    operands come from ParseUnary, so "-2 * 3" is (-2) * 3; reads left to right like ParseExpression.
    private Expression ParseTerm()
    {
        var left = ParseUnary();
        while (CurrentToken.Type is TokenType.Star or TokenType.Slash or TokenType.Percent)
        {
            var op = TakeCurrentTokenAndIterateNext().Type switch
            {
                TokenType.Star => BinaryOperator.Multiply,
                TokenType.Slash => BinaryOperator.Divide,
                _ => BinaryOperator.Remainder,
            };
            left = new BinaryExpression(left, op, ParseUnary());
        }

        return left;
    }

    // Handles: prefix '-', '+', '++' and '--'.
    // Example: "-5" → -(5);  "++i" → ++i (prefix increment)
    // Note:    calls itself for the operand, so operators can repeat: "- -5" is -(-5). Anything else goes to ParsePostfix.
    private Expression ParseUnary()
    {
        StackGuard.EnsureSufficientStack();

        var token = CurrentToken;
        switch (token.Type)
        {
            case TokenType.Plus:
                TakeCurrentTokenAndIterateNext();
                return new UnaryExpression(UnaryOperator.Plus, ParseUnary());

            case TokenType.Minus:
                TakeCurrentTokenAndIterateNext();
                return new UnaryExpression(UnaryOperator.Negate, ParseUnary());

            case TokenType.PlusPlus or TokenType.MinusMinus:
                TakeCurrentTokenAndIterateNext();
                return MakeIncrementExpression(token, ParseUnary(), isPrefix: true);

            default:
                return ParsePostfix();
        }
    }

    // Handles: postfix '++' and '--'.
    // Example: "i++" → i++ (postfix increment);  "-i++" → -(i++), not (-i)++
    // Note:    sits below ParseUnary in the chain (ParseUnary calls it), because postfix binds tighter than prefix:
    //          in "-i++" the ++ attaches to i first, then the minus. It is a loop rather than one optional
    //          operator, so "i++++" gets the clear "must be a variable" error.
    private Expression ParsePostfix()
    {
        var operand = ParsePrimary();
        while (CurrentToken.Type is TokenType.PlusPlus or TokenType.MinusMinus)
        {
            operand = MakeIncrementExpression(TakeCurrentTokenAndIterateNext(), operand, isPrefix: false);
        }

        return operand;
    }

    // Handles: a number, a variable, or an expression in parentheses (the highest precedence).
    // Example: "42" → 42;  "x" → x;  "(1 + 2)" → 1 + 2
    // Note:    parentheses start again from ParseExpression and create no node; the tree shape holds the grouping.
    private Expression ParsePrimary()
    {
        var token = CurrentToken;
        switch (token.Type)
        {
            case TokenType.Number: // 42
                TakeCurrentTokenAndIterateNext();
                return new NumberExpression(token.Value);

            case TokenType.Identifier: // x
                TakeCurrentTokenAndIterateNext();
                return new VariableExpression(token.Text);

            case TokenType.LeftParen: // ( ... )
                TakeCurrentTokenAndIterateNext();
                var inner = ParseExpression();
                Expect(TokenType.RightParen, "')'");
                return inner; // no node for parentheses: the tree shape already holds the grouping

            default:
                throw Unexpected(token, "a number, a variable or '('");
        }
    }

    // '=' → true with compound = null; '+=' → true with compound = Add; ...; any other token → false.
    private static bool TryGetAssignmentOperator(TokenType type, out BinaryOperator? compound)
    {
        compound = type switch
        {
            TokenType.PlusAssign => BinaryOperator.Add,
            TokenType.MinusAssign => BinaryOperator.Subtract,
            TokenType.StarAssign => BinaryOperator.Multiply,
            TokenType.SlashAssign => BinaryOperator.Divide,
            TokenType.PercentAssign => BinaryOperator.Remainder,
            _ => null,
        };
        return compound is not null || type == TokenType.Assign;
    }

    // Checks that the operand is a variable (not a number or a larger expression) and creates the IncrementExpression.
    // "(i)++" is fine: parentheses produce no node, so the operand is still a VariableExpression.
    // "5++", "(i + 1)++", "i++++" and "++i++" are rejected.
    private static IncrementExpression MakeIncrementExpression(Token op, Expression operand, bool isPrefix)
    {
        // "is not VariableExpression variable" checks the type and declares "variable", which is used after the if:
        // the if throws when operand is not a variable, so after it "variable" is always set.
        if (operand is not VariableExpression variable)
        {
            throw new CalculatorException($"Operator '{op.Text}' at column {op.Position + 1} can only be applied to a variable");
        }

        var incrementOperator = op.Type == TokenType.PlusPlus ? IncrementOperator.Increment : IncrementOperator.Decrement;

        return new IncrementExpression(variable.Name, incrementOperator, isPrefix);
    }

    private Token Expect(TokenType type, string description)
    {
        if (CurrentToken.Type != type)
        {
            throw Unexpected(CurrentToken, description);
        }

        return TakeCurrentTokenAndIterateNext();
    }

    private static CalculatorException Unexpected(Token found, string expected)
    {
        var what = found.Type == TokenType.End ? "end of line" : $"'{found.Text}'";
        return new CalculatorException($"Expected {expected} but found {what} at column {found.Position + 1}");
    }
}
