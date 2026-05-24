# Research & Design Decisions

## Summary
- **Feature**: `kes-build-check-only`
- **Discovery Scope**: Extension
- **Key Findings**:
  - `source/cli/KoromoEventScript.Cli` には lexer、parser、diagnostic formatter は存在するが、CLI entrypoint と build command orchestration は未実装である。
  - `docs/spec/cli-tool-spec.md` は `kes build [PROJECT_DIR] [options]`、`--check-only`、diagnostic layout、exit code、project root resolution を既に定義している。
  - `.kel` parser は構文木を返すだけで、`chapter` などのキー意味論は parser の責務外であるため、このspecでは参照 `.ke` の最小抽出だけを build check-only 層で扱う。

## Research Log

### CLI仕様とIssue範囲
- **Context**: Issue #16 の対象が `kes build --check-only` の最小骨組みであり、既存CLI仕様との整合が必要。
- **Sources Consulted**: `docs/spec/cli-tool-spec.md`, `docs/task-breakdown.md`, `docs/testing-strategy.md`, GitHub Issue #16
- **Findings**:
  - `--check-only` は成果物を生成せず検証のみ行う。
  - `PROJECT_DIR` 省略時は現在ディレクトリまたは親ディレクトリから `kes.xml` を探索する。
  - diagnostic は text と JSON Lines が仕様化済みで、exit code は `0`, `2`, `3`, `6` が今回の最小範囲に直接関係する。
- **Implications**:
  - design はCLI引数解析、project root resolution、config load、syntax parse、diagnostic emission、exit code mapping に限定する。
  - `.k`、manifest、runtime は明示的に生成・起動しない。

### 既存CLIコードの拡張点
- **Context**: 実装すべき場所と再利用すべき既存型を特定するため。
- **Sources Consulted**: `source/cli/KoromoEventScript.Cli`, `tests/KoromoEventScript.Cli.Tests`
- **Findings**:
  - `KeLexer`, `KeParser`, `KelParser` は既存で、失敗時に `LexerException` / `ParserException` から `Diagnostic` を取得できる。
  - `DiagnosticFormatter` は text と JSON Lines の整形を既に提供している。
  - `Program.cs` や command routing は存在しない。
- **Implications**:
  - 新規componentはCLI entrypoint、argument parser、build check-only service、project config loader、entry `.ke` resolver に分ける。
  - parser/formatterは既存実装を変更最小で利用し、意味解析やIR生成は追加しない。

### Project config と `.kel` 参照解決
- **Context**: `kes.xml` と entry `.kel` から `.ke` をどこまで解決するかを決める必要がある。
- **Sources Consulted**: `docs/spec/kes-config.xsd`, `docs/spec/kel-file-spec.md`, `testdata/projects/minimal/kes.xml`, `testdata/projects/minimal/events/main.kel`
- **Findings**:
  - `kes.xml` は `Project.Entry` と `Paths.*` を定義する。
  - `.kel` parser は key/value/objects を保持するが、キー意味論は消費側の責務である。
  - 既存testdataでは chapter object の `chapter = "events/chapter001.kc"` が `.ke` 相当のスクリプト参照として使われている。
- **Implications**:
  - 最小骨組みでは `Project.Entry` の `.kel` を解析し、rootまたはnested object内の `chapter` string/identifier value を `.ke` 入力候補として扱う。
  - 拡張子の完全な正規化や semantic validation は後続phaseに残す。

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| Direct Program-only implementation | `Program.cs` に引数解析、設定読込、解析、出力をまとめる | 最小ファイル数 | テストしづらく、exit code と診断の組み合わせが膨らむ | 却下 |
| Thin entrypoint plus services | `Program` は routing のみ、処理を typed services に委譲 | 既存parser/formatterを再利用しやすく、NUnitで直接検証できる | 少数の新規型が必要 | 採用 |
| External command-line parser adoption | System.CommandLine 等を導入 | 将来のCLI拡張に強い | 依存追加とversion確認が必要、Issue #16 の最小範囲を超える | 却下 |

## Design Decisions

### Decision: Thin entrypoint plus typed services
- **Context**: CLI entrypointが未実装で、build check-onlyをテスト可能にする必要がある。
- **Alternatives Considered**:
  1. `Program.cs` に全処理を書く。
  2. `Program.cs` は薄くし、argument/config/build処理を分離する。
- **Selected Approach**: `Program` / `CliApplication` / `BuildCheckOnlyCommand` / `ProjectConfigLoader` / `KelScriptReferenceResolver` に責務を分ける。
- **Rationale**: command routing と validation workflow を分離することで、exit code とdiagnosticをNUnitから検証できる。
- **Trade-offs**: ファイル数は増えるが、各componentの責務は小さい。
- **Follow-up**: `Program` のconsole I/Oは直接テストせず、service resultを中心に検証する。

### Decision: New dependencies are not introduced
- **Context**: Issue #16 は最小骨組みであり、外部CLI parser導入は過剰。
- **Alternatives Considered**:
  1. .NET標準APIで簡易argument parsingを実装する。
  2. 外部command-line parserを採用する。
- **Selected Approach**: .NET標準APIのみで、`build`, `--check-only`, optional `PROJECT_DIR`, `--log-format` の最小解析を行う。
- **Rationale**: 既存projectは外部依存がなく、今回必要な引数は限定的。
- **Trade-offs**: 将来CLI全体を拡張する際にparser置換の余地がある。
- **Follow-up**: 今回のparser contractを狭く保ち、将来のCLI spec拡張で差し替え可能にする。

### Decision: `.kel` chapter references are resolved as build-layer input discovery
- **Context**: `.kel` parserはキー意味論を持たず、build check-onlyは参照 `.ke` を解析する必要がある。
- **Alternatives Considered**:
  1. `.kel` parserに `chapter` 意味論を追加する。
  2. build層で `KelDocumentSyntax` を走査し、`chapter` valuesを入力候補として抽出する。
- **Selected Approach**: build層の resolver が `chapter` key の string/identifier value をプロジェクトルート相対パスとして解決する。
- **Rationale**: parser責務を維持しながらIssue #16の `.kel` と `.ke` 解析を満たせる。
- **Trade-offs**: 完全な `.kel` semantic validation は行わない。
- **Follow-up**: Phase 2以降で `entry` / `chapter` 意味論を正式化する場合はresolver contractを再検証する。

## Risks & Mitigations
- `KES2001` など既存parser diagnosticsが `KES2xxx` で、requirementsのsyntax exit code `3` と分類がずれる可能性がある — check-only workflowでは lexer/parser例外を syntax-stage failure として扱い、exit code `3` にmapする。
- testdataに `.kc` 拡張子が残っている一方でIssueは `.ke` を対象にしている — resolverはこのspecでは参照されたファイルをそのまま解析対象にし、拡張子強制は行わない。
- `kes.xml` の完全XSD validationを実装するとscopeが膨らむ — 必須要素/属性の読込とXML parse error診断に限定し、完全schema validationは後続に残す。

## References
- `docs/spec/cli-tool-spec.md` — CLI command, diagnostics, exit codes, path resolution.
- `docs/spec/kes-config.xsd` — `kes.xml` shape.
- `docs/spec/kel-file-spec.md` — `.kel` parser responsibility boundary.
- `docs/task-breakdown.md` — Phase 1-5 scope.
- `docs/testing-strategy.md` — CLI test expectations and exit code alignment.
