using System.Linq;
using System.Reflection;
using Xunit;

namespace SupportTicket.PluginTests;

// Infrastructure guard, not a business test: the Dataverse sandbox only loads .NET Framework assemblies.
// A .NET Framework assembly binds to mscorlib; .NET (Core) and .NET Standard assemblies bind to
// System.Runtime / netstandard instead.
public class PluginAssemblyTests
{
    [Fact]
    public void PluginAssembly_IsCompiledForNetFramework()
    {
        var references = Assembly.Load("SupportTicket.Plugins")
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();

        Assert.Contains("mscorlib", references);
        Assert.DoesNotContain("System.Runtime", references);
        Assert.DoesNotContain("netstandard", references);
    }
}
