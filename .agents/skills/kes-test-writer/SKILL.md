---
name: kes-test-writer
description: Add or update KoromoEventScript tests, testdata, NUnit projects, diagnostic snapshots, golden tests, CLI integration tests, and PR test evidence. Use when implementing behavior that needs verification or when asked to improve test coverage.
---

# KES Test Writer

Use this skill whenever a KoromoEventScript change needs tests or test evidence.

## Required Reading

- Read `docs/testing-strategy.md`.
- Read `docs/development-workflow.md` for PR expectations.
- Read the relevant product spec through `kes-spec-reader`.
- Read existing tests before adding new patterns.

Specification files live under `docs/spec/`. If a path is missing, find it with `rg --files docs`.

## Test Framework

C# tests use NUnit.

New C# test projects should use:

- `NUnit`
- `NUnit3TestAdapter`
- `Microsoft.NET.Test.Sdk`

Tests must run through `dotnet test`. Do not introduce xUnit or MSTest unless the Issue explicitly approves it.

## Test Selection

Choose the smallest test layer that proves the behavior:

- Lexer test: tokenization, comments, strings, tags, indentation.
- Parser test: AST shape, blocks, `say`, `nar`, `select`, `label`, `jump`.
- Diagnostic test: code, level, file, line, column, message.
- Semantic test: imports, names, tags, types, duplicate definitions.
- Golden test: `.ke` to `.k`, manifest generation, stable emitted output.
- VM test: execution state, jumps, selections, text progression.
- CLI integration test: command behavior, exit code, stdout, stderr, files.
- LSP test: diagnostics, completion, definition, formatting.
- Runtime state test: save/load, input mapping, non-render state.

Prefer lower-level tests for localized logic and integration tests for CLI contracts.

## Testdata Rules

Use `testdata/` for reusable KES inputs and expected outputs:

```txt
testdata/
    ke/
        valid/
        invalid/
    kel/
        valid/
        invalid/
    projects/
        minimal/
    snapshots/
        diagnostics/
        ir/
        manifest/
```

Do not bury long `.ke` or `.kel` samples inside test source when a file would be clearer.

## Diagnostic Tests

Assert all meaningful fields:

- level
- code
- file
- line
- column
- message

Use the diagnostic code ranges from `docs/spec/cli-tool-spec.md`:

- `KES1xxx`: syntax error.
- `KES2xxx`: compile error.
- `KES3xxx`: runtime error.
- `KES4xxx`: warning.
- `KES9xxx`: CLI/tool error.

## Golden Tests

Use golden tests for stable generated artifacts:

- IR or `.k` output.
- `manifest.json`.
- JSON Lines diagnostics.

When updating golden files, explain in the PR whether the change is a spec change, an intentional implementation change, or a bug fix.

## CLI Integration Tests

CLI tests should verify:

- command line arguments
- exit code
- stdout
- stderr
- generated files
- no unexpected source mutation unless specified

Prioritize:

```txt
kes --version
kes --help
kes init
kes build --check-only
kes build
kes clean --dry-run
kes publish --target windows
```

## PR Test Evidence

In the PR body, include:

- test commands
- pass/fail result
- tests added or updated
- reason if tests were not added
- reason if any expected snapshot changed

Map important acceptance criteria to test names when possible.
