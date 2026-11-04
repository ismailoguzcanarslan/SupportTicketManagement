using SupportTicket.DeploymentHelper.Configuration;
using Xunit;

namespace SupportTicket.DeploymentHelperTests.Configuration;

public sealed class ManifestValidatorTests : IDisposable
{
    private readonly TestRepository _repo = new();
    private readonly ManifestValidator _validator;

    public ManifestValidatorTests()
    {
        _validator = new ManifestValidator("ogz", _repo.Root);
        _repo.WriteFile("dist/forms/account.js");
    }

    public void Dispose() => _repo.Dispose();

    [Fact]
    public void ValidManifest_HasNoErrors()
    {
        var manifest = Manifest(WebResource("ogz_/forms/account.js", "dist/forms/account.js"));
        manifest.Forms.Add(Form("ogz_/forms/account.js", "onload"));

        Assert.Empty(_validator.ValidateWebResources(manifest));
    }

    [Theory]
    [InlineData("new_/forms/account.js", "must start with 'ogz_/'")]
    [InlineData("ogz_/forms/acc ount.js", "may contain only")]
    [InlineData("ogz_/forms/account.ts", "unsupported file extension")]
    public void InvalidName_IsReported(string name, string expected)
    {
        var errors = _validator.ValidateWebResources(Manifest(WebResource(name, "dist/forms/account.js")));

        Assert.Contains(errors, e => e.Contains(expected));
    }

    [Fact]
    public void MissingFile_AsksToBuildFirst()
    {
        var errors = _validator.ValidateWebResources(Manifest(WebResource("ogz_/forms/contact.js", "dist/forms/contact.js")));

        Assert.Contains(errors, e => e.Contains("build it first"));
    }

    [Fact]
    public void PathOutsideRepository_IsRejected()
    {
        var errors = _validator.ValidateWebResources(Manifest(WebResource("ogz_/forms/account.js", "../outside/account.js")));

        Assert.Contains(errors, e => e.Contains("inside the repository"));
    }

    [Fact]
    public void DuplicateName_IsRejected()
    {
        var entry = WebResource("ogz_/forms/account.js", "dist/forms/account.js");

        Assert.Contains(_validator.ValidateWebResources(Manifest(entry, entry)), e => e.Contains("more than once"));
    }

    [Fact]
    public void Form_RequiresExplicitGuidKnownLibraryAndSupportedEvent()
    {
        var manifest = Manifest(WebResource("ogz_/forms/account.js", "dist/forms/account.js"));
        manifest.Forms.Add(new FormEntry
        {
            Entity = "account",
            FormId = "Main form",
            Library = "ogz_/forms/other.js",
            Events = [new FormEventEntry { Event = "onchange", Function = "Ogz.Forms.Account.onChange" }],
        });

        var errors = _validator.ValidateWebResources(manifest);

        Assert.Contains(errors, e => e.Contains("explicit form GUID"));
        Assert.Contains(errors, e => e.Contains("must also be listed under webResources"));
        Assert.Contains(errors, e => e.Contains("'onchange' is not supported"));
    }

    [Fact]
    public void Form_RejectsInvalidFunctionName()
    {
        var manifest = Manifest(WebResource("ogz_/forms/account.js", "dist/forms/account.js"));
        manifest.Forms.Add(Form("ogz_/forms/account.js", "onload", "Ogz.Forms.Account.onLoad()"));

        Assert.Contains(_validator.ValidateWebResources(manifest), e => e.Contains("not a valid function name"));
    }

    [Fact]
    public void Plugins_RequirePrefixedPackageAndBuiltAssembly()
    {
        var manifest = new DeploymentManifest
        {
            Plugins = new PluginsEntry { PackageId = "SupportTicketPlugins", Assembly = "bin/Plugins.dll", PackageDirectory = "bin" },
        };

        var errors = _validator.ValidatePlugins(manifest);

        Assert.Contains(errors, e => e.Contains("must start with 'ogz_'"));
        Assert.Contains(errors, e => e.Contains("assembly not found"));
    }

    private static DeploymentManifest Manifest(params WebResourceEntry[] entries) => new() { WebResources = [.. entries] };

    private static WebResourceEntry WebResource(string name, string file) => new() { Name = name, File = file };

    private static FormEntry Form(string library, string formEvent, string function = "Ogz.Forms.Account.onLoad") => new()
    {
        Entity = "account",
        FormId = Guid.NewGuid().ToString(),
        Library = library,
        Events = [new FormEventEntry { Event = formEvent, Function = function }],
    };
}
