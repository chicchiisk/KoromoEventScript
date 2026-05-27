# Research & Design Decisions

## Summary

- **Feature**: `kes-duplicate-definition-diagnostics`
- **Discovery Scope**: Extension
- **Key Findings**:
  - 既存の `DefinitionCollector` は `DefinitionScope` と `ScopedSymbolDefinition` を使い、同一スコープ重複を `KES2009` compile diagnostic として検出している。
  - 現行 `Diagnostic` は主位置だけを持つため、Issue #20 の「重複元と重複先の位置情報」を構造化して運ぶには診断モデルと JSON Lines formatter の拡張が必要である。
  - `SemanticAnalyzer` は import 成功後、name resolution 前に definition collection diagnostics を compile error として返すため、check-only の stage ordering は既存フローを維持できる。

## Research Log

### 既存定義収集とスコープ境界

- **Context**: 同一スコープ内の `actor`、`fn`、`class`、`enum`、`var` 重複をどこで検出するか確認した。
- **Sources Consulted**: `source/cli/KoromoEventScript.Cli/Semantics/DefinitionCollector.cs`, `DefinitionModels.cs`, `tests/KoromoEventScript.Cli.Tests/Semantics/DefinitionCollectorTests.cs`, `.kiro/specs/kes-definition-collection/design.md`
- **Findings**:
  - `DefinitionCollector` は `definitionsByScope` を持ち、`StringComparer.Ordinal` で同一 scope/name の重複を検出している。
  - module / class / enum / function / method / block の scope model は既に存在する。
  - 既存テストは module scope、class scope、shadowing、異なる class scope の同名許可を検証している。
- **Implications**: 新しい定義収集コンポーネントは不要であり、重複診断の位置情報強化と不足ケースのテスト追加に集中する。

### 診断出力契約

- **Context**: 重複元と重複先の位置情報を text と JSON Lines の両方で扱う方法を確認した。
- **Sources Consulted**: `source/cli/KoromoEventScript.Cli/Diagnostics/Diagnostic.cs`, `DiagnosticFormatter.cs`, `DiagnosticSink.cs`, `tests/KoromoEventScript.Cli.Tests/Diagnostics/DiagnosticFormatterTests.cs`, `docs/spec/cli-tool-spec.md`
- **Findings**:
  - CLI 仕様は標準診断として level/code/file/line/column/message を定義している。
  - 現行 JSON Lines は標準フィールドのみを出力する。
  - 主位置は duplicate definition location として使えるが、original definition location は追加情報として保持する必要がある。
- **Implications**: `Diagnostic` に optional related locations を追加し、既存診断の JSON shape は標準フィールドを維持しながら、関連位置がある場合だけ追加フィールドを出す。

### check-only と stage ordering

- **Context**: compile error の終了コードと先行 stage failure の扱いを確認した。
- **Sources Consulted**: `SemanticAnalyzer.cs`, `BuildCheckOnlyCommand.cs`, `CliExitCode.cs`, `docs/spec/cli-tool-spec.md`, `.kiro/specs/kes-import-resolution/design.md`
- **Findings**:
  - `BuildCheckOnlyCommand` は parse diagnostics があれば semantic analysis を実行しない。
  - `SemanticAnalyzer` は import failure を先に返し、definition diagnostics があれば `CliExitCode.CompileError` として name resolution をスキップする。
  - compile error exit code は `4` として定義済みである。
- **Implications**: この仕様は stage ordering を変更せず、definition diagnostics の内容だけを強化する。

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| 既存 `DefinitionCollector` 拡張 | 現行 scope table と重複検出を維持し、diagnostic payload を拡張する | 最小変更、既存テストと整合、責務が明確 | `Diagnostic` contract の互換性に注意が必要 | 採用 |
| 専用 duplicate validator 追加 | 収集後の `DefinitionTable` を別 validator で検査する | validation を分離できる | 既存 `DefinitionCollector` の重複判定と責務が重複する | 不採用 |
| formatter だけで message へ位置を埋め込む | `Diagnostic` を変えず message に original location を入れる | 変更が小さい | JSON Lines で機械可読な original location を提供できない | 不採用 |

## Design Decisions

### Decision: 診断の関連位置を `Diagnostic` に追加する

- **Context**: 2.3 と 3.4 は original definition location を診断から確認できることを要求する。
- **Alternatives Considered**:
  1. message 文字列のみで original location を表現する。
  2. `Diagnostic` に optional related locations を追加する。
- **Selected Approach**: `DiagnosticRelatedLocation` を追加し、`Diagnostic` が 0 件以上の関連位置を保持する。
- **Rationale**: 既存の主位置フィールドを duplicate definition location として維持しつつ、JSON Lines で original location を機械可読にできる。
- **Trade-offs**: formatter とテストの更新が必要になるが、既存診断は related locations なしで同じ標準フィールドを維持できる。
- **Follow-up**: text formatter では related location がある場合に読みやすい追記を行い、既存 text layout の先頭形式を壊さないことをテストする。

### Decision: 重複判定は `DefinitionCollector` の scope-local table に残す

- **Context**: 1.1-1.5 は同一スコープ内の重複を対象にしており、既存 scope model がこの境界を所有している。
- **Alternatives Considered**:
  1. `DefinitionCollector` の `definitionsByScope` で判定する。
  2. `SemanticAnalyzer` で全 definition table を再走査する。
- **Selected Approach**: `DefinitionCollector` が同一 scope/name の first definition を保持し、duplicate diagnostic に first definition を related location として渡す。
- **Rationale**: 重複検出と scope ownership が同じ場所にあり、異なる class scope の同名許可や case-sensitive 比較を既存構造で表現できる。
- **Trade-offs**: module をまたぐ同名衝突や import collision はこの仕様の責務にしない。
- **Follow-up**: module 名が同じ複数 document が semantic input に入る場合は、同じ module scope として検証するか、既存 import ambiguity で遮断されるかをテストで固定する。

## Risks & Mitigations

- `Diagnostic` contract 拡張による既存 JSON テストの回帰 — related location がない診断では既存標準フィールドを維持し、追加フィールドは必要時のみ出力する。
- text output の互換性低下 — 先頭の `file:line:column level code:` 形式を維持し、original location は message 後方へ追記する。
- shadowing との混同 — `KES2014` は関連位置追加の対象にせず、Issue #20 の `KES2009` 重複診断だけを強化する。

## References

- `docs/spec/cli-tool-spec.md` — CLI 診断、JSON Lines、終了コード。
- `docs/spec/kes-language-spec.md` — スコープ規則、重複定義とシャドーイング。
- `.kiro/specs/kes-definition-collection/design.md` — 既存 scoped definition collection の境界。
