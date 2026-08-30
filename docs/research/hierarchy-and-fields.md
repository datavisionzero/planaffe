# Hierarchy Levels and Field Sets: GitHub, GitLab, Linear, Jira

**Date:** 2026-08-30
**Context:** research backlog from [`VISION.md`](../../VISION.md), sections 7, 8 and 16.

## Questions

1. **Hierarchy:** which structural levels do GitHub Issues, GitLab, Linear and Jira model? How do they relate, how deep may nesting go (hard limits), what is optional and what is mandatory? What is the smallest set a team of 1–5 people can work with?
2. **Field set:** which fields does an issue have in these four systems, which are mandatory on creation, and how exactly are priority, estimation, due date, status, labels, assignees and issue relationships handled?

## Method

Primary sources only: `docs.github.com`, `docs.gitlab.com`, `linear.app/docs` plus `developers.linear.app` and the official GraphQL endpoint `api.linear.app/graphql`, as well as `developer.atlassian.com`, `support.atlassian.com` and `confluence.atlassian.com`. No blog posts, no comparison articles. Web search was used only to find the right documentation pages; the statements themselves are verified against the documentation text.

Where a claim could not be backed by a primary source, that is stated explicitly. No field names and no limits were guessed.

**Timing:** the market moved noticeably between 2024 and 2026 — GitHub added sub-issues, issue types, issue dependencies and issue fields; GitLab is migrating issues and epics onto a unified work-item model. Some of the limits quoted are explicitly dated (e.g. Jira guardrails from March and September 2026, GitLab fields from version 19.1). This snapshot is as of 2026-08-30.

---

# Part 1: Hierarchy

## 1.1 GitHub

For a long time GitHub knew only two levels (repository → issue) and retrofitted the hierarchy later.

**Levels:**

| Level | Bound to | Note |
|---|---|---|
| Organisation | — | defines issue types and issue fields |
| Repository | org/user | holds issues, labels, milestones |
| Milestone | repository | optional grouping |
| Issue | repository | — |
| Sub-issue | parent issue | a real hierarchy |
| Project (v2) | user/org | **orthogonal**, not a level in the tree |

**Sub-issues — the hard limits.** The documentation is unusually explicit here: "You can add up to **100 sub-issues** per parent issue and create up to **eight levels** of nested sub-issues" ([Adding sub-issues](https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/adding-sub-issues)). Sub-issues may live in another repository, but the REST API requires: "The sub-issue must belong to the **same repository owner** as the parent issue" ([REST: Sub-issues](https://docs.github.com/en/rest/issues/sub-issues)).

Every issue has **at most one** parent. That is not documented as a sentence but follows unambiguously from the API shape: the endpoint is `GET /repos/{owner}/{repo}/issues/{issue_number}/parent` (singular), the issue schema carries `parent_issue_url` as a scalar, and adding one takes a `replace_parent` parameter ([REST: Sub-issues](https://docs.github.com/en/rest/issues/sub-issues)).

**Milestones** are bound to a repository: "You can use milestones to track progress on groups of issues or pull requests **in a repository**" ([About milestones](https://docs.github.com/en/issues/using-labels-and-milestones-to-track-work/about-milestones)). An issue carries **at most one** milestone — the REST parameter `milestone` is a scalar and the response field a single object, not an array ([REST: Issues](https://docs.github.com/en/rest/issues/issues)).

**Projects (v2)** are not a level in the issue tree but a view and metadata level across repositories: "a project is an adaptable table, board, and roadmap … at the user or organization level", and "you can include issues and pull requests from any organization" ([About projects](https://docs.github.com/en/issues/planning-and-tracking-with-projects/learning-about-projects/about-projects)). Limits: at most 50 fields per project (ibid.) and "a maximum of **50,000 items** across both active views and the archive page" ([Adding items](https://docs.github.com/en/issues/planning-and-tracking-with-projects/managing-items-in-your-project/adding-items-to-your-project)).

**There are no epics.** The overview page [About issues](https://docs.github.com/en/issues/tracking-your-work-with-issues/learning-about-issues/about-issues) lists as structural features only sub-issues, labels, milestones, issue types, issue dependencies and projects — "epic" does not appear. The word shows up in the documentation only as a *working term* in a Copilot tutorial: "Epics represent large bodies of work, while features and tasks break the work into smaller, actionable pieces" ([Plan a project](https://docs.github.com/en/copilot/tutorials/plan-a-project)). The concept is rebuilt from an issue type plus sub-issues.

**Task lists are gone.** The structured `[tasklist]` block is dead: "Tasklist blocks are retired" ([About tasklists](https://docs.github.com/en/get-started/writing-on-github/working-with-advanced-formatting/about-tasklists)); the page points at sub-issues. Plain Markdown checkboxes (`- [ ]`) still work but create neither a hierarchy nor a typed relationship.

## 1.2 GitLab

GitLab has the most complex of the four models — and it is at the same time the one in which the **licence tier** decides the hierarchy. That matters especially for planaffe, because the target group works self-hosted and therefore effectively compares against GitLab Free.

**Levels:** instance → group/subgroup → project → work item. Subgroups may "be nested up to **20 levels**", but the documentation recommends in the same breath: "To avoid performance problems, nest groups to a maximum of **five levels** or fewer" ([Subgroups](https://docs.gitlab.com/user/group/subgroups/)).

**Work items.** GitLab traces issues, tasks, epics and OKRs back to a common model. The user documentation names as types issues, epics, tasks, objectives and key results, and test cases ([Work items](https://docs.gitlab.com/user/work_items/)). The developer documentation lists the system-defined types in full with ID and availability: Issue (1, CE+EE), Incident (2, CE+EE), Test Case (3, EE), Requirement (4, EE), Task (5, CE+EE), Objective (6, EE), Key Result (7, EE), Epic (8, EE), Ticket (9, CE+EE) ([Work items development](https://docs.gitlab.com/development/work_items/)).

**The permitted hierarchy**, verbatim: "Child items can be: A child epic nested under a parent epic. An issue such as a feature or bug assigned to an epic. A task that can be a child of an issue" ([Child items](https://docs.gitlab.com/user/work_items/child_items/)). The tree shown there reads `Parent epic → Child epic → Issue → Task`.

**Epic nesting — a hard limit:** "Epics can contain multiple nested child epics, up to a total of **7 levels deep**", and "The maximum number of direct child issues and epics is **5000**" ([Child items](https://docs.gitlab.com/user/work_items/child_items/)). The section carrying that number bears the tier badge **Ultimate**.

**Licence tiers — the decisive point:**

| Level | GitLab Free? | Source |
|---|---|---|
| Groups/subgroups | yes | [Subgroups](https://docs.gitlab.com/user/group/subgroups/) |
| Issues | yes | [Work items](https://docs.gitlab.com/user/work_items/) |
| Tasks (a kind of issue) | yes | [Tasks](https://docs.gitlab.com/user/tasks/) |
| Milestones (project + group) | yes | [Milestones](https://docs.gitlab.com/user/project/milestones/) |
| **Epics** | **no** (Premium+) | [Epics](https://docs.gitlab.com/user/group/epics/) |
| **Nested epics** | **no** (Ultimate) | [Child items](https://docs.gitlab.com/user/work_items/child_items/) |
| **Iterations** | **no** (Premium+) | [Iterations](https://docs.gitlab.com/user/group/iterations/) |
| **OKRs** | **no** (Ultimate) | [OKRs](https://docs.gitlab.com/user/okrs/) |

For a self-hosted GitLab Free, exactly this remains: **group → project → issue → task**, plus milestones as an optional grouping. Precisely the set planaffe needs.

**Tasks** are free: "A task in GitLab is a planning item that can be created in an issue. Use tasks to break down user stories captured in issues into smaller, trackable items" ([Tasks](https://docs.gitlab.com/user/tasks/)). The documentation shows only `Issue → Task`, i.e. **one** child level; whether tasks can have child tasks of their own and whether there is a depth limit for that is *not documented*.

**Milestones:** "Project milestones apply to issues and merge requests in that project only. Group milestones apply to any issue, epic, or merge request in that group's projects." And explicitly: "Every issue, epic, or merge request can be assigned **one milestone**" ([Milestones](https://docs.gitlab.com/user/project/milestones/)).

**Iterations** (sprints) are Premium+ and "are only available to **groups**"; they sit in *iteration cadences* and "require both a start and an end date. Iteration date ranges cannot overlap within an iteration cadence" ([Iterations](https://docs.gitlab.com/user/group/iterations/)).

**Additional finding:** since GitLab 19.1, custom work-item types are configurable (Premium+), with the limit of "a maximum of **40** work item types in a top-level group or organization, including types provided by GitLab". Notable for the hierarchy question: "New types are available at the project level only. Their widgets and **hierarchy restrictions match those of issues**" ([Configurable work item types](https://docs.gitlab.com/user/work_items/configurable_work_item_types/)).

## 1.3 Linear

Linear has the cleanest model and separates **execution hierarchy** (issue/sub-issue) from **planning grouping** (project, cycle, initiative) consistently.

**Levels**, per the official conceptual model ([Conceptual model](https://linear.app/docs/conceptual-model)):

- **Workspace** — "the container for all issues, teams and other concepts relating to an individual company"
- **Team** — "the primary organizational unit in Linear. Each team owns its own workflow, triage process, and planning cadence."
- **Issue** — "the fundamental unit of work in Linear"
- **Cycle** — "a team's repeating planning period for issues"
- **Project** — "group issues together around a shared outcome"
- **Milestone** — "a concept used to further organize issues inside an individual project"
- **Initiative** — "sit above projects and represent broader strategic efforts"

**Cardinalities.** An issue belongs to **exactly one** team; the GraphQL description of the field `Issue.team: Team!` says: "Every issue must belong to exactly one team, which determines the available workflow states, labels, and other team-specific configuration" (introspection of `api.linear.app/graphql`), confirmed by "each issue belongs to exactly one team" ([Teams](https://linear.app/docs/teams)). A project, on the other hand, "can be shared across multiple teams" ([Projects](https://linear.app/docs/projects)).

An issue can be assigned to a project **and** a cycle **and** a milestone at the same time — they are independent fields — but only **one** of each: "issues can only be associated with one project at a time" ([Projects](https://linear.app/docs/projects)), "Each issue can be assigned to one milestone" ([Project milestones](https://linear.app/docs/project-milestones)). For cycles the single-valuedness follows only from the schema cardinality (`Issue.cycle` is a single field); *a prose citation is missing*.

**Sub-issues:** the schema description of the `Issue` type states a limit: "Issues support sub-issues (**parent-child hierarchy up to 10 levels deep**), labels, due dates, estimates, and SLA tracking" (introspection of `api.linear.app/graphql`). **Important:** that number appears *only* in the GraphQL schema; the user documentation [Parent and sub-issues](https://linear.app/docs/parent-and-sub-issues) names no depth limit.

Inheritance when creating a sub-issue (ibid.): team, priority and project are **always** inherited; cycle and assignee conditionally; **labels are not**.

**Cycles** are optional and switchable per team (`Team.cyclesEnabled`), 1–8 weeks long ([Cycles](https://linear.app/docs/use-cycles)). They are "similar to a sprint" but not tied to releases.

**Initiatives** sit above projects and contain **no issues directly**. They are nestable: "Sub-initiatives can be nested **up to five levels deep** in a tree-like structure" and — unusually — "**Initiatives can have multiple parents**" ([Sub-initiatives](https://linear.app/docs/sub-initiatives)). Sub-initiatives are **Enterprise plan**.

**Roadmaps as a concept of their own are gone.** The schema marks the type: `Roadmap: "[Deprecated] A roadmap for grouping projects. Use Initiative instead"` (introspection); the documentation confirms the rename ([Projects](https://linear.app/docs/projects)).

**There are no epics.** No corresponding GraphQL type and no issue field exists. How Linear catches the concept is shown most clearly by the Jira import documentation: "Jira epics automatically sync as **Linear projects**, maintaining parent-child relationships between issues and their projects/epics" ([Jira import](https://linear.app/docs/jira)). For work packages that are "too large to be a single issue but too small to be a project", Linear points at parent/sub-issues ([Parent and sub-issues](https://linear.app/docs/parent-and-sub-issues)).

## 1.4 Jira

Jira is the only system with an **explicitly numbered, configurable** hierarchy.

**Default levels**, verbatim: "By default, Jira is set up with three levels of work type hierarchy: a level for larger pieces of work (**level 1**, by default called Epic), a level for standard work items (**level 0**, called Story), and a level for smaller pieces of work (**level -1**, called Subtask)" ([Configure the work type hierarchy](https://support.atlassian.com/jira-cloud-administration/docs/configure-the-issue-type-hierarchy/)). In software projects, story, bug and task sit on level 0 ([What are issue types](https://support.atlassian.com/jira-cloud-administration/docs/what-are-issue-types/)).

The API confirms the numbering: `hierarchyLevel` — "Use: `-1` for Subtask. `0` for Base"; and in the hierarchy payload "0, 1, 2, 3 .. n; Negative values for subtasks" ([REST v3: Issue types](https://developer.atlassian.com/cloud/jira/platform/rest/v3/api-group-issue-types/)). Through `POST /rest/api/3/issuetype` only `-1` and `0` can be created — higher levels cannot.

**Additional levels** cost money: "**Jira Premium and Enterprise** customers can also create and manage additional levels in their work type hierarchy"; and new levels always appear **at the top**: "A new level will be created at the top of the work type hierarchy" ([Configure the work type hierarchy](https://support.atlassian.com/jira-cloud-administration/docs/configure-the-issue-type-hierarchy/)).

**A hard limit on the number of additional levels could not be substantiated.** On the contrary: the Data Center documentation speaks of "create **as many hierarchy levels** … as you want" ([Configuring initiatives and other hierarchy levels](https://confluence.atlassian.com/advancedroadmapsserver0329/configuring-initiatives-and-other-hierarchy-levels-1021218664.html)), the OpenAPI schema of "0, 1, 2, 3 .. n", and the official [Data limits and guardrails](https://support.atlassian.com/jira-cloud-administration/docs/data-limits-and-guardrails/) page does not list hierarchy levels at all. The name **"initiative" is not a system default** either, but an example of a convention — it is "not included in the default hierarchy level in Advanced Roadmaps" (DC documentation, ibid.).

**Subtasks are the leaves:** "any work type can be both a parent and a child work item — the only exception being subtasks, which can only be a child"; and explicitly "**A subtask can't have any child work items**" ([What are issue types](https://support.atlassian.com/jira-cloud-administration/docs/what-are-issue-types/)). The same page names a display limit: "a work item can only display up to **500** child work items".

**An issue belongs to exactly one project:** "An issue can live in a **single** Jira project" ([Advanced Roadmaps DC](https://confluence.atlassian.com/advancedroadmapsserver0329/configuring-initiatives-and-other-hierarchy-levels-1021218664.html)).

**Sprints, versions and components** are not hierarchy levels but cross-cutting groupings. Sprints exist only in Scrum: "Only Scrum teams can use sprints" ([What is a sprint](https://support.atlassian.com/jira-software-cloud/docs/what-is-a-sprint/)). `fixVersions`, `versions` (= affects version) and `components` are **arrays** in the official create example, so they can be set multiple times ([REST v3: Issues](https://developer.atlassian.com/cloud/jira/platform/rest/v3/api-group-issues/)). Components exist only in company-managed projects ([What are Jira components](https://support.atlassian.com/jira-software-cloud/docs/what-are-jira-components/)).

**Boards are a view, not a container.** A board is a JQL-filtered view that can span project boundaries: "you might create a board that includes work items **from multiple spaces**, one space, or from a particular component" ([Configure filters](https://support.atlassian.com/jira-software-cloud/docs/configure-filters/)); display limit 5,000 items ([What is a Jira board](https://support.atlassian.com/jira-software-cloud/docs/what-is-a-jira-software-board/)).

## 1.5 Hierarchy comparison

| | GitHub | GitLab | Linear | Jira |
|---|---|---|---|---|
| **Top administrative level** | organisation | instance | workspace | site |
| **Grouping above the container** | — | group/subgroup (up to 20 deep, ≤ 5 recommended) | team (sub-teams up to 5, Enterprise) | — |
| **Container of the issue** | repository | project | team | project |
| **Container membership** | exactly 1 | exactly 1 | exactly 1 | exactly 1 |
| **Issue level** | issue | issue (work item) | issue | story/task/bug (level 0) |
| **Child level** | sub-issue | task | sub-issue | subtask (level -1) |
| **Nesting depth** | **8 levels** | issue→task = 1 documented level | **10 levels** (only in the schema) | subtask = leaf, **no** children |
| **Max. children per parent** | **100** | 5000 (epic children) | not documented | 500 (display limit) |
| **Level above the issue** | — (no epic) | epic (**Premium+**), up to 7 deep (Ultimate) | — (no epic); project as grouping | epic (level 1); further levels **Premium+** |
| **Milestone/version** | milestone (repo), **max. 1** | milestone (project + group), **max. 1** | project milestone, **max. 1** | fixVersions/versions, **several** |
| **Sprint/cycle** | iteration field in Projects v2 | iteration (**Premium+**, group only) | cycle (optional per team, 1–8 weeks) | sprint (Scrum only) |
| **Portfolio level** | — | objective/key result (**Ultimate**) | initiative (sub-initiatives: 5 deep, **Enterprise**) | custom level ≥ 2 (**Premium+**) |
| **Board/project view** | Projects v2 (cross-repo, 50,000 items) | issue boards | views | boards (JQL, 5,000 items) |
| **Native "epic" construct** | **no** | yes (Premium+) | **no** | yes (= an issue type on level 1) |

**How to read this:** across all four systems only **two levels are genuinely universal and free**: a container (repo/project/team) and the issue. A **child level** is likewise present everywhere and free everywhere. Everything above that — epic, initiative, objective, custom level — is either absent altogether (GitHub, Linear) or paid (GitLab Premium, Jira Premium).

It is also notable that the two systems with the **fastest reputation** (GitHub, Linear) have no epic construct at all and deliberately map the concept onto parent/child or projects.

---

# Part 2: Field Sets

## 2.1 GitHub

Basis: `POST /repos/{owner}/{repo}/issues` and the issue response schema, [REST: Issues](https://docs.github.com/en/rest/issues/issues).

| Field | Required/optional | Range | Note |
|---|---|---|---|
| `title` | **required** | string or integer | the only mandatory field on creation |
| `body` | optional | string (Markdown) | — |
| `assignees` | optional | array of logins | **max. 10**; without push rights "silently dropped" |
| `labels` | optional | array | without push rights "silently dropped" |
| `milestone` | optional | milestone **number**, scalar | max. 1 per issue |
| `type` | optional | name of an issue type | scalar → exactly 1 type per issue |
| `issue_field_values` | optional | array of field ID plus value | only for org repositories with the feature enabled |
| `state` | **not settable on creation** | `open` / `closed` | a new issue is always `open` |
| `state_reason` | update only | `completed`, `not_planned`, `duplicate`, `reopened`, `null` | "Ignored unless state is changed" |
| `locked` / `active_lock_reason` | lock endpoint only | `off-topic`, `too heated`, `resolved`, `spam` | — |
| `parent_issue_url` | derived | URL or null | scalar → max. 1 parent |
| `sub_issues_summary`, `issue_dependencies_summary` | read-only | counters | — |

**Priority:** **absent** from the classic issue core schema. Since the *issue fields* feature, however, native at organisation level: "Instead of relying on labels or free-text workarounds, you can create fields like priority, effort, impact"; "Fields are defined at the organization level"; "You can create up to **25 issue fields** per organization." The default fields created automatically are **Priority** (Urgent/High/Medium/Low), **Effort** (High/Medium/Low), **Start date** and **Target date** ([Managing issue fields](https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/managing-issue-fields-in-your-organization)). API: [REST: Issue fields](https://docs.github.com/en/rest/orgs/issue-fields).

**Estimation:** no numeric estimate field in the core schema. The default issue field **Effort** is ordinal (High/Medium/Low), not numeric (ibid.).

**Due date:** no `due_date` on the issue. Substitutes: the default issue field **Target date**, the due date of a **milestone** ([About milestones](https://docs.github.com/en/issues/using-labels-and-milestones-to-track-work/about-milestones)), or a date field in Projects v2.

**Assignees:** "Both issues and pull requests support up to **10 assignees**" ([Assigning issues](https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/assigning-issues-and-pull-requests-to-other-github-users)). GitHub is thus the only one of the four that allows multiple assignment without a surcharge.

**Relationships:** four mechanisms.

1. **Parent/child** through sub-issues (see 1.1).
2. **Issue dependencies** — a real, typed blocks/blocked-by relation with its own endpoints `…/dependencies/blocked_by` and `…/dependencies/blocking` ([REST: Issue dependencies](https://docs.github.com/en/rest/issues/issue-dependencies)); UI actions "Mark as blocked by" / "Mark as blocking", and "Blocked issues are marked with a 'Blocked' icon … so you can easily identify bottlenecks" ([Creating issue dependencies](https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/creating-issue-dependencies)).
3. **Duplicate** — through `state_reason: "duplicate"` and the text convention "Duplicate of" ([Using keywords](https://docs.github.com/en/get-started/writing-on-github/working-with-advanced-formatting/using-keywords-in-issues-and-pull-requests)).
4. **Linked pull requests** through closing keywords (`closes`, `fixes`, `resolves` …), but only "when the pull request targets the repository's **default branch**" ([Linking a PR to an issue](https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/linking-a-pull-request-to-an-issue)).

A **`related` relation type is missing** — plain `#123` mentions only produce an untyped cross-reference in the timeline feed.

**Issue types:** "You can create up to **25 issue types**"; "The default types are **task, bug, and feature**" ([Managing issue types](https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/managing-issue-types-in-an-organization)).

## 2.2 GitLab

Basis: `POST /projects/:id/issues`, [REST: Issues](https://docs.gitlab.com/api/issues/).

| Field | Required/optional | Range | Note |
|---|---|---|---|
| `id` (project) | **required** | ID or URL-encoded path | — |
| `title` | **required** | string | — |
| `description` | optional | string | "Limited to **1,048,576 characters**" |
| `assignee_id` | optional | integer | "**Only appears on GitLab Free**" |
| `assignee_ids` | optional | array | "**Premium and Ultimate only**" |
| `labels` | optional | comma-separated names | labels that do not exist are created |
| `milestone_id` / `milestone` | optional | ID or title | mutually exclusive; max. 1 |
| `due_date` | optional | `YYYY-MM-DD` | — |
| `start_date` | optional | `YYYY-MM-DD` | "Introduced in GitLab **19.1**" |
| `weight` | optional | integer ≥ 0 | "**Premium and Ultimate only**" |
| `epic_id` | optional | integer ≥ 0 | "**Premium and Ultimate only**" |
| `issue_type` | optional | `issue`, `incident`, `test_case`, `task` | default `issue`; on **update**, `task` is not permitted |
| `confidential` | optional | boolean | default `false` |
| `severity` | optional | `unknown`, `low`, `medium`, `high`, `critical` | "Applies only to incidents" |
| `state_event` (update only) | optional | `close`, `reopen` | — |
| `health_status`, `iteration_id` | **not settable** through the issues REST API | — | `GET` filters only (Ultimate and Premium+ respectively) |

**Priority: no native field.** No attribute table of the issues API contains `priority`. Instead, two mechanisms:

- **Prioritised labels:** "Labels can have relative priorities … **Priority sorting is based on the highest priority label only**" ([Labels](https://docs.gitlab.com/user/project/labels/)).
- **Sorting:** `order_by` accepts `priority` and `label_priority` among others ([REST: Issues](https://docs.gitlab.com/api/issues/)). The semantics of "priority" are remarkably tangled: "Issues with milestones that have due dates, where the soonest assigned milestone is listed first. Issues with milestones with no due dates. Issues with a higher priority label. Issues without a prioritized label. **Ties are broken arbitrarily**" ([Sorting issue lists](https://docs.gitlab.com/user/project/issues/sorting_issue_lists/)).
- As a convention, the documentation additionally names scoped labels: `priority::low` / `priority::high` ([Labels](https://docs.gitlab.com/user/project/labels/)).

**Estimation:** `weight` (Premium+, "whole, positive numbers", API ≥ 0, [Work item weight](https://docs.gitlab.com/user/work_items/weight/)) and **time tracking** (Free): the quick actions `/estimate` and `/spend`, units `mo w d h m`, where "a month equals 160 hours, a week equals 40 hours" ([Time tracking](https://docs.gitlab.com/user/project/time_tracking/)).

**Status:** only **two** persisted states, `opened` and `closed`. A real status field is Premium+: "Status provides more granular tracking than the traditional binary open/closed state system used in **GitLab Free**." Default statuses there: **To do, In progress, Done, Won't do, Duplicate**; the five categories: **Triage, To do, In progress, Done, Canceled**. And the coupling: "Statuses in the Done and Canceled categories automatically set work items to **closed** state. All other categories maintain work items in **open** state" ([Status](https://docs.gitlab.com/user/work_items/status/)).

**Scoped labels** (Premium+): "A scoped label uses a **double-colon (`::`) syntax**", and the exclusivity: "An issue … cannot have two scoped labels, of the form `key::value`, with the same `key`. If you add a new label with the same key but a different value, the previous label is replaced" ([Labels](https://docs.gitlab.com/user/project/labels/)).

**Workflow labels:** GitLab documents `workflow::` labels explicitly — but as a convention **of the GitLab project itself**, not as a product feature: "Issues use the following workflow labels to specify the current issue status", including `~"workflow::ready for development"`, `~"workflow::in dev"`, `~"workflow::in review"`, `~"workflow::blocked"`, `~"workflow::complete"` ([Labels — development](https://docs.gitlab.com/development/labels/)). That a fresh instance would come with such labels preinstalled is *not documented*.

**Relationships:** the base page "Linked issues" is Free, and the choices are "relates to", "blocks", "is blocked by" — **but** the section "Blocking issues" carries the **Premium/Ultimate** badge ([Related issues](https://docs.gitlab.com/user/project/issues/related_issues/)). API values: "`relates_to`, `blocks`, `is_blocked_by`, defaults to `relates_to`" ([REST: Issue links](https://docs.gitlab.com/api/issue_links/)). Cross-project links are possible. **`duplicate` is not a link type**; instead there is the quick action `/duplicate` ([Quick actions](https://docs.gitlab.com/user/project/quick_actions/)) and the response field `closed_as_duplicate_of`.

## 2.3 Linear

Basis: introspection of `api.linear.app/graphql` (the types `Issue`, `IssueCreateInput`, `WorkflowState`, `IssueRelationType`, `Team`) and [developers.linear.app](https://linear.app/developers/graphql).

**The most striking finding: `IssueCreateInput` has exactly one NON_NULL field — `teamId`.** Even `title` is declared nullable in the schema.

| Field | Required/optional | Range | Note |
|---|---|---|---|
| `teamId` | **required** (`String!`) | team ID | the only mandatory field |
| `title` | optional (in the schema) | string | the getting-started guide treats it as required — a contradiction, see open points |
| `description` | optional | "The issue description in **markdown** format" | — |
| `assigneeId` | optional | **one** user ID | scalar, not an array |
| `delegateId` | optional | user ID | "The identifier of the **agent user** to delegate the issue to" |
| `stateId` | optional | workflow state ID | default: "team's first Backlog state or Triage if enabled" |
| `priority` | optional | `0`–`4` | see below |
| `estimate` | optional | int | the scale is configured per team |
| `dueDate` | optional | `TimelessDate` | **date only, no time of day** |
| `labelIds` | optional | array | — |
| `parentId` | optional | UUID **or** identifier (`LIN-123`) | — |
| `projectId`, `cycleId`, `projectMilestoneId` | optional | 1 each | — |
| `subscriberIds` | optional | array | involvement without assignment |
| `sortOrder`, `prioritySortOrder`, `subIssueSortOrder` | optional | float | manual ordering |
| `slaType` | optional | `all`, `onlyBusinessDays` | — |

**Priority — a fixed scale, verbatim from the schema:** "The priority of the issue. **0 = No priority, 1 = Urgent, 2 = High, 3 = Medium, 4 = Low**." Optional, with an effective default of `0`. And explicitly not extensible: "We don't have the option to set custom priorities or more granular priorities" — as a workaround the documentation suggests additional statuses or labels ([Priority](https://linear.app/docs/priority)). A team can require priority before an issue leaves triage (`Team.requirePriorityToLeaveTriage`).

**Estimate — four scales, opt-in per team.** Schema: `Team.issueEstimationType` "Must be one of **`notUsed`, `exponential`, `fibonacci`, `linear`, `tShirt`**". Values per [Estimates](https://linear.app/docs/estimates): exponential 1/2/4/8/16, Fibonacci 1/2/3/5/8, linear 1/2/3/4/5, T-shirt XS–XL (each +2 in "extended" mode). Configured per team; `Issue.estimate` is nullable — "Null if no estimate has been set."

**Workflow states — seven types, verbatim from the schema** (`WorkflowState.type`): "One of **`triage`, `backlog`, `unstarted`, `started`, `completed`, `canceled`, `duplicate`**." States are configurable per team; at least one per category is required, and the order of the categories is fixed. The default workflow: "Backlog > Todo > In Progress > Done > Canceled" ([Configuring workflows](https://linear.app/docs/configuring-workflows)). An upper bound on the number of states is *not documented*.

**Assignee: exactly one** (`Issue.assignee: User`, nullable — "Null if the issue is unassigned"). **In addition** there is the separate field `Issue.delegate: User` — "The agent user that is delegated to work on this issue. Set when an **AI agent** has been assigned to perform work on this issue." For involvement without assignment there are `subscribers`.

**Relationships — four types** in the enum `IssueRelationType`: `blocks`, `duplicate`, `related`, `similar`. Relations are **directed**: "Issue relations represent directional relationships … Each relation connects a source issue to a related issue" — "blocked by" is not an enum constant of its own but the inverse direction (`Issue.relations` versus `Issue.inverseRelations`). The UI documentation names four actions: blocked by, blocks, related, duplicate ([Issue relations](https://linear.app/docs/issue-relations)).

**No issue-type field.** Neither `Issue` nor `IssueCreateInput` has a type field. For that the documentation recommends workspace labels — labels "used by all teams (e.g. **'Bug'**)" — and **label groups** as an exclusive choice: "only one label from a given label group can be applied to an issue at a time", at most 250 labels per group ([Labels](https://linear.app/docs/labels)).

## 2.4 Jira

Basis: `POST /rest/api/3/issue`, [REST v3: Issues](https://developer.atlassian.com/cloud/jira/platform/rest/v3/api-group-issues/).

**Jira defines no fixed global list of mandatory fields.** Verbatim: "The fields that can be set in the issue or subtask are determined using the **Get create issue metadata**. These are the same fields that appear on the issue's create screen." Whether a field is mandatory is reported by `createmeta` per project and issue type through the `required` attribute (ibid.) and is controlled by field configurations: "you can also indicate whether a field can be left empty … using the toggle in the **Required** column" ([Create or edit a field configuration](https://support.atlassian.com/jira-cloud-administration/docs/create-or-edit-a-field-configuration/)). Even `assignee` can become mandatory that way — the API example response shows exactly that.

| Field | Required/optional | Range | Note |
|---|---|---|---|
| `project` | effectively required | object `{id}` or `{key}` | exactly 1 |
| `issuetype` | effectively required | object | `createmeta` reports `required: true` |
| `summary` | effectively required | string | maximum length *not documented* |
| `description` | depends on configuration | **ADF** (`{"type":"doc","version":1,…}`) | **not Markdown** |
| `assignee` | depends on configuration | **one** object | `"-1"` = default assignee, `null` = unassigned |
| `reporter` | depends on configuration | one object | separate from the assignee |
| `priority` | depends on configuration | one object `{id}` | see below |
| `labels` | optional | array of strings | — |
| `duedate` | optional | `"2019-05-11"` | a plain date |
| `parent` | optional | one object `{key}` | — |
| `components` | optional | **array** | company-managed only |
| `fixVersions` / `versions` | optional | **arrays** | release and affects-version respectively |
| `timetracking` | optional | `{"originalEstimate":"10", …}` | strings, see below |
| `security` | optional | one object | issue security level |
| `customfield_XXXXX` | depends on configuration | anything | story points live here |
| `status`, `resolution` | **not in the create body** | — | through transitions only |

**Priority — configurable, with defaults.** "Jira comes with a set of default priorities: **Highest, High, Medium, Low, Lowest**. You can modify these default priorities, create new ones" ([Defining priority field values](https://confluence.atlassian.com/adminjiraserver/defining-priority-field-values-938847101.html)); "Both the priorities and their meanings can be **customized by your administrator**" ([Statuses, priorities, resolutions](https://support.atlassian.com/jira-cloud-administration/docs/what-are-issue-statuses-priorities-and-resolutions/)). Priority schemes allow a subset per project plus "a default priority that is assigned to all newly created work items" ([Manage priority schemes](https://support.atlassian.com/jira-cloud-administration/docs/manage-priority-schemes/)). Limit from September 2026: **100 priorities per space** ([Guardrails](https://support.atlassian.com/jira-cloud-administration/docs/data-limits-and-guardrails/)). Whether priority is mandatory in principle depends solely on the field configuration — a blanket statement on that is *not substantiable*.

**Estimation — story points are a custom field** with an **instance-specific** ID. The Atlassian KB shows how to find it and documents the example `customfield_10106: "Story Points"` ([Get custom field IDs](https://confluence.atlassian.com/jirakb/get-custom-field-ids-for-jira-and-jira-service-management-744522503.html)) — a fixed, universally valid number does **not** exist. On top of that the field names differ by project type: team-managed uses "**Story point estimate**", company-managed "**Story Points**", and "you can't use the Story points field in a team-managed project" ([KB: Story points not showing](https://support.atlassian.com/jira/kb/story-points-not-showing-in-custom-board-for-a-team-managed-jira-project/)). Values "must be numerical but can include decimal points" ([Enable estimation](https://support.atlassian.com/jira-software-cloud/docs/enable-estimation/)). By default, story points are assigned only to "story or epic-type" items ([What are story points](https://support.atlassian.com/jira-software-cloud/docs/what-are-story-points/)).

**Time tracking:** "weeks (w), days (d), hours (h), and minutes (m). For example … **2d 4h 30m**"; without a unit the admin default applies ([Log time](https://support.atlassian.com/jira-software-cloud/docs/log-time-on-an-issue/)). The conversion depends on "working hours per day" and "working days per week" ([Configure time tracking](https://support.atlassian.com/jira-cloud-administration/docs/configure-time-tracking/)).

**Status — arbitrarily many, but exactly three categories.** "All statuses, even custom statuses you create yourself, must belong to **one of three status categories – To do, In progress, or Done**. These categories are represented by the colors grey, blue, and green respectively (**this can't be customized**)" ([What is a workflow status](https://support.atlassian.com/jira-cloud-administration/docs/what-is-a-workflow-status/)). Limit from September 2026: 200 statuses per workflow ([Guardrails](https://support.atlassian.com/jira-cloud-administration/docs/data-limits-and-guardrails/)).

**Transitions are restricted** — and that is the core of Jira's ceremony: the transitions API returns only transitions that "can be performed by the user on an issue, **based on the issue's status**"; a transition with a screen "updates the fields from the transition screen"; and without the "transition issues" permission nothing is listed at all ([REST v3: Issues](https://developer.atlassian.com/cloud/jira/platform/rest/v3/api-group-issues/)).

**Resolution** is a field of its own beside the status: "A work item resolution is usually set when the status is changed … Once a work item is resolved (that is, the work item's Resolution field is filled in), references to that work item show the key in **strikethrough** text." Default values: **Done**, **Won't do**, **Duplicate**, **Cannot reproduce** ([Statuses, priorities, resolutions](https://support.atlassian.com/jira-cloud-administration/docs/what-are-issue-statuses-priorities-and-resolutions/)).

**Assignee: exactly one.** `PUT /rest/api/3/issue/{issueIdOrKey}/assignee` — "**Assigns an issue to a user**"; `"-1"` sets the project default, `null` makes the issue unassigned; in the create example `assignee` is a single object.

**Relationships — four default types, configurable:** "New installations of Jira come with **four default types of links**: relates to / relates to; duplicates / is duplicated by; blocks / is blocked by; clones / is cloned by. You can add, edit or delete link types to suit your organization" ([Configuring issue linking](https://confluence.atlassian.com/adminjiraserver/configuring-issue-linking-938847862.html)). Every type has an inward and an outward direction ("'Issue A' that is blocked by 'Issue B' has an outward link of type *is blocked by*"). API: [REST v3: Issue link types](https://developer.atlassian.com/cloud/jira/platform/rest/v3/api-group-issue-link-types/).

**Custom fields — limits:** "Maximum **700 fields per space**" and "**No limit** on the total number of custom fields across the site" (limits from March 2026); team-managed: "You can create up to **50 custom fields** in a team-managed space" ([Available custom fields](https://support.atlassian.com/jira-software-cloud/docs/available-custom-fields-for-team-managed-projects/), [Guardrails](https://support.atlassian.com/jira-cloud-administration/docs/data-limits-and-guardrails/)).

## 2.5 Cross-system field comparison

**Legend:** ● native and free · ◐ native but paid, or only through an additional mechanism · ○ not native

| Field | GitHub | GitLab (Free) | Linear | Jira | Core consensus? |
|---|---|---|---|---|---|
| **Title** | ● **required** | ● **required** | ● (schema: optional) | ● effectively required | **yes — the only near-universal mandatory field** |
| **Container membership** | ● repo (from the URL) | ● project **required** | ● `teamId` **required** | ● `project` required | **yes** |
| **Description** | ● Markdown | ● Markdown, max. 1,048,576 chars | ● Markdown | ● **ADF**, not Markdown | **yes** (the format differs) |
| **Status (open/closed)** | ● `open`/`closed` | ● `opened`/`closed` | ● through the state type | ● through the workflow | **yes** |
| **Status (granular)** | ○ only through project fields | ◐ Premium+ | ● configurable per team | ● configurable | no |
| **Status categories** | ○ | ◐ 5 (Premium) | ● 7 types | ● 3 categories, not customisable | **yes, once granular** |
| **Reason for closing** | ● `state_reason` | ◐ `closed_as_duplicate_of` + canceled category | ● `canceled` / `duplicate` as state types | ● `resolution` | **yes** (modelled differently) |
| **Labels** | ● several | ● several | ● several + label groups | ● several | **yes** |
| **Assignee** | ● up to **10** | ● **exactly 1** | ● **exactly 1** | ● **exactly 1** | **yes — 3 of 4: exactly one** |
| **Priority** | ◐ issue field (org) | ○ only label priority | ● `0`–`4`, fixed | ● configurable | **yes in purpose** — very inconsistently modelled |
| **Parent (hierarchy)** | ● max. 1 | ● | ● | ● | **yes** |
| **blocks / blocked by** | ● issue dependencies | ◐ **Premium+** | ● `blocks` (directed) | ● `blocks` | **yes** |
| **related** | ○ | ● `relates_to` | ● `related` | ● `relates to` | 3 of 4 |
| **duplicate** | ◐ through `state_reason` | ◐ quick action | ● relation **and** state type | ● link type **and** resolution | 4 of 4, each differently |
| **Due date** | ○ (only issue field / milestone) | ● `due_date` | ● `dueDate` (date only) | ● `duedate` | 3 of 4 |
| **Start date** | ◐ issue field | ● since 19.1 | ○ | ○ | no |
| **Estimation (points)** | ◐ ordinal "Effort" | ◐ `weight`, Premium+ | ● optional, team opt-in | ◐ custom field | **no — nowhere a core field, nowhere mandatory** |
| **Time estimate** | ○ | ● `/estimate`, `/spend` | ○ | ● `timetracking` | 2 of 4 |
| **Milestone** | ● max. 1 | ● max. 1 | ● max. 1 | ● several (`fixVersions`) | **yes**, but optional everywhere |
| **Sprint/cycle** | ◐ projects iteration | ◐ Premium+ | ● optional per team | ● Scrum only | no |
| **Issue type** | ● 25/org (task/bug/feature) | ● 4 values in Free | ○ labels only | ● configurable | 3 of 4 |
| **Subscriber/watcher** | ● | ● | ● | ● | yes (notifications) |
| **Confidentiality** | ○ | ● `confidential` | ○ | ● security level | no |
| **Custom fields** | ◐ 25 issue fields/org | ◐ Premium+ | ○ | ● 700/space | no |
| **Agent delegation** | ○ | ○ | ● `delegate` | ○ | **no — Linear alone** |

### The core consensus

Taking only the fields that exist natively and without surcharge in **all four** systems leaves a remarkably small set:

**Title · container membership · description · status (at least open/closed) · labels · assignee · parent · blocks/blocked-by · timestamps and author**

Everything else is either modelled inconsistently (priority), optional or opt-in (estimation, milestone, due date, cycle), or paid (GitLab weight/epic/iteration/status, Jira hierarchy levels, Linear sub-initiatives).

### Three patterns that repeat across all four systems

1. **Only a single genuine mandatory field.** GitHub requires only `title` on creation; Linear only `teamId`; GitLab `title` plus the project; Jira even makes mandatoriness fully configurable. The barrier to capture is deliberately at practically zero everywhere — filling in comes later.
2. **Above the status sits a machine-readable category everywhere.** Jira: three non-customisable `statusCategory` values. Linear: seven `WorkflowState.type` values. GitLab: five categories to which the open/closed semantics are automatically coupled. The real consensus is not *which* statuses exist, but that every status belongs to a fixed, small set of categories that tools can program against.
3. **"Done" and "abandoned" are distinguished everywhere** — through `state_reason` (GitHub), `resolution` (Jira), a state type `canceled` of its own (Linear) or the canceled category (GitLab). A bare `closed` is not enough for any of the four.

---

# Part 3: Consequences for planaffe

## 3.1 Recommended hierarchy

```
Instance
└── Project             (key, PLAN-42)
    └── Issue
        └── Sub-issue   (exactly one level)
```

**That is exactly the proposal from [`VISION.md`](../../VISION.md) section 7 — the research confirms it.** The reasoning:

- **Container plus issue is universal.** All four systems bind every issue to exactly one container and allow no multiple membership (repository, project, team, project). None of the four deviates.
- **A child level is likewise universal and free everywhere** — sub-issue (GitHub), task (GitLab Free), sub-issue (Linear), subtask (Jira). Leaving it out of the MVP would be the one point at which planaffe fell behind all four comparison systems.
- **Nobody in this target group needs more than one level.** Jira, the oldest and most ceremonious system, explicitly allows subtasks **no** children ([What are issue types](https://support.atlassian.com/jira-cloud-administration/docs/what-are-issue-types/)) — and thus gets by in its default configuration with the same depth proposed here. GitLab Free documents only `Issue → Task`. The deep trees (GitHub 8, Linear 10, GitLab epics 7) exist, but they are equipment for large organisations — and in GitLab's case explicitly tied to Ultimate.

**What is left out, and why:**

| Left out | Reason |
|---|---|
| **The epic as a type of its own** | The two systems with the strongest reputation for speed have **no epic at all**: GitHub does not ([About issues](https://docs.github.com/en/issues/tracking-your-work-with-issues/learning-about-issues/about-issues)), Linear does not — Linear even maps imported Jira epics onto **projects** ([Jira import](https://linear.app/docs/jira)). In GitLab the epic is Premium+ and not available in Free at all. In Jira an epic is technically nothing more than an issue type on level 1. An issue with children is an epic. A type of its own contributes nothing. |
| **Milestones** | Present in all four — but optional everywhere and limited to **max. 1 per issue** everywhere, so functionally identical to an exclusive label. Its only added value over a label is the progress display plus a due date. The target group needs neither, per the vision (non-goal "roadmaps"). **Retrofittable** without changing the hierarchy. |
| **Sprints/cycles/iterations** | GitLab: Premium+. Jira: Scrum only. Linear: an explicitly switchable opt-in per team. GitHub: exists only inside Projects v2. No system makes it mandatory — and for agents that do not work in two-week rhythms it is pointless. Matches vision section 5. |
| **Portfolio levels** (initiative/objective/custom level) | Consistently the most expensive features of the comparison systems (Linear Enterprise, GitLab Ultimate, Jira Premium). Irrelevant for 1–5 people. |
| **Groups/subgroups above projects** | Only GitLab has this. GitLab itself warns about deep nesting: "To avoid performance problems, nest groups to a maximum of five levels or fewer" ([Subgroups](https://docs.gitlab.com/user/group/subgroups/)). With ≤ 5 people, a flat project list is enough. |
| **Boards as a persisted level** | Jira demonstrates that a board is a **view**, not a membership — it is defined by JQL and can span project boundaries ([Configure filters](https://support.atlassian.com/jira-software-cloud/docs/configure-filters/)). A board is a saved filter, not a data model. |

**One refinement to the vision's proposal:** sub-issues should **not** be allowed to point into another project. GitHub allows cross-repository sub-issues, but only within the same owner ([REST: Sub-issues](https://docs.github.com/en/rest/issues/sub-issues)); Jira requires for the parent relationship that "parent and child are members of the same project" ([REST v3: Issues](https://developer.atlassian.com/cloud/jira/platform/rest/v3/api-group-issues/)). Project-local parent relationships keep ID allocation and permission checks trivial. Only **relationships** (blocks/related) should be able to cross projects — GitLab allows exactly that: "You can link issues in different projects" ([Related issues](https://docs.gitlab.com/user/project/issues/related_issues/)).

## 3.2 Recommended field set (MVP)

| Field | Required | Range | Reason |
|---|---|---|---|
| `key` | assigned | `PLAN-42` | a universal stable reference |
| `project` | **required** | 1 project | mandatory and single-valued in all four systems |
| `title` | **required** | string | the only near-universal mandatory field |
| `description` | optional | **Markdown** | 3 of 4 use Markdown; Jira's ADF is the outlier and considerably more awkward for agents |
| `status` | default `backlog` | `backlog`, `todo`, `in_progress`, `done`, `canceled` | see below |
| `priority` | default `0` | `0`–`4` (none/low/medium/high/urgent) | see below |
| `labels` | optional | n | present everywhere; the flexibility valve |
| `assignee` | optional | **exactly 0 or 1** | 3 of 4 allow exactly one |
| `claim` | optional | identity plus timestamp | planaffe-specific, see below |
| `parent` | optional | 0 or 1, project-local | single-valued everywhere |
| `blocked_by` / `blocks` | optional | n:m, across projects | **an addition to the vision**, see below |
| `author` | assigned | human or agent | — |
| `created_at`, `updated_at`, `closed_at` | assigned | timestamps | — |

That is **13 fields** — comparable to Linear's core and well below GitLab or Jira.

### Priority: a fixed scale `0`–`4`, not a label

That settles the open question from vision 8: **a fixed scale**. Three arguments from the research:

1. **Linear proves that a fixed, non-extensible scale is enough** — `0 = No priority, 1 = Urgent, 2 = High, 3 = Medium, 4 = Low`, and explicitly: "We don't have the option to set custom priorities" ([Priority](https://linear.app/docs/priority)). Linear is nevertheless not considered limited.
2. **Jira shows where configurability leads:** priority schemes, field configurations, a limit of 100 priorities per space ([Manage priority schemes](https://support.atlassian.com/jira-cloud-administration/docs/manage-priority-schemes/), [Guardrails](https://support.atlassian.com/jira-cloud-administration/docs/data-limits-and-guardrails/)) — exactly the configuration orgy the vision's guiding principle 3 rejects.
3. **GitLab shows where the label route leads:** without a native field, GitLab has to document a multi-stage sorting heuristic that falls back on milestone due dates and ends with "**Ties are broken arbitrarily**" ([Sorting issue lists](https://docs.gitlab.com/user/project/issues/sorting_issue_lists/)). For an agent that is supposed to deterministically fetch "the next most important issue", a non-deterministic sort criterion is unusable. An integer is not.

It is also telling that **GitHub is currently leaving the label route**: issue fields are justified explicitly with "instead of relying on **labels or free-text workarounds**" ([Managing issue fields](https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/managing-issue-fields-in-your-organization)) — and priority ships as one of four default fields.

A recommendation on the encoding: **do not adopt Linear's ordering.** In Linear, `1 = Urgent` and `4 = Low`, with `0` meaning "no priority" — sorted numerically ascending, unprioritised issues land ahead of urgent ones. For planaffe a monotonically increasing scale (`0 = none` … `4 = urgent`) is more robust for agents, because `ORDER BY priority DESC` then works without a special case.

### Status: five values, with category semantics

The default workflow from vision 9 is confirmed and matches the defaults of the comparison systems: Linear's default workflow is "Backlog > Todo > In Progress > Done > Canceled" ([Configuring workflows](https://linear.app/docs/configuring-workflows)), GitLab's default statuses are To do / In progress / Done / Won't do / Duplicate ([Status](https://docs.gitlab.com/user/work_items/status/)).

**The more important finding, however, concerns not the status list but the category above it.** All three systems with granular status put a small, fixed, non-configurable set of categories underneath — Jira three ("this can't be customized"), Linear seven types, GitLab five with automatic coupling to open/closed. For planaffe that means: the five statuses should be fixed in the schema (not configurable), and `done` and `canceled` should make the issue count as closed automatically — the way GitLab does it: "Statuses in the Done and Canceled categories automatically set work items to closed state" ([Status](https://docs.gitlab.com/user/work_items/status/)).

**A separate `resolution` field like Jira's is unnecessary.** The distinction "done versus abandoned" is already covered by `done` against `canceled` — exactly Linear's solution, where `completed` and `canceled` are state types of their own. Jira's separation of status and resolution is historical and produces precisely the mandatory-field dialogs the vision's guiding principle 4 rules out.

**The vision's question "how many workflow variants do we offer?" is answered by the research with: ideally none.** No comparison system offers pre-built workflow *variants* to choose from; they offer either a fixed set (GitHub, GitLab Free) or full configurability (Jira, Linear, GitLab Premium). A choice between "minimal" and "standard" is a third route nobody takes — and it forces CLI and UI to support two status models. A single fixed set is more consistent; whoever only wants `open`/`closed` uses `todo` and `done` and ignores the rest.

### Assignee: exactly one

GitLab Free, Linear and Jira allow **exactly one** assignee; GitLab even makes multiple assignment an explicit Premium feature ([Multiple assignees](https://docs.gitlab.com/user/project/issues/multiple_assignees_for_issues/)), which shows that it counts as an add-on and not a basic function. Only GitHub allows up to 10 — and is the outlier.

For an issue tracker whose core feature is exclusive claiming, multiple assignment would be contradictory anyway: "several are responsible" and "exactly one is working on it right now" cannot sensibly be maintained side by side.

### An addition to the vision: `blocked_by` / `blocks` belongs in the MVP

**This is the only substantial deviation from vision section 8.** The vision lists `parent` as the only issue relationship and maps blocking onto a `blocked` label. The research argues against that:

- **All four systems have a typed blocks/blocked-by relationship** — GitHub only recently retrofitted, with its own REST endpoints ([Issue dependencies](https://docs.github.com/en/rest/issues/issue-dependencies)); GitLab (Premium+), Linear and Jira likewise. No system solves blocking with a label.
- **A label cannot say *what* is being waited for**, and it does not dissolve automatically when the blocking issue closes. A human notices that while reviewing; an agent does not.
- **For the agent-first orientation it is the decisive field.** The core command from vision 10 — "give me the next ready issue and claim it" — cannot be answered correctly without dependencies. "Ready" does not only mean "well specified" but also "nothing blocks it any more". GitHub describes exactly this benefit: "Blocked issues are marked with a 'Blocked' icon … so you can easily **identify bottlenecks**" ([Creating issue dependencies](https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/creating-issue-dependencies)).

The recommendation is **a single directed relation** `blocks` — as in Linear, where "blocked by" is simply the inverse direction and not an enum value of its own ("Each relation connects a source issue to a related issue", schema description of `IssueRelation`). One edge, two reading directions; the CLI shows both.

`related` and `duplicate` are **not** adopted: `related` is purely informational and worthless for autonomous work (GitHub still does not have it); `duplicate` is already covered by `status = canceled` plus a comment — and that is exactly how GitLab solves it too, through the quick action `/duplicate` rather than a link type.

### Confirmed omissions

| Left out | Reason |
|---|---|
| **Estimation / story points** | The vision's assumption is confirmed. In **none** of the four systems is estimation a core or mandatory field: Linear makes it a team opt-in with the scale value `notUsed` ([Estimates](https://linear.app/docs/estimates)), GitLab makes `weight` a Premium feature, Jira solves story points through a **custom field** with an instance-specific ID and two different names depending on the project type ([KB: Story points](https://support.atlassian.com/jira/kb/story-points-not-showing-in-custom-board-for-a-team-managed-jira-project/)) — a field not even Atlassian has unified cleanly. On top of that: story points exist to plan **human capacity** across sprints. Without sprints (vision 5) they have no addressee, and an agent derives nothing from "5 points" that it does not already know from the description. |
| **Due date** | The hardest omission: 3 of 4 have it natively (GitLab `due_date`, Linear `dueDate`, Jira `duedate`), only GitHub does not. Nevertheless: a due date is a field meant to **produce reminders** — and planaffe explicitly has no notifications in the MVP (vision 13). A date that wakes nobody is a date that goes stale. For an agent it is meaningless as well: it works an issue immediately or not at all; it does not defer anything to Thursday. Priority does the ordering better. **Recommendation: leave out, but revisit together with notifications** — the two belong together. |
| **Issue type (bug/feature/task)** | 3 of 4 have it (GitHub 25/org, GitLab 4 values, Jira configurable) — **Linear does not** and explicitly points to labels: workspace labels are meant for categories "used by all teams (e.g. **'Bug'**)" ([Labels](https://linear.app/docs/labels)). A label `bug` does the same as a type field without introducing a second classification system. Linear's **label groups** (an exclusive choice within a group) are the elegant middle ground — but a post-MVP candidate, not MVP. |
| **Multiple assignees** | see above |
| **`resolution` as a field of its own** | see above — `canceled` covers it |
| **Reporter separate from author** | Only Jira separates `reporter` from the creator. That is sensible in a helpdesk context, which vision 5 excludes. |
| **Components, versions, fixVersions** | Jira only, and there only in company-managed projects ([Jira components](https://support.atlassian.com/jira-software-cloud/docs/what-are-jira-components/)). Labels cover the grouping. |
| **`confidential` / security level** | GitLab and Jira have it, GitHub and Linear do not. It does not fit the coarse permission model of vision 11. |
| **Custom fields** | Matches the vision's guiding principle 3. Notably: **Linear simply has none** — and is still the system the vision names as its interaction model. |
| **Start date** | Only GitLab (since 19.1) and GitHub (as an issue field). No consensus, no benefit for agents. |

### Agent-first: what an agent actually reads

Sorting the field set by who uses it produces a clear split.

**An agent needs, in order to work autonomously:**

| Field | For what |
|---|---|
| `key` | quotable in a branch, a commit, a PR |
| `title` + `description` | **the actual assignment** — all the semantics live here |
| `status` | is there something to do, and in which phase |
| `labels` | filters like `ready-for-agent`, `needs-human-triage` |
| `priority` | deterministic selection of the next issue |
| `blocked_by` | **may I even start** |
| `claim` | exclusivity against other agents |
| `parent` | the context of the larger whole |

**Pure human ceremony** (left out of the MVP): story points and weight (capacity planning for humans), due date (calendar semantics), milestone, sprint and cycle (planning rhythm for humans), components and versions (reporting structure), resolution beside status (a reporting artefact), transition screens with mandatory fields (governance).

**The separation of assignee and claim is confirmed by Linear.** Beside `assignee`, Linear has a separate field `delegate: User` — "The agent user that is delegated to work on this issue. Set when an **AI agent** has been assigned to perform work on this issue" (schema description, introspection of `api.linear.app/graphql`). With that, Linear separates exactly the two concepts vision 8 and 10 separate: *who is accountable* (assignee) versus *who is machine-working on it right now*. That is a strong signal that planaffe's claim concept is not exotic but the direction the field is moving in — and at the same time that planaffe can go beyond Linear's field here with an atomic claim, a timestamp and expiry, because no comparison system documents claim *semantics* (exclusivity, expiry).

### On the vision's question of a "machine-readable definition of done"

**Not one of the four systems has a field of its own for acceptance criteria.** All four solve it in the description, with Markdown checkboxes. GitLab goes one step further and allows checklist entries in the description to be turned into real tasks through "convert to child item" ([Tasks](https://docs.gitlab.com/user/tasks/)) — which also shows that GitLab regards the criterion as *either* text *or* a sub-issue, never as a third field.

**Recommendation: no field of its own.** Acceptance criteria belong in the Markdown description. An agent reads the description in full anyway; an additional structured field would only raise the question of which of the two applies. Where criteria are to be tracked individually, they are sub-issues — exactly GitLab's model.

## 3.3 Consequences for the vision

The research largely confirms vision 7 and 8. Concretely:

- **Confirmed:** the flat hierarchy project → issue → sub-issue (one level); no epic; no sprints; no custom fields; exactly one assignee; priority as a fixed scale; leave out estimates; leave out the due date; acceptance criteria in the description.
- **To be added:** `blocked_by`/`blocks` as a typed relationship — without it, `issues next --ready` cannot keep its core promise.
- **To be refined:** sub-issues project-local, relationships across projects. A monotonically increasing priority scale instead of Linear's encoding. A fixed status set instead of selectable workflow variants. `done`/`canceled` close the issue automatically.
- **Still open:** whether milestones are retrofitted later as a lightweight grouping. The research says they are optional and single-valued everywhere, so they can be added at any time without migrating existing data. No reason to build them in the MVP.

---

# Open Points and Uncertainties

The following could **not** be substantiated in a primary source and must not be treated as established:

1. **Linear's sub-issue depth limit of 10 levels** comes exclusively from the GraphQL schema description of the `Issue` type. The user documentation [Parent and sub-issues](https://linear.app/docs/parent-and-sub-issues) names no limit. Whether the server enforces it was not tested.
2. **Jira's limit on additional hierarchy levels** apparently does not exist — the DC documentation says "as many hierarchy levels", the OpenAPI schema "0, 1, 2, 3 .. n", and the [guardrails page](https://support.atlassian.com/jira-cloud-administration/docs/data-limits-and-guardrails/) does not list the point. That is a *non-finding*, not evidence of unboundedness.
3. **Whether GitHub enforces the limits "100 sub-issues" and "8 levels" hard through the API** — the REST documentation names no corresponding error cases.
4. **Whether GitLab tasks can have child tasks of their own**, and what depth applies there — the documentation shows only `Issue → Task`.
5. **The complete GitLab matrix of permitted parent-child combinations** is not published; the developer documentation points explicitly at the source code (`app/models/work_items/types_framework/system_defined/definitions/`).
6. **Whether a Jira issue can belong to several sprints at once** (sprint history) — the documentation speaks in the singular but does not say so explicitly.
7. **Whether Jira's `priority` is mandatory in principle** — that depends solely on the field configuration; a blanket statement does not exist in the primary source.
8. **The maximum length of Jira's `summary`** (frequently quoted as 255) is not substantiated in the sources checked.
9. **Whether GitHub's "issue fields" is generally available or still preview** — the documentation pages carry no preview notice but speak of "with the feature enabled".
10. **The semantics of Linear's relation type `similar`** — the enum value exists but has no description and does not appear in the UI documentation.
11. **The maximum number of workflow states per team in Linear** and **the maximum number of assignees in GitLab Premium/Ultimate** — neither is quantified anywhere.
12. **Whether a fresh GitLab instance preinstalls `workflow::` labels** — the documented labels are the convention of the GitLab project itself ([Labels — development](https://docs.gitlab.com/development/labels/)), not demonstrably a product default.
13. **Linear's `title` is nullable in the schema**, while the official getting-started example treats it as required. What the server does when the title is missing is undocumented and was not tested (it requires authentication).

The general caveat also applies that **licence tiers and limits change** — several of the Jira limits quoted take effect only in March and September 2026 per Atlassian, and GitLab fields such as `start_date` are version-bound (19.1). Before a decision with a longer half-life, the tier assignments should be checked again.
