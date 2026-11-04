using SupportTicket.DeploymentHelper.Configuration;
using SupportTicket.DeploymentHelper.Deployment;

namespace SupportTicket.DeploymentHelper.Plugins;

/// <summary>
/// Menu option 2: uploads the plug-in package, registers the steps declared by PluginStep/ActionStep
/// attributes, adds them to the main solution, and verifies the result. Obsolete registrations are
/// reported, never deleted.
/// </summary>
public sealed class PluginDeploymentTarget : IDeploymentTarget
{
    private const string PluginPackageTable = "pluginpackage";

    private readonly DeploymentSettings _settings;
    private readonly DeploymentManifest _manifest;
    private readonly ManifestValidator _validator;
    private readonly PluginStepDiscovery _discovery;
    private readonly PluginRegistrationRepository _repository;
    private readonly SolutionRepository _solutions;
    private readonly IDataverseSession _session;
    private readonly IUserPrompt _prompt;

    public PluginDeploymentTarget(
        DeploymentSettings settings,
        DeploymentManifest manifest,
        ManifestValidator validator,
        PluginStepDiscovery discovery,
        PluginRegistrationRepository repository,
        SolutionRepository solutions,
        IDataverseSession session,
        IUserPrompt prompt)
    {
        _settings = settings;
        _manifest = manifest;
        _validator = validator;
        _discovery = discovery;
        _repository = repository;
        _solutions = solutions;
        _session = session;
        _prompt = prompt;
    }

    public int MenuKey => 2;

    public string Title => "Deploy Plug-ins & Actions";

    public bool IsAvailable => true;

    public Task RunAsync(DeploymentRunOptions options, CancellationToken cancellationToken)
    {
        var errors = _validator.ValidatePlugins(_manifest);
        if (errors.Count > 0) return Fail("Manifest", errors);

        var plugins = _manifest.Plugins!;
        var assemblyPath = _validator.ResolveInRepository(plugins.Assembly)!;
        var package = PluginPackageFile.FindLatest(_validator.ResolveInRepository(plugins.PackageDirectory)!, plugins.PackageId);

        var packageErrors = PluginPackageValidator.Validate(
            PluginPackageInspector.Inspect(package.Content),
            plugins.PackageId,
            package.Version,
            Path.GetFileNameWithoutExtension(assemblyPath),
            File.ReadAllBytes(assemblyPath));
        if (packageErrors.Count > 0) return Fail($"Plug-in package {Path.GetFileName(package.Path)}", packageErrors);

        var discovery = _discovery.Discover(assemblyPath);
        var validation = discovery.Errors.Concat(StepValidator.Validate(discovery.Steps)).ToList();
        if (validation.Count > 0) return Fail("Registration attributes", validation);

        _prompt.WriteLine($"Package {Path.GetFileName(package.Path)}: {discovery.Steps.Count} declared registration(s).");

        // Connects (and shows the environment) on first use.
        if (TargetSolution.Resolve(_solutions, _settings, _prompt) is not { } solution) return Task.CompletedTask;

        var snapshot = _repository.LoadSnapshot(plugins.PackageId, discovery.Steps);
        var plan = PluginRegistrationPlanner.Plan(discovery.Steps, snapshot, package.Content, package.Version, discovery.PluginTypeNames);
        var members = _solutions.GetMembers(solution.Id, ComponentIds(snapshot));
        var associations = PluginSolutionPlanner.Plan(plan, members, _solutions.GetComponentType(PluginPackageTable));
        Print(plan, associations, solution);

        if (plan.IsBlocked)
        {
            _prompt.WriteLine($"The package cannot be deployed: {plan.Package.Reason}. Nothing was changed.");
            return Task.CompletedTask;
        }

        var hasChanges = plan.HasRegistrationChanges || associations.Any(a => a.Action == AssociationAction.Add);
        if (!hasChanges && !plan.HasCustomApiChanges)
        {
            _prompt.WriteLine($"Nothing to deploy: everything is registered and in {solution.UniqueName}.");
            return Task.CompletedTask;
        }

        if (options.DryRunOnly)
        {
            _prompt.WriteLine("Dry run: no changes made.");
            return Task.CompletedTask;
        }

        var report = new DeploymentReport();
        var applied = false;
        if (hasChanges
            && _prompt.ConfirmByTypingEnvironment($"Apply the plug-in changes above to {_session.Description}, solution {solution.UniqueName}?", _session.EnvironmentName))
        {
            Apply(plan, associations, plugins.PackageId, package, solution, report);
            applied = true;
        }
        else if (hasChanges)
        {
            _prompt.WriteLine("Plug-in deployment cancelled.");
        }

        if (plan.HasCustomApiChanges && !report.HasFailures
            && _prompt.ConfirmByTypingEnvironment("Link the plug-in types above as Custom API main handlers? This changes the Custom API definitions.", _session.EnvironmentName))
        {
            LinkCustomApis(plan, plugins.PackageId, report);
        }

        report.Print(_prompt);

        if (applied)
        {
            var after = LoadSnapshot(plugins.PackageId, plan);
            var afterMembers = _solutions.GetMembers(solution.Id, ComponentIds(after));
            TargetSolution.PrintVerification(_prompt, PluginDeploymentVerifier.Verify(plan.Steps, after, afterMembers, solution.UniqueName), solution.UniqueName);
        }

        return Task.CompletedTask;
    }

    private void Apply(
        PluginRegistrationPlan plan,
        IReadOnlyList<SolutionAssociation> associations,
        string packageId,
        PluginPackageFile package,
        SolutionInfo solution,
        DeploymentReport report)
    {
        try
        {
            switch (plan.Package.Action)
            {
                case PlannedAction.Create:
                    _repository.CreatePackage(packageId, package, solution.UniqueName);
                    report.Created($"package {packageId} {package.Version}");
                    break;
                case PlannedAction.Update:
                    _repository.UpdatePackage(plan.Package.ExistingId!.Value, package);
                    report.Updated($"package {packageId} {package.Version}");
                    break;
                default:
                    report.Unchanged($"package {packageId}");
                    break;
            }
        }
        catch (Exception ex)
        {
            report.Failed($"package {packageId}", ex);
            return; // Steps depend on the package's plug-in types.
        }

        // Plug-in types are created by Dataverse from the package; read them (and ids) again.
        var snapshot = LoadSnapshot(packageId, plan);

        foreach (var step in plan.Steps)
        {
            var label = PluginLabels.Step(step.Declared);
            try
            {
                switch (step.Action)
                {
                    case PlannedAction.Blocked:
                        report.Skipped($"{label}: {step.BlockedReason}");
                        continue;
                    case PlannedAction.Create:
                        var stepId = CreateStep(step.Declared, snapshot, solution.UniqueName);
                        foreach (var image in step.Images) _repository.CreateImage(stepId, step.Declared, image.Declared, solution.UniqueName);
                        report.Created(label);
                        break;
                    case PlannedAction.Update:
                        _repository.UpdateStep(step.ExistingStepId!.Value, step.Declared);
                        report.Updated(label);
                        break;
                    default:
                        report.Unchanged(label);
                        break;
                }

                if (step.Action is PlannedAction.Update or PlannedAction.Unchanged)
                {
                    foreach (var image in step.Images)
                    {
                        if (image.Action == PlannedAction.Create) _repository.CreateImage(step.ExistingStepId!.Value, step.Declared, image.Declared, solution.UniqueName);
                        if (image.Action == PlannedAction.Update) _repository.UpdateImage(image.ExistingId!.Value, image.Declared);
                        if (image.Action is PlannedAction.Create or PlannedAction.Update) report.Updated($"{label}: image {image.Declared.Alias}");
                    }
                }
            }
            catch (Exception ex)
            {
                report.Failed(label, ex);
            }
        }

        // Existing components that are not yet in the solution. New ones were created inside it.
        foreach (var association in associations.Where(a => a.Action == AssociationAction.Add))
        {
            try
            {
                _solutions.Add(association, solution.UniqueName);
                report.Updated($"{association.Label}: added to {solution.UniqueName}");
            }
            catch (Exception ex)
            {
                report.Failed($"{association.Label}: add to {solution.UniqueName}", ex);
            }
        }
    }

    private Guid CreateStep(DeclaredStep step, RegistrationSnapshot snapshot, string solutionUniqueName)
    {
        if (!snapshot.PluginTypes.TryGetValue(step.PluginTypeName, out var pluginTypeId))
            throw new InvalidOperationException($"Plug-in type {step.PluginTypeName} was not found in the uploaded package.");

        var message = snapshot.Messages[step.MessageName];
        Guid? filterId = step.EntityLogicalName is { } entity
            ? snapshot.Filters[RegistrationSnapshot.FilterKey(step.MessageName, entity)]
            : null;

        return _repository.CreateStep(step, pluginTypeId, message.Id, filterId, solutionUniqueName);
    }

    private void LinkCustomApis(PluginRegistrationPlan plan, string packageId, DeploymentReport report)
    {
        var snapshot = LoadSnapshot(packageId, plan);
        foreach (var link in plan.CustomApiLinks.Where(l => l.Action == PlannedAction.Update))
        {
            var label = $"Custom API {link.CustomApi.UniqueName} -> {link.Declared.PluginTypeName}";
            try
            {
                if (!snapshot.PluginTypes.TryGetValue(link.Declared.PluginTypeName, out var pluginTypeId))
                    throw new InvalidOperationException("plug-in type not found in the package");
                _repository.LinkCustomApi(link.CustomApi.Id, pluginTypeId);
                report.Updated(label);
            }
            catch (Exception ex)
            {
                report.Failed(label, ex);
            }
        }
    }

    private static IEnumerable<Guid> ComponentIds(RegistrationSnapshot snapshot) =>
        (snapshot.Package is { } package ? new[] { package.Id } : Array.Empty<Guid>())
            .Concat(snapshot.Steps.Select(s => s.Id))
            .Concat(snapshot.Steps.SelectMany(s => s.Images).Select(i => i.Id));

    private RegistrationSnapshot LoadSnapshot(string packageId, PluginRegistrationPlan plan) =>
        _repository.LoadSnapshot(packageId, plan.Steps.Select(s => s.Declared)
            .Concat(plan.CustomApiLinks.Select(l => l.Declared)).ToList());

    private void Print(PluginRegistrationPlan plan, IReadOnlyList<SolutionAssociation> associations, SolutionInfo solution)
    {
        foreach (var line in TargetSolution.Header(_session.EnvironmentName, solution)) _prompt.WriteLine(line);

        _prompt.WriteLine($"[{plan.Package.Action}] {PluginLabels.Package} {plan.Package.Version} ({plan.Package.Reason})");

        foreach (var step in plan.Steps)
        {
            var detail = step.Action switch
            {
                PlannedAction.Blocked => $" - {step.BlockedReason}",
                PlannedAction.Update => $" - {string.Join(", ", step.Differences)}",
                _ => string.Empty,
            };
            _prompt.WriteLine($"[{step.Action}] {PluginLabels.Step(step.Declared)}{detail}");

            foreach (var image in step.Images.Where(i => i.Action != PlannedAction.Unchanged))
                _prompt.WriteLine($"    [{image.Action}] image {image.Declared.Alias} ({image.Declared.Attributes ?? "all attributes"})");
        }

        foreach (var link in plan.CustomApiLinks)
            _prompt.WriteLine($"[{link.Action}] Custom API {link.CustomApi.UniqueName} main handler -> {link.Declared.PluginTypeName}{(link.BlockedReason is null ? string.Empty : " - " + link.BlockedReason)}");

        foreach (var obsolete in plan.ObsoleteSteps)
            _prompt.WriteLine($"[Obsolete, not deleted] step {obsolete.Name}");

        foreach (var obsolete in plan.ObsoleteImages)
            _prompt.WriteLine($"[Obsolete, not deleted] {obsolete}");

        _prompt.WriteLine();
        foreach (var association in associations) _prompt.WriteLine(TargetSolution.Describe(association, solution.UniqueName));
        if (associations.Count == 0 && !plan.IsBlocked)
            _prompt.WriteLine($"[Solution] all plug-in components are already in {solution.UniqueName}");
        _prompt.WriteLine();
    }

    private Task Fail(string area, IEnumerable<string> errors)
    {
        _prompt.WriteLine($"{area} is not valid; nothing was deployed:");
        foreach (var error in errors) _prompt.WriteLine($"  - {error}");
        return Task.CompletedTask;
    }
}
