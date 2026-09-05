# A Comment Can Be Corrected and Withdrawn by Its Author

A comment gains `PATCH /comments/{id}` and `DELETE /comments/{id}`. Its author
may rewrite it, and its author or any user may take it away. This replaces the
consequence of
[ADR 0020](./0020-a-newline-is-a-line-break-and-stored-text-is-not-hard-wrapped.md)
that said a comment cannot be edited; the reasoning of ADR 0020 itself — that a
newline is a line break — is untouched.

## Why

ADR 0020 said it plainly: "a comment is a thing somebody said, and the record of
what was said does not get rewritten because the renderer learned something."
That was right about what a renderer may cause, and it is still right. What it
did not weigh is that the person who said the thing has no way to take it back.

A comment is written once and kept for ever, so a typo is kept for ever, a
sentence sent one paragraph too early is kept for ever, and a comment on the
wrong issue is kept for ever. The only way out was a second comment withdrawing
the first, which leaves both standing and reads worse than either. Every one of
those is a person correcting themselves, and an issue tracker that cannot let a
person correct themselves in its own interface is missing something ordinary.

The trigger was operational rather than theoretical: comments are what an agent
leaves behind on the way through an issue, and a human reading afterwards has no
way to tidy them without a database client. ADR 0013 already conceded exactly
this for issues — agents produce noise, and a human must be able to clear it up
from the interface — and it is the same argument here, on an object one
paragraph long.

## What is decided

**The author edits; a user clears up.** Only the author may rewrite a comment,
because a text somebody else rewrote is no longer what its byline says was said.
Deleting is different: the author may, and so may any user, on anybody's comment
including an agent's — ADR 0013's concession, with ADR 0013's reason. An agent
deletes only its own.

**An edit is visible.** The comment carries `edited_at`, and the byline says so.
A text that changes quietly is the thing VISION 7's history exists against.

**Deleting is final and recorded.** No grace period: ADR 0013's floor,
`--deleted` list and sweep hang on the issue, the label and the project, and
rebuilding all of it for an object the size of a paragraph would buy a second
lifetime nobody asked for. The row goes; the history entry stays and says that
it went and who took it.

**The history entry names the fact, not the text.** It says a comment was
edited or withdrawn, and which one; it does not carry the old body. A history
that archived what somebody has just withdrawn would preserve exactly what the
withdrawal was for — and a body may be a mebibyte, so two of them per edit is
also the wrong arithmetic.

**No time window.** No five minutes, no "only while nobody has answered". A
rule with a deadline has to be explained, and it fails in precisely the case
that would justify it: the mistake is usually noticed later.

**Questions and answers are not touched.** A question is a state, not an
utterance: `needs-you`, the long poll and the open flag all read it. Making an
answered question editable is a different decision with different consequences,
and it will be taken on its own when it is due.

## Why not the alternatives

**Leave it as it was.** That is ADR 0020's position, and the cost of it turned
out to be borne by the person who made the mistake rather than by the renderer.
Nothing about the newline decision requires it.

**A version history per comment** — every draft readable afterwards. That is the
large answer, and it is not paid for by an object of this size. `edited_at` says
what a reader needs: this is not the first version.

**A tombstone that keeps the row and blanks the body.** It buys an audit trail
of deletions in the conversation itself, at the price of a hole in every thread
and a second lifetime rule. The history already carries the audit trail, and it
is the place that is not editable.

## Consequences

**Comments written before ADR 0020 can be unwrapped.** ADR 0020 wrote them off —
"the handful written before this decision stay as they are and read as a
staircase" — and they no longer have to.

**`updated_at` of the issue moves on both acts**, as it already does when a
comment is written, so a client holding `If-Match` learns about it.

**The console does not follow yet.** `pa issue view` prints comments without
their ids, and it cannot address what it does not show. How it shows an id
without burying the conversation in them is its own question, taken after this
contract stands.

**The full-text index follows by itself.** `comment.search` is a stored
generated column over `body`, so an edit recomputes it and a delete takes it
with the row.
