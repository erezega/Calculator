using Calculator;

Console.WriteLine("Hello, World!");

var c = new CalculatorException("d");
var withLine = c.AtLine(1);

Console.WriteLine(withLine.Message);    // Line 1: d
Console.WriteLine(withLine.LineNumber); // 1
Console.WriteLine(c.LineNumber);        // (empty line: null, c is unchanged)