using Planaffe.Domain;
using Planaffe.Domain.Epics;

namespace Planaffe.UnitTests;

/// <summary>The epic as a bracket (VISION 7): closing and reopening, deleting and restoring.</summary>
public sealed class EpicTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void An_epic_closes_once_and_reopens_to_open()
    {
        var epic = Epic.Create(Guid.NewGuid(), 1, "Backend", Guid.NewGuid(), Now.AddDays(-1));
        Assert.False(epic.Closed);

        epic.Close(Now);
        Assert.True(epic.Closed);
        Assert.Equal(Now, epic.ClosedAt);
        Assert.Equal(Now, epic.UpdatedAt);
        Assert.Equal(RefusalCode.Transition, Assert.Throws<Refusal>(() => epic.Close(Now.AddHours(1))).Code);

        epic.Reopen(Now.AddHours(2));
        Assert.False(epic.Closed);
        Assert.Null(epic.ClosedAt);
        Assert.Equal(Now.AddHours(2), epic.UpdatedAt);
    }

    [Fact]
    public void Reopening_an_open_epic_changes_nothing()
    {
        var epic = Epic.Create(Guid.NewGuid(), 1, "Backend", Guid.NewGuid(), Now.AddDays(-1));

        epic.Reopen(Now);

        Assert.False(epic.Closed);
        Assert.Equal(Now.AddDays(-1), epic.UpdatedAt);
    }

    [Fact]
    public void Deleting_is_soft_once_and_restoring_brings_it_back()
    {
        var by = Guid.NewGuid();
        var epic = Epic.Create(Guid.NewGuid(), 1, "Backend", Guid.NewGuid(), Now.AddDays(-1));

        epic.Delete(by, Now);
        epic.Delete(Guid.NewGuid(), Now.AddHours(1));
        Assert.True(epic.Deleted);
        Assert.Equal(Now, epic.DeletedAt);
        Assert.Equal(by, epic.DeletedBy);

        epic.Restore();
        Assert.False(epic.Deleted);
        Assert.Null(epic.DeletedBy);
    }

    [Fact]
    public void A_number_is_drawn_from_one_and_a_title_is_one_trimmed_line()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Epic.Create(Guid.NewGuid(), 0, "Backend", Guid.NewGuid(), Now));
        Assert.Equal("Backend", Epic.Create(Guid.NewGuid(), 1, "  Backend ", Guid.NewGuid(), Now).Title);
        Assert.Throws<ArgumentException>(() => Epic.NormalizeTitle("   "));
        Assert.Throws<ArgumentException>(() => Epic.NormalizeTitle(new string('x', Epic.TitleMaxLength + 1)));
    }
}
