# The Overview Grades Attention, Not Backlog Size

The overview of every project shows one **standing** per project, and that
standing is read off what is waiting for a human and whether an agent could
still take work. **The number of open issues never enters it.** It is on the
tile as a number, beside the two others, and it colours nothing.

This is the first screen of the product that answers across projects, and it
moves the boundary [`docs/human-interface.md`](../human-interface.md) drew when
it said there is no dashboard. That sentence stays true of the board; it stops
being true of this.

## Why the backlog is not the measure

**A full backlog is a healthy project.** Forty issues in `todo` with three
claims in flight is the state this product was built to produce: the work is
written down and the agents are getting through it. A gauge that turns red at
forty teaches its reader, within about a week, that red means nothing — and the
one project that genuinely needs somebody is then the same colour as the four
that do not.

**The signal already exists, one level down.** "Needs you" is the list of what
only a human can resolve, and the navigation already carries its count for the
project the frame is standing in. Everything the standing grades is that list,
summed per project: open questions, issues in `review`, unflagged issues where
triage is required, and the stuck ones. Nothing new is measured, and the two
places cannot disagree about what deserves a human.

**The other half is the quiet project.** Nothing needs you and nothing is
moving are the same picture from the outside and opposite states underneath: a
project can be finished, or it can have no `ready` issue, or every issue behind
a blocker, or no agent token at all. `idle` exists to tell that apart from
`clear`, and it is the one thing this screen shows that no existing list does.

## Why age outranks quantity

A question open for six days is worse than three asked this morning, so one
entry older than three days is enough for the worst step while three fresh ones
are needed otherwise. VISION 17 names the question as the only loop in the
system that does not close by itself: an agent asks, releases the issue, and
the issue waits for a human who has to think of looking. The overview is what
narrows that without anything being sent, addressed at anybody, or given a read
state — the same reasoning that made the navigation counts numbers on links
rather than notifications.

## Why this is not the portfolio level VISION 5 refuses

Nothing is aggregated in order to be reported. No object is created, no metric
is kept over time, no second hierarchy appears above the project, and there is
no view of it for anybody who is not doing the work. What exists is one derived
value per project, computed on read like `Workable`, for the single human who
runs all of these projects and today has to visit each one to find out which
of them is waiting. Roadmaps, OKRs and reporting remain out.

## Consequences

- **The standing is computed on the server**, delivered by `GET /standing`, and
  never assembled by a client. Two clients that each apply the thresholds
  themselves are two clients that will eventually disagree about the same
  project.
- **The scale is closed**, like the status set: five steps, no configuration,
  no per-project thresholds. Whoever wants a different rule changes it here for
  everybody, which is the point of guiding principle 3.
- **`idle` says which of its three cases it is** — nothing ready, everything
  blocked, or no agent at all — because they are repaired in three different
  places, and a shared word would send the reader to the wrong one.
- **The tile never rests on colour**: the step carries a symbol, a word and the
  reason that produced it, the way an issue row already marks status and
  priority twice over.
- **The number of open issues stays visible.** Refusing it as a grade is not
  refusing it as information, and hiding it would only move the reader to a
  list to count by hand.
