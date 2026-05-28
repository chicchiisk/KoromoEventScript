# Research & Design Decisions

## Summary

- **Feature**: `kes-minimal-type-checking`
- **Discovery Scope**: Extension
- **Key Findings**:
  - `SemanticAnalyzer` は import 解決、定義収集、名前解決の stage ordering を既に持つため、型検査は `NameResolver` 成功後の新 stage として追加できる。
  - `SyntaxNodes` は `VarStatementSyntax`、`CommandStatementSyntax`、`LessStatementSyntax`、`SayStatementSyntax` に token と位置情報を保持しているが、代入、`if`、`while`、`for` の専用 syntax node はまだない。
  - `DefinitionTable` は scope と `DefinitionKind` を持つが型注釈は保持しないため、型検査は syntax と definition table から一時的な型環境を構築する必要がある。

## Research Log

### Semantic validation pipeline

- **Context**: 型検査をどの stage に接続するかを確認した。
- **Sources Consulted**: `source/cli/KoromoEventScript.Cli/Semantics/SemanticAnalyzer.cs`、`SemanticModels.cs`、`NameResolver.cs`。
- **Findings**:
  - import 解決失敗時は `SemanticAnalysisResult.From(importResult, NameResolutionResult.Success())` で早期 return する。
  - 定義収集診断がある場合は `NameResolutionResult.Failure` として compile error を返し、名前解決へ進まない。
  - 名前解決は `DefinitionCollectionResult` と `ImportGraph` を入力にし、成功時だけ diagnostic なしで完了する。
- **Implications**:
  - 型検査は名前解決成功後に実行し、未定義参照がある場合は型推測による派生診断を出さない。
  - `SemanticAnalysisResult` に型検査結果を追加する設計が自然である。

### Syntax and parser coverage

- **Context**: requirements が要求する代入、式、制御構文、命令引数を現行 AST で表現できるか確認した。
- **Sources Consulted**: `SyntaxNodes.cs`、`KeParser.cs`、`TokenKind.cs`、`KeLexer.cs`。
- **Findings**:
  - 型注釈と式は token list として保持されるため、最小型検査は token stream evaluator で開始できる。
  - `TokenKind` は算術、比較、論理、配列、括弧、名前付き引数に必要な token を持つ。
  - `if`、`while`、`for`、代入文の syntax node と parser branch は現時点で不足している。
- **Implications**:
  - この spec は semantic type checker に加え、最小構文ノード追加を所有する。
  - 完全な式 AST への置換は不要で、token list を読む `ExpressionTypeEvaluator` を `TypeChecker` 内部に閉じる。

### Definition and type environment

- **Context**: 変数、引数、関数戻り値、actor の型をどこから取得するかを確認した。
- **Sources Consulted**: `DefinitionCollector.cs`、`DefinitionModels.cs`、`NameResolver.cs`。
- **Findings**:
  - `ScopedSymbolDefinition` は name、kind、location、scope id を持つが型情報は持たない。
  - function parameter と return type、var type annotation は syntax node 側に token list として存在する。
  - actor declaration は `DefinitionKind.Actor` として収集される。
- **Implications**:
  - 型検査は `TypeEnvironment` を一時構築し、syntax declaration と `DefinitionTable` の scope を対応付ける。
  - `DefinitionModels` へ型情報を永続的に追加する必要は現段階ではない。

### Testing shape

- **Context**: 設計を実装可能な test boundary に落とすため既存テスト構成を確認した。
- **Sources Consulted**: `DefinitionCollectorTests.cs`、`NameResolverTests.cs`、`SemanticAnalyzerTests.cs`、`BuildCheckOnlyCommandTests.cs`。
- **Findings**:
  - semantic unit tests は手動 syntax construction と parser source の両方を使う。
  - CLI tests は `TemporaryProject` と testdata fixture の両方を使う。
  - JSON Lines と text diagnostic の統合確認は `BuildCheckOnlyCommandTests` に集約されている。
- **Implications**:
  - `TypeCheckerTests` を追加し、型規則を unit level で固定する。
  - `SemanticAnalyzerTests` と `BuildCheckOnlyCommandTests` は stage ordering と出力契約の回帰に絞る。

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| NameResolver へ型検査を統合 | 参照解決と型検査を同じ class に追加する | 既存入力を再利用しやすい | 責務が肥大化し、未定義参照と型不一致の ordering が曖昧になる | 不採用 |
| 独立 TypeChecker stage | 名前解決成功後に `TypeChecker` を実行する | stage ordering が明確で、テスト境界を分けやすい | `SemanticAnalysisResult` への結果追加が必要 | 採用 |
| 完全な expression AST 導入 | parser が式 AST を構築して checker が読む | 将来の拡張性が高い | Issue #22 の範囲を超える parser 改修になる | 不採用 |
| token stream evaluator | 既存 token list から最小型を評価する | 変更範囲が小さく MVP に適合する | 複雑な式拡張時に再設計が必要 | 採用 |

## Design Decisions

### Decision: 型検査を独立 semantic stage にする

- **Context**: 型不一致は未定義参照の後段であり、前段診断と重複してはならない。
- **Alternatives Considered**:
  1. `NameResolver` に統合する。
  2. `SemanticAnalyzer` から独立 `TypeChecker` を呼ぶ。
- **Selected Approach**: `SemanticAnalyzer` が `NameResolver` 成功後に `TypeChecker.CheckTypes(graph, definitionResults)` を呼ぶ。
- **Rationale**: 責務が分かれ、6.5 の stage ordering を実装とテストで確認しやすい。
- **Trade-offs**: `SemanticAnalysisResult` と result model が増える。
- **Follow-up**: 実装時に `NameResolutionResult` 失敗時は型検査を実行しないことを unit/integration test で固定する。

### Decision: MVP 型だけを表す semantic type を導入する

- **Context**: `number`、`bool`、`string`、`Actor`、配列、`null`、`void` を一貫して比較する必要がある。
- **Alternatives Considered**:
  1. string 名だけで型比較する。
  2. `KesType` value object を作る。
- **Selected Approach**: `KesType` と `KesTypeKind` を導入し、配列は element type を持つ value object とする。
- **Rationale**: `null` assignability、array element、`void` の value usage を安全に表現できる。
- **Trade-offs**: 小さな型モデルが増える。
- **Follow-up**: enum/class の詳細検査は Unknown/Unsupported として扱い、Issue #22 の範囲を超えない。

### Decision: STL は最小組み込みシグネチャ表で扱う

- **Context**: Issue #22 は MVP 命令引数の型不一致を診断するが、STL 完全登録や `__systemcall__` 検査は別範囲である。
- **Alternatives Considered**:
  1. `NameResolver` の built-in callable set を拡張して型も持たせる。
  2. 型検査専用の `BuiltInSignatureRegistry` を作る。
- **Selected Approach**: `BuiltInSignatureRegistry` が MVP command/function signatures を提供する。
- **Rationale**: 参照可能性と型シグネチャの責務を分けられる。
- **Trade-offs**: built-in 名が一時的に `NameResolver` と二重管理になる。
- **Follow-up**: 将来の STL 完全登録仕様で両者を統合する余地を残す。

## Risks & Mitigations

- token stream evaluator が式の一部を過剰解釈するリスク — MVP 演算子と括弧、配列、呼び出しに限定し、未知形は派生診断を抑制する。
- built-in callable set と signature registry の drift — 共有テストで `NameResolver` の built-ins と型検査対象 built-ins の代表ケースを確認する。
- parser scope 増加による既存構文 regressions — `KeParserTests` と CLI fixture で既存 command/LESS/say の互換性を固定する。

## References

- `docs/spec/kes-language-spec.md` — 変数、式、配列、命令、actor、制御構文の公開仕様。
- `docs/spec/kes-language-stl-spec.md` — MVP 組み込み命令と関数のシグネチャ。
- `.kiro/specs/kes-definition-collection/requirements.md` — 定義収集と後続参照解決向け情報。
- `.kiro/specs/kes-undefined-reference-diagnostics/requirements.md` — 型検査前段の未定義参照診断。
