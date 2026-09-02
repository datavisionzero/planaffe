# Status Transitions Are Acts, Not a Field You Write

An issue's status is not written by `PATCH`. It changes through named acts of
the API — `claim`, `release`, `close`, `review`, `reopen` — each one an endpoint
with the rule of VISION 9 to 11 inside it. The single exception is parking:
`status` in a `PATCH` accepts `backlog` and `todo`, on an open, unclaimed issue
that is not in `review`, because that move carries no rule beyond the ones the
row's constraints already hold.

The obvious alternative is the one every tracker with a REST API has: `status`
is a field, `PATCH { "status": "done" }` sets it, and the server validates the
transition. It is one endpoint instead of five, it is what a generated client
makes trivial, and it keeps the contract small — which is the shape this
product otherwise prefers.

It loses because a status change here is never only a status change. Closing
clears the claim, sets `closed_at`, and lands in `review` or in `done`
depending on who asks and what the project's switch says (ADR 0014). Claiming
sets `in_progress` and a holder and an expiry that depends on the holder's kind,
and may or may not be allowed to displace another holder. Reopening keeps the
`result` and clears `closed_at`. Handing in wants a `result`; rejecting wants a
comment. A `PATCH` that carried all of that would carry `force`, `result`,
`comment` and the switch logic as side effects of one field — and a client
reading the contract would see a writable `status` and a list of forbidden
values, never the acts. The acts are what the CLI exposes (`pa issue close`,
`pa issue claim`) and what the vision names; the contract should name them too,
so that a generated client has `CloseIssue` and not `PatchIssue` with a
comment.

The second reason is ADR 0002: these rules belong in Domain, and an act is a
method there — `Close(by, status, result, reviewRequired)` — while a field
write is a setter with validation bolted on. The shape of the API follows the
shape of the rules.

## Consequences

- **Five endpoints where one would do**, and a sixth kind of write (`PATCH`
  `status`) for the one move without a rule. The transition table in
  [`api.md`](../api.md) is the whole state machine, and it is short.
- **The claim is an act as well**, which is what makes `next` and `claim` the
  same act with a different way of picking the issue — one type in
  Application, as ADR 0002 wanted.
- **A client cannot put an issue into an impossible state**, because there is
  no request that says `in_progress` without a holder or `done` without
  `closed_at`; the row's check constraints (`storage.md`) are the last line,
  not the first.
- **A new status, if one ever came, is a new act** — visible in the contract
  diff as such, rather than a new allowed string nobody notices.
