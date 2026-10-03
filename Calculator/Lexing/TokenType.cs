namespace Calculator.Lexing;

public enum TokenType
{
    Number,        // 42
    Identifier,    // x, count, _tmp1

    Plus,          // +
    Minus,         // -
    Star,          // *
    Slash,         // /
    Percent,       // %
    PlusPlus,      // ++
    MinusMinus,    // --

    Assign,        // =
    PlusAssign,    // +=
    MinusAssign,   // -=
    StarAssign,    // *=
    SlashAssign,   // /=
    PercentAssign, // %=

    LeftParen,     // (
    RightParen,    // )
    Semicolon,     // ;  (optional, only at the end of a line)

    End,           // end of the line
}
