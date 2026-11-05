using System;
using System.Linq;
using Microsoft.Xrm.Sdk;

namespace SupportTicket.Base.Changes;

/// <summary>
/// Compares Dataverse column values by what they mean, not by object identity. The SDK types
/// (OptionSetValue, Money, EntityReference, ...) are classes, so <c>==</c> and even
/// <see cref="object.Equals(object)"/> would report two equal choices as different.
/// </summary>
/// <remarks>
/// Rules:
/// <list type="bullet">
/// <item>null and "" are the same text: Dataverse stores an empty string as null.</item>
/// <item>A choice equals another choice, its int value or an early-bound enum with that value,
/// so <c>HasChangedTo(column, ogz_support_ticket_ogz_priority.Critical)</c> works.</item>
/// <item>Money equals Money or a decimal with the same amount.</item>
/// <item>Lookups are equal when the ids match and, when both name a table, the tables match.</item>
/// <item>Multi-select choices are equal when they hold the same values, in any order.</item>
/// <item><see cref="AliasedValue"/> is compared by the value it wraps.</item>
/// </list>
/// </remarks>
public static class ValueComparer
{
    public static bool AreEqual(object? left, object? right)
    {
        left = Normalize(left);
        right = Normalize(right);

        if (left is null || right is null) return left is null && right is null;

        if (left is EntityReference leftReference && right is EntityReference rightReference)
        {
            return leftReference.Id == rightReference.Id
                && (string.IsNullOrEmpty(leftReference.LogicalName)
                    || string.IsNullOrEmpty(rightReference.LogicalName)
                    || string.Equals(leftReference.LogicalName, rightReference.LogicalName, StringComparison.OrdinalIgnoreCase));
        }

        if (left is OptionSetValueCollection leftChoices && right is OptionSetValueCollection rightChoices)
        {
            var leftValues = leftChoices.Select(choice => choice.Value).OrderBy(value => value);
            var rightValues = rightChoices.Select(choice => choice.Value).OrderBy(value => value);
            return leftValues.SequenceEqual(rightValues);
        }

        return left.Equals(right);
    }

    private static object? Normalize(object? value) =>
        value switch
        {
            null => null,
            AliasedValue aliased => Normalize(aliased.Value),
            string text => text.Length == 0 ? null : text,
            OptionSetValue choice => choice.Value,
            Enum choice => Convert.ToInt32(choice),
            Money money => money.Value,
            OptionSetValueCollection { Count: 0 } => null,
            _ => value,
        };
}
