using System;
using Microsoft.Xrm.Sdk;
using SupportTicket.Base.Changes;
using SupportTicket.Base.Registration;
using SupportTicket.Entities;
using Xunit;
using Fields = SupportTicket.Entities.ogz_support_ticket.Fields;
using Priority = SupportTicket.Entities.ogz_support_ticket_ogz_priority;

namespace SupportTicket.PluginTests.Base;

public class EntityChangeTests
{
    private static readonly Guid Id = Guid.NewGuid();

    private static Entity Ticket(params (string Column, object? Value)[] columns)
    {
        var ticket = new Entity(ogz_support_ticket.EntityLogicalName, Id);
        foreach (var (column, value) in columns) ticket[column] = value;
        return ticket;
    }

    private static OptionSetValue Choice(Priority priority) => new((int)priority);

    private static EntityChange<ogz_support_ticket> Update(Entity target, Entity? preImage, Func<string[], Entity?>? retrieve = null) =>
        new(MessageName.Update, ogz_support_ticket.EntityLogicalName, Id, target, preImage, retrieve);

    // --- Update with a pre-image -------------------------------------------------------------

    [Fact]
    public void Update_SentWithADifferentValue_HasChanged()
    {
        var change = Update(Ticket((Fields.ogz_priority, Choice(Priority.Critical))), Ticket((Fields.ogz_priority, Choice(Priority.Normal))));

        Assert.True(change.IsSent(Fields.ogz_priority));
        Assert.True(change.HasChanged(Fields.ogz_priority));
    }

    [Fact]
    public void Update_SentWithTheSameValue_IsSentButNotChanged()
    {
        var change = Update(Ticket((Fields.ogz_priority, Choice(Priority.Normal))), Ticket((Fields.ogz_priority, Choice(Priority.Normal))));

        Assert.True(change.IsSent(Fields.ogz_priority));
        Assert.False(change.HasChanged(Fields.ogz_priority));
    }

    [Fact]
    public void Update_NotSent_HasNotChanged()
    {
        var change = Update(Ticket((Fields.ogz_ticket_title, "New title")), Ticket((Fields.ogz_priority, Choice(Priority.Normal))));

        Assert.False(change.IsSent(Fields.ogz_priority));
        Assert.False(change.HasChanged(Fields.ogz_priority));
    }

    [Fact]
    public void Update_ClearingAValue_HasChanged()
    {
        var change = Update(Ticket((Fields.ogz_description, null)), Ticket((Fields.ogz_description, "Printer offline")));

        Assert.True(change.HasChanged(Fields.ogz_description));
        Assert.True(change.HasChangedTo(Fields.ogz_description, null));
    }

    [Fact]
    public void Update_FillingAColumnTheImageLeftOutAsEmpty_HasChanged()
    {
        // Images leave out columns without a value: missing means it was empty.
        var change = Update(Ticket((Fields.ogz_description, "Printer offline")), Ticket());

        Assert.True(change.HasChanged(Fields.ogz_description));
        Assert.True(change.HasChangedFrom(Fields.ogz_description, null));
    }

    [Fact]
    public void Update_EmptyTextOverNull_HasNotChanged()
    {
        var change = Update(Ticket((Fields.ogz_description, "")), Ticket());

        Assert.False(change.HasChanged(Fields.ogz_description));
    }

    [Fact]
    public void HasChangedTo_And_HasChangedFrom_CheckTheNewAndTheOldValue()
    {
        var change = Update(Ticket((Fields.ogz_priority, Choice(Priority.Critical))), Ticket((Fields.ogz_priority, Choice(Priority.Normal))));

        Assert.True(change.HasChangedTo(Fields.ogz_priority, Priority.Critical));
        Assert.True(change.HasChangedTo(Fields.ogz_priority, (int)Priority.Critical));
        Assert.False(change.HasChangedTo(Fields.ogz_priority, Priority.High));
        Assert.True(change.HasChangedFrom(Fields.ogz_priority, Priority.Normal));
        Assert.False(change.HasChangedFrom(Fields.ogz_priority, Priority.Low));
    }

    [Fact]
    public void HasChangedTo_IsFalseWhenTheValueWasAlreadyThere()
    {
        var change = Update(Ticket((Fields.ogz_priority, Choice(Priority.Critical))), Ticket((Fields.ogz_priority, Choice(Priority.Critical))));

        Assert.False(change.HasChangedTo(Fields.ogz_priority, Priority.Critical));
    }

    [Fact]
    public void HasAnyChanged_IsTrueWhenOneOfTheColumnsChanged()
    {
        var change = Update(Ticket((Fields.ogz_ticket_title, "New")), Ticket((Fields.ogz_ticket_title, "Old")));

        Assert.True(change.HasAnyChanged(Fields.ogz_priority, Fields.ogz_ticket_title));
        Assert.False(change.HasAnyChanged(Fields.ogz_priority, Fields.ogz_description));
    }

    [Fact]
    public void Current_IsTheTargetOverThePreImage()
    {
        var change = Update(
            Ticket((Fields.ogz_priority, Choice(Priority.Critical))),
            Ticket((Fields.ogz_priority, Choice(Priority.Normal)), (Fields.ogz_description, "Printer offline")));

        Assert.Equal(Priority.Critical, change.Current.ogz_priority);
        Assert.Equal("Printer offline", change.Current.ogz_description);
        Assert.Equal(Priority.Normal, change.Previous!.ogz_priority);
        Assert.Equal(Id, change.Current.Id);
        Assert.Equal("Printer offline", change.GetValue<string>(Fields.ogz_description));
        Assert.Equal(Choice(Priority.Normal).Value, change.GetPreviousValue<OptionSetValue>(Fields.ogz_priority)!.Value);
    }

    [Fact]
    public void Current_DoesNotChangeTheTargetOrTheImage()
    {
        var target = Ticket((Fields.ogz_priority, Choice(Priority.Critical)));
        var preImage = Ticket((Fields.ogz_description, "Printer offline"));

        Update(target, preImage);

        Assert.False(target.Contains(Fields.ogz_description));
        Assert.False(preImage.Contains(Fields.ogz_priority));
    }

    // --- Update without a pre-image ----------------------------------------------------------

    [Fact]
    public void Update_WithoutPreImage_EverySentColumnCountsAsChanged()
    {
        var change = Update(Ticket((Fields.ogz_priority, Choice(Priority.Normal))), preImage: null);

        Assert.True(change.HasChanged(Fields.ogz_priority));
        Assert.True(change.HasChangedTo(Fields.ogz_priority, Priority.Normal));
        Assert.False(change.HasChangedFrom(Fields.ogz_priority, null));
        Assert.Null(change.Previous);
        Assert.Null(change.GetPreviousValue<OptionSetValue>(Fields.ogz_priority));
    }

    // --- Create and Delete -------------------------------------------------------------------

    [Fact]
    public void Create_AColumnSentWithAValueHasChanged()
    {
        var target = Ticket((Fields.ogz_ticket_title, "Printer offline"), (Fields.ogz_description, null));
        var change = new EntityChange<ogz_support_ticket>(MessageName.Create, ogz_support_ticket.EntityLogicalName, Guid.Empty, target, preImage: null);

        Assert.True(change.HasChanged(Fields.ogz_ticket_title));
        Assert.True(change.HasChangedFrom(Fields.ogz_ticket_title, null));
        Assert.False(change.HasChanged(Fields.ogz_description));
        Assert.False(change.HasChanged(Fields.ogz_priority));
    }

    [Fact]
    public void Delete_HasNoTargetAndCurrentIsThePreImage()
    {
        var change = new EntityChange<ogz_support_ticket>(
            MessageName.Delete, ogz_support_ticket.EntityLogicalName, Id, target: null, Ticket((Fields.ogz_ticket_title, "Printer offline")));

        Assert.Null(change.Target);
        Assert.False(change.IsSent(Fields.ogz_ticket_title));
        Assert.False(change.HasChanged(Fields.ogz_ticket_title));
        Assert.Equal("Printer offline", change.Current.ogz_ticket_title);
    }

    // --- EnsureColumns -----------------------------------------------------------------------

    [Fact]
    public void EnsureColumns_ReadsOnlyTheMissingColumnsInOneRequest()
    {
        string[]? requested = null;
        var calls = 0;
        var change = Update(
            Ticket((Fields.ogz_priority, Choice(Priority.Critical))),
            Ticket((Fields.ogz_ticket_title, "Printer offline")),
            columns =>
            {
                calls++;
                requested = columns;
                return Ticket((Fields.ogz_description, "Stored description"));
            });

        change.EnsureColumns(Fields.ogz_priority, Fields.ogz_ticket_title, Fields.ogz_description, Fields.ogz_ticket_number);

        Assert.Equal(1, calls);
        Assert.Equal(new[] { Fields.ogz_description, Fields.ogz_ticket_number }, requested);
        Assert.Equal("Stored description", change.Current.ogz_description);
        Assert.False(change.Current.Contains(Fields.ogz_ticket_number));
    }

    [Fact]
    public void EnsureColumns_ReadsEachColumnOnlyOnce_EvenWhenItHasNoValue()
    {
        var calls = 0;
        var change = Update(Ticket(), preImage: null, _ => { calls++; return Ticket(); });

        change.EnsureColumns(Fields.ogz_description).EnsureColumns(Fields.ogz_description, Fields.ogz_description);

        Assert.Equal(1, calls);
        Assert.Null(change.Current.ogz_description);
    }

    [Fact]
    public void EnsureColumns_DoesNotCallDataverseWhenEverythingIsKnown()
    {
        var calls = 0;
        var change = Update(Ticket((Fields.ogz_description, "Sent")), preImage: null, _ => { calls++; return null; });

        change.EnsureColumns(Fields.ogz_description);

        Assert.Equal(0, calls);
    }

    [Fact]
    public void EnsureColumns_DoesNothingWhenTheRecordCannotBeRead()
    {
        var withoutReader = Update(Ticket(), preImage: null);
        var calls = 0;
        var unsaved = new EntityChange<ogz_support_ticket>(
            MessageName.Create, ogz_support_ticket.EntityLogicalName, Guid.Empty, new Entity(ogz_support_ticket.EntityLogicalName), null,
            _ => { calls++; return null; });

        withoutReader.EnsureColumns(Fields.ogz_description);
        unsaved.EnsureColumns(Fields.ogz_description);

        Assert.False(withoutReader.Current.Contains(Fields.ogz_description));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void EnsureColumns_NeverOverwritesASentValue()
    {
        var change = Update(
            Ticket((Fields.ogz_description, "Sent")),
            preImage: null,
            _ => Ticket((Fields.ogz_description, "Stored"), (Fields.ogz_ticket_title, "Stored title")));

        change.EnsureColumns(Fields.ogz_description, Fields.ogz_ticket_title);

        Assert.Equal("Sent", change.Current.ogz_description);
        Assert.Equal("Stored title", change.Current.ogz_ticket_title);
    }
}
