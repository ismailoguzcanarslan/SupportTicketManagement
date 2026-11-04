using Microsoft.Xrm.Sdk;

namespace SupportTicket.DeploymentHelper.Deployment;

/// <summary>
/// Fixed solution component types (componenttype option values) of the components this tool
/// deploys. pluginpackage has no fixed type; it is resolved from metadata (SolutionRepository).
/// </summary>
public static class SolutionComponents
{
    /// <summary>A table; its objectid is the table's MetadataId.</summary>
    public const int Entity = 1;
    public const int SystemForm = 60;
    public const int WebResource = 61;
    public const int SdkMessageProcessingStep = 92;
    public const int SdkMessageProcessingStepImage = 93;

    /// <summary>Creates a record directly inside the solution (supported for solution-aware tables).</summary>
    public static Guid CreateInSolution(IOrganizationService service, Entity record, string solutionUniqueName)
    {
        var request = new OrganizationRequest("Create") { ["Target"] = record, ["SolutionUniqueName"] = solutionUniqueName };
        return (Guid)service.Execute(request)["id"];
    }
}
