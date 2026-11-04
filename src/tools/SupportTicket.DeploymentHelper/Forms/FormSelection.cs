using SupportTicket.DeploymentHelper.Configuration;
using SupportTicket.DeploymentHelper.Deployment;

namespace SupportTicket.DeploymentHelper.Forms;

/// <summary>A form found in Dataverse for a manifest entry without a form id.</summary>
public sealed record DiscoveredForm(Guid Id, string Name, string Entity, bool IsDefault, bool IsCustomizable);

/// <summary>Reads candidate forms from Dataverse. Abstracted so selection is testable offline.</summary>
public interface IFormDiscovery
{
    IReadOnlyList<DiscoveredForm> FindForms(string entity, int formType);
}

public sealed record FormSelectionResult(IReadOnlyList<DiscoveredForm> Selected, string? Error)
{
    public static FormSelectionResult None { get; } = new([], null);
}

/// <summary>Parses a menu answer such as "1", "1,2" or "2, 1". Pure.</summary>
public static class FormSelection
{
    /// <summary>Empty input selects nothing. Every number must be one of the listed forms.</summary>
    public static FormSelectionResult Parse(string? input, IReadOnlyList<DiscoveredForm> forms)
    {
        if (string.IsNullOrWhiteSpace(input)) return FormSelectionResult.None;

        var selected = new List<DiscoveredForm>();
        foreach (var part in input.Split(',', StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(part, out var number) || number < 1 || number > forms.Count)
                return new FormSelectionResult([], $"'{part}' is not a form number between 1 and {forms.Count}.");

            var form = forms[number - 1];
            if (!selected.Contains(form)) selected.Add(form);
        }

        return new FormSelectionResult(selected, null);
    }
}

/// <summary>
/// Turns the manifest's form entries into concrete forms. Entries with a form id are used as they
/// are; for entries without one, the matching forms are discovered and the user chooses which to
/// modify. Nothing is selected automatically, and the choice is not saved.
/// </summary>
public sealed class FormTargetResolver
{
    private const int MaxAttempts = 3;

    private readonly IFormDiscovery _discovery;
    private readonly IUserPrompt _prompt;

    public FormTargetResolver(IFormDiscovery discovery, IUserPrompt prompt)
    {
        _discovery = discovery;
        _prompt = prompt;
    }

    public IReadOnlyList<FormEntry> Resolve(IEnumerable<FormEntry> entries)
    {
        var resolved = new List<FormEntry>();

        foreach (var entry in entries)
        {
            if (!entry.IsDiscovery)
            {
                resolved.Add(entry);
                continue;
            }

            var forms = _discovery.FindForms(entry.Entity, FormTypes.Parse(entry.FormType)!.Value);
            var selected = Select(entry, forms);
            resolved.AddRange(selected.Select(form => entry.ForForm(form.Id)));

            foreach (var form in selected)
                _prompt.WriteLine($"[Selected] form '{form.Name}' ({form.Id}) gets {entry.Library}");
            if (selected.Count > 0)
                _prompt.WriteLine("This selection is used for this run only. To make it permanent for automation, add the formId to the manifest.");
            _prompt.WriteLine();
        }

        return resolved;
    }

    private IReadOnlyList<DiscoveredForm> Select(FormEntry entry, IReadOnlyList<DiscoveredForm> forms)
    {
        _prompt.WriteLine();
        if (forms.Count == 0)
        {
            _prompt.WriteLine($"No active {entry.FormType} forms found for {entry.Entity}; {entry.Library} is not registered on any form.");
            return [];
        }

        _prompt.WriteLine($"{entry.FormType} forms of {entry.Entity} (choose where to register {entry.Library}):");
        for (var i = 0; i < forms.Count; i++)
        {
            var form = forms[i];
            var notes = string.Join(", ", new[] { form.IsDefault ? "default" : null, form.IsCustomizable ? null : "not customizable" }.Where(n => n is not null));
            _prompt.WriteLine($"  {i + 1} - {form.Name} ({form.Id}){(notes.Length > 0 ? $" [{notes}]" : string.Empty)}");
        }

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var answer = _prompt.ReadLine("Form numbers, e.g. 1 or 1,2 (Enter = none): ");
            if (answer is null) return []; // end of input: never guess

            var result = FormSelection.Parse(answer, forms);
            if (result.Error is null)
            {
                if (result.Selected.Count == 0) _prompt.WriteLine($"No form selected; {entry.Library} is not registered on any form.");
                return result.Selected;
            }

            _prompt.WriteLine(result.Error);
        }

        _prompt.WriteLine($"No valid selection; {entry.Library} is not registered on any form.");
        return [];
    }
}
