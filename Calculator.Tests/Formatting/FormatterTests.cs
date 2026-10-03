using System.Globalization;
using Calculator.Formatting;

namespace Calculator.Tests.Formatting;

public class FormatterTests
{
    [Fact]
    public void AssignmentExample()
    {
        var variables = new Dictionary<string, int> { ["i"] = 82, ["j"] = 1, ["x"] = 6, ["y"] = 80 };

        Assert.Equal("(i=82,j=1,x=6,y=80)", Formatter.Format(variables));
    }

    [Fact]
    public void Empty_PrintsEmptyParentheses()
    {
        Assert.Equal("()", Formatter.Format(new Dictionary<string, int>()));
    }

    [Fact]
    public void SingleVariable()
    {
        Assert.Equal("(x=5)", Formatter.Format(new Dictionary<string, int> { ["x"] = 5 }));
    }

    [Fact]
    public void SortsByName_RegardlessOfInsertionOrder()
    {
        var variables = new Dictionary<string, int> { ["y"] = 80, ["x"] = 6, ["j"] = 1, ["i"] = 82 };

        Assert.Equal("(i=82,j=1,x=6,y=80)", Formatter.Format(variables));
    }

    [Fact]
    public void SortsOrdinal_ByCharacterCode()
    {
        // Uppercase letters come before '_', which comes before lowercase letters;
        // names are compared character by character, so "x10" comes before "x2".
        var variables = new Dictionary<string, int>
        {
            ["b"] = 1, ["a"] = 2, ["B"] = 3, ["A"] = 4, ["_x"] = 5, ["x2"] = 6, ["x10"] = 7,
        };

        Assert.Equal("(A=4,B=3,_x=5,a=2,b=1,x10=7,x2=6)", Formatter.Format(variables));
    }

    [Fact]
    public void NegativeAndExtremeValues()
    {
        var variables = new Dictionary<string, int> { ["max"] = int.MaxValue, ["min"] = int.MinValue, ["x"] = -9 };

        Assert.Equal("(max=2147483647,min=-2147483648,x=-9)", Formatter.Format(variables));
    }

    [Theory]
    [InlineData("sv-SE")] // prints -9 with the minus sign U+2212
    [InlineData("he-IL")] // adds an invisible direction mark before -9
    [InlineData("en-US")]
    public void Output_DoesNotDependOnRegionalSettings(string cultureName)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);
            var variables = new Dictionary<string, int> { ["b"] = 1, ["A"] = 2, ["x"] = -9 };

            Assert.Equal("(A=2,b=1,x=-9)", Formatter.Format(variables));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
