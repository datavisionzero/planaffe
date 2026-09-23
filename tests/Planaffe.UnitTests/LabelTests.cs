using Planaffe.Domain.Projects;

namespace Planaffe.UnitTests;

/// <summary>A label and its normalization (VISION 8): what is kept, what is trimmed, what is refused.</summary>
public sealed class LabelTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Name_group_and_description_are_trimmed_and_a_blank_description_is_none()
    {
        var label = Label.Create(Guid.NewGuid(), " area:infra ", " area ", "   ", Now);

        Assert.Equal("area:infra", label.Name);
        Assert.Equal("area", label.Group);
        Assert.Null(label.Description);

        label.Describe("  Compose, CI, the image. ");
        Assert.Equal("Compose, CI, the image.", label.Description);
    }

    [Theory]
    [InlineData("One line\nand another")]
    [InlineData("One line\rand another")]
    public void A_description_is_one_line(string description) =>
        Assert.Throws<ArgumentException>(() => Label.Create(Guid.NewGuid(), "bug", null, description, Now));

    [Fact]
    public void A_description_has_a_limit()
    {
        Assert.Throws<ArgumentException>(() => Label.Create(Guid.NewGuid(), "bug", null, new string('x', Label.DescriptionMaxLength + 1), Now));
        Assert.Equal(Label.DescriptionMaxLength, Label.Create(Guid.NewGuid(), "bug", null, new string('x', Label.DescriptionMaxLength), Now).Description!.Length);
    }

    [Fact]
    public void Renaming_and_regrouping_normalize_and_a_null_group_leaves_the_group()
    {
        var label = Label.Create(Guid.NewGuid(), "bug", "kind", null, Now);

        label.Rename(" defect ");
        Assert.Equal("defect", label.Name);
        Assert.Throws<ArgumentException>(() => label.Rename("Defect"));

        label.Regroup(null);
        Assert.Null(label.Group);
        Assert.Equal("group", Assert.Throws<ArgumentException>(() => label.Regroup("Kind")).ParamName);
    }

    [Fact]
    public void Deleting_keeps_the_first_moment_and_restoring_clears_it()
    {
        var label = Label.Create(Guid.NewGuid(), "bug", "kind", null, Now);

        label.Delete(Now);
        label.Delete(Now.AddHours(1));
        Assert.True(label.Deleted);
        Assert.Equal(Now, label.DeletedAt);

        label.Restore();
        Assert.False(label.Deleted);
    }

    [Fact]
    public void Every_project_starts_with_the_kind_group()
    {
        var kind = Label.Kind(Guid.NewGuid(), Now);

        Assert.Equal(["bug", "feature", "chore"], kind.Select(l => l.Name));
        Assert.All(kind, l => Assert.Equal(Label.KindGroup, l.Group));
        Assert.All(kind, l => Assert.NotNull(l.Description));
    }
}
