using Xunit;

namespace SupportTicket.EntityGeneratorTests.Infrastructure;

// Self-tests for the test project wiring, not generator behavior.
public class TestInfrastructureTests
{
    [Fact]
    public void TestData_IsCopiedToOutput()
    {
        Assert.True(File.Exists(TestData.GetPath("README.md")), $"Missing fixtures under {TestData.Root}");
    }

    [Fact]
    public void NormalizeLineEndings_ConvertsCrLfToLf()
    {
        Assert.Equal("a\nb\n", TestData.NormalizeLineEndings("a\r\nb\r\n"));
    }
}
