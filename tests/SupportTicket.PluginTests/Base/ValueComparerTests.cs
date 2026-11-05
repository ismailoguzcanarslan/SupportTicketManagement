using System;
using Microsoft.Xrm.Sdk;
using SupportTicket.Base.Changes;
using SupportTicket.Entities;
using Xunit;

namespace SupportTicket.PluginTests.Base;

public class ValueComparerTests
{
    private static readonly Guid Id = Guid.NewGuid();

    [Fact]
    public void Null_EqualsNullAndEmptyText()
    {
        Assert.True(ValueComparer.AreEqual(null, null));
        Assert.True(ValueComparer.AreEqual(null, ""));
        Assert.True(ValueComparer.AreEqual("", null));
        Assert.False(ValueComparer.AreEqual(null, "text"));
        Assert.False(ValueComparer.AreEqual(new OptionSetValue(1), null));
    }

    [Fact]
    public void Text_IsComparedExactly()
    {
        Assert.True(ValueComparer.AreEqual("Printer", "Printer"));
        Assert.False(ValueComparer.AreEqual("Printer", "printer"));
    }

    [Fact]
    public void Choice_EqualsChoiceIntAndEnumWithTheSameValue()
    {
        var critical = (int)ogz_support_ticket_ogz_priority.Critical;

        Assert.True(ValueComparer.AreEqual(new OptionSetValue(critical), new OptionSetValue(critical)));
        Assert.True(ValueComparer.AreEqual(new OptionSetValue(critical), critical));
        Assert.True(ValueComparer.AreEqual(new OptionSetValue(critical), ogz_support_ticket_ogz_priority.Critical));
        Assert.False(ValueComparer.AreEqual(new OptionSetValue(critical), ogz_support_ticket_ogz_priority.High));
    }

    [Fact]
    public void Money_EqualsMoneyOrDecimalWithTheSameAmount()
    {
        Assert.True(ValueComparer.AreEqual(new Money(10.5m), new Money(10.50m)));
        Assert.True(ValueComparer.AreEqual(new Money(10.5m), 10.5m));
        Assert.False(ValueComparer.AreEqual(new Money(10m), new Money(11m)));
    }

    [Fact]
    public void Lookup_EqualsOnIdAndTable()
    {
        Assert.True(ValueComparer.AreEqual(new EntityReference("account", Id), new EntityReference("account", Id) { Name = "Contoso" }));
        Assert.True(ValueComparer.AreEqual(new EntityReference("account", Id), new EntityReference("Account", Id)));
        Assert.False(ValueComparer.AreEqual(new EntityReference("account", Id), new EntityReference("contact", Id)));
        Assert.False(ValueComparer.AreEqual(new EntityReference("account", Id), new EntityReference("account", Guid.NewGuid())));
    }

    [Fact]
    public void Lookup_WithoutTable_IsComparedOnIdOnly()
    {
        Assert.True(ValueComparer.AreEqual(new EntityReference { Id = Id }, new EntityReference("account", Id)));
    }

    [Fact]
    public void MultiSelectChoice_EqualsTheSameValuesInAnyOrder()
    {
        var oneTwo = new OptionSetValueCollection { new OptionSetValue(1), new OptionSetValue(2) };
        var twoOne = new OptionSetValueCollection { new OptionSetValue(2), new OptionSetValue(1) };
        var one = new OptionSetValueCollection { new OptionSetValue(1) };

        Assert.True(ValueComparer.AreEqual(oneTwo, twoOne));
        Assert.False(ValueComparer.AreEqual(oneTwo, one));
        Assert.True(ValueComparer.AreEqual(new OptionSetValueCollection(), null));
    }

    [Fact]
    public void AliasedValue_IsComparedByItsValue()
    {
        Assert.True(ValueComparer.AreEqual(new AliasedValue("account", "name", "Contoso"), "Contoso"));
    }

    [Fact]
    public void OtherValues_UseTheirOwnEquality()
    {
        var now = DateTime.UtcNow;

        Assert.True(ValueComparer.AreEqual(now, now));
        Assert.True(ValueComparer.AreEqual(true, true));
        Assert.False(ValueComparer.AreEqual(1, 2));
        Assert.False(ValueComparer.AreEqual(Id, Guid.NewGuid()));
    }
}
