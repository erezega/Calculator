namespace Calculator.Lexing;

/// <summary>
/// A single token produced by the lexer.
/// Design trade-off: <see cref="Value"/> is only meaningful for number tokens and is ignored otherwise.
/// The alternative, a separate token type per kind (e.g. a NumberToken subclass), would need classes,
/// inheritance and type checks in the parser; one flat struct with an unused field is simpler.
/// </summary>
/// <param name="Type">Kind of token.</param>
/// <param name="Text">Exact source text of the token (empty for <see cref="TokenType.End"/>).</param>
/// <param name="Position">Zero-based column where the token starts; used in error messages.</param>
/// <param name="Value">Numeric value; only meaningful for <see cref="TokenType.Number"/>.</param>
public readonly record struct Token(TokenType Type, string Text, int Position, int Value = 0);
