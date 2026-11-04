namespace SupportTicket.EntityGeneratorTests.Infrastructure;

/// <summary>Locates the offline fixtures in TestData/, copied next to the test assembly.</summary>
public static class TestData
{
    public static string Root { get; } = Path.Combine(AppContext.BaseDirectory, "TestData");

    public static string GetPath(params string[] relativeParts) =>
        Path.Combine([Root, .. relativeParts]);

    public static string ReadText(params string[] relativeParts) =>
        File.ReadAllText(GetPath(relativeParts));

    /// <summary>Reads a golden file with line endings normalized, so comparisons ignore CRLF/LF checkouts.</summary>
    public static string ReadExpectedOutput(params string[] relativeParts) =>
        NormalizeLineEndings(ReadText(["ExpectedOutput", .. relativeParts]));

    public static string NormalizeLineEndings(string text) =>
        text.Replace("\r\n", "\n");
}
