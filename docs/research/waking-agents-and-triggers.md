# Waking Agents: Trigger Mechanics, Auth Worlds, and the Gap Behind the NAT

**Date:** 2026-08-30
**Context:** research backlog from [`VISION.md`](../../VISION.md) — sections 6.1 (CLI), 10 ("what is next?"), 11 (claiming), 12 (users and permissions), 13 (technical guard rails), 15.1 (already noted), 15.5 (the context package), 15.8 ("the agent as a named colleague") and 15.9 ("automatic start").

## Questions

Today everything in planaffe is initiated by a human: the human sits in front of their harness and says "fetch the next free ticket", and `pa next` delivers and claims atomically. That remains the MVP. What is open is the step after it — 15.8 notes it as a problem: *"A ticket waiting for a specific agent lies there until exactly that agent runs again — and nobody wakes it."*

1. **Auth worlds.** What is technically possible **and** contractually covered in unattended operation — once with a consumer subscription (Claude Pro/Max, ChatGPT Plus/Pro/Business), once with a provider API key?
2. **Trigger mechanics.** What concretely triggers an agent run today, where does it run, which auth world does it presuppose, and can it be self-hosted?
3. **Transport.** A webhook reaches an HTTP endpoint, not a laptop behind a NAT. Which patterns bridge that, and which of them are compatible with planaffe's guard rail "operations consist of Postgres backups — nothing else"?
4. **The industry playbook.** How do issue trackers model "the agent's turn", "the agent reports an interim state", "the agent asks back"? Is a standard emerging?
5. **Consequences.** Which option blocks assignment to a named agent least, without burdening the MVP?

## Method

Primary sources only: vendor documentation (`code.claude.com/docs`, `platform.claude.com/docs`, `support.claude.com`, `learn.chatgpt.com/docs`, `developers.openai.com`, `docs.github.com`, `docs.gitlab.com`, `linear.app/developers`, `docs.stripe.com`, `postgresql.org/docs`, `pgbouncer.org`), the vendors' legal texts (`anthropic.com/legal/*`, `openai.com/policies/*`), specifications (`standardwebhooks.com`, WHATWG HTML, the MCP and A2A specs) and official repositories. Web search served only to find the right page; every statement is verified against the documentation text itself. No third-party blog posts, no comparison articles, no forum material.

Two source limitations that matter for how much weight this can carry:

- **`openai.com/policies/*` was not retrievable live from this environment** (HTTP 403 throughout). The quotations from the terms of use, usage policies and business terms come from archive snapshots of the original text dated 2026-08-25 and 2026-08-21 respectively. The effective dates stated in the documents (1 January 2026 and 29 October 2025) lie well before that; a change within the snapshot window is unlikely but not excluded. Verify before a decision with legal weight.
- **Anthropic's consumer terms are served geographically.** The version checked is the EEA/Switzerland variant (contracting party "Anthropic Ireland, Limited"), effective October 8, 2025 — the relevant one for a German user.

**On the legal question:** this part reports what the terms say verbatim and separates "explicitly permitted", "explicitly forbidden" and "not addressed". It is not legal advice and contains no recommendation to circumvent any terms. Where the terms are silent, that silence is precisely the finding.

**Timing.** This field moves faster than any other in the vision. Much of what is quoted explicitly carries "research preview", "developer preview" or a beta-header date. The snapshot is as of 2026-08-30, and several central findings will look different in six months. What that means for the build decision is in "Consequences for the vision".

---

# Part 1: The Two Auth Worlds

The most important finding first: **the dividing line does not run where one expects it.** Both large vendors now document ways to run a consumer subscription unattended and triggered — Anthropic even with an HTTP endpoint of its own for starting a run. What is forbidden in both worlds is something else: *passing on* the subscription identity to third parties, and operating a product on someone else's subscription credentials.

## 1.1 Anthropic

### Which terms apply

The Claude Code documentation assigns this unambiguously: "Your use of Claude Code is subject to: **Commercial Terms of Service** — for Team, Enterprise, and Claude API users; **Consumer Terms of Service** — for Free, Pro, and Max users" ([Legal and compliance](https://code.claude.com/docs/en/legal-and-compliance)). The consumer terms confirm the boundary from the other side: "Our Commercial Terms of Service govern your use of any Anthropic API key … For clarity, this does not include Claude.ai or Claude Pro use for individuals or entities" ([Consumer Terms of Service](https://www.anthropic.com/legal/consumer-terms), effective October 8, 2025).

### What the consumer terms say about automation

The relevant clause sits in the list of prohibitions under "Use of our Services" and reads verbatim:

> "Except when you are accessing our Services via an Anthropic API Key or where we otherwise explicitly permit it, to access the Services through automated or non-human means, whether through a bot, script, or otherwise."
> — [Consumer Terms of Service](https://www.anthropic.com/legal/consumer-terms)

That is a prohibition **with two exceptions**: through an API key, or "where we otherwise explicitly permit it". The second exception is open-ended and is filled in by the product documentation (see below). The term "programmatic access" does not appear in the consumer terms.

Also forbidden there, each verbatim: account sharing — "You may not share your Account login information, Anthropic API key, or Account credentials with anyone else or make your Account available to anyone else"; resale — "…or resell the Services"; and circumventing protections — "bypassing any of our systems or protective measures". Rate limits are anchored contractually as a "technical limitation": "Different types of Service (including paid-for Services under a Subscription) may have technical restrictions associated with them, for example, the number of Inputs you may submit…".

### What the product documentation explicitly permits

Here sits the decisive passage, and it is unusually precise:

> "**OAuth authentication** is intended exclusively for purchasers of Claude Free, Pro, Max, Team, and Enterprise subscription plans and is designed to support ordinary use of Claude Code and other native Anthropic applications."
> "**Developers** building products or services that interact with Claude's capabilities … should use API key authentication … Anthropic does not permit third-party developers to offer Claude.ai login into their own applications, or to route requests through Free, Pro, or Max plan credentials on behalf of their users. Moreover, developers may not collect, store, or intermediate Claude.ai credentials or session tokens — sign-in to a Claude account must complete through Anthropic's own flow."
> "Nor does it prevent an end user from signing in to the unmodified Claude Code binary with their own Claude subscription…"
> — [Legal and compliance](https://code.claude.com/docs/en/legal-and-compliance)

**How to read this for planaffe:** what is forbidden is that *planaffe* collects, stores or routes requests through Claude credentials. What remains permitted is that the user runs their own, unmodified Claude Code installation on their own subscription — and planaffe only tells it *what* to do. That is exactly the architecture planaffe wants anyway (12.: "the token is the agent", not the model access).

The normative limit on continuous operation is not a hard boundary but a formula: "Advertised usage limits for Pro and Max plans assume **ordinary, individual usage** of Claude Code and the Agent SDK" (ibid.). What "ordinary, individual usage" means is not defined by the documentation.

### Subscription auth in CI is documented as intended

There is a command of its own for it:

> "For CI pipelines, scripts, or other environments where interactive browser login isn't available, generate a one-year OAuth token with `claude setup-token`" … "This token authenticates with your Claude subscription and requires a Pro, Max, Team, or Enterprise plan."
> — [Authentication](https://code.claude.com/docs/en/authentication)

And the GitHub Actions documentation lists both routes side by side: "`CLAUDE_CODE_OAUTH_TOKEN`: an OAuth token that authenticates with your Claude subscription, available on Pro, Max, Team, and Enterprise plans. Generate one by running `claude setup-token` locally." … "If you authenticate with an OAuth token, runs use your Claude subscription instead of API billing." ([Claude Code GitHub Actions](https://code.claude.com/docs/en/github-actions)). For secrets shared across repositories, the same page explicitly recommends the API key, "since an OAuth token is tied to the subscription of the person who ran `claude setup-token`".

Important for the build: the auth precedence is documented and partly at odds with itself. `ANTHROPIC_API_KEY` beats `CLAUDE_CODE_OAUTH_TOKEN`, and "In non-interactive mode (`-p`), the key is always used when present" (ibid.). The recommended script mode `--bare` does not read the OAuth token at all: "Bare mode does not read `CLAUDE_CODE_OAUTH_TOKEN`. If your script passes `--bare`, authenticate with `ANTHROPIC_API_KEY` or an `apiKeyHelper` instead."

### Rate limits in the subscription world

| | Pro | Max 5x | Max 20x |
|---|---|---|---|
| Session window | "reset every five hours" | same | same |
| Weekly limit | yes, "applies across all models" | yes | yes |
| Relative size | "at least five times the usage per session compared to our free service" | "five times more usage per session than the Pro plan" | "20 times more usage per session than the Pro plan" |

Sources: [What is the Pro plan?](https://support.claude.com/en/articles/8325606-what-is-the-pro-plan) (June 10, 2026), [What is the Max plan?](https://support.claude.com/en/articles/11049741-what-is-the-max-plan). There is additionally a **separate weekly limit for Opus**: the usage display names "when your plan's weekly usage limit resets **for Opus only** and all other models" ([Usage limit best practices](https://support.claude.com/en/articles/9797557-usage-limit-best-practices), June 2, 2026).

And everything counts against the same pool: "your usage of all different Claude product surfaces (claude.ai, Claude Code, Claude Desktop) counts towards the same usage limit" ([How do usage and length limits work?](https://support.claude.com/en/articles/11647753-how-do-usage-and-length-limits-work), July 13, 2026).

**What that means for continuous operation:** absolute numbers are documented nowhere (see open points). The only statement that carries weight is structural: an agent running around the clock out of a subscription shares its allowance with the same human's interactive work and runs into a five-hour **and** a weekly window. An issue tracker that wakes agents therefore cannot assume that a woken agent can actually work.

## 1.2 OpenAI

### Consumer terms

The list of prohibitions under "What you cannot do" contains the bullet:

> "Automatically or programmatically extract data or Output (defined below)."
> — [Terms of use](https://openai.com/policies/row-terms-of-use/), published/effective January 1, 2026 (archive snapshot 2026-08-25)

Unlike Anthropic, there is **no exception here for API keys or explicitly permitted cases**. Also there: "You may not share your account credentials or make your account available to anyone else"; "Interfere with or disrupt our Services, including circumvent any rate limits or restrictions or bypass any protective measures or safety mitigations"; "Modify, copy, lease, sell or distribute any of our Services."

The **business terms** are milder at exactly this point and add a different prohibition instead: "(f) extract data from the Services **other than as permitted through the Services**; … (h) … circumvent any rate limits …; **(i) violate or circumvent Usage Limits or otherwise configure the Services to avoid Usage Limits**" ([Business terms](https://openai.com/policies/business-terms/), effective January 1, 2026, archive snapshot 2026-08-21).

### The product documentation says something else than the list of prohibitions

This is the most interesting single finding of this research. OpenAI recommends the API key for automation — "Use API key authentication for programmatic Codex CLI workflows, such as CI/CD jobs" ([Authentication](https://learn.chatgpt.com/docs/auth)) — but has, for the other case, **a documentation page of its own with a step-by-step guide and GitHub Actions YAML**:

> "This guide shows how to keep ChatGPT-managed Codex auth working on a trusted CI/CD runner without calling the OAuth token endpoint yourself. **The right way to authenticate automation is with an API key. Use this guide only if you specifically need to run the workflow as your Codex account.**"
> — [Maintain Codex account auth in CI/CD (advanced)](https://learn.chatgpt.com/docs/auth/ci-cd-auth)

The conditions named there, verbatim: "you need ChatGPT-managed Codex auth rather than an API key · `codex login` cannot run on the remote runner · **the runner is trusted private infrastructure** · you can preserve the refreshed `auth.json` between runs · **only one machine or serialized job stream will use a given `auth.json` copy**". And the prohibitions within that path: "**Do not use this workflow for public or open-source repositories.**" and "**Do not share the same file across concurrent jobs or multiple machines.**"

The non-interactive documentation names the rate-limit advantage explicitly as an acknowledged motive: "Read this if you need to run CI/CD jobs with a Codex user account instead of an API key, such as enterprise teams … **or users who need ChatGPT/Codex rate limits instead of API key usage**" ([Non-interactive mode](https://learn.chatgpt.com/docs/non-interactive-mode)).

For enterprise there is additionally a token route without a browser: "In ChatGPT Enterprise workspaces, admins can grant the access token permission so permitted members can create Codex access tokens for trusted, non-interactive Codex local workflows … Access tokens are intended for trusted scripts, schedulers, and private CI runners" ([Authentication](https://learn.chatgpt.com/docs/auth)). For headless sign-in generally: `codex login --device-auth`.

**The gap stays open.** No clause exempts `codex exec` with a ChatGPT login from "Automatically or programmatically extract data or Output", and none includes it. The product documentation describes the path without a contractual caveat; the list of prohibitions is blanket. Any resolution would be interpretation — which is why it sits in the open points and not here.

### Usage policies

The only relevant passage on human oversight is narrowly framed: what is forbidden is "**automation of high-stakes decisions in sensitive areas without human review**" ([Usage policies](https://openai.com/policies/usage-policies/), effective October 29, 2025, archive snapshot 2026-08-25), followed by an exhaustive list of sensitive areas (critical infrastructure, education, housing, employment, financial activities and credit, insurance, legal, medical, essential government services, product safety components, national security, migration, law enforcement). Software development is not on it. **The usage policies contain no general requirement of human oversight and no prohibition of unattended use.**

Anthropic's usage policy is structurally the same: human-in-the-loop is demanded only for the enumerated "high-risk use cases"; the automation prohibitions concern account creation, spam and multi-account evasion ([Usage Policy](https://www.anthropic.com/legal/aup), effective September 15, 2025). On agentic use it points to a help article whose list of prohibitions (surveillance, harmful content, scaled abuse, unauthorized system access) likewise does not address unattended continuous operation ([Using Agents According to Our Usage Policy](https://support.claude.com/en/articles/12005017-using-agents-according-to-our-usage-policy), March 16, 2026).

### Rate limits in the subscription world

The Codex pricing documentation quotes ranges instead of numbers: "The estimates below show **local messages per five-hour window**. Cloud chats on ChatGPT plans use GPT-5.6 Sol and may use more of your allowance than local messages." The table's footnote: "On ChatGPT plans, **local messages and cloud chats share a five-hour window**. Additional weekly limits may apply." ([Pricing](https://learn.chatgpt.com/docs/pricing), undated, retrieved 2026-08-30).

The ranges themselves (local messages per five-hour window) run, for GPT-5.6 Sol for example, from "10-100" (Plus) through "50-500" (Pro 5x) to "200-2,000" (Pro 20x). And the documentation itself warns against point estimates: "Tasks that look similar can consume different amounts of your allowance."

## 1.3 Comparing the two worlds

| | Subscription (Claude Pro/Max, ChatGPT Plus/Pro/Business) | Provider API key |
|---|---|---|
| **Automation per the terms** | Anthropic: forbidden **except** "via an Anthropic API Key **or where we otherwise explicitly permit it**". OpenAI consumer: "Automatically or programmatically extract data or Output" forbidden across the board, with no exception | Anthropic commercial terms: no automation prohibition; explicitly "to power products and services Customer makes available to its own customers". OpenAI business terms: permitted "as permitted through the Services" |
| **Vendor-documented CI/headless path** | yes, both: `claude setup-token` → `CLAUDE_CODE_OAUTH_TOKEN`; `codex login --device-auth` or persisting `auth.json` on "trusted private infrastructure" | yes, the normal case |
| **The line both draw** | no passing on or intermediating credentials; no parallel use of the same credentials; no public repositories (OpenAI) | resale, competing products, reverse engineering |
| **Usage windows** | a 5-hour window **plus** a weekly limit, shared across all surfaces | usage-based, no windows |
| **Absolute limits documented?** | Anthropic: no, only relative. OpenAI: ranges, no weekly numbers | price per token |
| **Suitability for continuous operation** | limited, and explicitly dedicated to "ordinary, individual usage" (Anthropic) | built for it |

**What follows for planaffe:** planaffe must touch model credentials in neither world. Everything planaffe does has to stay at the level of "a signal to a harness the user runs and authenticates themselves". That is not a restriction; it coincides exactly with guiding principle 2 and with the token model from 12.

---

# Part 2: Trigger Mechanics Today

## 2.1 Claude Code

Anthropic has by now spelled the trigger question out — in several separate mechanisms that must not be confused, because they run in different auth worlds and in different places.

**Headless / `-p`.** The basic building block: "To run Claude Code in non-interactive mode, pass `-p` with your prompt and the CLI options you need" ([Run Claude Code programmatically](https://code.claude.com/docs/en/headless)). Output formats `text`, `json`, `stream-json`; the last delivers "newline-delimited JSON for real-time streaming". Exit behaviour is documented: "Claude Code exits with code 0 on success and a non-zero code when the run fails"; SIGTERM yields 143. For locked-down CI runs there is `--permission-mode dontAsk`: "Claude Code denies anything not in your `permissions.allow` rules or the read-only command set, which is useful for locked-down CI runs" (ibid.). **`-p` does not start itself** — it is what a trigger calls, not the trigger.

**Hooks.** The hooks reference lists more than thirty events (`SessionStart`, `Setup`, `UserPromptSubmit`, `PreToolUse`, `PostToolUse`, `Notification`, `Stop`, `FileChanged`, `SessionEnd` among others) and assigns them three cadences: "once per session … once per turn … on every tool call inside the agentic loop" ([Hooks reference](https://code.claude.com/docs/en/hooks)). Every documented definition describes a point **within** a running session — `SessionStart` included ("When a session begins or resumes"). **A hook cannot start a run.** An explicit negative statement to that effect is not in the documentation (see open points), but there is no session-creating event.

**`claude-code-action` (GitHub Actions).** Runs on the user's infrastructure: "The action executes entirely on your own GitHub runner" ([anthropics/claude-code-action](https://github.com/anthropics/claude-code-action)). Triggers are configurable — the inputs are `trigger_phrase` (default `@claude`), `assignee_trigger` ("The assignee username that triggers the action … Only used for issue assignment") and `label_trigger` ("The label name that triggers the action when applied to an issue"). The documentation separates two modes: "**Interactive mode**: when the workflow provides no `prompt` input, Claude waits for the trigger phrase … **Automation mode**: when the workflow provides a `prompt` input, Claude runs without waiting for a mention" ([Claude Code GitHub Actions](https://code.claude.com/docs/en/github-actions)).

Two protections there are instructive for planaffe: "on issue and pull request events, the triggering user must have **write access** to the repository" and "the Claude Code GitHub Action **rejects a bot actor** unless you list it in `allowed_bots`, which keeps bots from triggering Claude in a loop" (ibid.). Whoever wakes agents from tickets otherwise builds themselves a loop.

**Routines — the actual finding.** There is now a documented, subscription-authenticated HTTP endpoint for starting a run:

> "A routine is a saved Claude Code configuration … Routines execute on **Anthropic-managed cloud infrastructure**, or on your organization's self-hosted environment when routed there, **so they keep working when your laptop is closed**."
> "Each routine can have one or more triggers attached to it: **Scheduled** … **API**: trigger on demand by sending an **HTTP POST to a per-routine endpoint with a bearer token** … **GitHub**: run automatically in response to repository events"
> "Routines are available on **Pro, Max, Team, and Enterprise plans**."
> — [Automate work with routines](https://code.claude.com/docs/en/routines), research preview

The API reference is explicit: `POST https://api.anthropic.com/v1/claude_code/routines/{routine_id}/fire`, auth by "per-routine token (`sk-ant-oat01-...`)", the mandatory header `anthropic-beta: experimental-cc-routine-2026-04-01`, an optional field `text` ("Maximum 65,536 characters"), and a response with `claude_code_session_id` and `claude_code_session_url`. And explicitly: "Calling this endpoint requires a **claude.ai account on a Pro, Max, Team, or Enterprise plan** … Authenticate with a per-routine bearer token created in the Claude Code web UI **rather than a Claude API key**." ([Trigger a routine through the API](https://platform.claude.com/docs/en/api/claude-code/routines-fire))

Three details that matter for an issue tracker as the caller:

1. **No idempotency key.** "Each successful request creates a new session. There is no idempotency key. **If a webhook caller retries, the endpoint creates multiple sessions.**" (ibid.) An issue tracker with retry logic would therefore double its runs.
2. **The text passed along is explicitly untrusted.** "The `text` value doesn't reach the routine as a bare message. It arrives wrapped in a `<routine-fire-payload>` block that **labels it as untrusted data** and tells Claude not to follow instructions inside it unless the routine's own prompt says to." ([Routines](https://code.claude.com/docs/en/routines)) That is exactly the shape an issue tracker needs: ticket content is user content, not a command.
3. **The allowance is capped.** "Routines draw down subscription usage the same way interactive sessions do. In addition to the standard subscription limits, routines have a **daily cap on how many runs can start per account**." (ibid.) `429 rate_limit_error` with `Retry-After` is the documented failure case.

**Channels — the route into a *running* local session.** The fourth mechanism is the most interesting for planaffe, because it needs no cloud:

> "A channel is an **MCP server that pushes events into your running Claude Code session**, so Claude can react to things that happen while you're not at the terminal … **Events only arrive while the session is open**, so for an always-on setup you run Claude in a background process or persistent terminal."
> — [Push events into a running session with channels](https://code.claude.com/docs/en/channels), research preview

The reference describes the contract: "A channel is an MCP server that runs on the same machine as Claude Code. Claude Code spawns it as a subprocess and communicates over **stdio**." The server declares `capabilities: { experimental: { 'claude/channel': {} } }` and then sends `notifications/claude/channel` with `content` and `meta`; the event reaches the model as a `<channel source="…">` tag ([Channels reference](https://code.claude.com/docs/en/channels-reference)). The bundled example is literally "build a webhook receiver": "your server listens on a local HTTP port. External systems POST to that port, and your server pushes the payload to Claude."

Two warnings from the same page that concern planaffe directly: "Claude Code doesn't acknowledge notifications … If the session hasn't loaded your server as a channel, or the organization policy blocks it, **Claude Code drops the events silently** and returns no error to your server." And: "**An ungated channel is a prompt injection vector.** Anyone who can reach your endpoint can put text in front of Claude."

**Claude Code on the web.** "Claude Code on the web runs tasks on Anthropic-managed cloud infrastructure at claude.ai/code" and is "in research preview for Pro, Max, and Team users" ([Use Claude Code on the web](https://code.claude.com/docs/en/claude-code-on-the-web)). Auth is subscription only: "Claude Code on the Web always uses your subscription credentials. If you set `ANTHROPIC_API_KEY` … it doesn't override your subscription credentials" ([Authentication](https://code.claude.com/docs/en/authentication)). A GitHub trigger exists as an auto-fix: "Claude subscribes to GitHub activity on the PR, and when a check fails or a reviewer leaves a comment, Claude investigates and pushes a fix if one is clear."

**Agent SDK.** For third-party products the subscription route is excluded: "Anthropic does not allow third party developers to offer claude.ai login or rate limits for their products, including agents built on the Claude Agent SDK. Use the API key authentication methods described in the Quickstart instead" ([Agent SDK overview](https://code.claude.com/docs/en/agent-sdk/overview)).

## 2.2 OpenAI Codex

**`codex exec`.** "Use `codex exec` (or the short form `codex e`) for scripted or CI-style runs that should finish without human interaction" ([Non-interactive mode](https://learn.chatgpt.com/docs/non-interactive-mode)). With `--json`, "`stdout` … a JSON Lines (JSONL) stream"; event types "include `thread.started`, `turn.started`, `turn.completed`, `turn.failed`, `item.*`, and `error`". The sandbox is explicit instead of `--full-auto`: "Codex keeps `codex exec --full-auto` as a deprecated compatibility flag … Prefer the explicit `--sandbox workspace-write` flag in new scripts."

**Codex cloud.** "Run tasks in isolated cloud environments, work in parallel, and start work from the web, **GitHub, GitLab, Linear, or Slack**" ([Codex cloud](https://learn.chatgpt.com/docs/cloud)). Auth is necessarily by subscription: "**Codex cloud requires signing in with ChatGPT**" ([Authentication](https://learn.chatgpt.com/docs/auth)). Programmatically it can only be started through the CLI: "`codex cloud exec` submits a task directly, and `codex cloud list` returns recent chats for scripting" ([Developer commands](https://learn.chatgpt.com/docs/developer-commands)).

**GitHub integration.** The trigger is a mention: "If you mention `@codex` in a comment with anything other than `review`, **Codex starts a cloud chat using your pull request as context**" ([Review GitHub pull requests with Codex](https://learn.chatgpt.com/docs/third-party/github)). Plus optional automatic reviews when a PR is opened. **A label or assignment trigger is not documented.**

**Codex GitHub Action.** Official and API-key based: "Use the Codex GitHub Action (`openai/codex-action@v1`) to run Codex in CI/CD jobs … The action installs the Codex CLI, starts the Responses API proxy when you provide an API key, and runs `codex exec` under the permissions you specify" ([Codex GitHub Action](https://learn.chatgpt.com/docs/github-action)). Here too an actor protection: "**By default only users with write access can run the action**." And the warning relevant to issue trackers: "**Sanitize prompt inputs from pull requests, commit messages, or issue bodies to avoid prompt injection.** Review HTML comments or hidden text before feeding it to Codex."

**Codex SDK.** Exists for TypeScript and Python, but drives **local** threads: "The TypeScript library lets your application start, continue, and resume **local** Codex threads"; "The Python SDK controls the **local** Codex app-server over JSON-RPC" ([Codex SDK](https://learn.chatgpt.com/docs/codex-sdk)). A REST API for starting cloud tasks is not documented.

**OpenAI platform webhooks.** They exist and follow Standard Webhooks — but **not a single event concerns Codex**. The list covers `response.*`, `batch.*`, `fine_tuning.job.*`, `eval.run.*` and `realtime.call.incoming` ([Webhooks](https://developers.openai.com/api/docs/guides/webhooks)). Whoever wants to react to the completion of a Codex cloud task has only `codex cloud list --json`, which is polling.

## 2.3 GitLab Duo Agent Platform

The most interesting contrast for planaffe's target group, because GitLab is self-hostable and because its assignment model is what 15.8 has in mind.

**Triggers are a mention and an assignment to a service account.** The developer flow: "The event types **Mention** and **Assign** are configured in the trigger" and "To create a merge request from the issue … **Assign the Duo Developer service account to the issue**" ([Developer Flow](https://docs.gitlab.com/user/duo_agent_platform/flows/foundational_flows/developer/)). The ticket becomes the assignment: "When the flow service account is assigned to an issue or merge request, or assigned as a reviewer, **the IID of the resource is passed as the goal**" ([Custom flow YAML schema](https://docs.gitlab.com/user/duo_agent_platform/flows/custom_flows_schema/)) — the same page lists as trigger categories "mention events", "assign and assign reviewer events" and "pipeline events".

**Foreign agents are explicitly provided for.** GitLab calls them "external agents": "You can create an agent and integrate it with an external AI model provider … Then, in a project issue, epic, or merge request, you can **mention that external agent** in a comment or discussion and ask the agent to complete a task." The place of execution is the user's own CI: the agent "**Runs a CI/CD pipeline** and responds inside GitLab with either a ready-to-merge change or an inline comment" ([External agents](https://docs.gitlab.com/user/duo_agent_platform/agents/external/)).

Tested integrations are, verbatim, "Claude Code · OpenAI Codex · Amazon Q · Gemini". For the GitLab-managed variants GitLab supplies the credentials ("uses GitLab-managed credentials"); with Amazon Q and Gemini the user brings their own (`AWS_ACCESS_KEY_ID`, `GOOGLE_CREDENTIALS` etc. as CI/CD variables). On self-managed instances, a custom external agent can be defined as a YAML file in the repository (`.gitlab/duo/flows/claude.yaml`) plus a service account plus a trigger.

**The price:** tier "Premium, Ultimate", numerous feature flags, and to enable it "contact GitLab support". And GitLab names the risks itself: "**Prompt injection vulnerabilities**: GitLab implements third-party prompt scanning to lower the risk of prompt injections. **This scanning is not available for external agents.**" (ibid.)

**For planaffe this is the most direct evidence that the model from 15.8 holds:** an agent is a named account, you assign a ticket to it, and the run happens in the user's infrastructure. GitLab, however, needs CI as its execution layer — which planaffe does not have and does not want.

## 2.4 GitHub Copilot cloud agent

The case in which an **assignment** really does start a run — exactly what 15.8 has in mind.

> "you can assign Copilot cloud agent to straightforward issues on your backlog by **selecting 'Copilot' as the assignee**."
> "While working on a coding task, Copilot cloud agent has access to its own ephemeral development environment, **powered by GitHub Actions**, where it can explore your code, make changes, execute automated tests and linters and more."
> — [About Copilot cloud agent](https://docs.github.com/en/copilot/concepts/agents/coding-agent/about-coding-agent)

(A note on naming: GitHub has renamed "Copilot coding agent" to "Copilot **cloud** agent"; old URLs redirect.)

Progress becomes visible through the timeline and a PR: on an `@copilot` mention "an eyes emoji (👀) reaction appears on the comment. A 'Copilot has started work' event appears in the pull request timeline" … "Copilot will start working on the task, raise a pull request, then request a review from you when it's finished" ([Use cloud agent on GitHub](https://docs.github.com/en/copilot/how-tos/use-copilot-agents/cloud-agent/use-cloud-agent-on-github)).

The identity is a bot account: "the first node returned from the query will have the `login` value **`copilot-swe-agent`**" ([Use cloud agent via the API](https://docs.github.com/en/copilot/how-tos/use-copilot-agents/cloud-agent/use-cloud-agent-via-the-api)); in the REST call, `"copilot-swe-agent[bot]"` is set as the assignee.

Beside the assignment there is now a dedicated API — `POST /agents/repos/{owner}/{repo}/tasks` and siblings ([REST: Agent tasks](https://docs.github.com/en/rest/agent-tasks/agent-tasks?apiVersion=2026-03-10)), explicitly "in public preview and is subject to change" and "only available to users with a Copilot Business or Copilot Enterprise subscription" — plus `gh agent-task create` in the CLI.

**One limit planaffe should know:** "After assigning the issue, Copilot will not be aware of, and therefore won't react to, **any further comments** that are added to the issue." The run is a one-way street; steering happens only through the PR.

**And foreign agents?** Yes — under the name "third-party coding agents", not under the marketing term "Agent HQ" (which appears only in the GitHub blog, not in the documentation):

> "The following third-party agents are supported on GitHub: **Anthropic Claude · OpenAI Codex**"
> "**Third-party coding agents are currently in public preview.**"
> — [About third-party coding agents](https://docs.github.com/en/copilot/concepts/agents/about-third-party-coding-agents)

So it is **not an open interface** but a curated list of two vendors, each as an installed GitHub App, operated out of the user's Copilot subscription. Whoever wants to connect an agent of their own has only the generic route on GitHub: a GitHub App or an Actions workflow that reacts to labels and assignments — which is exactly what `claude-code-action` does.

Important against an obvious confusion: GitHub's **"custom agents" are not foreign runtimes** but Copilot personas — "**specialized versions of the Copilot agent**", defined as `.github/agents/<name>.agent.md` ([About custom agents](https://docs.github.com/en/copilot/concepts/agents/cloud-agent/about-custom-agents)).

## 2.5 The remaining systems

**Cursor cloud agents** (formerly background agents — "Cloud Agents were formerly called Background Agents", [Cloud Agent](https://cursor.com/docs/cloud-agent)). Triggers: `@cursor` in a GitHub PR/issue, a Bitbucket PR, Slack and **Linear**; web, iOS, desktop; `POST /v1/agents` against `https://api.cursor.com`; plus **automations**: "Cursor Automations run cloud agents in the background, **either on a schedule or in response to events** from GitHub, GitLab, Slack, webhooks, Linear, and more" ([Automations](https://cursor.com/docs/cloud-agent/automations)) — with a cron expression. Place of execution: "isolated VMs in the cloud". Self-hostable only partly and only for enterprise: "Self-hosted paths run tool calls on hardware you control … **The agent loop still runs in Cursor's cloud**" ([Choose a runtime](https://cursor.com/docs/cloud-agent/choose-runtime)). The back channel: webhooks with `X-Webhook-Signature` ("HMAC-SHA256 signature in the format `sha256=<hex_digest>`"), but only "`statusChange` events … specifically when an agent encounters an `ERROR` or `FINISHED` state" ([Webhooks](https://cursor.com/docs/background-agent/api/webhooks)).

**Google Jules.** The label trigger in its purest form: "You can start a task from a GitHub issue by applying the label **'jules'** (case insensitive)" ([Running tasks](https://jules.google/docs/running-tasks/)). Plus scheduled tasks, an alpha API (`POST https://jules.googleapis.com/v1alpha/sessions`, auth by `X-Goog-Api-Key`) and an official GitHub Action ([google-labs-code/jules-action](https://github.com/google-labs-code/jules-action), MIT). Place of execution: "a secure, cloud-based virtual machine (VM) with internet access" ([FAQ](https://jules.google/docs/faq/)). Auth: "Paid plans are accessed via a **Google AI Plans subscription**, which is currently available only for individual Google Accounts (ending in @gmail.com)" ([Usage limits](https://jules.google/docs/usage-limits/)) — free tier 15 tasks per 24 h.

**Devin.** The richest trigger menu, and particularly instructive for planaffe because it treats Linear the way planaffe would like to be treated:

> "1. **Assign the ticket to Devin** directly in Linear … 2. Add a **playbook label** (e.g. !plan, !implement, !triage, !review) to the ticket. Devin will start a session using the specific playbook that matches the label. 3. **Mention Devin in a ticket comment** with specific instructions. Devin will start a session and use your comment as the task instruction, without applying a playbook."
> — [Linear integration](https://docs.devin.ai/integrations/linear)

Plus `POST /v1/sessions`, Slack (`@Devin`, `/ask-devin`), Jira, Teams, GitHub PR comments, and automations: "Devin automations allow you to wire external events — Slack messages, GitHub webhooks, Linear ticket updates, **schedules**, and **custom webhooks** — to Devin sessions that start automatically" ([Automations](https://docs.devin.ai/product-guides/automations)); schedules in "iCalendar RRULE format". The place of execution is always Cognition's cloud ("Cognition hosts Devin"), even in the single-tenant model.

**OpenHands.** The only self-hostable system in the field — MIT-licensed. Three places of execution can be chosen: "**Docker sandbox (recommended)** — Runs the agent server inside a Docker container. · **Process sandbox** … · **Remote sandbox** — Runs the agent server in a remote environment" ([Sandboxes](https://docs.openhands.dev/openhands/usage/sandboxes/overview)). Auth is bring-your-own-key: "The LLM settings allows you to bring your own LLM and API key" ([LLM settings](https://docs.openhands.dev/openhands/usage/settings/llm-settings)), with the secrets `LLM_API_KEY`, `LLM_MODEL`, `LLM_BASE_URL`.

The trigger route has shifted: the frequently quoted `fix-me` label comes from the now-deprecated `openhands-resolver` (whose workflow literally checked `if: github.event.label.name == 'fix-me'` and ran in the user's GitHub Actions runner). Currently, for OpenHands Cloud: "On your repository, **label an issue with `openhands`** or add a message starting with **`@openhands`**. OpenHands will: 1. Comment on the issue to let you know it is working on it … 2. Open a pull request if it determines that the issue has been successfully resolved" ([GitHub installation](https://docs.openhands.dev/openhands/usage/cloud/github-installation)). The same vocabulary applies to GitLab. Headless locally: `openhands --headless -t "Your task here"`.

**The contrast bots.** Renovate is the most instructive counter-example, because its documentation clears up a widespread misconception:

> "**Setting a `schedule` does not itself cause or trigger Renovate to run.** It's like putting a sign on your office which says 'DHL deliveries only accepted between 9-11am'."
> — [Configuration options](https://github.com/renovatebot/renovate/blob/main/docs/usage/configuration-options.md)

The run comes from outside: "In all the above cases you must make sure that **some form of cron-like capability exists** to schedule when Renovate runs. We recommend that you run Renovate hourly, if possible." Renovate (AGPL-3.0) is therefore precisely *not* a self-starting bot but a CLI program with a time-window filter.

Dependabot solves the same thing through GitHub: `schedule.interval` with `daily`/`weekly`/`monthly`/`quarterly`/`semiannually`/`yearly`/`cron`, and "GitHub automatically runs the jobs that generate Dependabot pull requests on GitHub Actions" — self-hosted runners included, with "for security reasons, Dependabot updates on self-hosted runners will not run on public repositories" ([Dependabot on Actions](https://docs.github.com/en/code-security/concepts/supply-chain-security/dependabot-on-actions)).

Sourcery and CodeRabbit are pure event bots on the PR: Sourcery "reviews a pull request as soon as it opens, and again when you push new commits" plus `@sourcery-ai` commands and `sourcery-*` labels ([Reviews](https://docs.sourcery.ai/reviews/)); CodeRabbit "✅ **New PRs**: Automatic review when created / ✅ **New Commits**: Automatic review when pushed … ⚡ **Older PRs**: Use `@coderabbitai review` to trigger manually" ([FAQ](https://docs.coderabbit.ai/faq)). Sweep, once the most prominent issue-label bot, has left the field: "We're now building an AI coding assistant for **JetBrains**" ([sweepai/sweep README](https://github.com/sweepai/sweep)).

## 2.6 The trigger matrix

| System | Trigger | Place of execution | Auth world | Self-hostable? |
|---|---|---|---|---|
| **Claude Code `-p`** | a caller (shell, cron, CI) | user machine / CI runner | subscription OAuth token **or** API key (`--bare` API key only) | yes (the harness) |
| **Claude Code hooks** | events **inside** a running session | user machine | any | yes |
| **Claude Code channels** | an MCP server pushes into a **running** session (`notifications/claude/channel`) | user machine | subscription or console API key | yes (the channel server) |
| **`claude-code-action`** | `@claude` mention, `assignee_trigger`, `label_trigger`, cron, any GitHub event | **your own GitHub runner** | OAuth token (subscription), API key, WIF, Bedrock/Vertex/Foundry | yes (the runner) |
| **Claude Code routines** | cron, **HTTP POST to `/fire`**, GitHub events | Anthropic cloud (or a self-hosted environment) | **subscription only** (Pro/Max/Team/Enterprise), per-routine token | no |
| **Claude Code on the web** | manual, PR auto-fix | Anthropic cloud | subscription only | no |
| **`codex exec`** | a caller | user machine / CI runner | API key recommended; ChatGPT auth documented for "trusted private infrastructure" | yes (the harness) |
| **Codex cloud** | web, GitHub/GitLab/Linear/Slack, `codex cloud exec` | OpenAI cloud | **ChatGPT login only** | no |
| **Codex GitHub integration** | `@codex` mention in a PR; auto-review on open | OpenAI cloud | ChatGPT subscription | no |
| **`openai/codex-action`** | any GitHub event in the workflow | your own GitHub runner | **API key** | yes (the runner) |
| **Cursor cloud agents** | `@cursor` (GitHub/Bitbucket/Slack/Linear), web/iOS, `POST /v1/agents`, automations (cron + events) | Cursor cloud (VMs) | a paid Cursor plan; tool execution optionally on your own hardware (enterprise) | **no** for the agent loop |
| **GitHub Copilot cloud agent** | **assigning an issue to "Copilot"**, `@copilot`, the agents panel, `POST /agents/.../tasks`, `gh agent-task create` | **GitHub Actions**, `ubuntu-latest`, self-hosted runners possible | a paid Copilot plan; Actions minutes plus AI credits | no (only the runner) |
| **GitHub third-party agents** | as above, plus `@AGENT_NAME` | Actions minutes plus AI credits (the location is not explicitly documented) | Copilot subscription; **Claude and Codex only**, public preview | no |
| **Google Jules** | **the label `jules`** on an issue, the web UI, scheduled tasks, the `v1alpha` API, a GitHub Action | a Google Cloud VM (ephemeral) | Google account plus Google AI Plans; API key `X-Goog-Api-Key` (alpha) | no |
| **Devin** | **assignment in Linear/Jira**, a playbook label, a mention, Slack, `POST /v1/sessions`, automations (webhook/cron by RRULE) | Cognition cloud ("Devbox") | a paid plan or enterprise ACUs; `Bearer cog_…` | **no** |
| **OpenHands** | **the label `openhands`** / `@openhands` (GitHub and GitLab), Slack, `POST /api/v1/app-conversations`, automations, locally `--headless` | **selectable**: Docker/process locally, your own Actions runner, or OpenHands Cloud | **bring your own LLM key** (litellm-compatible) | **yes, MIT** |
| **Sourcery** | PR opened / new commits, `@sourcery-ai …`, `sourcery-*` labels | vendor cloud | seat subscription; BYO LLM only on the team plan through sales | only under an enterprise contract |
| **CodeRabbit** | PR created / pushed, `@coderabbitai …`, `.coderabbit.yaml` | vendor cloud or a container in your own infrastructure | seat subscription; CI through `CODERABBIT_API_KEY`; self-hosted with your own LLM | proprietary, enterprise from 500 seats |
| **Renovate** | **an external cron**; `schedule` is only a time-window filter | your own CI job / npx / Docker / Mend cloud | platform PAT, **no LLM** | **yes, AGPL-3.0** |
| **Dependabot** | `schedule.interval` in `dependabot.yml`, manually "check for updates" | GitHub Actions (self-hosted runners too) | the GitHub platform, **no LLM** | `dependabot-core` MIT |

## 2.7 What the matrix shows

**First: there are exactly three trigger families**, and all three have been the same for years.

1. **An event in a system the vendor operates themselves** (issue label, assignment, mention, PR opened). Works only if the agent operator *also* operates the event system or hangs off it by webhook.
2. **A schedule** (cron). Needs a scheduler running somewhere — and Renovate's documentation makes it mercilessly clear that a `schedule` field alone triggers nothing.
3. **An HTTP call** (an API, `/fire`, `POST /v1/agents`, `POST /v1/sessions`). The only trigger a *foreign* system can pull.

**Second: the place of execution correlates almost perfectly with the auth world.** If the agent runs in the vendor's cloud, the auth is a subscription (Copilot, Jules, Devin, Cursor, Codex cloud, Claude routines). If it runs at the user's end, the auth is an API key or a subscription token the user set themselves. **There is not a single system in the field in which a third party holds the user's model credentials** — which is exactly what both sets of terms forbid.

**Third: only one system in the whole field is genuinely self-hostable** (OpenHands, MIT) — and that one happens to be the only one with "bring your own key" as well. For planaffe's target group (3.: "who want to host their tools themselves") it is the only natural ally.

**Fourth, and decisive for 15.8: "an assignment starts a run" is built practice, not speculation.** GitHub (assignee "Copilot"), GitLab (a service account as assignee), Jira (the Rovo agent in the assignee field), Linear (delegation), Devin (Linear/Jira assignment). Five independent systems, the same gesture. planaffe's condition 7 in section 10 ("a ticket with an assignee only pulls for that identity") is therefore not exotic but the consensus — planaffe only has to settle **who starts the run**.

---

# Part 3: What Actually Works With Webhooks — and Where It Stops

## 3.1 The core problem, named cleanly

The Standard Webhooks specification says it in one sentence:

> "Webhooks are server-to-server, in the sense that both the customer and the service in the above description, **should be operating HTTP servers**, one to receive the API calls and one to receive the webhooks."
> — [Standard Webhooks, version 1.0.0](https://github.com/standard-webhooks/standard-webhooks/blob/main/spec/standard-webhooks.md)

That is exactly the gap. planaffe's target group (3.: "solo developers … at home on the console") runs no HTTP server for their own harness. The agent runs in a terminal on a laptop behind a NAT, often behind a firewall, often not at all.

How seriously the vendors themselves take this is shown by Linear: a webhook target has to be "available in a **publicly accessible HTTPS, non-localhost URL**" ([Webhooks](https://linear.app/developers/webhooks)). A laptop does not meet that.

## 3.2 The six patterns

### (a) Polling by the client

The status quo: `pa next` in a loop or in a cron job on the user's machine.

Both vendors document this as a feature in its own right. Claude Code has `/loop`: "Scheduled tasks let Claude re-run a prompt automatically on an interval. Use them to poll a deployment, babysit a PR…" — with a "Minimum interval: 1 minute" and the explicit limit "Tasks only fire while Claude Code is running and idle. Closing the terminal or letting the session exit stops them firing" ([Run prompts on a schedule](https://code.claude.com/docs/en/scheduled-tasks)). For runs without an open session there are desktop scheduled tasks: "A local task runs on your machine with direct access to your files and tools, but **only fires while the app is open and your computer is awake**" ([Schedule recurring tasks in Claude Code Desktop](https://code.claude.com/docs/en/desktop-scheduled-tasks)).

| | |
|---|---|
| **Advantages** | Zero server effort. Works behind any NAT. No new state in planaffe. Works in both auth worlds. Already built as soon as `pa next` exists. |
| **Disadvantages** | Latency equals the interval. Idle requests. The user has to set up the cron. With an aggressive interval, unnecessary load on Postgres. |
| **Security** | No new attack surface. The token never leaves the user's machine. |
| **Fits planaffe?** | **Completely.** No additional container, no new state, no scheduler. |

### (b) Long polling against our own API, woken by Postgres `LISTEN`/`NOTIFY`

The interesting case, because it needs no broker. The client holds a `GET /next?wait=60` open; meanwhile the planaffe app holds a Postgres connection with `LISTEN` and answers as soon as a matching ticket is written.

What the Postgres documentation settles about that:

- **Payload limit:** "In the default configuration it must be shorter than **8000 bytes**. (If binary data or large amounts of information need to be communicated, it's best to put it in a database table and send the key of the record.)" ([NOTIFY](https://www.postgresql.org/docs/current/sql-notify.html)) — irrelevant for planaffe, since only the ticket key would be transmitted anyway.
- **Transaction semantics:** "if a `NOTIFY` is executed inside a transaction, the notify events are **not delivered until and unless the transaction is committed**" and "notification events are only delivered between transactions. The upshot of this is that applications using `NOTIFY` for real-time signaling should try to **keep their transactions short**." (ibid.) That matches planaffe's atomic claim exactly: commit first, then wake.
- **Ordering and deduplication:** "If the same channel name is signaled multiple times with identical payload strings within the same transaction, only one instance of the notification event is delivered" and "it is also guaranteed that messages from different transactions are delivered in the order in which the transactions committed." (ibid.)
- **A dropped connection means loss:** "A session's listen registrations are **automatically cleared when the session ends**" ([LISTEN](https://www.postgresql.org/docs/current/sql-listen.html)). There is no persistence and no redelivery. Whoever is between two connections misses the signal with no substitute.
- **The race condition at startup** is described explicitly in the documentation and comes with a rule: "first execute (and commit!) that command, then in a new transaction inspect the database state as needed by the application logic, then rely on notifications to find out about subsequent changes." (ibid.)
- **The queue trap:** "There is a queue that holds notifications that have been sent but not yet processed by all listening sessions. If this queue becomes full, transactions calling `NOTIFY` will **fail at commit**. The queue is quite large (**8GB** in a standard installation) … However, no cleanup can take place if a session executes `LISTEN` and then enters a transaction for a very long time." ([NOTIFY](https://www.postgresql.org/docs/current/sql-notify.html)) — a hanging listener connection can therefore make writes fail.
- **Connection pooling:** PgBouncer documents `LISTEN` in its feature matrix as "Session pooling: **Yes** · Transaction pooling: **Never**" ([PgBouncer features](https://www.pgbouncer.org/features.html)). Whoever runs planaffe behind PgBouncer in transaction pooling loses `LISTEN` entirely. The app therefore needs its own dedicated connection outside the pool.

**The decisive point:** `LISTEN`/`NOTIFY` does not replace a broker, because there is no delivery guarantee. It is an **acceleration of polling**, not a substitute for it. The correct shape is always "long poll with a timeout, and query the database both on waking and on timing out"; the signal only shortens the wait. If the signal is lost, the timeout takes over, and the system is still correct.

| | |
|---|---|
| **Advantages** | Latency near zero without a broker. No additional container. The client holds an outgoing connection — the NAT is irrelevant. Falls back cleanly to polling when the signal is lost. |
| **Disadvantages** | The app has to carry long-open HTTP connections (a worker/thread model, reverse-proxy timeouts). A dedicated DB connection outside every pool. With many waiting clients, many open connections. |
| **Security** | No new attack surface beyond the existing API; the same token, the same permission check. |
| **Fits planaffe?** | **Yes.** No broker, no Redis, no additional container. Operations stay "app plus Postgres". |

### (c) SSE and WebSocket

The WHATWG specification describes SSE as a one-way server→client channel: "To enable servers to push data to web pages over HTTP or using dedicated server-push protocols, this specification introduces the `EventSource` interface" and "With server-sent events, it's possible for a server to send new data to a web page at any time" ([HTML Standard, server-sent events](https://html.spec.whatwg.org/multipage/server-sent-events.html)). Reconnection is built in: the stream can set the "reconnection time" through a `retry` field, and the client reports the `Last-Event-ID` header when reconnecting. MDN names the hard limit in the browser: "When not used over HTTP/2, SSE suffers from a limitation to the maximum number of open connections … the limit is per browser and is set to a very low number (6)" ([Using server-sent events](https://developer.mozilla.org/en-US/docs/Web/API/Server-sent_events/Using_server-sent_events)).

For planaffe the browser limit is irrelevant — the client would be the CLI, not the browser. Against long polling, SSE wins when *several* events are to flow over the same connection (progress, an answer to a question, a lost claim). For a single "here is your ticket", long polling is simpler: no reconnection logic, no event framing, no `Last-Event-ID` cursor.

WebSocket solves the same problem bidirectionally and costs a protocol of its own, a handshake layer of its own and reverse-proxy configuration. planaffe needs no bidirectionality — the client already speaks HTTP to the same API.

| | |
|---|---|
| **Advantages of SSE** | One channel for many events. Standardised reconnection. Plain HTTP. |
| **Disadvantages of SSE** | More state on both sides than long polling. Reverse-proxy buffering has to be switched off. |
| **Fits planaffe?** | **Yes, but later.** The right second stage once there is more than one event to transmit. |

### (d) GitHub Actions' self-hosted runner pattern

This is the proof that "an outgoing connection instead of a webhook" is a viable industry pattern — and GitHub documents the mechanics unusually openly:

> "A self-hosted runner connects to your GitHub Enterprise Server instance to receive job assignments … The self-hosted runner uses an **HTTP(S) long poll that opens a connection to GitHub for 50 seconds**, and if no response is received, it then times out and creates a new long poll."
> "**Only an outbound connection from the runner to GitHub Enterprise Server is required. There is no need for an inbound connection** from GitHub Enterprise Server to the runner."
> — [Communicating with self-hosted runners](https://docs.github.com/en/enterprise-server@3.13/actions/hosting-your-own-runners/managing-self-hosted-runners/communicating-with-self-hosted-runners) (GitHub Enterprise Server 3.13)

The current GitHub.com version of the same page names only the requirement, no longer the mechanism: "The host machine must be able to make **outbound HTTPS connections over port 443**" ([Communicating with self-hosted runners](https://docs.github.com/en/actions/concepts/runners/communicating-with-self-hosted-runners)). The long-poll wording with the 50 seconds is version-bound and no longer present in the newer versions.

Anthropic describes exactly the same pattern for remote control:

> "Your local Claude Code session makes **outbound HTTPS requests only and never opens inbound ports on your machine**. When you start Remote Control, it registers with the Anthropic API and **polls for work**."
> — [Continue local sessions from any device with Remote Control](https://code.claude.com/docs/en/remote-control)

**For planaffe this is the most important reference in this whole part:** two independent vendors solve "the server wants to hand work to a process on a foreign machine" with a held outgoing long-poll connection — not with webhooks, not with a broker, not with a tunnel.

### (e) Tunnels (ngrok, Cloudflare Tunnel)

Cloudflare Tunnel reverses the direction of the connection: `cloudflared` "creates **outbound-only connections** to Cloudflare's global network", and the service is thus "a secure way to connect your resources to Cloudflare **without a publicly routable IP address**" ([Cloudflare Tunnel](https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/)). ngrok describes the same effect: agent endpoints "are excellent for connecting to apps running on all networks, **even those behind a NAT (including double NAT)**" ([Agent Endpoints](https://ngrok.com/docs/universal-gateway/agent-endpoints/)).

| | |
|---|---|
| **Advantages** | Makes classic webhooks possible even on a laptop, without changing planaffe. |
| **Disadvantages** | A third vendor in the path. Another process on the user's machine. Another account, another token, another bill. The user has to establish the same outgoing connection planaffe could have directly — with a detour. |
| **Security** | The harness becomes reachable through a public URL. Whoever knows the URL can trigger runs, unless the local target authenticates on its own. |
| **Fits planaffe?** | **No, not as the recommended route.** It solves the problem by shifting the complexity to the user — the opposite of "started in minutes with `docker compose up`". As an option for users with their own infrastructure it remains open; planaffe has to build nothing for it. |

### (f) Message queue / broker

Ruled out by guard rail 13: "no queue, no Redis, no S3, no Elasticsearch in the MVP" and by the success criterion from 16: "operations consist of Postgres backups — nothing else."

That is not a convenience decision. A broker would destroy exactly the property planaffe plays against GitLab and Jira (2.: "self-hosting often means a dozen containers, a message broker, object storage…"). And it is not needed for this purpose: everything a broker would deliver here — a durable queue, delivery guarantees, retries — is already delivered in planaffe by the **ticket table itself**. A ticket in `todo` with an assignee *is* the queue, it is persistent, it survives every restart, and it is there anyway.

## 3.3 Assessment against the guiding principles

| Pattern | Additional container | Additional state in planaffe | Works behind a NAT | Latency | Verdict |
|---|---|---|---|---|---|
| Client polling / cron | no | none | yes | = the interval | **MVP-worthy, nearly built already** |
| Long poll + `LISTEN`/`NOTIFY` | no | one dedicated DB connection | yes | seconds | **The recommended next step** |
| SSE | no | open streams | yes | seconds | later, once there is more than one event |
| WebSocket | no | a protocol layer | yes | seconds | oversized |
| Outgoing webhooks | no | delivery state, retry history | **no** (the target must be reachable) | seconds | for users with their own infrastructure |
| Tunnel | at the user's end | none | yes | seconds | nothing to build, nothing to recommend |
| Broker | **yes** | queue state | yes | seconds | **ruled out** |

## 3.4 If planaffe does build outgoing webhooks

For users who have reachable infrastructure (CI, a server of their own), the classic webhook is the right route. There is a standard for it to be measured against.

**Standard Webhooks 1.0.0** ([specification](https://github.com/standard-webhooks/standard-webhooks/blob/main/spec/standard-webhooks.md), Apache-2.0) fixes three headers:

- `webhook-id`: "the unique webhook identifier … It remains the same no matter how many times a webhook that has failed is retried. The ID is often used as an **idempotency key**"
- `webhook-timestamp`: "integer unix timestamp (seconds since epoch)"
- `webhook-signature`: "the signature(s) of this webhook" — symmetric `HMAC-SHA256`, serialised as a version prefix plus base64: "a symmetric signature will be `v1`, followed by a comma (`,`), followed by the base64 encoded signature."

The recommended delivery semantics, verbatim: "retry delivery following a retry schedule **spanning multiple days**, with an exponential backoff … add some level of **random jitter**". The example schedule runs from "Immediately" to "24 hours / 75:35:05". Success is "a `2xx` status code"; `410 Gone` means "Sender should disable the webhook endpoint"; the recommended request timeout is "somewhere between 15 and 30s".

**How the three reference systems handle it:**

| | Signature | Timeout | Retries | Ordering |
|---|---|---|---|---|
| **GitHub** | `X-Hub-Signature-256`, "The hash signature always starts with `sha256=`" ([Validating webhook deliveries](https://docs.github.com/en/webhooks/using-webhooks/validating-webhook-deliveries)) | "within 10 seconds … If your server takes longer than that to respond, then GitHub terminates the connection and considers the delivery a failure" ([Best practices](https://docs.github.com/en/webhooks/using-webhooks/best-practices-for-using-webhooks)) | **none**: "GitHub does not automatically redeliver failed webhook deliveries" ([Handling failed deliveries](https://docs.github.com/en/webhooks/using-webhooks/handling-failed-webhook-deliveries)) | — |
| **Stripe** | `Stripe-Signature` with an embedded timestamp against replay; libraries with a "default tolerance of 5 minutes" ([Webhooks](https://docs.stripe.com/webhooks)) | 2xx "quickly … before running any complex logic" | "attempts to deliver events to your destination for **up to three days with an exponential back off** in live mode" (ibid.) | **no guarantee**: "Stripe doesn't guarantee the delivery of events in the order that they're generated"; "Webhook endpoints might occasionally receive the same event more than once" |
| **Linear** | `Linear-Signature`, "hex-encoded HMAC-SHA256 signature of the raw body contents"; timestamp check "within a minute" ([Webhooks](https://linear.app/developers/webhooks)) | **5 seconds** | "retried a maximum of **3 times** … after 1 minute, 1 hour, and finally after 6 hours. If the webhook URL continues to be unresponsive the webhook **might be disabled by Linear**, and must be re-enabled again manually" (ibid.) | — |
| **OpenAI** | follows Standard Webhooks (`webhook-id`, `webhook-timestamp`, `webhook-signature`) ([Webhooks](https://developers.openai.com/api/docs/guides/webhooks)) | "within a few seconds" | "continue to attempt delivery for **up to 72 hours** with exponential backoff"; `3xx` "will not be followed; they are treated as failures" (ibid.) | duplicates possible, "use the `webhook-id` header as an idempotency key" |

The spread is remarkable: from "no retries at all" (GitHub) to "72 hours" (OpenAI). Whoever needs a delivery guarantee builds it at the receiver, not at the sender.

**SSRF is the documented pitfall with user-defined targets.** The Standard Webhooks specification devotes a section of its own to it:

> "Webhooks implementations are **especially vulnerable to SSRF** as they let their consumers (customers) add any URLs they want, which will be called from the internal webhook system."
> "The main way to protect against SSRF is to prevent the webhooks from calling into internal networks and services. To achieve this you'd want to do two things: the first would be to proxy all webhook requests through a special proxy … that filters internal IP addresses, and the second would be to put the webhook workers (or proxy) in their own private subnet that can't access internal services."
> — [Standard Webhooks, version 1.0.0](https://github.com/standard-webhooks/standard-webhooks/blob/main/spec/standard-webhooks.md)

For a self-hosted planaffe instance that is particularly awkward: the container runs under `docker compose` in the same network as Postgres. A user who enters `http://db:5432` or a cloud metadata address as a webhook target makes planaffe reach into their own network. Whoever builds outgoing webhooks therefore needs at least target validation (HTTPS only, no private address ranges, no link-local addresses) — and even that is only half the battle against DNS rebinding. **That is an argument for *not* building outgoing webhooks as the first wake-up mechanism:** the client's held outgoing connection does not have this problem at all.

---

# Part 4: The Industry Playbook — "The Issue Tracker Wakes the Agent"

## 4.1 Linear: the only fully articulated agent interaction model

Linear is the most important single source in this part. It is the first issue tracker with an explicit, documented model for how an agent is woken, how it reports progress and how it asks back. The state is explicitly provisional: "Linear for Agents APIs are currently in active development and available as a **Developer Preview**. Functionality and Agent APIs may change before general availability." ([Getting started](https://linear.app/developers/agents))

### The agent is a workspace member

> "Agents behave similar to other users in a workspace. They can be @mentioned, **delegated issues through assignment**, create and reply to comments, collaborate on projects and documents, etc. App users are installed and managed by workspace admins."
> — [Getting started](https://linear.app/developers/agents)

And, central for planaffe: "**Agents installed in your workspace do not count as billable users**" (ibid.), "Agents are not counted as billable seats" ([AI agents](https://linear.app/docs/agents-in-linear)).

### Assignment is delegation, not assignment

That is the conceptually most important decision:

> "Assigning an issue to your app now sets it as the **`delegate`, not the `assignee`** — so humans maintain ownership while agents act on their behalf."
> — [Getting started](https://linear.app/developers/agents)
> "Agents are not traditional assignees. Assigning an issue to an agent delegates the issue to that agent **while the human teammate remains the primary assignee and owner**."
> — [AI agents](https://linear.app/docs/agents-in-linear)

Two OAuth scopes make it opt-in: `app:assignable` ("Allow the app to be assigned as a delegate on issues and made a member of projects") and `app:mentionable` ("Allow the app to be mentioned in issues, documents, and other editor surfaces").

### The wake-up mechanism is a webhook

> "An `AgentSession` webhook is sent to notify your agent when it's **mentioned, delegated an issue through assignment, or when a user provides additional prompts**."
> — [Developing the agent interaction](https://linear.app/developers/agent-interaction)

There are exactly two actions: `created` — "A new Agent Session has been created (triggered by a user mention or delegation). **You should start a new agent loop in response.**" — and `prompted` — "A user sent a new message into an existing Agent Session."

The response times are hard and documented:

- "You must return a response from your webhook receiver **within 5 seconds**."
- "If you receive a `created` event, you are expected to send an activity or update your external URL **within 10 seconds** to avoid the session being marked as unresponsive."
- "Follow-up activities after the first response can still be sent for **up to 30 minutes** before the session is considered stale. Note that this stale state is recoverable by sending another agent activity." ([Interaction best practices](https://linear.app/developers/agent-best-practices))

**That is the latency requirement a laptop agent fails.** Ten seconds to the first sign of life presupposes a running receiver — not a cron job that looks in every five minutes.

### The context package comes delivered

Linear solves the same problem as planaffe's 15.5, and does it with a pre-formatted string:

> "To construct a prompt for your agent, you can utilize the **`promptContext` field**, a formatted string containing the session's relevant context, such as issue details, comments, and guidance. Structured data can also be found in the `agentSession.issue`, `agentSession.comment`, `previousComments`, or `guidance` fields."
> — [Developing the agent interaction](https://linear.app/developers/agent-interaction)

The example in the documentation is XML-like: `<issue identifier="ENG-123">` with `<title>`, `<description>`, `<team>`, `<label>`, `<parent-issue>`, `<project>`, plus a `<primary-directive-thread>` with the comments and `<guidance><guidance-rule origin="team" …>`.

"Guidance" is almost literally planaffe's 15.3: "Agent guidance lets you provide instructions that agents will automatically receive when they work on issues in your workspace … **Workspace guidance applies across the entire organization, while team-specific guidance** can be used to include additional instructions unique to that team. When both exist, team guidance takes priority." ([AI agents](https://linear.app/docs/agents-in-linear))

### The back channel: five activity types

An agent reports progress through `AgentActivity` objects. "Your agent may emit **one of five allowed activity types**. These are validated server-side, and invalid shapes will be rejected." ([Developing the agent interaction](https://linear.app/developers/agent-interaction))

| Type | Meaning per the documentation | Fields |
|---|---|---|
| `thought` | "A thought or internal note." | `body` |
| `action` | "Describes a tool invocation. You may optionally include a result if the action has completed." | `action`, `parameter`, optionally `result` |
| `elicitation` | "**Requests clarification or confirmation from the user.**" | `body` |
| `response` | "Indicates work has been completed or a final result is available." | `body` |
| `error` | "Used to report an error or failure." | `body` |

Plus a sixth that only humans produce: "you may see references to a `prompt` type `AgentActivity`. That is a **user-generated message** … **An agent cannot generate a `prompt` type activity.**"

The session state is **derived** from these, not set: "Agent sessions can have one of 6 states: `pending`, `active`, `error`, `awaitingInput`, `complete`, `stale`. These will be visible to users." and "**You don't need to manage agent session state manually. Linear tracks session lifecycle automatically based on the last emitted activity.**"

Further building blocks: **signals** modify an activity — `stop` ("instructs the agent to halt work immediately") from human to agent, `auth` ("indicates that the agent requires the user to complete an account linking process") and `select` (a list of choices instead of free text) from agent to human ([Signals](https://linear.app/developers/agent-signals)). **Agent plans** are a checklist at session level with `content` and a `status` from `pending | inProgress | completed | canceled` — explicitly "currently in a technology preview". **Ephemeral activities** are overwritten by the next activity, and exist only for `thought` and `action`.

And an observation that supports planaffe's history decision: "**Comments may not be reliable to read from, as they are editable and may have changed since your agent's last run.** Instead, rely on Agent Activities as these are **frozen-in-time snapshots** of user input." ([Interaction best practices](https://linear.app/developers/agent-best-practices))

### Who sets the status?

Linear delegates that to the agent, with one rule: "If your agent is delegated by a human to work on an issue that is not in a `started`, `completed`, or `canceled` status type, **move the issue to the first status in `started` when your agent begins work**." And: "If your agent is working on implementation and no `Issue.delegate` is currently set, **it should set itself as the delegate**." (ibid.)

### What Linear additionally built

Its own Linear Agent has been in public beta since 2026-03-24 ([Changelog: introducing Linear Agent](https://linear.app/changelog/2026-03-24-introducing-linear-agent)). Notable for planaffe's 15.9: "You can also **trigger agent workflows automatically when issues enter triage**." — automations, available "on Business and Enterprise plans". A ticket's state change is therefore already a production trigger for agent runs; Linear puts it behind the paywall.

## 4.2 What planaffe already has of that — and what is missing

| Linear | planaffe today | Assessment |
|---|---|---|
| `delegate` separate from `assignee` | `claim` separate from `assignee` (8.) | **Conceptually identical**, and planaffe goes further: atomic, with a timestamp and expiry (11.). Linear documents no claim semantics. |
| `AgentSession` with 6 derived states | `status` plus `claim` with expiry; "the status change is derived, not written" (11.) | **The same principle.** Linear derives from the last activity, planaffe from the age of the claim. |
| `elicitation` as an activity type of its own | the **question** as an object of its own on the ticket (7.) | **planaffe is stronger here.** Linear's elicitation lives in the session; planaffe's question is a state on the ticket, queryable across the project, and it automatically makes the ticket unworkable (10.). |
| `AgentActivity` as an immutable record | the **history** (7.), system-written, not editable | **Identical in its reasoning** — Linear calls comments explicitly unreliable because they are editable. |
| `promptContext` as a ready-made context string | **the ticket as a context package** (15.5) | **Identical, not yet built.** Linear's field list is a usable template for the defined set. |
| `guidance` at workspace and team level | **project-wide instructions** (15.3), additionally considered on the epic | **Identical, not yet built.** Linear's precedence rule ("team guidance takes priority") is the template for project versus epic. |
| Agent plans (a checklist in the session) | — | Not planned. Defensible: planaffe has sub-issues. |
| A `stop` signal from human to agent | — | **A gap.** There is no way in planaffe to tell a running agent to stop. |
| A webhook with 5- and 10-second deadlines | — | Exactly the open question. |

## 4.3 GitHub, Jira, Sentry — three other answers to the same question

**GitHub Copilot coding agent** is the case in which an assignment really starts a run. Details in part 2.

**Jira/Atlassian** has the assignment model literally — for Rovo agents, not for the coding agent:

> "Once your agent connects to work items in Jira, you can collaborate in four ways: **Add an agent to the assignee field.** · **@ mention an agent in a comment.** · **Add an agent to workflow transitions.** · Add an agent to a column on the board."
> — [Collaborate with your Rovo agent on work items](https://support.atlassian.com/rovo/docs/collaborate-with-your-rovo-agent-on-work-items/), explicitly "currently in beta"

And further: "Once assigned, the agent will get to work on the task you've assigned it" ([Collaborate on work items with AI agents](https://support.atlassian.com/jira-software-cloud/docs/collaborate-on-work-items-with-ai-agents/)). The workflow trigger is called "trigger agent action" and fires "when any team member moves a work item to the allocated status". For autonomous operation Atlassian requires an automation rule: "For agents to work autonomously, they **must** be managed through your existing Confluence space or Jira Project administrators via an automation rule" ([Agents in automations](https://support.atlassian.com/rovo/docs/agents-in-automations/)).

The **Jira Coding Agent** (formerly Rovo Dev in Jira) is something else and knows neither assignment nor mention: "Pair up with Jira Coding Agent to transform your Jira work items into working code. Start coding instantly in a secure, cloud-based session in a dedicated, sandbox environment" ([What is the Jira Coding Agent?](https://support.atlassian.com/rovo/docs/work-with-rovo-dev-in-jira/)).

For building your own, **Forge** supplies the primitives, with documented limits: product triggers on events such as `avi:jira:created:issue`, `avi:jira:assigned:issue`, `avi:jira:commented:issue` — but "**Events for trigger modules may take up to 3 minutes** to reach your app after the triggering action occurs" ([Trigger module](https://developer.atlassian.com/platform/forge/manifest-reference/modules/trigger/), as of 29 July 2026). Scheduled triggers know only `fiveMinute`, `hour`, `day`, `week` and have no retries: "if the function throws an error, nothing will happen, and the function invocation will not be retried" ([Scheduled trigger](https://developer.atlassian.com/platform/forge/manifest-reference/modules/scheduled-trigger/)). Web triggers are unauthenticated: "**Web trigger URLs are publicly available and are not authenticated by the Forge platform**" ([Web trigger](https://developer.atlassian.com/platform/forge/manifest-reference/modules/web-trigger/)).

Automation for Jira closes the circle in both directions: the trigger "incoming webhook" ("The flow will run when a `HTTP POST` is sent to a specified webhook URL") and the action "send web request" — the latter with remarkably concrete hardening: "the only permitted ports for urls from the Send web request action are 80, 8080, 443, 6017, 8443, 8444, 7990, 8090, 8085, 8060, 8900, 9900" ([Jira automation actions](https://support.atlassian.com/cloud-automation/docs/jira-automation-actions/)). A port allowlist is the most pragmatic SSRF protection any of the systems examined documents.

**Sentry Seer** shows the reverse case — a non-issue-tracker that wakes agents. Triggers: manual ("You can always manually trigger the Autofix flow from the Issue Details page"), automatic on three conditions ("The issue has 10 or more events · The issue occurred within the last 14 days · The issue has a sufficient fixability score"), or from Slack ([Autofix](https://docs.sentry.io/product/ai-in-sentry/seer/autofix/)). Plus an API: `POST /api/0/organizations/{org}/issues/{issue_id}/autofix/` — "Trigger a Seer Issue Fix run for a specific issue. … The process runs asynchronously, and you can get the state using the GET endpoint" ([Seer API](https://docs.sentry.io/api/seer/)). And seven webhook events (`seer.root_cause_started` … `seer.pr_created`) for the back channel ([Seer webhooks](https://docs.sentry.io/integrations/integration-platform/webhooks/seer/)).

Most interesting for planaffe is Sentry's **handoff model**: "At the final code generation step, instead of having Seer generate the code fix directly, you can **hand off to an external coding agent** for implementation. Seer passes along the root cause and solution plan so the coding agent can act on them." Supported are Claude Agent, Cursor Cloud Agent and GitHub Copilot Cloud Agent — and the restriction is telling: "Coding agent handoff **only works with GitHub**."

## 4.4 Is a standard emerging?

### MCP: no — and the movement goes the other way

This is the clearest finding of this part, and it is stronger than the question assumed.

The current spec version is **2026-07-28** ([Versioning](https://modelcontextprotocol.io/specification/versioning)). It removed server-initiated requests:

> "Servers **MUST** send server-to-client requests (such as `roots/list`, `sampling/createMessage`, or `elicitation/create`) using the MRTR pattern. The previous pattern of server-initiated requests is no longer supported. **This is a breaking change.**"
> — [Multi round-trip requests](https://modelcontextprotocol.io/specification/2026-07-28/basic/patterns/mrtr)

Also, per the same version's changelog: removal of protocol-level sessions and the `Mcp-Session-Id` header, removal of the `initialize` handshake, removal of the GET stream endpoint. The architecture page says it in one sentence: "MCP is a **stateless** protocol: every request is self-contained" ([Architecture](https://modelcontextprotocol.io/specification/2026-07-28/architecture)). And the division of roles was never otherwise: "**Hosts**: LLM applications that **initiate connections**" ([Specification](https://modelcontextprotocol.io/specification/2026-07-28/index)).

For stdio it is excluded by definition anyway: "In the **stdio** transport, the client launches the MCP server as a subprocess" ([stdio transport](https://modelcontextprotocol.io/specification/2026-07-28/basic/transports/stdio)). A server that is a child process of its client cannot wake it.

Even the long-running operations are deliberately built as polling. The tasks extension (split out of the core into `io.modelcontextprotocol/tasks`) describes "allowing the client to retrieve the eventual result by **polling**" and justifies it explicitly: "The client responds via `tasks/update` — **no second connection or unsolicited server-to-client messages required**" ([Tasks extension](https://modelcontextprotocol.io/extensions/tasks/overview)). The push variant too runs over a `subscriptions/listen` stream opened by the client, and: "**Polling is the default.**"

**The answer to the guiding question: an MCP server cannot wake an agent. MCP always presupposes a running client.** A planaffe MCP server is an excellent second agent interface (15.1) — but as a wake-up mechanism it is worth nothing. The only exception is a *vendor-specific* attachment such as Claude Code's channels (`capabilities.experimental['claude/channel']`, see 2.1), and that likewise presupposes an already running session.

### A2A: yes — it is the only specification with a real wake-up path

A2A is at version **1.0.1** under the Linux Foundation ([A2A](https://github.com/a2aproject/A2A)). The wake-up mechanism is called push notifications:

> "**Push Notifications (WebHooks):** Agent sends HTTP POST requests to client-registered endpoints when task state changes · Client does not maintain persistent connection · Asynchronous delivery, **client must be reachable via HTTP** · Best for: Server-to-server integrations, long-running tasks, event-driven architectures · Requires `AgentCard.capabilities.pushNotifications` to be `true`"
> — [A2A specification v1.0.1](https://github.com/a2aproject/A2A/blob/v1.0.1/docs/specification.md), §3.5.1

The agent describes itself in an **agent card**: "A2A Servers **MUST** make an Agent Card available", discoverable among other ways through "Accessing `https://{server_domain}/.well-known/agent-card.json`" (§8.1/8.2). The **task lifecycle** is an enum with nine values — `TASK_STATE_UNSPECIFIED`, `SUBMITTED`, `WORKING`, `COMPLETED`, `FAILED`, `CANCELED`, `INPUT_REQUIRED`, `REJECTED`, `AUTH_REQUIRED` — with the spec distinguishing throughout between *terminal* (completed/failed/canceled/rejected) and *interrupted* (input-required/auth-required).

The delivery rules read like a short version of Standard Webhooks: "Clients **MUST** respond with HTTP 2xx status codes"; "Clients **SHOULD** process notifications **idempotently**, as duplicate deliveries may occur"; "Agents **MUST** attempt delivery **at least once**"; "Agents **SHOULD** include a reasonable timeout for webhook requests (recommended: **10-30 seconds**)" (§4.3.3).

And A2A is the only specification examined with **explicit SSRF guidance**:

> "Agents **SHOULD** validate webhook URLs to prevent SSRF (Server-Side Request Forgery) attacks: Reject private IP ranges (127.0.0.0/8, 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16) · Reject localhost and link-local addresses · Implement URL allowlists where appropriate"
> — A2A specification v1.0.1, §13.2

The wake-up flow is the same as Linear's, only without a payload: "The A2A Server decides when to send a push notification, typically when a task reaches a significant state change … Upon receiving a push notification (and successfully verifying its authenticity), the client typically uses the `GetTask` RPC method with the `taskId` from the notification to retrieve the complete, updated `Task` object" ([Streaming and async](https://github.com/a2aproject/A2A/blob/v1.0.1/docs/topics/streaming-and-async.md)).

The official demarcation from MCP is in appendix B of the spec: "**A2A is about agents _partnering_ on tasks, while MCP is more about agents _using_ capabilities**" ([A2A and MCP](https://a2a-protocol.org/v1.0.1/topics/a2a-and-mcp/)).

**A2A is nevertheless not an obvious route for planaffe** — for the same reason as webhooks: "client must be reachable via HTTP". A2A solves the problem *between servers*, not between a server and a laptop. What planaffe can take from it is the **vocabulary**: the task state set (submitted/working/input-required/completed/failed/canceled/rejected) is strikingly congruent with planaffe's `todo` / `in_progress` / question / `done` / `canceled`.

### AGENTS.md: a convention, not a protocol

"A simple, open format for guiding coding agents, used by over 60k open-source projects" … "Think of AGENTS.md as a **README for agents**" ([agents.md](https://agents.md/)). No format, no mandatory sections: "AGENTS.md is just standard Markdown. Use any headings you like." Precedence by proximity: "The closest AGENTS.md to the edited file wins; explicit user chat prompts override everything." Stewardship: "AGENTS.md is now stewarded by the Agentic AI Foundation under the Linux Foundation."

For planaffe it is relevant only as a contrast to 15.3: AGENTS.md applies to the repository, planaffe's project instructions apply to the project. That is exactly the gap 15.3 names — and AGENTS.md does not close it.

### The shared state model

Across Linear, A2A, Sentry and MCP tasks, four elements repeat:

| Element | Linear | A2A | MCP tasks | planaffe today |
|---|---|---|---|---|
| **The run is an object of its own** | `AgentSession` | `Task` | a task handle | — (only the `claim` on the ticket) |
| **Its state is derived** | "based on the last emitted activity" | from the last status update | from `tasks/get` | from the age of the claim (11.) |
| **"Waiting for a human" is a state of its own** | `awaitingInput`, the `elicitation` activity | `TASK_STATE_INPUT_REQUIRED` | `input_required` | the **question** (7.) |
| **The record is immutable** | `AgentActivity`, "frozen-in-time snapshots" | task history | — | the **history** (7.) |

**planaffe already has three of the four** — derived on its own, not copied. What is missing is the first: an object for the *run*. Today planaffe models only "this ticket is currently being worked on", not "this run started at 14:03, called three tools and has been waiting for an answer since 14:20".

That is not a gap the MVP has to close — but it is exactly where 15.2 (agent metadata on closing), 15.6 (measuring the cut) and 15.9 (a handover state on abort) converge. All three need the same thing: **several runs per ticket, each with its own identity and its own outcome.** 15.6 already says so explicitly ("the data model has to allow several runs to be booked on the same ticket later"). The research confirms it: four independent systems arrived at the same object.

---

# Part 5: Consequences for the Vision

## 5.1 What the research confirms about the vision

- **The claim is modelled correctly.** Linear separates `delegate` from `assignee` for the same reason planaffe separates `claim` from `assignee` — and none of the systems examined documents claim *semantics* with exclusivity and expiry. That remains planaffe's distinguishing feature.
- **Condition 7 in section 10 is the industry consensus.** Five systems start runs through an assignment. The mechanism from 15.8 ("if the bot is an identity, then `pa next` under its token fetches exactly what is intended for it") is the right build.
- **The question as a state of its own is stronger than what the others have.** Linear's `elicitation` lives in a session, A2A's `TASK_STATE_INPUT_REQUIRED` in a task. planaffe's question hangs on the ticket, is queryable across the project, and automatically makes the ticket unworkable. Keep it.
- **"No message broker" is not asceticism but correct.** The ticket table *is* the queue. A broker would rebuild a persistence that is already there.
- **Ticket content is untrusted content.** Anthropic wraps trigger text in `<routine-fire-payload>` and calls it explicitly "untrusted data"; OpenAI warns "Sanitize prompt inputs from … issue bodies to avoid prompt injection"; GitLab warns that its prompt scanning does not apply to external agents; Anthropic calls an unprotected channel "a prompt injection vector". **When planaffe delivers tickets to agents, that is the same class of content.** This is already true today for `pa next` and is not a new problem — but it belongs in the documentation, not in the footnotes.

## 5.2 What the research says against an obvious idea

**MCP does not help with waking.** The MCP server from 15.1 remains a very good idea as a *second agent interface* — but it cannot start an agent. The 2026-07-28 spec **removed** server-initiated requests, sessions and the GET stream; a stdio server is by definition a child process of its client; even the tasks extension is explicitly polling. Whoever builds 15.1 builds the interface "a running agent reads and writes planaffe" — not "planaffe wakes an agent". **That is an important correction to an expectation that creeps in easily.**

**Outgoing webhooks are the wrong first step for this target group.** They need a reachable HTTP server ("client must be reachable via HTTP", A2A; "publicly accessible HTTPS, non-localhost URL", Linear). A solo developer with a laptop does not have one. On top of that come the delivery state (retry history, disabling after failures) and the SSRF question, which is particularly sharp in a `docker compose` setup because the app container sits in the same network as Postgres.

## 5.3 The options, assessed

| Option | Effort | Operations | Auth worlds | Latency | Solves 15.8? |
|---|---|---|---|---|---|
| **(i) Polling by the harness** (`pa next` in cron or `/loop`) | **none** (falls out of the MVP) | nothing | **both, every harness** | the interval | partly — the named agent has to be running |
| **(ii) `pa watch` — a long poll against planaffe that starts the harness** | medium: one endpoint, one CLI subcommand, one dedicated DB connection | nothing additional | **both, every harness** | seconds | **yes, once the user keeps the daemon running** |
| **(iii) Outgoing webhooks** | medium to high: delivery state, retries, HMAC, SSRF defence, UI | delivery history in Postgres | both, but only for users with reachable infrastructure | seconds | only for CI and server users |
| **(iv) An MCP server** | medium | nothing | both | — | **no** (it does not wake) |
| **(v) A `/fire` call against Claude routines** or similar | small per vendor | nothing | **subscription only, Anthropic only**, research preview | seconds | yes, but vendor-bound |
| **(vi) A planaffe channel plugin for Claude Code** | small, outside the core | nothing | Claude Code, research preview | seconds | yes, for one harness |

### On (v) and (vi) — the two options that were not on the list

**(v) A vendor-specific starting point.** With `POST /v1/claude_code/routines/{id}/fire`, Anthropic has built exactly what 15.9 ("automatic start") describes: a ticket becomes `ready`, planaffe sends an HTTP POST, an agent sets off — and does so **out of the user's own subscription**, without planaffe ever seeing model credentials. The user creates the routine, generates the token, and stores the URL and token in planaffe.

Four things argue against it, and together they weigh heavily: the endpoint is a "research preview" behind a dated beta header (`experimental-cc-routine-2026-04-01`), there is **no idempotency key** ("If a webhook caller retries, the endpoint creates multiple sessions"), it runs in Anthropic's cloud against a fresh clone of the repository rather than at the local workplace, and it exists **at exactly one vendor**. planaffe would be building in a dependency it cannot mirror for Codex, Cursor or OpenHands. That is a recipe line for the documentation, not a feature.

**(vi) A planaffe channel plugin.** Claude Code's channels are an MCP server that pushes into a **running** session through `notifications/claude/channel`. Such a server could itself long-poll against `pa watch --json` and push "PLAN-42 has been assigned to you" into the open session. Attractive, because it means zero server effort — but it is an addition to (ii), not a replacement for it: the channel server would have to hold the same long-poll connection (ii) builds anyway. And it is a research preview, vendor-bound, and presupposes a permanently open session.

## 5.4 Recommendation

**Build (ii): a `pa watch` that holds an outgoing long-poll connection against the planaffe API and starts a command on a matching ticket. Woken server-side by Postgres `LISTEN`/`NOTIFY`, with a timeout fallback to an ordinary query. The command itself is post-MVP; the MVP creates only the endpoint whose client it later is.**

Five reasons:

1. **It is the pattern the industry chose for exactly this problem.** GitHub runners: "HTTP(S) long poll that opens a connection to GitHub for 50 seconds … There is no need for an inbound connection." Claude Code remote control: "makes outbound HTTPS requests only and never opens inbound ports on your machine … registers with the Anthropic API and polls for work." Two independent vendors, the same answer to the same question.
2. **It works in both auth worlds and with every harness.** `pa watch` starts a command; whether that is `claude -p`, `codex exec` or a shell script is none of planaffe's business. That keeps planaffe on the only level both sets of terms permit beyond doubt: it tells an agent *what* to do and never touches *what it does it with*.
3. **It costs no container.** `LISTEN`/`NOTIFY` is built into Postgres, the ticket table is the queue, the timeout is the delivery guarantee. Success criterion 16 ("operations consist of Postgres backups — nothing else") stays true.
4. **It has no SSRF surface.** The client opens the connection; planaffe never calls a user-defined URL. With option (iii) that is exactly the expensive part.
5. **It keeps every door open.** The same endpoint later carries SSE (several events per connection), feeds a channel plugin (vi), and turns outgoing webhooks (iii) into a *second* consumer of the same event rather than a parallel mechanism.

**What that means for the MVP — and it is little:**

- `pa next` gets an optional waiting mode: `pa next --wait 60`. It blocks until a matching ticket is there or the timeout fires. Server-side, a simple loop is fine at first — the `LISTEN`/`NOTIFY` optimisation is an implementation question behind the same API.
- That is already the full benefit: a user writes `while :; do pa next --wait 60 --claim --json | my-agent; done` and has options (i) and (ii) in one line, without planaffe shipping a daemon.
- `pa watch` as a subcommand of its own (start a process, collect its output, close the ticket) is the post-MVP stage. It then needs answers to: how many runs in parallel? What happens when the started process crashes? Is the claim released? Those are exactly the sort of questions to answer only once `pa next` exists.

**The honest counter-arguments:**

- **A daemon at the user's end is a process that has to run.** It does not solve "nobody wakes the agent" but shifts it to "nobody started the daemon". The difference is real all the same: starting a daemon is a one-off act; starting an agent is a per-ticket act. And the comparison against the status quo is not "nothing at all" but "cron with five minutes of latency" — the gain is **latency and clarity, not autonomy**.
- **Long-open HTTP connections are operational effort.** Reverse proxies buffer and close. Whoever puts planaffe behind nginx or Traefik has to raise the timeouts. That belongs in the documentation before it belongs in support requests.
- **`LISTEN` and connection pooling do not get along.** PgBouncer: "LISTEN | Transaction pooling: **Never**". The app needs a dedicated connection outside every pool, and a hanging listener transaction can, per the Postgres documentation, even make `NOTIFY` commits fail. Both are manageable, but they are real effort, and they contradict the reflex "Postgres is simple".
- **Assignment to a named agent remains a trap as long as the agent never runs.** Which is why the second half of 15.8 stays right and should be built **first**: *"a rule like the claim's: after a deadline the ticket is available to everybody again."* That costs one field and one condition in `pa next` — and it is the only measure that makes 15.8 safe without any new infrastructure. **Waking is the bonus; the expiry deadline is the duty.**

## 5.5 What else from the research belongs in the vision

1. **The run as an object of its own.** Linear (`AgentSession`), A2A (`Task`), MCP tasks and Cursor (`/v1/agents/{id}/runs`) arrived at the same object independently. 15.2, 15.6 and 15.9 all need it. For the MVP that means only what 15.6 already says: do not build the data model so that a ticket can have only one run.
2. **A `stop` signal is missing.** Linear has it as a human-to-agent signal, Cursor as `POST …/runs/{runId}/cancel`. planaffe today has no way to tell a running agent to stop — only `claim --force`, which takes over the claim without stopping the process. As soon as `pa watch` exists, that is an obvious second event on the same connection.
3. **The industry's latency expectations are high.** Linear: a first activity within 10 seconds, otherwise "unresponsive"; after 30 minutes without activity, "stale". planaffe's four-hour claim deadline is very generous by comparison — right for an agent run, but it shows that planaffe cannot distinguish "currently running" from "has not reported in for a long time". The handover state from 15.9 is the cheapest remedy.
4. **Guidance precedence rules are solved.** Linear: "When both exist, **team guidance takes priority**." That is the template for 15.3 (project versus epic text): the more specific one wins, and both are delivered.
5. **The due date from section 17 stays open.** The research delivers nothing new on it; it only confirms the coupling: a date needs a wake-up mechanism, and with `pa watch` that only exists once planaffe also produces time-triggered events. Server-side that would be a scheduler — exactly what 11. deliberately avoids for claims. **Recommendation: defer further, and do not design for it while building `pa watch`.**

---

# Open Points and Uncertainties

The following could **not** be substantiated in a primary source and must not be treated as established. Non-findings are not conjectures — they only say that the sources checked are silent on the question.

1. **Anthropic nowhere states explicitly whether a Pro/Max subscription is allowed in CI or continuous operation.** Checked: [Legal and compliance](https://code.claude.com/docs/en/legal-and-compliance), [GitHub Actions](https://code.claude.com/docs/en/github-actions), [Authentication](https://code.claude.com/docs/en/authentication), [Headless](https://code.claude.com/docs/en/headless), the consumer terms, and the support article [Use Claude Code with your Pro or Max plan](https://support.claude.com/en/articles/11145838-use-claude-code-with-your-pro-or-max-plan) (which mentions CI, GitHub Actions and headless with not a word). The closest thing is the normative formula "ordinary, individual usage" — **which the documentation nowhere defines** — plus the technical dedication "Use this for CI pipelines and scripts". Together they amount to neither a permission nor a prohibition.
2. **Anthropic's consumer terms name neither CI, GitHub Actions, headless operation nor unattended operation.** Full-text search over the version retrieved. The only point of contact is the generic "automated or non-human means" clause with its open exception.
3. **Only the EEA/CH version of Anthropic's consumer terms was retrievable.** A US version (Anthropic, PBC) could not be obtained at the same URL. For a German user the version checked is the right one; for other jurisdictions the text may differ.
4. **Whether OpenAI's blanket "Automatically or programmatically extract data or Output" applies to `codex exec` with a ChatGPT login remains open.** There is no clause exempting Codex and none including it — while the product documentation describes the same path step by step without a contractual caveat. **This gap is not closed in the primary sources.**
5. **OpenAI's legal texts could not be verified live** (HTTP 403 throughout on `openai.com`). All quotations come from archive snapshots of the original text dated 2026-08-25 and 2026-08-21.
6. **Absolute rate limits for Claude Pro/Max are quantified nowhere.** Checked: the support articles on Pro, Max, usage and length limits, best practices, and [Manage costs effectively](https://code.claude.com/docs/en/costs). All figures are relative ("at least five times", "20 times more usage per session") or point at the usage display. The separate Opus weekly limit too is only mentioned, not quantified.
7. **For OpenAI only ranges are documented, no weekly numbers.** "Additional weekly limits may apply" names neither a threshold nor a reset time.
8. **The current state of Anthropic's Agent SDK monthly allowance is open.** The support article of 16 June 2026 says the announced change is suspended ("We're pausing the changes"); a successor article could not be found. Until then, per the same page: "Claude Agent SDK, `claude -p`, and third-party app usage still draw from your subscription's usage limits."
9. **That a Claude Code hook cannot start a run is written nowhere as a sentence.** It follows from the complete event list (every event describes a point within a session), not from a negative statement.
10. **Where GitHub's third-party agents (Claude, Codex) physically execute is not documented.** All that is substantiated is "Coding agents consume GitHub Actions minutes and AI credits". A sentence like "executes on GitHub Actions runners" exists for Copilot's own agent, not for the third-party ones.
11. **"Agent HQ" is not a documentation term.** It appears exclusively in the GitHub blog; the product documentation knows only "third-party coding agents". Accordingly there is no documented preview/GA status for "Agent HQ" as a whole.
12. **Cursor's own documentation contradicts itself on webhooks.** The webhook page documents `statusChange` fully, while the API overview says "Webhooks are coming soon. The legacy v0 API still supports them." Which statement applies to v1 cannot be resolved.
13. **Whether Devin admits a customer's own LLM key material is not documented.** Checked: the billing and auth pages. There is likewise no explicit "not open source" statement — the finding rests on the absence of a self-hosting path plus "Cognition hosts Devin".
14. **Whether assigning a Rovo agent in Jira fires the Forge event `avi:jira:assigned:issue` is not documented.** Neither the Rovo documentation nor the Forge events reference treats agent accounts as assignee actors. Likewise, any latency or SLA figure for waking by assignment or mention is missing; only the generic Forge trigger latency of "up to 3 minutes" is documented.
15. **The selectable event types under a Rovo agent's "add automation trigger" are not published.** The button is named, the list is not.
16. **None of the `support.atlassian.com` pages quoted carries a change date.** Only `developer.atlassian.com` is dated. The Rovo assignment is explicitly marked as beta.
17. **A Seer trigger through Sentry alert rules could not be substantiated.** The only alert-mediated trigger is the Slack button "Fix with Seer".
18. **MCP's tasks extension has no documented client support.** The official extension support matrix does not list it; the spec says "Task support requires explicit opt-in from both client and server."
19. **There is no open MCP SEP on server-initiated waking.** The thematically closest SEPs all go the other way — they remove server-initiated communication. That is a non-finding, but a telling one: the standard's movement is documented, not merely absent.
20. **A2A's spec document still says internally "Latest Released Version 1.0.0", although the tag reads v1.0.1.** The version identity is substantiated only through the git tag, the releases API and the changelog. Furthermore: many A2A identifiers still in circulation (`tasks/pushNotificationConfig/set`, `message/stream`, `input-required` as a wire value) come from v0.3.0 and no longer exist in v1.0.x.
21. **The GHES wording "HTTP(S) long poll … for 50 seconds" is version-bound.** It appears in the versions for GitHub Enterprise Server 3.12 and 3.13; the current GitHub.com documentation names only the requirement ("outbound HTTPS connections over port 443"), no longer the mechanism. Whether the mechanism changed or only the documentation cannot be substantiated.
22. **It was not tested whether `pa next --wait` with `LISTEN`/`NOTIFY` holds up under load.** All statements about connection counts, proxy timeouts and pooling come from the Postgres and PgBouncer documentation, not from an experiment. A prototype with a realistic number of simultaneously waiting clients is worth building before the real thing.
23. **Sweep's current state cannot be substantiated.** `docs.sweep.dev` answers with HTTP 402 and the GitHub App with 404; only the documentation state of August 2024 plus the README announcement of the pivot can be quoted.
24. **`agents.md` has no formal specification document.** The only normative statements are FAQ lines on the website.

**General caveat.** An unusually large share of what is quoted here carries "research preview", "developer preview", "public preview", "alpha", "beta" or a dated beta header: Claude Code routines and channels, Claude Code on the web, Linear's entire agent API, GitHub's agent tasks API and third-party agents, Cursor's v1 API, Jules' API, Atlassian's Rovo assignment, MCP's tasks extension. **Every decision built on one of these interfaces is a bet on an interface that is allowed to change.** That is precisely the strongest reason to build planaffe's own wake-up mechanism so that it depends on none of them.
