using System.Globalization;

namespace Calculator.Lexing;

/// <summary>
/// Converts a single line of source text into a list of tokens, ending with an <see cref="TokenType.End"/> token.
/// Single pass over the characters, O(n) in the line length.
/// Operators are matched with maximal munch (longest match first): "i+++j" is i ++ + j.
/// </summary>
public static class Lexer
{
    public static IReadOnlyList<Token> Tokenize(string line)
    {
        var tokens = new List<Token>();
        var pos = 0;

        while (pos < line.Length)
        {
            var c = line[pos];

            // ' ', '\t': skipped, not a token
            if (char.IsWhiteSpace(c))
            {
                pos++;
            }
            // '4' in "42": reads all digits → Number "42" (Value 42)
            else if (char.IsAsciiDigit(c))
            {
                tokens.Add(ReadNumber(line, ref pos));
            }
            // 'c' in "count1", '_' in "_tmp": reads letters, digits and '_' → Identifier "count1"
            // A digit can't start one, so "1x" is not an identifier.
            else if (IsIdentifierStart(c))
            {
                tokens.Add(ReadIdentifier(line, ref pos));
            }
            // '+' in "++", '=', '(': reads one or two characters → PlusPlus, Assign, LeftParen;
            // anything else ('#', '.') throws
            else
            {
                tokens.Add(ReadOperator(line, ref pos));
            }
        }

        tokens.Add(new Token(TokenType.End, "", line.Length));
        return tokens;
    }

    private static Token ReadNumber(string line, ref int pos)
    {
        var start = pos;
        while (pos < line.Length && char.IsAsciiDigit(line[pos]))
        {
            pos++;
        }

        // "12abc" is not a valid token; don't split it into 12 and abc.
        if (pos < line.Length && IsIdentifierPart(line[pos]))
        {
            throw new CalculatorException($"Invalid number '{line[start..(pos + 1)]}' at column {start + 1}");
        }

        var digits = line[start..pos];

        // A leading zero conventionally means octal (010 == 8). Octal is not supported,
        // so reject it instead of silently treating it as decimal 10.
        if (digits.Length > 1 && digits[0] == '0')
        {
            throw new CalculatorException($"Numbers with a leading zero are not supported: '{digits}' at column {start + 1}");
        }

        // Convert the digits to an int. TryParse returns false (instead of throwing) when the value is above int.MaxValue. Example: greater than 2147483648
        // NumberStyles.None accepts only plain digits (no sign, spaces or thousands separators). Example: " 42 " or "1,000" or "-42" () - only uses as safety net in this solution
        // InvariantCulture makes the result independent of the machine's regional settings. Example: "1.000" '.' isn't a separator in en-US - only uses as safety net in this solution
        if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
        {
            throw new CalculatorException($"Integer literal out of range: '{digits}' at column {start + 1}");
        }

        return new Token(TokenType.Number, digits, start, value);
    }

    private static Token ReadIdentifier(string line, ref int pos)
    {
        var start = pos;
        while (pos < line.Length && IsIdentifierPart(line[pos]))
        {
            pos++;
        }

        return new Token(TokenType.Identifier, line[start..pos], start);
    }

    private static Token ReadOperator(string line, ref int pos)
    {
        var start = pos;
        var c = line[pos];
        // Look ahead one character to detect two-character operators ("++", "+=").
        // At the end of the line there is no next character, so use '\0' (matches no operator) instead of reading past the end.
        var next = pos + 1 < line.Length ? line[pos + 1] : '\0';

        // Two-character operators are checked before their one-character prefix (maximal munch).
        var (type, length) = c switch
        {
            '+' when next == '+' => (TokenType.PlusPlus, 2),
            '+' when next == '=' => (TokenType.PlusAssign, 2),
            '+' => (TokenType.Plus, 1),

            '-' when next == '-' => (TokenType.MinusMinus, 2),
            '-' when next == '=' => (TokenType.MinusAssign, 2),
            '-' => (TokenType.Minus, 1),

            '*' when next == '=' => (TokenType.StarAssign, 2),
            '*' => (TokenType.Star, 1),

            '/' when next == '=' => (TokenType.SlashAssign, 2),
            '/' => (TokenType.Slash, 1),

            '%' when next == '=' => (TokenType.PercentAssign, 2),
            '%' => (TokenType.Percent, 1),

            '=' => (TokenType.Assign, 1),
            '(' => (TokenType.LeftParen, 1),
            ')' => (TokenType.RightParen, 1),

            _ => throw new CalculatorException($"Unexpected character '{c}' at column {start + 1}"),
        };

        pos += length;
        return new Token(type, line.Substring(start, length), start);
    }

    // First character of an identifier: an ASCII letter or '_' (x, _tmp). A digit can't start one, so "1x" is not an identifier.
    private static bool IsIdentifierStart(char c) => char.IsAsciiLetter(c) || c == '_';

    // Any later character of an identifier: also allows digits (x1, count_2).
    private static bool IsIdentifierPart(char c) => IsIdentifierStart(c) || char.IsAsciiDigit(c);
}
