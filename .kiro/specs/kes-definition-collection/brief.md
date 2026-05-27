# Brief: kes-definition-collection

## Problem

CLI 利用者とコンパイラ開発者は、`actor`、`fn`、`class`、`enum`、`var` の主要定義を意味解析で一貫して扱う基盤がまだないため、後続の参照解決、型検査、Language Server 連携で同じ定義情報を再利用できない。現状の `kes-import-resolution` は import/name 解決のための最小定義収集に留まっており、スコープ単位の定義表としては不足している。

## Current State

`source/cli/KoromoEventScript.Cli/Semantics/DefinitionCollector.cs` は `var`、`label`、タグ付き `say` / `nar` など、既存 AST で観測できる一部トップレベル定義を収集している。`fn`、`class`、`enum`、`actor` は lexer の予約語や言語仕様には存在するが、parser/AST/semantic model としての定義収集は未整備である。

## Desired Outcome

意味解析が `actor`、`fn`、`class`、`enum`、`var` の定義をスコープごとに収集し、重複やスコープ境界を診断可能な形で保持できる。後続の参照解決は、この定義情報を入力として使える。

## Approach

既存の semantic boundary を拡張し、構文解析済み `ScriptSyntax` から定義を収集する専用モデルを作る。parser/AST に不足している主要定義ノードと source location を追加し、`DefinitionCollector` は import 解決とは独立したスコープ付き定義表を返す。これにより既存の `SemanticAnalyzer` / `NameResolver` は段階的に新しい定義表へ移行できる。

## Scope

- **In**: `actor`、`fn`、`class`、`enum`、`var` の定義収集、モジュール/クラス/関数またはメソッド/ブロックに対応するスコープモデル、同一スコープ重複とシャドーイング検出のための情報保持、後続参照解決へ渡す semantic model。
- **Out**: 完全な型検査、式評価、IR / `.k` 生成、runtime 起動、STL 組み込み定義の完全登録、VS Code Language Server 実装。

## Boundary Candidates

- Parser / AST: 主要定義構文と名前位置を表現する。
- Semantic definition collection: AST からスコープ付き定義表を構築する。
- Name-resolution integration: 既存 import/name 解決が新しい定義表を参照できる接続点を用意する。

## Out of Boundary

- import モジュール探索、循環 import、import 先ファイル読込の仕様変更。
- `label` / `jump` / `case` の制御フロー検査拡張。
- actor 素材、enum 値の型整合、class constructor / method body の完全検査。
- `.kel` 構文や project config の拡張。

## Upstream / Downstream

- **Upstream**: 既存 `.ke` lexer/parser、`kes-import-resolution` の `ScriptDocument`、`ImportGraph`、`SemanticAnalyzer`、診断/終了コード契約、`docs/spec/kes-language-spec.md` の定義構文とスコープ規則。
- **Downstream**: 本格的な参照解決、型検査、STL 組み込み定義、IR 生成、VS Code の定義ジャンプ/補完/診断。

## Existing Spec Touchpoints

- **Extends**: `kes-import-resolution` の最小 `DefinitionCollector` / `NameResolver` を、主要定義とスコープ対応の semantic model へ拡張する。
- **Adjacent**: `kes-build-check-only` は CLI stage ordering と診断出力を所有するため、今回の実装は既存の check-only フローに意味解析結果を渡す範囲に留める。

## Constraints

ドキュメントと spec は日本語で作成する。実装は既存 .NET / NUnit 構成と `KoromoEventScript.Cli.Semantics` の境界に合わせる。新しい外部依存は追加しない。既存の import 解決、タグ解決、CLI 終了コードの挙動を壊さない。
