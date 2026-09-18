# Agent skills

planaffe ships three optional skills for agents working in repositories whose
issues live in planaffe. Each is a self-contained folder under [`skills/`](../skills),
versioned with the CLI. They require `pa` configured for an agent identity and
the target project. They do not install the CLI or provision credentials.

| Skill | Outcome | Default selection |
|---|---|---|
| [`planaffe-plan-epic`](../skills/planaffe-plan-epic/SKILL.md) | Create implementable issues with acceptance criteria and blockers | Oldest open epic with zero issues, counting done and canceled issues too |
| [`planaffe-answer-questions`](../skills/planaffe-answer-questions/SKILL.md) | Discuss options with the user and save their answers | All open questions in the current project, across all epics |
| [`planaffe-deliver-epic`](../skills/planaffe-deliver-epic/SKILL.md) | Implement, check, commit, push and merge an epic | Epic of the first workable issue in `pa next` order belonging to an open epic |

These are product workflows, independent of this repository's own development
backlog. Their instructions contain no instance addresses, credentials or
private project data. A project must be resolved before any selection; missing
configuration never means permission to process the entire instance.

## Install

For Codex, ask the built-in skill installer to install the three folders from
this repository:

```text
Use $skill-installer to install these skills from datavisionzero/planaffe:
skills/planaffe-plan-epic
skills/planaffe-answer-questions
skills/planaffe-deliver-epic
```

Alternatively, copy the desired skill folders from a trusted checkout into the
agent's skill directory. For repository-scoped Codex use, the resulting paths
are `.agents/skills/planaffe-plan-epic/SKILL.md`,
`.agents/skills/planaffe-answer-questions/SKILL.md` and
`.agents/skills/planaffe-deliver-epic/SKILL.md`. Copy the folder, not just its
contents. Other harnesses can use their own supported skill installation path;
the workflows use the CLI and ordinary Markdown instructions.

Install from the same tag or commit as the CLI when pinning versions. Before
updating, compare any locally customized copy rather than overwriting it. These
folders are the distribution source; cloning planaffe alone does not install
them into a user's agent. No plugin or separate installer is required.

## Use

In the repository to work on, invoke a skill by name. These examples use Codex
syntax; `PROJ-E3` is a placeholder for an actual epic key:

```text
$planaffe-plan-epic PROJ-E3
$planaffe-plan-epic
$planaffe-answer-questions
$planaffe-deliver-epic PROJ-E3
$planaffe-deliver-epic
```

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

The delivery skill uses the harness's goal facility when available. Without
one it reports the limitation and tracks progress explicitly; a skill cannot
add that facility to a harness. An existing unrelated goal is not replaced.

Internal issue keys, questions and descriptions are not automatically copied
into public commits or pull requests. The agent records decisions and results
in planaffe and publishes only the implementation and its appropriate public
explanation. Goal tracking, git operations and forge access belong to the agent
environment; these skills add no new server or CLI commands.
