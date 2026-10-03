# Text-Based Calculator

A command-line calculator that runs a series of assignment statements, one per line, and prints the final value of every variable.

**Input:**

```
i = 0
j = ++i
x = i++ + 5
y = (5 + 3) * 10
i += y
```

**Output:**

```
(i=82,j=1,x=6,y=80)
```

The lexer, parser and evaluator are written by hand. No expression-evaluation library, scripting engine, `eval`-like API or parser generator is used.

---

## Build, run and test

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
dotnet build                                              # build the solution
dotnet test                                               # run all tests

dotnet run --project Calculator.Cli -- examples/example.txt   # read the lines from a file
printf 'a = 7\nb = a * 2\n' | dotnet run --project Calculator.Cli   # read from standard input
dotnet run --project Calculator.Cli                       # type the lines, finish with Ctrl+D
```

Arguments after `--` are passed to the program.

| Exit code | Meaning |
|---|---|
| `0` | Success. The result is printed to standard output. |
| `1` | Calculation error (syntax error, undefined variable, division by zero). The message, with its line number, is printed to standard error. |
| `2` | Usage or file error (too many arguments, file not found, no permission). |

Errors go to standard error, so `calc input.txt > result.txt` writes only the result to the file.

---

## Project structure

```
Calculator.sln
├── Calculator/                    class library: all the logic
│   ├── CalculatorEngine.cs        public entry point: runs all lines, returns the output
│   ├── CalculatorException.cs     the single error type (message + line number)
│   ├── Lexing/                    Lexer, Token, TokenType
│   ├── Parsing/                   Parser, Ast (syntax tree records)
│   ├── Evaluation/                Evaluator (variable store)
│   ├── Formatting/                Formatter (output line)
│   └── Helpers/                   StackGuard
├── Calculator.Cli/                console app: reads input, calls the engine, prints; no logic
├── Calculator.Tests/              xUnit tests, one file per component + end-to-end
└── examples/example.txt           the example above
```

---

## How it works

Each line goes through a pipeline. All lines share one variable store.

```
line → Lexer → tokens → Parser → syntax tree → Evaluator → variable store ─(after the last line)→ Formatter → output
```

| Component | Receives | Produces | Question it answers |
|---|---|---|---|
| **Lexer** | characters of one line | tokens | What are the words? |
| **Parser** | tokens of one line | syntax tree | What is the structure? |
| **Evaluator** | the tree + the variable store | updated variable store | What is the result? |
| **Formatter** | the final variable store | `(name=value,...)` | How is it printed? |
| **CalculatorEngine** | all lines | the output, or an error with its line number | Runs the pipeline line by line |

### Lexer

Reads the characters of one line once, left to right, and groups them into tokens: numbers, identifiers, operators and parentheses. Operators are matched with **maximal munch** (the longest possible operator wins), so `i+++j` is `i ++ + j`.

### Parser

A hand-written **recursive-descent** parser: one method per precedence level, each calling the next-stronger level for its operands. The call order is the order of operations upside down: the weakest operator is handled first and ends up at the top of the tree, so it is calculated last.

| Calculated | Operators | Method |
|---|---|---|
| 1st (strongest) | `( )`, numbers, variables | `ParsePrimary` |
| 2nd | `x++` `x--` | `ParsePostfix` |
| 3rd | `-x` `+x` `++x` `--x` | `ParseUnary` |
| 4th | `*` `/` `%` | `ParseTerm` |
| 5th | `+` `-` | `ParseExpression` |
| 6th (weakest) | `=` `+=` `-=` `*=` `/=` `%=` | `ParseStatement` |

`while` loops in `ParseExpression` and `ParseTerm` make the binary operators left-associative (`a - b - c` is `(a - b) - c`). Parentheses call back up to `ParseExpression` and produce no node: the shape of the tree already holds the grouping.

`y = (5 + 3) * 10` becomes:

```
AssignmentStatement (y)
└── BinaryExpression (*)
    ├── BinaryExpression (+)
    │   ├── NumberExpression 5
    │   └── NumberExpression 3
    └── NumberExpression 10
```

The tree nodes are immutable C# records, so two trees with the same shape are equal; parser tests compare whole trees with one assertion.

### Evaluator

Walks the tree recursively (children first, values flow up to the root) against a `Dictionary<string, int>` that is kept between lines.

### Formatter

Sorts the variables by name and prints `(name=value,name=value)`.

---

## Supported syntax

**Each line** is one of:

| Form | Example |
|---|---|
| Assignment | `x = 5`, `y = (x + 3) * 10` |
| Compound assignment | `x += 2`, `x -= y`, `x *= 3`, `x /= 2`, `x %= 4` |
| Standalone increment / decrement | `i++`, `++i`, `i--`, `--i` |
| Blank or whitespace-only line | skipped |

**In expressions:** integer literals, variables, parentheses, binary `+ - * / %`, unary `+` and `-`, prefix and postfix `++` and `--`. Whitespace between tokens is optional.

**Not supported:** floating-point numbers, bitwise, comparison and logical operators, the conditional operator `?:`, chained assignment (`a = b = 5`), function calls, and more than one statement per line.

---

## Semantics

### Numbers: 32-bit signed integers

| Rule | Example |
|---|---|
| Overflow wraps around | `2147483647 + 1` is `-2147483648` |
| Division truncates toward zero | `7 / 2` is `3`, `-7 / 2` is `-3` |
| The remainder takes the sign of the left operand | `-7 % 3` is `-1`, `7 % -3` is `1` |
| `int.MinValue / -1` wraps around instead of failing | result `-2147483648`; `int.MinValue % -1` is `0` |

.NET throws `OverflowException` for `int.MinValue / -1` and `int.MinValue % -1` even in unchecked code (the CPU's divide instruction faults on them), so the evaluator special-cases a divisor of `-1`. All arithmetic is written inside `unchecked(...)` to document that wrapping is intended.

### `++` and `--`

- They **update the variable immediately**, as soon as they are evaluated.
- **Prefix** (`++i`) returns the **new** value; **postfix** (`i++`) returns the **old** value.
- They only apply to a variable, possibly in parentheses: `i++`, `(i)++` and `++(i)` are valid; `5++` and `(i + 1)++` are errors.

### Evaluation order

- **Left to right:** the left operand is fully calculated before the right one. With `i = 1`, `x = i++ + i` gives `1 + 2 = 3`.
- **Compound assignment** `x op= value` means `x = x op value`, and **`x` is read before `value` is calculated**. With `x = 1`, `x += x++` gives `1 + 1 = 2`.
- **`i = i++`** leaves `i` unchanged: `i++` returns the old value, which is then assigned back.

### Variables

- Created by their first assignment. Reading a variable that was never assigned is an error, and so is a compound assignment or `++`/`--` on one (`x += 1` needs `x`).
- Names: an ASCII letter or `_`, followed by letters, digits or `_`. Case-sensitive.

### Output

- Variables sorted by name with **ordinal** comparison (character codes): uppercase before `_` before lowercase, and `x10` before `x2`. The order is the same on every machine.
- Numbers are printed with the invariant culture. Some regional settings would print `-9` with a different minus character (U+2212) or add an invisible direction mark.
- No variables: `()`.

---

## Errors

Evaluation stops at the first error. Nothing is printed to standard output; the message goes to standard error as `Error: <message>`, with the line number (counting blank lines, so it matches the line in the file).

| Kind | Example line | Message |
|---|---|---|
| Unknown character | `x = 1 # 2` | `Line 1: Unexpected character '#' at column 7` |
| Invalid number | `x = 12abc` | `Line 1: Invalid number '12a' at column 5` |
| Leading zero | `x = 010` | `Line 1: Numbers with a leading zero are not supported: '010' at column 5` |
| Number out of range | `x = 2147483648` | `Line 1: Integer literal out of range: '2147483648' at column 5` |
| Missing operand | `x = 1 +` | `Line 1: Expected a number, a variable or '(' but found end of line at column 8` |
| Missing `)` | `x = (1 + 2` | `Line 1: Expected ')' but found end of line at column 11` |
| Extra tokens | `x = 1 2` | `Line 1: Expected end of line but found '2' at column 7` |
| `++`/`--` on a non-variable | `x = 5++` | `Line 1: Operator '++' at column 6 can only be applied to a variable` |
| Not a statement | `i + 1` | `Line 1: Invalid statement: expected an assignment (x = 1) or an increment (x++, --x)` |
| Undefined variable | `x = y` | `Line 1: Undefined variable 'y'` |
| Division by zero | `x = 1 / 0`, `x = 1 % 0` | `Line 1: Division by zero` |
| Too deeply nested | thousands of nested `(` | `Line 1: Expression is too deeply nested` |

---

## Complexity

- **Time:** O(n) in the total size of the input. Every character is read once by the lexer and every token once by the parser; the evaluator visits every node once.
- **Memory:** O(number of variables), plus the tokens and tree of the current line. The input is streamed, so a large file is never loaded at once.
- **Recursion depth:** proportional to the nesting of one line, bounded by the stack guard.

---

## Tests

```bash
dotnet test
```

| File | Covers |
|---|---|
| `Lexing/LexerTests.cs` | every token, maximal munch, positions, invalid characters and numbers |
| `Parsing/ParserTests.cs` | tree shapes: precedence, associativity, parentheses, prefix/postfix, every syntax error |
| `Evaluation/EvaluatorTests.cs` | the example line by line, arithmetic, overflow, `++`/`--`, evaluation order, compound assignment, runtime errors |
| `Formatting/FormatterTests.cs` | output format, ordinal sorting, independence from regional settings |
| `StackGuardTests.cs` | deeply nested input gives an error instead of crashing |
| `CalculatorEngineTests.cs` | end to end: the full example, blank lines, line numbers from every stage, lazy reading |
