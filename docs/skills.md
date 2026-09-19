# Agent skills

planaffe ships six optional skills for agents working in repositories whose
issues live in planaffe. Each is a self-contained folder under [`skills/`](../skills),
versioned with the CLI. They require `pa` configured for an agent identity and
the target project. They do not install the CLI or provision credentials.

| Skill | Outcome | Default selection |
|---|---|---|
| [`planaffe-project-overview`](../skills/planaffe-project-overview/SKILL.md) | Summarize unfinished work, questions and progress, and recommend a next step without changing anything | Current project, across all epics and repository labels |
| [`planaffe-plan-epic`](../skills/planaffe-plan-epic/SKILL.md) | Create implementable issues with acceptance criteria and blockers | Oldest open epic with zero issues, counting done and canceled issues too |
| [`planaffe-answer-questions`](../skills/planaffe-answer-questions/SKILL.md) | Discuss options with the user and save their answers | All open questions in the current project, across all epics |
| [`planaffe-deliver-epic`](../skills/planaffe-deliver-epic/SKILL.md) | Implement, check, commit, push and merge an epic | Epic of the first workable issue in `pa next` order belonging to an open epic |
| [`planaffe-deliver-next-issue`](../skills/planaffe-deliver-next-issue/SKILL.md) | Deliver exactly one issue as one goal through commit, push and verified merge | First workable issue in the current repository, with or without an epic |
| [`planaffe-deliver-standalone-issues`](../skills/planaffe-deliver-standalone-issues/SKILL.md) | Deliver ready, workable issues without an epic as one goal, with a commit per implemented issue and a verified merge | Current project's eligible standalone queue in the current repository |

These are product workflows, independent of this repository's own development
backlog. Their instructions contain no instance addresses, credentials or
private project data. A project must be resolved before any selection; missing
configuration never means permission to process the entire instance.

## Install

For Codex, ask the built-in skill installer to install the six folders from
this repository:

```text
Use $skill-installer to install these skills from datavisionzero/planaffe:
skills/planaffe-project-overview
skills/planaffe-plan-epic
skills/planaffe-answer-questions
skills/planaffe-deliver-epic
skills/planaffe-deliver-next-issue
skills/planaffe-deliver-standalone-issues
```

Alternatively, copy the desired skill folders from a trusted checkout into the
agent's skill directory. For repository-scoped Codex use, the resulting paths
are `.agents/skills/planaffe-project-overview/SKILL.md`,
`.agents/skills/planaffe-plan-epic/SKILL.md`,
`.agents/skills/planaffe-answer-questions/SKILL.md`,
`.agents/skills/planaffe-deliver-epic/SKILL.md`,
`.agents/skills/planaffe-deliver-next-issue/SKILL.md` and
`.agents/skills/planaffe-deliver-standalone-issues/SKILL.md`. Copy the folder,
not just its contents. Other harnesses can use their own supported skill installation path;
the workflows use the CLI and ordinary Markdown instructions.

Install from the same tag or commit as the CLI when pinning versions. Before
updating, compare any locally customized copy rather than overwriting it. These
folders are the distribution source; cloning planaffe alone does not install
them into a user's agent. No plugin or separate installer is required.

## Use

In the repository to work on, invoke a skill by name. These examples use Codex
syntax; `PROJ-E3` is a placeholder for an actual epic key:

```text
$planaffe-project-overview
$planaffe-plan-epic PROJ-E3
$planaffe-plan-epic
$planaffe-answer-questions
$planaffe-deliver-epic PROJ-E3
$planaffe-deliver-epic
$planaffe-deliver-next-issue
$planaffe-deliver-standalone-issues
```

The project overview gives a short, read-only snapshot: open epics and their
purpose, remaining issues, workable issues, open questions and reviews. It
highlights partly finished epics, empty epics and work outside open epics, then
recommends a concrete next step with a reason. It covers the whole current
project, including other repository labels, and does not claim work, answer
questions, change tickets or start implementation.

Planning asks only for decisions it cannot resolve from the epic and code. It
creates clear issues as ready and attaches a question to work still waiting on
a human. Repeated invocation fills gaps without duplicating existing work.

The question round presents one issue at a time, with recommended options and
their consequences. A clear user answer authorizes saving it without a second
confirmation. Free-form answers, skipping and stopping are supported. Skipped
questions remain open. The skill does not implement issues, change readiness or
approve reviews. Answering a question may make work available to other agents.

Explicitly requesting epic delivery includes committing, pushing and merging
the epic's work. The agent preferably commits once per issue and pushes after
implementation, then follows the request through CI to actual merge. Required
human reviews and repository rules still apply. A project that defines done as
merged may require incremental merges to unblock dependent issues. Missing
access or approvals are reported rather than bypassed.

Standalone issue delivery follows the same publication and verification path
for the current repository's ready, workable issues without an epic. It uses
`pa next --ready --epic none` for selection and atomically claims one issue at
a time. A ticket with an epic never enters this run. The agent makes a focused
commit for each implemented issue and stops when the scoped queue is empty,
then verifies the merge and reconciles issue status.

Single-issue delivery calls `pa next --claim` once, without an epic filter, to
select and claim the highest-ranked workable issue in the current repository.
The issue may belong to an epic or stand alone. The agent creates one goal for
that issue and follows its commit and request through actual merge and issue
acceptance. It does not claim another issue during the run.

The epic and standalone queue skills use the harness's goal facility when
available; otherwise they report the limitation and track progress explicitly.
The single-issue skill requires a goal and stops before claiming when the
harness cannot create one. An existing unrelated goal is never replaced.

Internal issue keys, questions and descriptions are not automatically copied
into public commits or pull requests. The agent records decisions and results
in planaffe and publishes only the implementation and its appropriate public
explanation. Goal tracking, git operations and forge access belong to the agent
environment; these skills add no new server or CLI commands.
