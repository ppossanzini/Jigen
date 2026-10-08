<!-- skill-governance:start -->
# Agent governance

## Communication
- Use short sentences and plain language.
- State what is being done, what is complete, and what remains.
- Put outcomes before implementation details.
- Mention files, symbols, commands, searches, tool calls, and internal reasoning only when they help verification or a user decision.
- Keep progress updates to one or two sentences. Avoid jargon, repeated plans, and long reference lists.
- Lead final responses with the result and verification status, then blockers or remaining work.

## Authority and workflow
- Precedence: direct user request, applicable `AGENTS.md`, locked project decisions, base skills, resolved implementation skills.
- Unresolved conflicts stop the affected work and are reported plainly.
- Authoritative project documents are `docs/StackProfile.md` and `docs/ProjectInfo.md`.
- Active workflow: `workflow-development`.
- Mandatory phase order: `base-project-agent-governance`, `phase-development-technology-resolution`, `phase-development-project-conventions`, `phase-development-configuration-options`, `phase-development-task-execution`, `phase-development-deviation-routing`, `phase-development-development-handoff`.
- Always-on backend skill: `base-be-base-rules`.
- Resolved implementation: `backend-platform` / `.NET 10` / `implementation-be-dotnet-dev`.
- Capability skills are task-triggered and never replace base or implementation skills.
- Each task declares its id, touched scope, activated skills, and reason for capability skills. Verification and task outcome are recorded in `docs/development/dossier.md`.

## Working rules
- Make the smallest change that solves the task and touch only what it needs.
- Keep output proportional. Do not create unrequested ADRs, tables, diagrams, or files.
- Take rules from project documents, not memory. Ask when no written rule or direct command resolves a choice.
- Correct code that conflicts with approved rules instead of copying the conflict.
- Ask at most one question per turn, only when project documents do not answer it.
- Choices not fixed by a written rule or direct command belong to the user.
- Keep equivalent requests predictable in structure, files, and steps.
- Skill rules apply as written. Only a direct user decision may set one aside, and the skipped rule must be named.
- Never add obligations or prohibitions not stated by an applicable skill.
- The user's direct command has highest authority. When it conflicts with project governance, execute it and correct the project document afterward.
- Generated operational scripts and command files belong in `.ai-generated-operations`, never the repository root.
- Prefer `Guid.CreateVersion7` over `Guid.NewGuid`.

## Git safety
- The working tree is the source for edits. Never replace it from Git history or the index without an explicit request.
- Git history may be read for context, but never used to restore or overwrite local changes.
- Never run `git checkout`, `git restore`, `git reset`, `git clean`, or equivalent destructive commands without an explicit request for that operation.
- Never copy content from `git show`, `git diff`, a commit, or the index over a current file without an explicit request.
- Read the current file before editing. Preserve concurrent or user-authored changes; ask before replacing conflicting content.

## File protection
- After this bootstrap, the model must not create, modify, regenerate, replace, move, or delete any `AGENTS.md` without an explicit user request naming that file.
- A workflow, skill, inconsistency, phase transition, or inferred need is not authorization.
- Report a proposed amendment and wait for explicit authorization.
<!-- skill-governance:end -->
