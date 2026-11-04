using SupportTicket.DeploymentHelper.Deployment;
using Xunit;

namespace SupportTicket.DeploymentHelperTests.Deployment;

public class SolutionTests
{
    private const string Main = "OguzLabsSupportTicketManagement";

    private static readonly SolutionInfo Solution = new(Guid.NewGuid(), Main, "Support Ticket Management", false, "ogz");

    [Fact]
    public void UnmanagedMainSolutionOfOurPublisher_IsValid()
    {
        Assert.Empty(SolutionValidator.Validate(Main, "ogz", Solution));
    }

    [Fact]
    public void MissingSolution_IsReported_AndNeverCreated()
    {
        var error = Assert.Single(SolutionValidator.Validate(Main, "ogz", null));

        Assert.Contains("does not exist", error);
        Assert.Contains("does not create solutions", error);
    }

    [Fact]
    public void ManagedSolution_IsRejected()
    {
        Assert.Contains(SolutionValidator.Validate(Main, "ogz", Solution with { IsManaged = true }), e => e.Contains("is managed"));
    }

    [Theory]
    [InlineData("Default")]
    [InlineData("Active")]
    [InlineData("")]
    public void SystemOrMissingSolutionName_IsRejected(string uniqueName)
    {
        Assert.NotEmpty(SolutionValidator.Validate(uniqueName, "ogz", Solution with { UniqueName = uniqueName }));
    }

    [Fact]
    public void DefaultSolutionById_IsRejected()
    {
        var defaultSolution = Solution with { Id = Guid.Parse("fd140aaf-4df4-11dd-bd17-0019b9312238") };

        Assert.Contains(SolutionValidator.Validate(Main, "ogz", defaultSolution), e => e.Contains("Default Solution"));
    }

    [Fact]
    public void SolutionOfAnotherPublisher_IsRejected()
    {
        Assert.Contains(SolutionValidator.Validate(Main, "ogz", Solution with { PublisherPrefix = "new" }), e => e.Contains("publisher prefix 'new'"));
    }

    [Fact]
    public void ExistingMember_IsNeverAddedTwice()
    {
        var id = Guid.NewGuid();
        var members = new Dictionary<Guid, SolutionMember> { [id] = new(id, 61, 0) };

        Assert.Null(SolutionAssociation.ForExisting("Web Resource x", 61, id, members));
        Assert.Equal(AssociationAction.Add, SolutionAssociation.ForExisting("Web Resource y", 61, Guid.NewGuid(), members)!.Action);
    }

    [Fact]
    public void PlanOutput_NamesTheSolutionAndHowTheComponentJoinsIt()
    {
        Assert.Equal($"[Solution] Plugin Package -> {Main} (created in the solution)",
            TargetSolution.Describe(SolutionAssociation.ForCreate("Plugin Package", 10090), Main));
        Assert.Equal($"[Solution] Form account/x -> {Main} (add existing component)",
            TargetSolution.Describe(new SolutionAssociation("Form account/x", 60, Guid.NewGuid(), AssociationAction.Add), Main));
        Assert.Equal(["Environment: DEV", $"Solution: {Main} (Support Ticket Management, unmanaged, publisher ogz)", ""],
            TargetSolution.Header("DEV", Solution));
    }
}
