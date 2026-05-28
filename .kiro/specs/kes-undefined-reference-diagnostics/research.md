# Research & Design Decisions

## Summary

- **Feature**: `kes-undefined-reference-diagnostics`
- **Discovery Scope**: Extension
- **Key Findings**:
  - 既存の意味解析は `ImportResolver`、`DefinitionCollector`、`NameResolver`、`SemanticAnalyzer` の順で構成され、未定義名と未定義タグの一部はすでに `NameResolver` に集約されている。
  - `SymbolDefinition` は名前と位置だけを持つ legacy 形式であり、actor / function / variable の参照種別を正確に分けるには `DefinitionTable` / `ScopedSymbolDefinition` の `DefinitionKind` を使う必要がある。
  - `CommandStatementSyntax`、`LessStatementSyntax`、`SayStatementSyntax` は参照名の位置を十分に保持していないため、参照箇所診断を安定させるには syntax node に name location を追加する必要がある。

## Research Log

### 既存の意味解析パイプライン

- **Context**: 未定義参照診断をどこへ統合するかを確認した。
- **Sources Consulted**: `source/cli/KoromoEventScript.Cli/Semantics/SemanticAnalyzer.cs`、`NameResolver.cs`、`DefinitionCollector.cs`、`SemanticModels.cs`
- **Findings**:
  - `SemanticAnalyzer` は import 解決失敗時に name resolution を実行しない。
  - 定義収集診断がある場合も name resolution を実行せず、既存 stage ordering を保持している。
  - `NameResolver` は現在 `SymbolDefinition` の集合で local/imported symbols を解決し、`KES2010`、`KES2012`、`KES2013` を出力している。
- **Implications**:
  - 新しい設計は `NameResolver` を置き換えるのではなく、同じ semantic validation stage に拡張する。
  - 6.1、6.2、6.3 の stage ordering は `SemanticAnalyzer` の既存制御を維持すれば満たせる。

### 参照位置と syntax node

- **Context**: 診断位置が参照箇所を指すために必要な source location を確認した。
- **Sources Consulted**: `source/cli/KoromoEventScript.Cli/Parsing/SyntaxNodes.cs`、`KeParser.cs`
- **Findings**:
  - `JumpStatementSyntax` と `CaseClauseSyntax` は tag location を保持している。
  - `CommandStatementSyntax.Name`、`LessStatementSyntax.Name`、`SayStatementSyntax.Speaker` は文字列のみで位置を持たない。
  - 関数参照と actor 参照は、名前トークン位置を syntax node に追加しないと正確に診断できない。
- **Implications**:
  - parser の公開 syntax record を後方互換的に拡張し、既存テストの手動構築コードには default location を許容する。

### 定義種別と scope

- **Context**: 変数、actor、関数の種別ごとに解決可否を分けられるかを確認した。
- **Sources Consulted**: `DefinitionModels.cs`、`DefinitionCollector.cs`
- **Findings**:
  - `DefinitionKind` は `Variable`、`Function`、`Actor`、`Parameter`、`ClassField`、`ClassMethod` などをすでに表現できる。
  - `DefinitionTable` は scope と parent-child 関係を保持する。
  - module-level legacy `SymbolDefinition` だけでは種別を判定できない。
- **Implications**:
  - name resolution は `DefinitionCollectionResult.DefinitionTable` を入力に含め、参照種別ごとに許可する `DefinitionKind` を判定する。

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| 既存 `NameResolver` の拡張 | 既存 resolver に参照分類、scope-aware lookup、位置付き参照収集を追加する | stage ordering と診断出力を維持しやすい | 既存 `SymbolDefinition` 入力だけでは不足する | 採用 |
| 新規 resolver の追加 | 変数、actor、関数、label ごとに専用 resolver を作る | 責務を分けやすい | task が分散し、診断順序の調整が複雑になる | 不採用 |
| parser 段階で未定義診断 | 構文解析中に参照を診断する | 実装箇所が少ない | import と定義表が未確定なので正確に判定できない | 不採用 |

## Design Decisions

### Decision: `DefinitionTable` を name resolution の主入力にする

- **Context**: actor / function / variable の参照種別を区別する必要がある。
- **Alternatives Considered**:
  1. `SymbolDefinition` に kind を追加する。
  2. `DefinitionTable` の `ScopedSymbolDefinition` を使う。
- **Selected Approach**: `NameResolver.ResolveNames` が `DefinitionCollectionResult` または module ごとの `DefinitionTable` を受け取り、`DefinitionKind` と scope 情報を使って参照を解決する。
- **Rationale**: 既存の定義収集成果を再利用し、今後の scope-aware な型検査にもつながる。
- **Trade-offs**: `SemanticAnalyzer` から `NameResolver` へ渡す入力形を変えるため、既存 unit test の helper 更新が必要。
- **Follow-up**: legacy `SymbolDefinition` をすぐ削除せず、既存 import collision と診断出力との互換を保つ。

### Decision: syntax node に参照名 location を追加する

- **Context**: 関数名、LESS 名、`say` 話者の診断位置が参照箇所を指す必要がある。
- **Alternatives Considered**:
  1. 診断位置を行頭または既存引数位置で代用する。
  2. parser が取得済みの token location を syntax node に保存する。
- **Selected Approach**: `CommandStatementSyntax`、`LessStatementSyntax`、`SayStatementSyntax` に `SourceLocation` を追加する。
- **Rationale**: requirements の位置要件を満たし、診断の再現性を高める。
- **Trade-offs**: syntax node コンストラクタ呼び出しを更新する必要がある。
- **Follow-up**: 既存テストの手動 syntax 構築では default location を使えるよう record の引数順を慎重に設計する。

### Decision: 外部ライブラリは導入しない

- **Context**: 未定義参照診断は既存 AST と定義表の走査で実現できる。
- **Alternatives Considered**:
  1. 既存 C# コレクションと record で実装する。
  2. 名前解決用ライブラリを導入する。
- **Selected Approach**: 既存コードベース内で実装する。
- **Rationale**: DSL 固有の scope と import graph に合わせる必要があり、外部依存の利点が小さい。
- **Trade-offs**: resolver logic は自前で保守する。
- **Follow-up**: resolver helper を小さく分け、テストで回帰を抑える。

## Risks & Mitigations

- 関数参照と変数参照の分類が曖昧になるリスク — command / LESS / function call / identifier reference を `ReferenceKind` として明示する。
- scope lookup が既存の定義収集 scope とずれるリスク — `DefinitionTable` の scope parent-child を唯一の探索基準にし、unit test で関数内・class method 内・import 越しのケースを固定する。
- 既存 import collision / ambiguous reference 診断を壊すリスク — `KES2011`、`KES2012` のテストを維持し、NameResolverTests に既存ケースを残す。

## References

- `source/cli/KoromoEventScript.Cli/Semantics/NameResolver.cs` — 既存の名前解決と診断コード。
- `source/cli/KoromoEventScript.Cli/Semantics/DefinitionModels.cs` — scope と definition kind の既存データ構造。
- `source/cli/KoromoEventScript.Cli/Parsing/SyntaxNodes.cs` — 参照位置を保持する syntax node。
- `tests/KoromoEventScript.Cli.Tests/Semantics/NameResolverTests.cs` — 既存 resolver の期待動作。
