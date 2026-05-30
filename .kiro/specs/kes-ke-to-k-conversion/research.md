# Research & Design Decisions

## Summary

- **Feature**: `kes-ke-to-k-conversion`
- **Discovery Scope**: Extension
- **Key Findings**:
  - 既存 CLI は `SourceFileParser`、`SemanticAnalyzer`、`ScriptDocument` により `.kc` AST と検証済み document 群を既に扱っているため、変換器は parser / semantic diagnostics を置き換えず後段に接続できる。
  - `.klib` の公開契約は `docs/spec/k-intermediate-representation-spec.md` にあり、今回の設計はその emitter 実装境界だけを所有する。
  - `testdata/snapshots/ir/.gitkeep` が存在し、golden / snapshot 形式の比較先を追加しやすい。

## Research Log

### 既存 CLI pipeline

- **Context**: `.kc` AST から `.klib` を作るため、既存 build check pipeline のどこに接続するかを確認した。
- **Sources Consulted**: `source/cli/KoromoEventScript.Cli/Commands/Build/BuildCheckOnlyCommand.cs`、`source/cli/KoromoEventScript.Cli/Build/SourceFileParser.cs`、`source/cli/KoromoEventScript.Cli/Semantics/SemanticAnalyzer.cs`
- **Findings**:
  - `BuildCheckOnlyCommand` は project root 解決、config load、`.kel` parse、`.kc` parse、semantic analysis を順に実行する。
  - `ScriptDocument` は `ProjectRelativePath`、`ModuleName`、`ScriptSyntax` を持ち、`.klib` の module / debug 情報の入力にできる。
  - `--check-only` は既存 artifact を変更しないことがテストで固定されている。
- **Implications**:
  - 変換器は `SemanticAnalysisResult.Succeeded` 後に呼び出せる独立 service とする。
  - 今回の仕様は `--check-only` の artifact 非変更保証を壊さない。通常 build への接続は別 task で明示的に扱う。

### AST と制御フロー情報

- **Context**: 受け入れ条件の `say`、`nar`、`select`、`jump`、通常命令に必要な AST 情報を確認した。
- **Sources Consulted**: `source/cli/KoromoEventScript.Cli/Parsing/SyntaxNodes.cs`、`source/cli/KoromoEventScript.Cli/Semantics/NameResolver.cs`
- **Findings**:
  - `SayStatementSyntax` は speaker、tag、lines、speaker/tag location を持つ。
  - `NarStatementSyntax` は tag、lines、tag location を持つ。
  - `SelectStatementSyntax` は `CaseClauseSyntax` の text、tag、tag location を持つ。
  - `NameResolver` は jump と select case の tag を local document 内の label として検証している。
- **Implications**:
  - 変換器は local document 内で label index map を作り、`jump` と `select` case を index に解決できる。
  - 未解決 label は semantic analysis で先に検出される前提だが、変換器も防御的に failure result を返す。

### `.klib` IR 契約

- **Context**: 生成される `.klib` document の top-level field、opcode、source mapping、manifest 参照を確認した。
- **Sources Consulted**: `docs/spec/k-intermediate-representation-spec.md`
- **Findings**:
  - `.klib` は `format: koromo.klib`、version、features、module、imports、instructions、labels、manifestRefs、debug を持つ。
  - instruction は `index`、`op`、`args`、`result`、`source` を共通 field とする。
  - `say`、`nar`、`command`、`jump`、`select`、`label` の opcode 契約が定義済みである。
  - manifest の artifact path、hash、asset 実体、locale 本文は `.klib` が所有しない。
- **Implications**:
  - C# model は `.klib` 仕様の field 順に合わせて定義し、serializer は deterministic JSON を出力する。
  - manifestRefs は最小 scripts 参照を必須とし、assets / localeKeys は空配列を正規形にする。

### テスト配置

- **Context**: golden test の配置と既存テスト方針を確認した。
- **Sources Consulted**: `tests/KoromoEventScript.Cli.Tests/**`、`testdata/snapshots/ir/.gitkeep`
- **Findings**:
  - NUnit を使い、fixture と `testdata/` を組み合わせたテストが既存パターンである。
  - diagnostics snapshot は `testdata/snapshots/diagnostics` に存在し、IR 用の `testdata/snapshots/ir` が準備済みである。
- **Implications**:
  - 変換器の単体テストと snapshot 比較テストを追加する。
  - 比較対象には timestamp や絶対 path を含めない。

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| AST 直接 JSON 生成 | AST traversal 中に JSON を直接組み立てる | ファイル数が少ない | escaping、field order、将来拡張、型安全性が弱い | 不採用 |
| Typed model plus serializer | `.klib` document model を C# record で表し serializer で出力する | 型安全、テスト容易、field order を管理しやすい | model 定義の初期コストがある | 採用 |
| VM loader 併設 | emitter と loader / validator を同時に作る | 契約検証が強い | Issue #25 の範囲を超える | 不採用 |

## Design Decisions

### Decision: typed `.klib` model を作る

- **Context**: `.klib` 出力は golden test で安定比較でき、将来の VM loader とも契約を共有できる必要がある。
- **Alternatives Considered**:
  1. JSON 文字列を直接生成する。
  2. `Dictionary<string, object>` ベースで動的に組み立てる。
  3. C# record ベースの typed model を作る。
- **Selected Approach**: C# record ベースの typed model を `Intermediate` 配下に追加する。
- **Rationale**: nullable と型で必須 field を表現でき、serializer と test が同じ model を参照できる。
- **Trade-offs**: 初期の型定義は増えるが、仕様変更時のレビュー対象が明確になる。
- **Follow-up**: 実装時に field order と JSON property naming を snapshot で固定する。

### Decision: 変換器は semantic analysis 後の `ScriptDocument` を入力にする

- **Context**: 未定義 tag や actor/function 参照は既存 semantic diagnostics が既に扱う。
- **Alternatives Considered**:
  1. raw `ScriptSyntax` だけを入力にする。
  2. `ScriptDocument` と `SemanticAnalysisResult` を入力にする。
  3. build command 内で parser から直接 JSON を出す。
- **Selected Approach**: `ScriptDocument` と import graph / semantic success 前提を入力にした converter を設計する。
- **Rationale**: file path、module name、AST が揃い、既存 semantic diagnostics を再実装しない。
- **Trade-offs**: check-only 以外の build 統合は別途 task 化が必要。
- **Follow-up**: 通常 build コマンドが実装される時点で output path と manifest 生成との接続を再検証する。

### Decision: manifest は最小参照だけを出力する

- **Context**: requirements は manifest 全体生成を out of scope としている。
- **Alternatives Considered**:
  1. manifest schema 全体を同時に生成する。
  2. `.klib` には manifest 情報を一切含めない。
  3. `.klib` 仕様の `manifestRefs` に最小 scripts/assets/localeKeys を含める。
- **Selected Approach**: `manifestRefs` は自身の script 参照、空の assets / localeKeys、必要に応じた locale / asset key 集合に限定する。
- **Rationale**: `.klib` 仕様と後続 package 作業の接続点を保ちつつ、manifest 所有情報を奪わない。
- **Trade-offs**: 完全な runtime package 生成は別仕様に残る。
- **Follow-up**: manifest 生成仕様で scripts ID と artifact path の対応を再検証する。

## Risks & Mitigations

- SourceLocation が text line には不足している — statement-level mapping を primary とし、line-level 詳細化は後続 parser 拡張時に再検証する。
- `.kc` / `.kc` 混在 testdata が残る — display path は既存 parser / project config の値を正規化して使い、拡張子移行は別作業にする。
- 通常 build への接続範囲が広がる — `--check-only` の artifact 非変更保証を守り、emitter 単体と build integration を分けて task 化する。

## References

- `docs/spec/k-intermediate-representation-spec.md` — `.klib` document と opcode の authoritative specification。
- `source/cli/KoromoEventScript.Cli/Commands/Build/BuildCheckOnlyCommand.cs` — 既存 build check pipeline。
- `source/cli/KoromoEventScript.Cli/Parsing/SyntaxNodes.cs` — `.kc` AST 入力。
- `source/cli/KoromoEventScript.Cli/Semantics/NameResolver.cs` — label / reference diagnostics。
