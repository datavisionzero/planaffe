using Planaffe.Domain.Spaces;

namespace Planaffe.UnitTests;

/// <summary>
/// The rules the space holds by itself: that it is addressed by a name, that
/// its title is one line, that it starts open to agents, and that every change
/// moves the version (<c>CONTEXT.md</c>, Space; VISION 18, ADR 0027).
/// </summary>
public sealed class SpaceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Later = Now.AddHours(1);

    private static readonly Guid Author = Guid.CreateVersion7();

    [Fact]
    public void A_new_space_is_named_titled_and_open_to_agents()
    {
        var space = Space.Create("handbuch", "Handbuch", Author, Now);

        Assert.Equal("handbuch", space.Name);
        Assert.Equal("Handbuch", space.Title);
        Assert.False(space.ClosedToAgents);
        Assert.Equal(Author, space.CreatedBy);
        Assert.Equal(Now, space.CreatedAt);
        Assert.Equal(Now, space.UpdatedAt);
        Assert.False(space.Deleted);
    }

    [Theory]
    [InlineData("Handbuch")]
    [InlineData("with space")]
    [InlineData("with_underscore")]
    [InlineData("")]
    public void A_name_is_a_slug_like_a_pages(string name) =>
        Assert.Throws<ArgumentException>(() => Space.Create(name, "Handbuch", Author, Now));

    [Fact]
    public void A_title_is_one_line()
    {
        Assert.Throws<ArgumentException>(() => Space.Create("handbuch", "  ", Author, Now));
        Assert.Throws<ArgumentException>(() => Space.Create("handbuch", "two\nlines", Author, Now));
        Assert.Throws<ArgumentException>(() =>
            Space.Create("handbuch", new string('a', Space.TitleMaxLength + 1), Author, Now));
    }

    [Fact]
    public void Renaming_moves_the_address_and_the_version()
    {
        var space = Space.Create("handbuch", "Handbuch", Author, Now);

        space.Rename("firmenhandbuch", Later);

        Assert.Equal("firmenhandbuch", space.Name);
        Assert.Equal(Later, space.UpdatedAt);
    }

    [Fact]
    public void The_switch_closes_the_space_to_agents_and_opens_it_again()
    {
        var space = Space.Create("personal", "Personal", Author, Now);

        space.CloseToAgents(true, Later);
        Assert.True(space.ClosedToAgents);
        Assert.Equal(Later, space.UpdatedAt);

        space.CloseToAgents(false, Later.AddHours(1));
        Assert.False(space.ClosedToAgents);
    }

    [Fact]
    public void Deleting_is_soft_and_the_first_deletion_is_the_one_that_counts()
    {
        var space = Space.Create("handbuch", "Handbuch", Author, Now);

        space.Delete(Author, Now);
        space.Delete(Author, Later);

        Assert.True(space.Deleted);
        Assert.Equal(Now, space.DeletedAt);
        Assert.Equal(Author, space.DeletedBy);

        space.Restore();

        Assert.False(space.Deleted);
        Assert.Null(space.DeletedAt);
        Assert.Null(space.DeletedBy);
    }
}
