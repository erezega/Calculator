namespace Calculator.Parsing;

// The AST (abstract syntax tree) of one line. Nodes are immutable records, so two trees with the
// same shape and values are equal; parser tests compare whole trees with a single Assert.Equal.
// There is no node for parentheses: the shape of the tree already encodes the grouping.

public enum BinaryOperator
{
    Add,       // +
    Subtract,  // -
    Multiply,  // *
    Divide,    // /
    Remainder, // %
}

public enum UnaryOperator
{
    Plus,   // +x
    Negate, // -x
}

public enum IncrementOperator
{
    Increment, // ++
    Decrement, // --
}

public abstract record Expression;

/// <summary>An integer literal: <c>42</c>.</summary>
public sealed record NumberExpression(int Value) : Expression;

/// <summary>A variable read: <c>x</c>.</summary>
public sealed record VariableExpression(string Name) : Expression;

/// <summary><c>-x</c> or <c>+x</c>.</summary>
public sealed record UnaryExpression(UnaryOperator Operator, Expression Operand) : Expression;

/// <summary><c>a + b</c>, <c>a * b</c>, ...</summary>
public sealed record BinaryExpression(Expression Left, BinaryOperator Operator, Expression Right) : Expression;

/// <summary>
/// <c>++x</c>, <c>--x</c> (prefix: the value is the new value) or
/// <c>x++</c>, <c>x--</c> (postfix: the value is the old value). Both update the variable.
/// Holds a name rather than a sub-expression because only a variable can be incremented.
/// </summary>
public sealed record IncrementExpression(string Name, IncrementOperator Operator, bool IsPrefix) : Expression;

public abstract record Statement;

/// <summary>
/// <c>x = value</c> (<see cref="Operator"/> is null) or a compound assignment such as
/// <c>x += value</c> (<see cref="Operator"/> is <see cref="BinaryOperator.Add"/>), meaning x = x + value.
/// </summary>
public sealed record AssignmentStatement(string Name, BinaryOperator? Operator, Expression Value) : Statement;

/// <summary>A line that only increments or decrements a variable: <c>i++</c>, <c>--i</c>.</summary>
public sealed record IncrementStatement(IncrementExpression Increment) : Statement;
