using Calculator.Helpers;
using Calculator.Parsing;

namespace Calculator.Evaluation;

/// <summary>
/// Runs statements (syntax trees from the parser) against a variable store that is kept between lines.
///
/// How it works: <see cref="Evaluate"/> walks the tree recursively. A node calculates its children first,
/// then itself, so values flow up from the leaves to the root.
///
/// Rules:
/// <code>
/// left to right        the left operand is calculated before the right: "i++ + i" with i = 1 is 1 + 2 = 3
/// ++ / --              update the variable immediately; prefix returns the new value, postfix the old one
/// x op= value          x = x op value, and x is read BEFORE value is calculated: "x += x++" with x = 1 is 2
/// 32-bit integers      overflow wraps around; division truncates toward zero; % takes the sign of the left operand
/// errors               undefined variable, division or remainder by zero
/// </code>
/// </summary>
public sealed class Evaluator
{
    private readonly Dictionary<string, int> _variables = new();

    /// <summary>The current value of every variable assigned so far (read-only for callers).</summary>
    public IReadOnlyDictionary<string, int> Variables => _variables;

    // Handles: one line (statement).
    // Example: "x = 5" → stores x = 5;  "i++" → increments i
    // Note:    on an error nothing is assigned, but a '++' / '--' that already ran has already updated its variable.
    public void Execute(Statement statement)
    {
        switch (statement)
        {
            case AssignmentStatement assignment:
                ExecuteAssignment(assignment);
                break;

            case IncrementStatement increment:
                Evaluate(increment.Increment); // only the update of the variable matters; the value is not used
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(statement), statement, "Unknown statement type");
        }
    }

    // Handles: '=' and the compound assignments '+=', '-=', '*=', '/=', '%='.
    // Example: "x = 5" → x is 5;  "x += 2" with x = 5 → x is 7
    // Note:    for compound assignments the current value is read first, so "x += 1" fails if x is undefined.
    private void ExecuteAssignment(AssignmentStatement assignment)
    {
        if (assignment.Operator is null)
        {
            _variables[assignment.Name] = Evaluate(assignment.Value);
            return;
        }

        // x op= value means x = x op value. Read x BEFORE calculating the right side:
        // "x += x++" with x = 1 is 1 + 1 = 2 (the x++ inside does not change the 1 that was already read).
        var current = Read(assignment.Name);
        var value = Evaluate(assignment.Value);
        _variables[assignment.Name] = Apply(assignment.Operator.Value, current, value);
    }

    // Handles: any expression node; calls itself for the children.
    // Example: "(5 + 3) * 10" → 80
    // Note:    one case per node type in Ast.cs.
    private int Evaluate(Expression expression)
    {
        // A long "1 + 1 + 1 + ..." is built by a loop in the parser, but evaluating it recurses once per '+'.
        StackGuard.EnsureSufficientStack();

        return expression switch
        {
            NumberExpression number => number.Value,
            VariableExpression variable => Read(variable.Name),
            UnaryExpression unary => EvaluateUnary(unary),
            BinaryExpression binary => EvaluateBinary(binary),
            IncrementExpression increment => EvaluateIncrement(increment),
            _ => throw new ArgumentOutOfRangeException(nameof(expression), expression, "Unknown expression type"),
        };
    }

    // Handles: -x and +x.
    // Example: "-5" → -5;  "- -5" → 5
    // Note:    -int.MinValue wraps around to int.MinValue, like any other 32-bit overflow.
    private int EvaluateUnary(UnaryExpression unary)
    {
        var operand = Evaluate(unary.Operand);
        return unary.Operator == UnaryOperator.Negate ? unchecked(-operand) : operand;
    }

    // Handles: a + b, a - b, a * b, a / b, a % b.
    // Example: "i++ + i" with i = 1 → 1 + 2 = 3
    // Note:    the left side is calculated first, so its side effects (i++) are visible to the right side.
    private int EvaluateBinary(BinaryExpression binary)
    {
        var left = Evaluate(binary.Left);
        var right = Evaluate(binary.Right);
        return Apply(binary.Operator, left, right);
    }

    // Handles: ++x, --x, x++, x--.
    // Example: "j = ++i" with i = 1 → i is 2, j is 2;  "j = i++" with i = 1 → i is 2, j is 1
    // Note:    the store is updated right away, before the rest of the expression is calculated.
    private int EvaluateIncrement(IncrementExpression increment)
    {
        var oldValue = Read(increment.Name);
        var newValue = increment.Operator == IncrementOperator.Increment
            ? unchecked(oldValue + 1)
            : unchecked(oldValue - 1);

        _variables[increment.Name] = newValue;
        return increment.IsPrefix ? newValue : oldValue;
    }

    // 32-bit integer arithmetic. unchecked: overflow wraps around (int.MaxValue + 1 is int.MinValue) instead of throwing.
    private static int Apply(BinaryOperator op, int left, int right) => op switch
    {
        BinaryOperator.Add => unchecked(left + right),
        BinaryOperator.Subtract => unchecked(left - right),
        BinaryOperator.Multiply => unchecked(left * right),
        BinaryOperator.Divide => Divide(left, right),
        BinaryOperator.Remainder => Remainder(left, right),
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, "Unknown operator"),
    };

    // Truncates toward zero: 7 / 2 is 3, -7 / 2 is -3.
    private static int Divide(int left, int right)
    {
        if (right == 0)
        {
            throw new CalculatorException("Division by zero");
        }

        // int.MinValue / -1 throws OverflowException in .NET, even in unchecked code.
        // x / -1 is -x, which wraps around like any other overflow (int.MinValue / -1 is int.MinValue).
        return right == -1 ? unchecked(-left) : left / right;
    }

    // The result takes the sign of the left operand: -7 % 3 is -1, 7 % -3 is 1.
    private static int Remainder(int left, int right)
    {
        if (right == 0)
        {
            throw new CalculatorException("Division by zero");
        }

        // int.MinValue % -1 also throws OverflowException in .NET; any number % -1 is 0.
        return right == -1 ? 0 : left % right;
    }

    private int Read(string name) =>
        _variables.TryGetValue(name, out var value)
            ? value
            : throw new CalculatorException($"Undefined variable '{name}'");
}
