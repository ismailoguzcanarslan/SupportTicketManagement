using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;

namespace SupportTicket.Base.Changes;

/// <summary>
/// One record as a plug-in sees it: the columns the caller sent (<see cref="Target"/>), the record before
/// the operation (<see cref="Previous"/>, from the pre-image) and both merged (<see cref="Current"/>).
/// Answers "did this column really change?" and "what is the value now?" without each plug-in
/// re-implementing it. Create one per execution with <c>context.GetChange&lt;T&gt;()</c>.
/// </summary>
/// <remarks>
/// <para>
/// Update: the Target holds only the columns that were sent, and a column can be sent with its current
/// value. <see cref="HasChanged"/> therefore compares the sent value with the pre-image; register a
/// pre-image that contains the columns you check. Without a pre-image every sent column counts as changed,
/// because the old value is unknown.
/// </para>
/// <para>
/// Dataverse leaves columns without a value out of images and retrieved records. A column that the
/// pre-image does not contain is therefore treated as having been empty.
/// </para>
/// <para>Create: there is no previous record, so a column changed when it was sent with a value.</para>
/// <para>Delete: there is no Target; <see cref="Current"/> is the pre-image and nothing changed.</para>
/// </remarks>
public sealed class EntityChange<T>
    where T : Entity
{
    private readonly Func<string[], Entity?>? _retrieveColumns;
    private readonly HashSet<string> _knownColumns = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="messageName">SDK message, e.g. <see cref="Registration.MessageName.Update"/>.</param>
    /// <param name="entityLogicalName">Table of the record.</param>
    /// <param name="id">Record id; <see cref="Guid.Empty"/> for a record that does not exist yet.</param>
    /// <param name="target">Columns sent with the request; null for Delete.</param>
    /// <param name="preImage">The record before the operation; null when no pre-image was registered.</param>
    /// <param name="retrieveColumns">
    /// Reads the given columns of the stored record, used by <see cref="EnsureColumns"/>. Null when the record
    /// cannot be read (e.g. before it is created).
    /// </param>
    public EntityChange(
        string messageName,
        string entityLogicalName,
        Guid id,
        Entity? target,
        Entity? preImage,
        Func<string[], Entity?>? retrieveColumns = null)
    {
        MessageName = messageName ?? throw new ArgumentNullException(nameof(messageName));
        _retrieveColumns = retrieveColumns;

        var current = new Entity(entityLogicalName, id);
        Copy(preImage, current);
        Copy(target, current);

        Current = current.ToEntity<T>();
        Previous = preImage?.ToEntity<T>();
        Target = target?.ToEntity<T>();
    }

    public string MessageName { get; }

    /// <summary>The columns sent with the request; null for Delete.</summary>
    public T? Target { get; }

    /// <summary>The record before the operation (pre-image); null for Create or without a pre-image.</summary>
    public T? Previous { get; }

    /// <summary>
    /// The record as it will be after the operation: <see cref="Target"/> over <see cref="Previous"/>, plus any
    /// columns read by <see cref="EnsureColumns"/>. Columns that none of them contain are missing, not empty.
    /// </summary>
    public T Current { get; }

    /// <summary>True when the request sent this column, whether or not its value differs.</summary>
    public bool IsSent(string column) => Target?.Contains(column) == true;

    /// <summary>True when the column was sent and its value differs from the value before the operation.</summary>
    public bool HasChanged(string column)
    {
        if (!IsSent(column)) return false;

        var sent = Target![column];
        if (MessageName == Registration.MessageName.Create) return !ValueComparer.AreEqual(sent, null);
        if (Previous is null) return true;

        return !ValueComparer.AreEqual(sent, PreviousValue(column));
    }

    /// <summary>True when the column changed and its new value equals <paramref name="value"/>.</summary>
    /// <remarks>Choices can be given as OptionSetValue, int or early-bound enum.</remarks>
    public bool HasChangedTo(string column, object? value) =>
        HasChanged(column) && ValueComparer.AreEqual(Target![column], value);

    /// <summary>
    /// True when the column changed and its value before the operation equals <paramref name="value"/>.
    /// Always false on Update without a pre-image, because the old value is unknown.
    /// </summary>
    public bool HasChangedFrom(string column, object? value) =>
        HasChanged(column)
        && (Previous is not null || MessageName == Registration.MessageName.Create)
        && ValueComparer.AreEqual(PreviousValue(column), value);

    /// <summary>True when any of the columns changed.</summary>
    public bool HasAnyChanged(params string[] columns) => columns.Any(HasChanged);

    /// <summary>Value of <see cref="Current"/>, or default when it is missing.</summary>
    public TValue? GetValue<TValue>(string column) => Current.GetAttributeValue<TValue>(column);

    /// <summary>Value before the operation, or default when there is no pre-image or it was empty.</summary>
    public TValue? GetPreviousValue<TValue>(string column) =>
        Previous is null ? default : Previous.GetAttributeValue<TValue>(column);

    /// <summary>
    /// Makes sure <see cref="Current"/> has the given columns: those not sent and not in the pre-image are read
    /// from the stored record in one request. Does nothing when all are known or the record cannot be read.
    /// Each column is read at most once per execution.
    /// </summary>
    /// <returns>This instance, so calls can be chained.</returns>
    public EntityChange<T> EnsureColumns(params string[] columns)
    {
        var missing = columns
            .Where(column => !Current.Contains(column) && !_knownColumns.Contains(column))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (missing.Length == 0 || _retrieveColumns is null || Current.Id == Guid.Empty) return this;

        var stored = _retrieveColumns(missing);
        foreach (var column in missing)
        {
            _knownColumns.Add(column);
            // A column without a value is not returned; it stays missing, which reads as empty.
            if (stored is not null && stored.Contains(column)) Current[column] = stored[column];
        }

        return this;
    }

    private object? PreviousValue(string column) =>
        Previous is not null && Previous.Contains(column) ? Previous[column] : null;

    private static void Copy(Entity? source, Entity destination)
    {
        if (source is null) return;
        foreach (var attribute in source.Attributes) destination[attribute.Key] = attribute.Value;
    }
}
