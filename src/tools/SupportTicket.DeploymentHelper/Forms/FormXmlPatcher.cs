using System.Xml;
using System.Xml.Linq;
using SupportTicket.DeploymentHelper.Configuration;

namespace SupportTicket.DeploymentHelper.Forms;

public sealed record FormPatchResult(bool Succeeded, string? Error, string? UpdatedXml, IReadOnlyList<string> Changes)
{
    public bool HasChanges => Changes.Count > 0;

    public static FormPatchResult Failed(string error) => new(false, error, null, []);
}

/// <summary>
/// Adds a form library and event handlers to FormXML incrementally. Only the missing
/// &lt;Library&gt; and &lt;Handler&gt; nodes are added; every other node, attribute and whitespace is kept.
/// Anything unexpected makes the patch fail instead of guessing.
/// </summary>
public static class FormXmlPatcher
{
    public static FormPatchResult Patch(string formXml, string library, IEnumerable<FormEventEntry> events, Func<Guid> newId)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(formXml, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException ex)
        {
            return FormPatchResult.Failed($"FormXML could not be parsed: {ex.Message}");
        }

        var form = document.Root;
        if (form is null || form.Name.LocalName != "form" || form.Name.Namespace != XNamespace.None)
            return FormPatchResult.Failed("FormXML root is not <form>.");

        var changes = new List<string>();

        var libraries = SingleOrCreate(form, "formLibraries", out var libraryError);
        if (libraryError is not null) return FormPatchResult.Failed(libraryError);

        var hasLibrary = libraries!.Elements("Library")
            .Any(l => string.Equals((string?)l.Attribute("name"), library, StringComparison.OrdinalIgnoreCase));
        if (!hasLibrary)
        {
            libraries.Add(new XElement("Library", new XAttribute("name", library), new XAttribute("libraryUniqueId", Braced(newId()))));
            changes.Add($"add form library {library}");
        }

        var eventsElement = SingleOrCreate(form, "events", out var eventsError);
        if (eventsError is not null) return FormPatchResult.Failed(eventsError);

        foreach (var formEvent in events)
        {
            // Custom handlers live in the non-application <event>; application="true" holds system handlers.
            var candidates = eventsElement!.Elements("event")
                .Where(e => (string?)e.Attribute("name") == formEvent.Event && (string?)e.Attribute("attribute") is null)
                .Where(e => !string.Equals((string?)e.Attribute("application"), "true", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (candidates.Count > 1)
                return FormPatchResult.Failed($"FormXML has {candidates.Count} custom '{formEvent.Event}' event nodes; fix the form manually.");

            var eventElement = candidates.SingleOrDefault();
            if (eventElement is null)
            {
                eventElement = new XElement("event",
                    new XAttribute("name", formEvent.Event),
                    new XAttribute("application", "false"),
                    new XAttribute("active", "false"));
                eventsElement.Add(eventElement);
            }

            var handlers = eventElement.Element("Handlers");
            if (handlers is null)
            {
                handlers = new XElement("Handlers");
                eventElement.AddFirst(handlers);
            }

            var exists = handlers.Elements("Handler").Any(h =>
                (string?)h.Attribute("functionName") == formEvent.Function
                && string.Equals((string?)h.Attribute("libraryName"), library, StringComparison.OrdinalIgnoreCase));
            if (exists) continue;

            handlers.Add(new XElement("Handler",
                new XAttribute("functionName", formEvent.Function),
                new XAttribute("libraryName", library),
                new XAttribute("handlerUniqueId", Braced(newId())),
                new XAttribute("enabled", "true"),
                new XAttribute("parameters", string.Empty),
                new XAttribute("passExecutionContext", formEvent.PassExecutionContext ? "true" : "false")));
            changes.Add($"add {formEvent.Event} handler {formEvent.Function}");
        }

        return changes.Count == 0
            ? new FormPatchResult(true, null, formXml, changes)
            : new FormPatchResult(true, null, document.Root!.ToString(SaveOptions.DisableFormatting), changes);
    }

    // Order of <form> children as Dataverse writes them; new sections are inserted after their predecessors.
    private static readonly string[] FormChildOrder = ["tabs", "header", "footer", "events", "DisplayConditions", "formLibraries"];

    private static XElement? SingleOrCreate(XElement form, string name, out string? error)
    {
        var existing = form.Elements(name).ToList();
        error = existing.Count > 1 ? $"FormXML has more than one <{name}>; fix the form manually." : null;
        if (existing.Count == 1) return existing[0];
        if (existing.Count > 1) return null;

        var created = new XElement(name);
        var predecessors = FormChildOrder.TakeWhile(n => n != name).ToHashSet(StringComparer.Ordinal);
        var anchor = form.Elements().LastOrDefault(e => predecessors.Contains(e.Name.LocalName));
        if (anchor is not null) anchor.AddAfterSelf(created);
        else form.AddFirst(created);
        return created;
    }

    private static string Braced(Guid id) => id.ToString("B");
}
