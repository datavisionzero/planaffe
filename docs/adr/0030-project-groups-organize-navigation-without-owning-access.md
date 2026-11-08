# Project Groups Organize Navigation Without Owning Access

A project optionally belongs to one flat **project group**. A group has a unique,
case-sensitive name (up to 100 characters) and an optional description (up to
500 characters). Its UUID is stable across renames. There are no nested groups.

This deliberately extends the flat model in VISION 7 and the statement in ADR
0024 that no object appears above projects. The purpose is navigation across
many projects, not portfolio planning: groups have no workflow, reporting,
keys, instructions or permissions. Spaces remain independent (ADR 0027).

Administrators create, rename and dissolve groups and assign projects. This
follows the glossary's administrator responsibility for organization outside
one project's content. Users and agents see only groups containing a live
project they can access; administrators see all groups, including empty ones.
Group membership never grants project access. Counts include visible projects
only. Dissolving a group sets every project's reference to null, including
soft-deleted projects, without deleting content or changing access.

The overview groups projects into collapsible sections, retains the server's
attention order within each section, and indicates attention on collapsed
sections. A URL filter selects one group. The project switcher is a searchable
dialog with group filters, name/key/group search, keyboard navigation and a
visible current project. It preserves the current view when switching.

The API and CLI expose the same group lifecycle and assignment. Existing
projects migrate to Ungrouped. No new dependency is introduced.
