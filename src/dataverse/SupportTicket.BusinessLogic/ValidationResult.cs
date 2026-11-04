using System.Collections.Generic;

namespace SupportTicket.BusinessLogic;

/// <summary>Outcome of a business validation. The caller decides how to surface the errors.</summary>
public sealed class ValidationResult
{
    private readonly List<string> _errors = new();

    public bool IsValid => _errors.Count == 0;

    public IReadOnlyList<string> Errors => _errors;

    internal void AddError(string message) => _errors.Add(message);
}
