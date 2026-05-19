---
name: kes-issue-implementer
description: Implement one KoromoEventScript GitHub Issue as one scoped branch and Pull Request. Use when asked to work on a GitHub Issue, create a branch, implement an Issue, prepare a PR, address an Issue's acceptance criteria, or follow the repository's AI development workflow.
---

# KES Issue Implementer

Use this skill to turn one GitHub Issue into one focused implementation branch and PR.

## Core Rule

Keep the unit of work small:

- One Issue.
- One branch.
- One PR.
- One coherent test story.

Do not broaden scope just because related problems are visible. Record extra work as follow-up Issue suggestions.

## Required Reading

Before coding, read only the relevant docs:

- Always read `AGENT.md`.
- Always read `docs/development-workflow.md`.
- Always read `docs/testing-strategy.md`.
- Read `docs/task-breakdown.md` when the Issue is part of MVP planning.
- Use `kes-spec-reader` when the Issue needs product or language requirements.
- Use `kes-test-writer` when adding or changing tests.

Specification files live under `docs/spec/`. If a path is missing, find it with `rg --files docs`.

## Workflow

1. Inspect the working tree.
   - Run `git status --short`.
   - Do not revert user changes.
   - If unrelated dirty files exist, leave them alone.

2. Understand the Issue.
   - Identify purpose, scope, out-of-scope items, acceptance criteria, required tests, and dependencies.
   - If the Issue is too broad, propose a smaller split before implementation.

3. Create or use a branch.
   - Use `feature/issue-N-short-name`, `fix/issue-N-short-name`, `docs/issue-N-short-name`, or `ci/issue-N-short-name`.
   - Do not mix multiple Issues on one branch.

4. Read targeted specs.
   - Prefer the smallest set of specs that answers the task.
   - If specs conflict, stop and ask for a decision or document a PR question.

5. Implement narrowly.
   - Follow existing code patterns.
   - Do not invent new architecture unless the Issue asks for it.
   - Keep unrelated formatting churn out of the PR.

6. Add tests.
   - Implementation changes normally require tests.
   - C# tests use NUnit.
   - Use `testdata/` for KES inputs and expected outputs.
   - If tests are not added, explain why in the PR body.

7. Verify.
   - Run the required Issue tests.
   - Run broader checks when the change touches shared behavior.
   - At minimum, run `git diff --check` for changed files.

8. Prepare PR notes.
   - Link the Issue with `Closes #N`.
   - Summarize changes.
   - List specs read.
   - Map acceptance criteria to implementation or tests.
   - Include test commands and results.
   - Note anything intentionally out of scope.

## Stop Conditions

Stop and ask, or leave a PR question, when:

- The Issue acceptance criteria contradict repository specs.
- Required dependencies are not implemented.
- The change requires a public spec change not listed in the Issue.
- The implementation would touch unrelated modules.
- Tests cannot be run for environmental reasons.

## PR Checklist

Before handing off for human review, confirm:

- The PR handles only the target Issue.
- Required docs were read.
- Tests were added or explicitly waived.
- `git diff --check` passes for changed files.
- PR body explains acceptance criteria coverage.
- Existing user changes were not reverted.
