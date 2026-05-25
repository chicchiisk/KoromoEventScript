# Implementation Plan

## Task Format Template

Use whichever pattern fits the work breakdown:

### Major task only

- [ ] {{NUMBER}}. {{TASK_DESCRIPTION}}{{PARALLEL_MARK}}
  - {{DETAIL_ITEM_1}} *(Include details only when needed. If the task stands alone, omit bullet items.)*
  - *Requirements: {{REQUIREMENT_IDS}}*

### Major + Sub-task structure

- [ ] {{MAJOR_NUMBER}}. {{MAJOR_TASK_SUMMARY}}
- [ ] {{MAJOR_NUMBER}}.{{SUB_NUMBER}} {{SUB_TASK_DESCRIPTION}}{{SUB_PARALLEL_MARK}}
  - {{DETAIL_ITEM_1}}
  - {{DETAIL_ITEM_2}}
  - {{OBSERVABLE_COMPLETION_ITEM}} *(At least one detail item should state the observable completion condition for this task.)*
  - *Requirements: {{REQUIREMENT_IDS}}* *(IDs only; do not add descriptions or parentheses.)*
  - *Boundary: {{COMPONENT_NAMES}}* *(Only for (P) tasks. Omit when scope is obvious.)*
  - *Depends: {{TASK_IDS}}* *(Only for non-obvious cross-boundary dependencies. Most tasks omit this.)*

> **Parallel marker**: Append `(P)` only to tasks that can be executed in parallel. Omit the marker when running in `--sequential` mode.
>
> **Optional test coverage**: When a sub-task is deferrable test work tied to acceptance criteria, mark the checkbox as `- [ ]*` and explain the referenced requirements in the detail bullets.
