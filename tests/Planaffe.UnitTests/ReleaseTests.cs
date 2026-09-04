using Planaffe.Domain.Releases;

namespace Planaffe.UnitTests;

public sealed class ReleaseTests
{
    [Fact]
    public void Publishing_names_and_freezes_the_open_release()
    {
        var now = DateTimeOffset.Parse("2026-09-04T12:00:00Z");
        var by = Guid.NewGuid();
        var release = Release.Open(Guid.NewGuid(), now.AddHours(-1));

        release.Publish(" v1.0.0 ", "Notes.", by, now);

        Assert.Equal("v1.0.0", release.Name);
        Assert.Equal(ReleaseStatus.Published, release.Status);
        Assert.Equal("Notes.", release.Description);
        Assert.Equal(now, release.PublishedAt);
        Assert.Equal(by, release.PublishedBy);
        Assert.Throws<InvalidOperationException>(() => release.Publish("v2", null, by, now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("unreleased")]
    [InlineData("NONE")]
    [InlineData("two\nlines")]
    public void Invalid_and_reserved_names_are_refused(string name) =>
        Assert.Throws<ArgumentException>(() => Release.NormalizeName(name));
}
