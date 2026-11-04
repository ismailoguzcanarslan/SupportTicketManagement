namespace SupportTicket.DeploymentHelperTests;

/// <summary>A throwaway folder acting as the repository root for file-based tests.</summary>
public sealed class TestRepository : IDisposable
{
    public TestRepository()
    {
        Root = Path.Combine(Path.GetTempPath(), "ogz-deploy-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string WriteFile(string relativePath, string content = "console.log('x');")
    {
        var path = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); } catch (IOException) { }
    }
}
