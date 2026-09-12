using Planaffe.Domain.Spaces;

namespace Planaffe.UnitTests;

/// <summary>
/// The rules the knowledge base's page holds by itself: where it sits in the
/// tree, how deep it may go, that every change moves the version, and what a
/// deletion records about the subtree it went with (<c>CONTEXT.md</c>, Space
/// page; VISION 18, ADR 0028).
/// </summary>
public sealed class SpacePageTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Later = Now.AddHours(1);

    private static readonly Guid Author = Guid.CreateVersion7();

    private static readonly Guid Space = Guid.CreateVersion7();

    [Fact]
    public void A_page_without_a_parent_hangs_directly_under_the_space()
    {
        var page = Root("onboarding", "Onboarding");

        Assert.Equal(Space, page.SpaceId);
        Assert.Null(page.ParentId);
        Assert.Equal(0, page.Depth);
        Assert.Equal("onboarding", page.Slug);
        Assert.Equal("Onboarding", page.Title);
        Assert.Equal(string.Empty, page.Body);
        Assert.Equal(Author, page.CreatedBy);
        Assert.Equal(Now, page.CreatedAt);
        Assert.Equal(Now, page.UpdatedAt);
        Assert.False(page.Deleted);
    }

    [Fact]
    public void A_child_is_one_level_below_its_parent()
    {
        var parent = Root("company", "Company");

        var child = SpacePage.Create(Space, parent, "onboarding", "Onboarding", null, Author, Now);

        Assert.Equal(parent.Id, child.ParentId);
        Assert.Equal(1, child.Depth);
    }

    /// <summary>
    /// Three levels, and the limit is the type's rather than a caller's: the
    /// depth is derived from the parent and cannot be passed in (VISION 18).
    /// </summary>
    [Fact]
    public void A_fourth_level_is_refused()
    {
        var first = Root("company", "Company");
        var second = SpacePage.Create(Space, first, "handbook", "Handbook", null, Author, Now);
        var third = SpacePage.Create(Space, second, "onboarding", "Onboarding", null, Author, Now);

        Assert.Equal(SpacePage.MaxDepth, third.Depth);
        Assert.Throws<ArgumentException>(() =>
            SpacePage.Create(Space, third, "day-one", "Day one", null, Author, Now));
    }

    [Fact]
    public void A_parent_in_another_space_is_refused()
    {
        var elsewhere = SpacePage.Create(Guid.CreateVersion7(), null, "company", "Company", null, Author, Now);

        Assert.Throws<ArgumentException>(() =>
            SpacePage.Create(Space, elsewhere, "onboarding", "Onboarding", null, Author, Now));
    }

    [Theory]
    [InlineData("Onboarding")]
    [InlineData("with space")]
    [InlineData("with_underscore")]
    [InlineData("")]
    public void A_slug_is_a_slug(string slug) =>
        Assert.Throws<ArgumentException>(() => Root(slug, "Onboarding"));

    [Fact]
    public void A_title_is_one_line()
    {
        Assert.Throws<ArgumentException>(() => Root("onboarding", "  "));
        Assert.Throws<ArgumentException>(() => Root("onboarding", "two\nlines"));
        Assert.Throws<ArgumentException>(() => Root("onboarding", new string('a', SpacePage.TitleMaxLength + 1)));
    }

    [Fact]
    public void Renaming_retitling_and_rewriting_move_the_version_and_name_the_writer()
    {
        var second = Guid.CreateVersion7();
        var page = Root("onboarding", "Onboarding");

        page.Rename("einstieg", second, Later);
        Assert.Equal("einstieg", page.Slug);

        page.Retitle("Einstieg", second, Later);
        Assert.Equal("Einstieg", page.Title);

        page.Rewrite("Der Text.", second, Later);
        Assert.Equal("Der Text.", page.Body);

        Assert.Equal(Later, page.UpdatedAt);
        Assert.Equal(second, page.UpdatedBy);
    }

    [Fact]
    public void Rewriting_with_nothing_empties_the_document()
    {
        var page = SpacePage.Create(Space, null, "onboarding", "Onboarding", "Der Text.", Author, Now);

        page.Rewrite(null, Author, Later);

        Assert.Equal(string.Empty, page.Body);
    }

    [Fact]
    public void Moving_takes_the_depth_of_the_new_parent()
    {
        var company = Root("company", "Company");
        var handbook = SpacePage.Create(Space, company, "handbook", "Handbook", null, Author, Now);
        var page = Root("onboarding", "Onboarding");

        page.MoveUnder(Space, handbook, Author, Later);

        Assert.Equal(handbook.Id, page.ParentId);
        Assert.Equal(2, page.Depth);
        Assert.Equal(Later, page.UpdatedAt);
    }

    [Fact]
    public void Moving_to_the_root_of_another_space_takes_the_space_along()
    {
        var elsewhere = Guid.CreateVersion7();
        var page = Root("onboarding", "Onboarding");

        page.MoveUnder(elsewhere, null, Author, Later);

        Assert.Equal(elsewhere, page.SpaceId);
        Assert.Null(page.ParentId);
        Assert.Equal(0, page.Depth);
    }

    [Fact]
    public void Moving_under_a_parent_of_another_space_is_refused()
    {
        var elsewhere = SpacePage.Create(Guid.CreateVersion7(), null, "company", "Company", null, Author, Now);
        var page = Root("onboarding", "Onboarding");

        Assert.Throws<ArgumentException>(() => page.MoveUnder(Space, elsewhere, Author, Later));
    }

    /// <summary>
    /// What the act asks before it moves a subtree: how many levels are left
    /// below the new place, against how tall the subtree is.
    /// </summary>
    [Fact]
    public void Room_says_how_many_levels_are_left()
    {
        Assert.Equal(2, SpacePage.Room(0));
        Assert.Equal(1, SpacePage.Room(1));
        Assert.Equal(0, SpacePage.Room(SpacePage.MaxDepth));
    }

    [Fact]
    public void A_deletion_on_its_own_records_no_companion()
    {
        var page = Root("onboarding", "Onboarding");

        page.Delete(Author, Later);

        Assert.True(page.Deleted);
        Assert.Equal(Later, page.DeletedAt);
        Assert.Equal(Author, page.DeletedBy);
        Assert.Null(page.DeletedWith);
    }

    [Fact]
    public void A_page_that_went_with_a_subtree_names_the_page_it_went_with()
    {
        var parent = Root("company", "Company");
        var child = SpacePage.Create(Space, parent, "onboarding", "Onboarding", null, Author, Now);

        child.Delete(Author, Later, parent.Id);

        Assert.Equal(parent.Id, child.DeletedWith);
    }

    /// <summary>
    /// A page deleted on its own before its parent went keeps the deletion it
    /// had: that is what holds it back when the parent is restored.
    /// </summary>
    [Fact]
    public void A_second_deletion_changes_nothing()
    {
        var parent = Root("company", "Company");
        var child = SpacePage.Create(Space, parent, "onboarding", "Onboarding", null, Author, Now);
        child.Delete(Author, Now);

        child.Delete(Author, Later, parent.Id);

        Assert.Equal(Now, child.DeletedAt);
        Assert.Null(child.DeletedWith);
    }

    [Fact]
    public void Restoring_clears_all_three()
    {
        var page = Root("onboarding", "Onboarding");
        page.Delete(Author, Later, Guid.CreateVersion7());

        page.Restore();

        Assert.False(page.Deleted);
        Assert.Null(page.DeletedBy);
        Assert.Null(page.DeletedWith);
    }

    private static SpacePage Root(string slug, string title) =>
        SpacePage.Create(Space, null, slug, title, null, Author, Now);
}
