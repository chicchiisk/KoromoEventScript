# Research & Design Decisions

## Summary

- **Feature**: `kes-definition-collection`
- **Discovery Scope**: Extension
- **Key Findings**:
  - 既存の `DefinitionCollector` は import/name 解決向けの flat な `SymbolDefinition` を返しており、スコープ階層や定義種別を保持できない。
  - Lexer は `actor`、`fn`、`class`、`enum`、`public`、`private` を予約語として認識済みだが、Parser / AST は主要宣言構文をまだ表現していない。
  - 既存の `SemanticAnalyzer` は import 解決後に定義収集を実行しているため、同じ semantic stage にスコープ付き定義収集を接続できる。

## Research Log

### 既存 Parser / AST の拡張点

- **Context**: 1.1-1.5 は主要定義の名前と source location を意味解析入力として扱うことを要求している。
- **Sources Consulted**:
  - `source/cli/KoromoEventScript.Cli/Lexing/KeLexer.cs`
  - `source/cli/KoromoEventScript.Cli/Parsing/KeParser.cs`
  - `source/cli/KoromoEventScript.Cli/Parsing/SyntaxNodes.cs`
  - `docs/spec/kes-language-spec.md`
- **Findings**:
  - Lexer は主要宣言キーワードをすでに `Keyword` として分類する。
  - Parser は `var`、`label`、`jump`、`say`、`nar`、`select`、LESS/command のみを AST 化している。
  - `docs/spec/kes-language-spec.md` は `fn`、`class`、`enum`、`actor` の宣言構文とスコープ規則を定義済みである。
- **Implications**:
  - 新しい外部依存は不要。
  - AST に主要宣言ノード、パラメータ、enum member、class member、通常 block を追加する必要がある。

### 既存 Semantics の接続点

- **Context**: 2.1-5.5 はスコープ付き定義収集、診断、check-only 統合を要求している。
- **Sources Consulted**:
  - `source/cli/KoromoEventScript.Cli/Semantics/DefinitionCollector.cs`
  - `source/cli/KoromoEventScript.Cli/Semantics/SemanticModels.cs`
  - `source/cli/KoromoEventScript.Cli/Semantics/SemanticAnalyzer.cs`
  - `source/cli/KoromoEventScript.Cli/Semantics/NameResolver.cs`
- **Findings**:
  - `SemanticAnalyzer` は import 成功後に全 document の定義収集を行い、diagnostic があれば compile error として返す。
  - 既存 `NameResolver` は `IReadOnlyDictionary<string, IReadOnlyList<SymbolDefinition>>` を入力にするため、当面は新しい定義表から module-level symbol view を提供すれば互換性を保てる。
  - タグ定義とタグ参照は既存 flow control 仕様に近く、Issue #19 の主要定義とは分離する必要がある。
- **Implications**:
  - `DefinitionCollector` はスコープ付き結果を返す方向へ拡張し、既存 `NameResolver` 向け互換 view を同時に保持する。
  - import 解決やタグ解決の責務は変更しない。

### スコープ規則

- **Context**: 2.1-3.5 は module/class/function-or-method/block の parent-child 関係、重複、シャドーイングを要求している。
- **Sources Consulted**:
  - `docs/spec/kes-language-spec.md` の `スコープ規則`
- **Findings**:
  - module scope は top-level `var`、`fn`、`class`、`enum`、`actor` を共有する。
  - class scope は member `var` と member `fn` を持ち、異なる class 間の同名 member は許容される。
  - function/method scope は parameters と直下 local `var` を持つ。
  - 同一 scope の重複と outer scope の shadowing は compile error である。
- **Implications**:
  - Scope identity と parent identity を明示的に持つモデルが必要。
  - 重複と shadowing は同じ visitor pass 内で検査できる。

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| Flat symbol table 拡張 | 既存 `SymbolDefinition` に種別や親情報を追加する | 変更量が少ない | nested scope、class member、shadowing を表現しづらい | 不採用 |
| Scoped definition table | `DefinitionScope` と `ScopedSymbolDefinition` を追加し、階層化する | 要求の scope 規則を直接表現できる | 既存 `NameResolver` との互換接続が必要 | 採用 |
| NameResolver 完全刷新 | 定義収集と参照解決を同時に新モデルへ移行する | 最終形に近い | Issue #19 の範囲を超え、既存 import/tag 解決のリスクが高い | 不採用 |

## Design Decisions

### Decision: スコープ付き定義表を新しい semantic model として追加する

- **Context**: 既存 flat symbol model では 2.5、3.2、3.4 を安全に満たせない。
- **Alternatives Considered**:
  1. `SymbolDefinition` に scope string を足す。
  2. `DefinitionScope` と `ScopedSymbolDefinition` を追加する。
- **Selected Approach**: `DefinitionScope`、`ScopedSymbolDefinition`、`DefinitionKind`、`ScopeKind` を追加し、`DefinitionCollectionResult` が scope tree と diagnostics を保持する。
- **Rationale**: Scope parent-child、definition kind、source location を型で表現でき、後続参照解決にも流用しやすい。
- **Trade-offs**: モデル数は増えるが、責務は `Semantics` に閉じる。
- **Follow-up**: 実装時に既存 `NameResolver` へ渡す module-level symbol view を維持する。

### Decision: Parser は主要宣言を構文情報として表現するが型検査しない

- **Context**: 1.1-1.5 は構文情報の認識を要求し、Out of scope は完全型検査を除外している。
- **Alternatives Considered**:
  1. 宣言行のみを ad hoc に token scan する。
  2. AST に宣言ノードを追加する。
- **Selected Approach**: AST に `FunctionDeclarationSyntax`、`ClassDeclarationSyntax`、`EnumDeclarationSyntax`、`ActorDeclarationSyntax`、`ParameterSyntax`、`ClassMemberSyntax` を追加する。
- **Rationale**: Source location と block 境界を明確にでき、syntax error と semantic error を既存契約どおり分離できる。
- **Trade-offs**: Parser の責務は増えるが、意味解析は AST に依存するだけでよくなる。
- **Follow-up**: 式/型 token は token list として保持し、型解釈は後続 spec に残す。

### Decision: 既存 import/name/tag 解決は互換 view で接続する

- **Context**: 4.1-5.5 は後続参照解決と check-only 統合を要求するが、完全な参照解決刷新は範囲外である。
- **Alternatives Considered**:
  1. `NameResolver` を新 scope model に全面移行する。
  2. 新 scope model から module-level `SymbolDefinition` view を生成する。
- **Selected Approach**: `DefinitionCollectionResult` に主要定義表を持たせつつ、既存 `NameResolver` が読む symbol view を提供する。
- **Rationale**: 既存 import 解決、タグ解決、CLI exit code の回帰を避けられる。
- **Trade-offs**: 一時的に新旧 model が共存する。
- **Follow-up**: 後続の本格参照解決 spec で `NameResolver` を scope model へ移行する。

## Risks & Mitigations

- Parser 拡張で既存 command/LESS 構文が壊れる — 既存 parser tests と minimal/import fixture を回帰テストに含める。
- `fn` / `class` block 内の構文表現が過剰に膨らむ — Issue #19 では定義収集に必要な名前、位置、body statement のみ保持し、型解釈や式評価はしない。
- 新旧 definition model の不整合 — module-level compatibility view の unit test と `SemanticAnalyzerTests` で検証する。

## References

- `docs/spec/kes-language-spec.md` — 主要定義構文とスコープ規則。
- `.kiro/specs/kes-import-resolution/design.md` — 既存 semantic stage と import/name 解決の境界。
