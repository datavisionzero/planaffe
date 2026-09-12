using Planaffe.Domain;

namespace Planaffe.UnitTests;

/// <summary>
/// What an address that is a name may look like (<c>CONTEXT.md</c>, Slug;
/// ADR 0021). A page carries one and so does a space, which is why the form
/// belongs to neither of them (ADR 0027).
/// </summary>
public sealed class SlugTests
{
    [Theory]
    [InlineData("architecture")]
    [InlineData("betriebshandbuch")]
    [InlineData("adr-0021")]
    [InlineData("a")]
    [InlineData("7")]
    [InlineData("one-two-three")]
    public void A_slug_is_lower_case_words_joined_by_single_hyphens(string slug) =>
        Assert.Equal(slug, Slug.Normalize(slug));

    [Theory]
    [InlineData("")]
    [InlineData("-architecture")]
    [InlineData("architecture-")]
    [InlineData("two--hyphens")]
    [InlineData("Architecture")]
    [InlineData("with space")]
    [InlineData("with_underscore")]
    [InlineData("with/slash")]
    [InlineData("with.dot")]
    [InlineData("umlaut-ä")]
    public void Everything_else_is_not_a_slug(string slug) =>
        Assert.Throws<ArgumentException>(() => Slug.Normalize(slug));

    [Fact]
    public void A_slug_has_a_ceiling()
    {
        Assert.Equal(Slug.MaxLength, Slug.Normalize(new string('a', Slug.MaxLength)).Length);
        Assert.Throws<ArgumentException>(() => Slug.Normalize(new string('a', Slug.MaxLength + 1)));
    }

    [Fact]
    public void Surrounding_space_is_not_part_of_the_address() =>
        Assert.Equal("architecture", Slug.Normalize("  architecture  "));
}
