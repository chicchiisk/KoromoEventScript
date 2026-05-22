---
name: github-issue-implementer
description: 'Read a specified GitHub Issue and implement it in KoromoEventScript. Use when asked to inspect issue #N, issue URLs, acceptance criteria, create a branch, make the change, add tests, and prepare a PR.'
argument-hint: 'Issue number or URL to implement'
---

# GitHub Issue Implementer

Use this skill when the task starts from a GitHub Issue and the expected outcome is a focused implementation in this repository.

## What This Skill Produces

- A narrow implementation for one specified Issue.
- Tests or an explicit test waiver.
- Validation evidence.
- PR-ready notes that map the Issue to the change.

## Inputs

Accept any of these inputs:

- Issue number such as `14`
- Short form such as `#14`
- Full GitHub Issue URL

If the repository owner or repository name is ambiguous, resolve it before changing code.

## Required Reading

Read these before implementation:

- `AGENT.md`
- `docs/development-workflow.md`
- `docs/testing-strategy.md`

Read targeted specs only when needed:

- `docs/spec/`
- Use `kes-spec-reader` when the Issue touches language, CLI, runtime, or extension behavior.
- Use `kes-test-writer` when new tests, snapshots, or testdata are required.

## Workflow

1. Normalize the Issue reference.
   - Convert the user input to an Issue number or URL.
   - If the user gave multiple Issues, stop and ask which single Issue to implement first.

2. Inspect the current working tree.
   - Run `git status --short`.
   - Do not revert unrelated user changes.
   - If the working tree contains conflicting edits in the same area, stop and ask before proceeding.

3. Read the Issue from GitHub.
   - Prefer `gh issue view <issue> --comments`.
   - Capture the title, purpose, acceptance criteria, scope boundaries, dependencies, and any linked PRs or specs.
   - If `gh` is unavailable, ask for the Issue text or use the browser-based repo tools if available.

4. Define the smallest implementable slice.
   - Convert the Issue into a falsifiable local hypothesis.
   - Identify the nearest code path that directly controls the behavior.
   - If the Issue is too broad, propose a smaller first slice before editing.

5. Create or confirm the branch.
   - Use one Issue per branch.
   - Preferred names: `feature/issue-N-short-name`, `fix/issue-N-short-name`, `docs/issue-N-short-name`, `test/issue-N-short-name`.
   - Do not mix unrelated fixes into the branch.

6. Read only the minimum relevant code and specs.
   - Start from the most concrete anchor: failing test, target file, referenced symbol, or owning implementation.
   - Avoid broad repo exploration.
   - If behavior is specified in docs, implementation must follow the docs or stop and surface the conflict.

7. Implement narrowly.
   - Keep public behavior aligned with the Issue and repository specs.
   - Preserve existing style and APIs unless the Issue requires a change.
   - Do not fold in unrelated refactoring.

8. Add or update tests.
   - Prefer the narrowest test that proves the requested behavior.
   - Use NUnit tests under `tests/`.
   - Use `testdata/` and snapshots when the behavior is input-driven.
   - If no test is added, document why.

9. Validate immediately after the first substantive edit.
   - Run the cheapest focused check first.
   - Then run the narrowest relevant test command.
   - At minimum, run `git diff --check` on changed files.
   - If validation fails, repair the same slice before widening scope.

10. Prepare PR-ready notes.
   - Include `Closes #N`.
   - Summarize the implemented behavior.
   - List the specs and docs read.
   - Map acceptance criteria to code or tests.
   - Record test commands and results.
   - Note any intentionally deferred work.

## Decision Points

- If the Issue conflicts with current specs, stop and ask for a product decision.
- If the Issue depends on unfinished prerequisite work, report the dependency and avoid speculative implementation.
- If the Issue asks for multiple separable changes, implement only one coherent slice and list the rest as follow-up work.
- If required tools such as `gh` are unavailable, fall back to user-provided Issue text instead of guessing.

## Completion Checks

Before handoff, confirm all of the following:

- The change addresses exactly one specified Issue.
- The Issue text was read directly or supplied by the user.
- The implementation matches the accepted scope.
- Relevant tests were added or explicitly waived.
- Focused validation ran, and `git diff --check` passes for changed files.
- PR notes can explain what changed, why, and how it was validated.

## Example Prompts

- `/github-issue-implementer 14`
- `/github-issue-implementer #27`
- `/github-issue-implementer https://github.com/owner/repo/issues/42`
- `Issue #14 を読んで、このリポジトリのルールに沿って実装して`