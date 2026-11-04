namespace SupportTicket.DeploymentHelper.Configuration;

public static class RepositoryPaths
{
    private const string SolutionFileName = "SupportTicketManagement.sln";

    /// <summary>Finds the repository root (the folder containing the .sln), starting from <paramref name="startDirectory"/>.</summary>
    public static string FindRoot(string startDirectory)
    {
        for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFileName))) return directory.FullName;
        }

        throw new DirectoryNotFoundException($"Could not find {SolutionFileName} above {startDirectory}.");
    }
}
