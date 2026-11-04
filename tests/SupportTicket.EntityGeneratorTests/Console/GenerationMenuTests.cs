
using SupportTicket.EntityGenerator.Console;
using Xunit;

namespace SupportTicket.EntityGeneratorTests.Console;

public class GenerationMenuTests
{
    [Theory]
    [InlineData("0", GenerationMode.Exit)]
    [InlineData("1", GenerationMode.Backend)]
    [InlineData("2", GenerationMode.Frontend)]
    [InlineData("3", GenerationMode.Both)]
    public void TryParseSelection_ValidInput_ReturnsExpectedMode(string input, GenerationMode expectedMode)
    {
        // Act
        var result = GenerationMenu.TryParseSelection(input, out var mode);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedMode, mode);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("4")]
    [InlineData("-1")]
    [InlineData("")]
    public void TryParseSelection_InvalidInput_ReturnsFalse(string input)
    {
        // Act
        var result = GenerationMenu.TryParseSelection(input, out _);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("", GenerationMode.Exit)]
    [InlineData("x\n9\n", GenerationMode.Exit)]
    [InlineData("x\n2\n", GenerationMode.Frontend)]
    public void Show_ReturnsInsteadOfHanging_WhenInputEnds(string input, GenerationMode expected)
    {
        var originalIn = System.Console.In;
        var originalOut = System.Console.Out;
        try
        {
            System.Console.SetIn(new StringReader(input));
            System.Console.SetOut(TextWriter.Null);

            Assert.Equal(expected, GenerationMenu.Show());
        }
        finally
        {
            System.Console.SetIn(originalIn);
            System.Console.SetOut(originalOut);
        }
    }
}
