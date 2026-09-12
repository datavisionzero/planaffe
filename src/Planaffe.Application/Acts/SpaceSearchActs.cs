using Planaffe.Application.Ports;
using Planaffe.Domain;

namespace Planaffe.Application.Acts;

/// <summary>One step of the way down to a hit: a page above it, and its address.</summary>
public sealed record SpacePageStep(string Path, string Title);

/// <summary>
/// One piece of an excerpt: a run of the body, and whether the search matched
/// it. A sequence of these is what an excerpt is, rather than a string with
/// markup in it — nothing the instance writes is put into a browser's tree as
/// HTML (ADR 0007), and a client that has to guess which words matched cannot
/// show them.
/// </summary>
public sealed record ExcerptSegment(string Text, bool Hit);

/// <summary>
/// One hit: the page, the space it is in, the way down to it, and the excerpt.
/// It carries no body — a hit is the way to a page, and the page is read
/// complete under its own address (ADR 0012).
/// </summary>
public sealed record SpacePageHitShape(
    string Path,
    string Space,
    string SpaceTitle,
    string Title,
    IReadOnlyList<SpacePageStep> Trail,
    IReadOnlyList<ExcerptSegment> Excerpt);

/// <summary>
/// One full-text search across the knowledge base: the titles and the bodies
/// of the pages, in the spaces the caller may see (VISION 18).
/// </summary>
/// <remarks>
/// <para>
/// It is access-true by construction and not by a filter somebody remembered.
/// The set of spaces comes from <see cref="SpaceScope"/> — the one place that
/// subtracts the spaces closed to an agent — and goes into the query, so a
/// closed space is absent from every row and from the number of rows alike
/// (ADR 0027).
/// </para>
/// <para>
/// It stays away from the tracker's search. Whoever searches the knowledge
/// base is asking a different question from whoever searches tickets, and one
/// list holding both would have to explain itself in every row.
/// </para>
/// </remarks>
public sealed class SearchSpacePages(ISpaces spaces, SpaceScope scope, ISpacePages pages, InstanceSettings settings)
{
    /// <summary>What a search answers with when nobody says how much.</summary>
    public const int DefaultLimit = 20;

    /// <summary>
    /// The most it answers with. A search is the best few, not a page of a
    /// list: there is no cursor here and deliberately none, because the
    /// answer to "not among these" is a better question and not a second page.
    /// </summary>
    public const int MaxLimit = 100;

    /// <param name="query">The words, as a search box takes them: <c>claim expired</c>, <c>"for update"</c>, <c>-flaky</c>.</param>
    /// <param name="space">One space by name, or nothing for the whole knowledge base.</param>
    /// <exception cref="Refusal"><c>validation</c> on <c>q</c> or <c>limit</c>; <c>not-found</c> for a space this caller has not got.</exception>
    public async Task<IReadOnlyList<SpacePageHitShape>> ExecuteAsync(
        string? query, string? space, int? limit, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw Refusal.Validation("q", "A search is a search for something: q carries the words to look for.");
        }

        var take = limit ?? DefaultLimit;
        if (take is < 1 || take > MaxLimit)
        {
            throw Refusal.Validation("limit", $"Between 1 and {MaxLimit} hits, and {take} is neither.");
        }

        // A space by name is resolved through the same lookup as everywhere
        // else, so one the caller has not got answers `not-found` rather than
        // an empty list: an empty list would say it exists and holds nothing.
        IReadOnlyCollection<Guid> ids = string.IsNullOrWhiteSpace(space)
            ? [.. await scope.SpaceIdsAsync(cancellationToken)]
            : [(await spaces.LiveAsync(scope, space, settings, cancellationToken)).Id];

        var hits = await pages.SearchAsync(ids, query, take, cancellationToken);

        return [.. hits.Select(Shape)];
    }

    private static SpacePageHitShape Shape(SpacePageHitRow hit) =>
        new(
            hit.Path,
            hit.Space,
            hit.SpaceTitle,
            hit.Title,
            [.. hit.TrailPaths.Zip(hit.TrailTitles, (path, title) => new SpacePageStep(path, title))],
            Excerpts.Of(hit.Headline));
}

/// <summary>
/// The excerpt as it arrives from the store — the body with
/// <see cref="Excerpt"/>'s two marks around what matched — turned into the
/// pieces that leave the instance.
/// </summary>
/// <remarks>
/// A mark without its partner is no mark: the body had both characters taken
/// out of it before the excerpt was cut, so an unbalanced one can only be an
/// excerpt cut through a match. The character goes and the words stay, because
/// dropping the piece would lose a word of the page.
/// </remarks>
public static class Excerpts
{
    public static IReadOnlyList<ExcerptSegment> Of(string? headline)
    {
        if (string.IsNullOrEmpty(headline))
        {
            return [];
        }

        var segments = new List<ExcerptSegment>();
        var at = 0;

        while (at < headline.Length)
        {
            var start = headline.IndexOf(Excerpt.Start, at);
            var stop = start < 0 ? -1 : headline.IndexOf(Excerpt.Stop, start + 1);

            if (start < 0 || stop < 0)
            {
                Add(headline[at..].Replace($"{Excerpt.Start}", string.Empty, StringComparison.Ordinal)
                    .Replace($"{Excerpt.Stop}", string.Empty, StringComparison.Ordinal), hit: false);
                break;
            }

            Add(headline[at..start], hit: false);
            Add(headline[(start + 1)..stop], hit: true);
            at = stop + 1;
        }

        return segments;

        void Add(string text, bool hit)
        {
            if (text.Length > 0)
            {
                segments.Add(new ExcerptSegment(text, hit));
            }
        }
    }
}
