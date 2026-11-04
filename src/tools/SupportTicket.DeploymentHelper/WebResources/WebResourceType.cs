namespace SupportTicket.DeploymentHelper.WebResources;

/// <summary>webresource.webresourcetype option values by file extension.</summary>
public static class WebResourceType
{
    private static readonly Dictionary<string, int> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".htm"] = 1,
        [".html"] = 1,
        [".css"] = 2,
        [".js"] = 3,
        [".xml"] = 4,
        [".png"] = 5,
        [".jpg"] = 6,
        [".gif"] = 7,
        [".xsl"] = 9,
        [".ico"] = 10,
        [".svg"] = 11,
        [".resx"] = 12,
    };

    public static int? FromFileName(string fileName) =>
        ByExtension.TryGetValue(Path.GetExtension(fileName), out var type) ? type : null;
}
