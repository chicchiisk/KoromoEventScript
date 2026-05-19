---
name: kes-spec-reader
description: Locate, read, and apply the relevant KoromoEventScript specifications for implementation, testing, review, or Issue planning. Use when a task mentions language behavior, CLI behavior, runtime behavior, VS Code support, Unity/Unreal integration, test strategy, workflow, or project architecture.
---

# KES Spec Reader

Use this skill to gather the minimum relevant specification context before implementation or review.

## First Step

Find the current spec paths. Product specifications live under `docs/spec/`.

Use `rg --files docs` and prefer the current file location in the working tree.

## Spec Map

Read by task type:

| Task | Read |
|---|---|
| Overview, component boundaries | `docs/spec/overview.md` |
| Language syntax or semantics | `docs/spec/kes-language-spec.md` |
| CLI commands, logging, exit codes | `docs/spec/cli-tool-spec.md` |
| Project config XML | `docs/spec/kes-config.xsd` |
| Windows runtime | `docs/spec/windows-runtime-spec.md` |
| VS Code extension or LSP | `docs/spec/vscode-ext-spec.md` |
| Unity runtime | `docs/spec/unity-runtime-spec.md` |
| Unreal runtime | `docs/spec/unreal-runtime-spec.md` |
| GitHub workflow | `docs/development-workflow.md` |
| Test policy | `docs/testing-strategy.md` |
| MVP planning | `docs/task-breakdown.md` |

## Reading Strategy

1. Start from the Issue or user request.
2. Identify the component: language, CLI, VM, runtime, VS Code, test, CI, or docs.
3. Read only the matching specs.
4. Search within specs for exact keywords from the task.
5. Summarize requirements as implementation constraints.
6. Call out ambiguities before coding.

## Output Shape

When summarizing specs, provide:

- specs read
- relevant requirements
- acceptance criteria implied by the specs
- tests that should cover the behavior
- open questions or contradictions

Keep summaries concise. Do not paste long spec sections.

## Conflict Handling

If specs conflict:

- Prefer newer, more specific documents over broad overview text.
- Prefer explicit acceptance criteria in the Issue for the current PR scope.
- Do not silently change public behavior.
- Ask for a decision or document the conflict in the PR.

## Implementation Guardrails

- Do not implement behavior that contradicts the specs.
- Do not expand scope beyond the relevant spec area.
- If the spec is missing for required behavior, propose a docs update or a follow-up Issue.
- If tests are needed, use `kes-test-writer`.
