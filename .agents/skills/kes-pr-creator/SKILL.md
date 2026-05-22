---
name: kes-pr-creator
description: Create a GitHub Pull Request for KoromoEventScript from the current branch against origin/main. Use when asked to open a PR, prepare a Draft PR, summarize branch diff vs origin/main, write a PR body, push the current branch, or hand off work for human review.
argument-hint: Optional: issue number, PR title hint, or whether to create draft/ready-for-review
---

# KES PR Creator

Use this skill to create one GitHub Pull Request from the current branch against `origin/main`.

This skill is for the handoff stage after implementation work is complete enough to review.

## Required Context

Read these before creating the PR body:

- `AGENT.md`
- `docs/development-workflow.md`
- `docs/testing-strategy.md` when tests are part of the change

If the work was driven by a GitHub Issue, also inspect the Issue or the notes already gathered during implementation.

## Outcome

Produce a GitHub PR that:

- compares the current branch to `origin/main`
- targets `main`
- links the Issue when one exists
- includes a reviewable summary of the actual diff
- lists specs read, acceptance criteria coverage, tests, review points, and out-of-scope items

Default to a Draft PR unless the user explicitly asks for ready-for-review.

## Workflow

1. Inspect branch state.
   - Run `git status --short`.
   - Run `git rev-parse --abbrev-ref HEAD`.
   - Confirm the current branch is not `main`.
   - Do not discard unrelated user changes.

2. Refresh comparison base.
   - Run `git fetch origin main`.
   - Compare against `origin/main`, not the local `main` branch.

3. Measure the diff to be proposed.
   - Run `git log --oneline origin/main..HEAD`.
   - Run `git diff --stat origin/main...HEAD`.
   - Run targeted `git diff -- <changed files>` or `git diff --name-only origin/main...HEAD` as needed.
   - Use these results to summarize only what is actually on the branch.

4. Verify PR readiness.
   - Confirm there is at least one commit or file change ahead of `origin/main`.
   - Confirm focused validation was run for the touched area.
   - If tests were expected but not run, record that clearly in the PR body.

5. Resolve PR metadata.
   - Infer the Issue number from the branch name, recent work, or commits when possible.
   - Draft a concise PR title from the issue and branch scope.
   - Include `Closes #N` when the relationship is clear.
   - If the Issue number or title is ambiguous, stop and ask instead of guessing.

6. Build the PR body.
   - Include these sections in Japanese unless the user requests English:
   - 対応 Issue
   - 変更内容
   - 参照した仕様
   - 満たした受け入れ条件
   - 実行したテスト
   - レビューしてほしい点
   - 対象外
   - Keep the body tightly aligned to the branch diff.
   - Mention spec gaps or follow-up Issues instead of broadening the PR.

7. Push the branch if needed.
   - Check whether the branch already has an upstream.
   - If not, push with `git push -u origin <branch>`.
   - Do not force-push unless the user explicitly asks.

8. Create the PR on GitHub.
   - Prefer GitHub CLI.
   - Draft PR: `gh pr create --base main --head <branch> --draft --title "..." --body-file <file>`
   - Ready PR: omit `--draft` only when the user asked for it.
   - If a template exists, preserve its required sections.

9. Confirm the result.
   - Capture the PR URL.
   - Report the final title, base branch, head branch, and whether it is Draft.
   - Summarize any warnings, such as unrun tests or unresolved questions.

## Decision Points

- Dirty working tree:
  Do not create a PR that depends on uncommitted changes. Ask whether to commit them first if they are part of the branch scope.

- No diff from `origin/main`:
  Stop. There is nothing meaningful to open as a PR.

- No clear Issue mapping:
  Create a PR without `Closes #N` only if the user confirms that no Issue link is needed.

- Missing validation:
  Prefer to run the narrowest relevant checks before creating the PR.

- Spec contradiction or scope creep:
  Record it in the PR body as a question or out-of-scope item rather than silently widening the implementation.

## Quality Bar

Before creating the PR, confirm:

- The diff is only for the intended unit of work.
- The PR body reflects the actual `origin/main...HEAD` diff.
- Required specs and tests are listed.
- Unrelated local changes were not reverted.
- The branch is pushed and reviewable.

## Output Template

Use a body close to this shape:

```md
## 対応 Issue
- Closes #N

## 変更内容
- ...

## 参照した仕様
- ...

## 満たした受け入れ条件
- ...

## 実行したテスト
- `dotnet test ...`

## レビューしてほしい点
- ...

## 対象外
- ...
```

## Stop Conditions

Stop and ask the user when:

- the branch contains unrelated work
- the Issue link is unclear and cannot be inferred safely
- the branch has not been committed yet
- GitHub CLI is unavailable or authentication is missing
- required tests or CI evidence are missing and the user wants a non-draft PR