namespace Planaffe.Domain.Projects;

/// <summary>
/// How one project stands for the human who runs it (<c>CONTEXT.md</c>,
/// Standing): five steps, the worst applicable one winning, derived on read and
/// never stored.
/// </summary>
/// <remarks>
/// The order of the members is the order of the scale, best first, and the
/// comparison below relies on it.
/// </remarks>
public enum Standing
{
    /// <summary>Nothing is open at all.</summary>
    Clear,

    /// <summary>Nothing waits for a human, and an agent has work or is on it.</summary>
    Running,

    /// <summary>Nothing waits for a human, and no agent could take anything either.</summary>
    Idle,

    /// <summary>A little waits for a human, and none of it for long.</summary>
    Waiting,

    /// <summary>Much waits for a human, or something has waited too long.</summary>
    Neglected,
}

/// <summary>
/// The one thing that most explains a standing, so that a reader is sent to the
/// place it is repaired rather than to a colour.
/// </summary>
/// <remarks>
/// The first four are the four groups of "needs you", in its order, and they
/// name why a project is <see cref="Standing.Waiting"/> or
/// <see cref="Standing.Neglected"/>. The three after them are the three ways to
/// be <see cref="Standing.Idle"/>, and they are told apart because they are
/// repaired in three different places: an agent token is created, a blocker is
/// resolved, an issue is written or flagged.
/// </remarks>
public enum StandingBecause
{
    /// <summary>An open question waits for an answer.</summary>
    Question,

    /// <summary>An issue waits to be accepted.</summary>
    Review,

    /// <summary>Triage is required and an issue carries no <c>ready</c>.</summary>
    Unready,

    /// <summary>A chain of blockers ends somewhere no agent can pull.</summary>
    Stuck,

    /// <summary>The instance has no agent that could take anything.</summary>
    NoAgent,

    /// <summary>Nothing is workable, and something waits behind an open blocker.</summary>
    Blocked,

    /// <summary>There is work, and none of it is workable right now.</summary>
    NothingReady,

    /// <summary>An agent is on it, or could be.</summary>
    Working,

    /// <summary>There is nothing.</summary>
    Nothing,
}

/// <summary>
/// What one project's standing is read off: the four groups of "needs you", how
/// long the oldest of them has been waiting, what the work looks like, and
/// whether there is an agent at all.
/// </summary>
/// <param name="Oldest">
/// When the oldest entry of the four groups began to wait — a question since it
/// was asked, an issue in review since it entered review, and the other two
/// since the issue was written, which is the closest thing either has to a
/// beginning. <c>null</c> when nothing waits.
/// </param>
/// <param name="Ready">What the caller asking would be handed, counted.</param>
/// <param name="Blocked">Open issues waiting behind at least one open blocker.</param>
/// <param name="Open">Every issue that is not <c>done</c> or <c>canceled</c>.</param>
/// <param name="Agents">
/// Live agent tokens on the instance — the same number that stands beside "needs
/// you", and for the same reason: at <c>0</c> nothing gets picked up whatever
/// the lists say.
/// </param>
public sealed record StandingFacts(
    int Questions,
    int InReview,
    int Unready,
    int Stuck,
    DateTimeOffset? Oldest,
    int InProgress,
    int Ready,
    int Blocked,
    int Open,
    int Agents)
{
    /// <summary>How many entries "needs you" would hold for this project.</summary>
    public int NeedsYou => Questions + InReview + Unready + Stuck;
}

/// <summary>
/// The scale of ADR 0024: what waits for a human, and whether an agent could
/// still take work. The number of open issues never enters it.
/// </summary>
public static class StandingScale
{
    /// <summary>
    /// Past this, one entry is enough for the worst step. Age outranks quantity
    /// because the loop VISION 17 leaves open is a question nobody has thought
    /// of looking at, and three days is where "later today" stops being a
    /// plausible answer.
    /// </summary>
    public static readonly TimeSpan TooLong = TimeSpan.FromDays(3);

    /// <summary>How many fresh entries add up to the same thing.</summary>
    public const int Many = 3;

    /// <summary>The step, worst applicable first.</summary>
    public static Standing Of(StandingFacts facts, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(facts);

        if (facts.NeedsYou >= Many || facts.Oldest is { } since && now - since >= TooLong)
        {
            return Standing.Neglected;
        }

        if (facts.NeedsYou > 0)
        {
            return Standing.Waiting;
        }

        // Nothing waits for a human. Whether that means finished or stalled is
        // the whole reason `idle` is a step of its own: from outside they are
        // the same silence.
        if (facts.Open == 0)
        {
            return Standing.Clear;
        }

        return facts.InProgress > 0 || (facts.Ready > 0 && facts.Agents > 0) ? Standing.Running : Standing.Idle;
    }

    /// <summary>The one reason to name beside the step.</summary>
    public static StandingBecause Why(StandingFacts facts, Standing standing)
    {
        ArgumentNullException.ThrowIfNull(facts);

        if (standing is Standing.Waiting or Standing.Neglected)
        {
            // The order of "needs you", so that the sentence on the tile and the
            // first row of that list name the same thing.
            return facts.Questions > 0 ? StandingBecause.Question
                : facts.InReview > 0 ? StandingBecause.Review
                : facts.Unready > 0 ? StandingBecause.Unready
                : StandingBecause.Stuck;
        }

        if (standing is Standing.Clear)
        {
            return StandingBecause.Nothing;
        }

        if (standing is Standing.Running)
        {
            return StandingBecause.Working;
        }

        // Idle, and which of the three it is decides where the reader is sent.
        // No agent comes first: with none, the other two are true of every
        // project at once and repairing either changes nothing. Then the
        // blockers, because a chain is a lever a reader can pull; and what is
        // left is work that exists and is not workable, which the "Ready"
        // screen explains issue by issue.
        return facts.Agents == 0 ? StandingBecause.NoAgent
            : facts.Blocked > 0 ? StandingBecause.Blocked
            : StandingBecause.NothingReady;
    }
}
