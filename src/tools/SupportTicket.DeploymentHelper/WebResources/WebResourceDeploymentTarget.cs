using SupportTicket.DeploymentHelper.Configuration;
using SupportTicket.DeploymentHelper.Deployment;
using SupportTicket.DeploymentHelper.Forms;
using SupportTicket.DeploymentHelper.Frontend;

namespace SupportTicket.DeploymentHelper.WebResources;

/// <summary>
/// Menu option 1: builds the TypeScript Web Resources, then deploys them into the main solution and,
/// when the manifest lists forms, registers their form library and OnLoad/OnSave handlers and adds
/// those forms to the solution. Web Resources and forms are confirmed separately, then published with
/// one targeted PublishXml (only the changed Web Resources and the tables of the changed forms, never
/// PublishAll), then verified against the published state.
/// </summary>
public sealed class WebResourceDeploymentTarget : IDeploymentTarget
{
    private readonly DeploymentSettings _settings;
    private readonly DeploymentManifest _manifest;
    private readonly ManifestValidator _validator;
    private readonly IFrontendBuild _build;
    private readonly WebResourceRepository _repository;
    private readonly SolutionRepository _solutions;
    private readonly IDataverseSession _session;
    private readonly IUserPrompt _prompt;

    public WebResourceDeploymentTarget(
        DeploymentSettings settings,
        DeploymentManifest manifest,
        ManifestValidator validator,
        IFrontendBuild build,
        WebResourceRepository repository,
        SolutionRepository solutions,
        IDataverseSession session,
        IUserPrompt prompt)
    {
        _settings = settings;
        _manifest = manifest;
        _validator = validator;
        _build = build;
        _repository = repository;
        _solutions = solutions;
        _session = session;
        _prompt = prompt;
    }

    public int MenuKey => 1;

    public string Title => "Deploy Web Resources";

    public bool IsAvailable => true;

    public async Task RunAsync(DeploymentRunOptions options, CancellationToken cancellationToken)
    {
        // Build first (also in dry-run): a failed build stops before Dataverse is touched, so a
        // stale bundle can never be deployed.
        var build = await _build.BuildAsync(_manifest, cancellationToken);
        foreach (var message in build.Messages) _prompt.WriteLine(message);
        if (!build.Succeeded)
        {
            _prompt.WriteLine("TypeScript build failed; nothing was deployed.");
            return;
        }

        _prompt.WriteLine();
        await Deploy(options);
    }

    private Task Deploy(DeploymentRunOptions options)
    {
        var errors = _validator.ValidateWebResources(_manifest);
        if (errors.Count > 0)
        {
            _prompt.WriteLine("Manifest is not valid; nothing was deployed:");
            foreach (var error in errors) _prompt.WriteLine($"  - {error}");
            return Task.CompletedTask;
        }

        var local = _manifest.WebResources.Select(entry => new LocalWebResource(
            entry.Name,
            string.IsNullOrWhiteSpace(entry.DisplayName) ? null : entry.DisplayName.Trim(),
            WebResourceType.FromFileName(entry.Name)!.Value,
            File.ReadAllBytes(_validator.ResolveInRepository(entry.File)!))).ToList();

        // Connects (and shows the environment) on first use.
        if (TargetSolution.Resolve(_solutions, _settings, _prompt) is not { } solution) return Task.CompletedTask;

        // Concrete target forms: explicit ids from the manifest, plus the forms the user selects now
        // (also in dry-run), before anything is planned.
        var forms = new FormTargetResolver(_repository, _prompt).Resolve(_manifest.Forms);

        // Plan against the latest saved copies (what an update replaces) and compare with the published
        // ones, so changes that were saved but never published are published now.
        var names = local.Select(l => l.Name).ToList();
        var webResourcePlan = WebResourcePlanner.Plan(local,
            _repository.GetWebResources(names, CustomizationState.Latest),
            _repository.GetWebResources(names, CustomizationState.Published));
        var formPlan = PlanForms(forms);
        var members = _solutions.GetMembers(solution.Id,
            webResourcePlan.Where(p => p.ExistingId is not null).Select(p => p.ExistingId!.Value)
                .Concat(forms.Select(f => Guid.Parse(f.FormId))));
        var tables = _solutions.GetTableMembers(solution.Id, formPlan.Where(f => f.Action != PlannedAction.Blocked).Select(f => f.Entry.Entity));
        var webResourceAssociations = WebResourceSolutionPlanner.PlanWebResources(webResourcePlan, members);
        var formAssociations = WebResourceSolutionPlanner.PlanForms(formPlan, members, tables);

        Print(webResourcePlan, formPlan, webResourceAssociations.Concat(formAssociations).ToList(), members, tables, solution);

        var webResourceChanges = webResourcePlan.Where(p => p.Action is PlannedAction.Create or PlannedAction.Update).ToList();
        var webResourcePublishes = webResourcePlan.Where(p => p.PublishPending).ToList();
        var webResourceAdds = webResourceAssociations.Where(a => a.Action == AssociationAction.Add).ToList();
        var formChanges = formPlan.Where(p => p.Action == PlannedAction.Update || p.PublishPending).ToList();
        var formAdds = formAssociations.Where(a => a.Action == AssociationAction.Add).ToList();

        if (webResourceChanges.Count + webResourcePublishes.Count + webResourceAdds.Count + formChanges.Count + formAdds.Count == 0)
        {
            _prompt.WriteLine($"Nothing to deploy: everything is up to date and in {solution.UniqueName}.");
            return Task.CompletedTask;
        }

        if (options.DryRunOnly)
        {
            _prompt.WriteLine("Dry run: no changes made.");
            return Task.CompletedTask;
        }

        var report = new DeploymentReport();
        var publishedIds = new List<Guid>();
        var deployed = new List<LocalWebResource>();
        var blockedLibraries = webResourcePlan.Where(p => p.Action == PlannedAction.Blocked).Select(p => p.Local.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var webResourceWork = webResourceChanges.Count + webResourcePublishes.Count + webResourceAdds.Count;
        if (webResourceWork > 0
            && _prompt.ConfirmByTypingEnvironment(
                $"Deploy {webResourceChanges.Count} Web Resource change(s), publish {webResourcePublishes.Count} saved but unpublished Web Resource(s) " +
                $"and add {webResourceAdds.Count} existing Web Resource(s) to {solution.UniqueName} on {_session.Description}?",
                _session.EnvironmentName))
        {
            ApplyWebResources(webResourcePlan, webResourceAdds, solution, publishedIds, blockedLibraries, report);
            deployed.AddRange(webResourcePlan.Where(p => p.Action != PlannedAction.Blocked).Select(p => p.Local));
        }
        else if (webResourceWork > 0)
        {
            _prompt.WriteLine("Web Resource deployment cancelled.");
            blockedLibraries.UnionWith(webResourceChanges.Where(c => c.Action == PlannedAction.Create).Select(c => c.Local.Name));
        }

        var publishEntities = new List<string>();
        var formsToDeploy = formPlan
            .Where(f => formChanges.Contains(f) || formAdds.Any(a => a.ObjectId == Guid.Parse(f.Entry.FormId)))
            .ToList();
        var deployableForms = formsToDeploy.Where(f => !blockedLibraries.Contains(f.Entry.Library)).ToList();
        foreach (var skipped in formsToDeploy.Except(deployableForms)) report.Skipped($"{skipped.Label}: library {skipped.Entry.Library} is not deployed");

        var formsDeployed = false;
        if (deployableForms.Count > 0
            && _prompt.ConfirmByTypingEnvironment($"Modify, publish or add {deployableForms.Count} form(s) to {solution.UniqueName} as listed above?", _session.EnvironmentName))
        {
            ApplyForms(deployableForms, formAdds, solution, publishEntities, report);
            formsDeployed = true;
        }

        try
        {
            _repository.Publish(publishedIds, publishEntities);
            if (publishedIds.Count + publishEntities.Count > 0)
                _prompt.WriteLine($"Published {publishedIds.Count} Web Resource(s) and {publishEntities.Count} table(s).");
        }
        catch (Exception ex)
        {
            report.Failed("publish", ex);
        }

        report.Print(_prompt);

        if (deployed.Count > 0 || formsDeployed)
            Verify(deployed, formsDeployed ? deployableForms : [], solution);

        return Task.CompletedTask;
    }

    private List<FormPlanItem> PlanForms(IEnumerable<FormEntry> forms) =>
        forms
            .Select(form =>
            {
                var id = Guid.Parse(form.FormId);
                return FormEventPlanner.Plan(form,
                    _repository.GetForm(id, CustomizationState.Latest),
                    _repository.GetForm(id, CustomizationState.Published),
                    Guid.NewGuid);
            })
            .ToList();

    private void ApplyWebResources(
        IReadOnlyList<WebResourcePlanItem> plan,
        IReadOnlyList<SolutionAssociation> adds,
        SolutionInfo solution,
        List<Guid> publishedIds,
        HashSet<string> blockedLibraries,
        DeploymentReport report)
    {
        foreach (var item in plan)
        {
            try
            {
                switch (item.Action)
                {
                    case PlannedAction.Create:
                        publishedIds.Add(_repository.Create(item, solution.UniqueName));
                        report.Created(item.Local.Name);
                        break;
                    case PlannedAction.Update:
                        _repository.Update(item.ExistingId!.Value, item);
                        publishedIds.Add(item.ExistingId.Value);
                        report.Updated(item.Local.Name);
                        break;
                    case PlannedAction.Blocked:
                        report.Skipped($"{item.Local.Name}: {item.Reason}");
                        break;
                    case PlannedAction.Unchanged when item.PublishPending:
                        publishedIds.Add(item.ExistingId!.Value);
                        report.Unchanged($"{item.Local.Name} (published)");
                        break;
                    default:
                        report.Unchanged(item.Local.Name);
                        break;
                }
            }
            catch (Exception ex)
            {
                blockedLibraries.Add(item.Local.Name);
                report.Failed(item.Local.Name, ex);
            }
        }

        AddToSolution(adds, solution, report);
    }

    private void ApplyForms(
        IReadOnlyList<FormPlanItem> forms,
        IReadOnlyList<SolutionAssociation> adds,
        SolutionInfo solution,
        List<string> publishEntities,
        DeploymentReport report)
    {
        foreach (var form in forms.Where(f => f.Action == PlannedAction.Update))
        {
            try
            {
                _repository.UpdateFormXml(Guid.Parse(form.Entry.FormId), form.UpdatedXml!);
                publishEntities.Add(form.Entry.Entity);
                report.Updated(form.Label);
            }
            catch (Exception ex)
            {
                report.Failed(form.Label, ex);
            }
        }

        // Forms are published per table; a saved but unpublished form needs no write, only that.
        foreach (var form in forms.Where(f => f.PublishPending))
        {
            publishEntities.Add(form.Entry.Entity);
            report.Unchanged($"{form.Label} (published)");
        }

        var formIds = forms.Select(f => Guid.Parse(f.Entry.FormId)).ToHashSet();
        AddToSolution(adds.Where(a => a.ObjectId is { } id && formIds.Contains(id)).ToList(), solution, report);
    }

    private void AddToSolution(IEnumerable<SolutionAssociation> adds, SolutionInfo solution, DeploymentReport report)
    {
        foreach (var association in adds)
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

    /// <summary>Reads the published state again: what users get must contain the deployment.</summary>
    private void Verify(IReadOnlyList<LocalWebResource> deployed, IReadOnlyList<FormPlanItem> forms, SolutionInfo solution)
    {
        var after = _repository.GetWebResources(deployed.Select(l => l.Name).ToList(), CustomizationState.Published);
        var formsAfter = forms
            .Select(f => FormEventPlanner.Plan(f.Entry, _repository.GetForm(Guid.Parse(f.Entry.FormId), CustomizationState.Published), Guid.NewGuid))
            .ToList();
        var members = _solutions.GetMembers(solution.Id,
            after.Values.Select(r => r.Id).Concat(forms.Select(f => Guid.Parse(f.Entry.FormId))));
        var tables = _solutions.GetTableMembers(solution.Id, forms.Select(f => f.Entry.Entity));

        TargetSolution.PrintVerification(_prompt,
            WebResourceDeploymentVerifier.Verify(deployed, after, formsAfter, members, tables, solution.UniqueName),
            solution.UniqueName);
    }

    private void Print(
        IReadOnlyList<WebResourcePlanItem> webResources,
        IReadOnlyList<FormPlanItem> forms,
        IReadOnlyList<SolutionAssociation> associations,
        IReadOnlyDictionary<Guid, SolutionMember> members,
        IReadOnlyDictionary<string, SolutionMember> tables,
        SolutionInfo solution)
    {
        foreach (var line in TargetSolution.Header(_session.EnvironmentName, solution)) _prompt.WriteLine(line);

        foreach (var item in webResources)
            _prompt.WriteLine($"[{item.DisplayAction}] {WebResourceSolutionPlanner.WebResourceLabel(item.Local.Name)} ({item.Reason}, {item.Local.Content.Length:N0} bytes)");

        foreach (var form in forms)
        {
            _prompt.WriteLine($"[{form.DisplayAction}] {form.Label}: {form.Reason}");
            foreach (var change in form.Changes) _prompt.WriteLine($"    + {change}");
        }

        _prompt.WriteLine();
        foreach (var form in forms.Where(f => f.Action != PlannedAction.Blocked
                     && !members.ContainsKey(Guid.Parse(f.Entry.FormId))
                     && FormMembership.IsIncludedByTable(f.Entry.Entity, tables)))
            _prompt.WriteLine($"[Solution] {WebResourceSolutionPlanner.FormLabel(form)}: in {solution.UniqueName} through its table {form.Entry.Entity} (all subcomponents)");
        foreach (var association in associations) _prompt.WriteLine(TargetSolution.Describe(association, solution.UniqueName));
        if (associations.Count == 0)
            _prompt.WriteLine($"[Solution] all Web Resources and forms are already in {solution.UniqueName}");
        _prompt.WriteLine();
    }
}
