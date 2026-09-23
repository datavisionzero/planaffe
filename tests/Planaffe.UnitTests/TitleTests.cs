using Planaffe.Domain.Epics;
using Planaffe.Domain.Issues;

namespace Planaffe.UnitTests;

/// <summary>A title is one line, whichever line break a client sends.</summary>
public sealed class TitleTests
{
    [Theory]
    [InlineData("First\nsecond")]
    [InlineData("First\rsecond")]
    [InlineData("First\r\nsecond")]
    public void A_title_that_spans_lines_is_refused(string title)
    {
        Assert.Throws<ArgumentException>(() => Issue.NormalizeTitle(title));
        Assert.Throws<ArgumentException>(() => Epic.NormalizeTitle(title));
    }
}
