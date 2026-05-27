# Implementation Plan

- [ ] 1. Foundation: 主要宣言を構文として扱える土台を整える
- [x] 1.1 主要宣言の parser 期待動作をテストで固定する
  - top-level `actor`、`fn`、`class`、`enum`、`var` の名前と source location が観測できるテストを追加する
  - class member の `var` / `fn`、function / method parameter、local `var` を含む構文サンプルを追加する
  - 不完全な主要宣言が既存の syntax diagnostic として失敗し、部分的な定義情報を生成しないことを確認できる
  - 完了時には、主要宣言 parser テストが未実装状態で失敗し、期待する AST 上の観測点が明確になる
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_
  - _Boundary: KeParserTests_

- [x] 1.2 主要宣言を表現する AST を追加する
  - function、class、enum、actor、parameter、class member、block の構文情報を表現できるようにする
  - 名前の source location は keyword ではなく identifier 位置を保持する
  - 型注釈や initializer は型解釈せず、後続処理が参照できる構文情報として保持する
  - 完了時には、parser が主要宣言を返すための構文レコードを型安全に参照できる
  - _Requirements: 1.1, 1.2, 1.3, 1.4_
  - _Boundary: Declaration Syntax Nodes_

- [x] 1.3 top-level の `actor`、`fn`、`class`、`enum` を解析する
  - import placement rule を維持したまま、主要宣言を top-level statement として扱う
  - function と actor の body は通常 statement block として保持する
  - enum member は enum scope 内の名前候補として source location 付きで保持する
  - 完了時には、top-level 主要宣言の parser テストが成功し、既存 command/text/select/label/jump 構文も継続して解析できる
  - _Requirements: 1.1, 1.5_
  - _Boundary: KeParser_

- [x] 1.4 class member、parameter、local `var` を解析する
  - class body 内の member `var` と member `fn` を class member として扱う
  - function / method parameter の名前と位置を保持する
  - function / method / actor body 内の local `var` が block 内 statement として観測できる
  - 完了時には、class member、parameter、local `var` の parser テストが成功し、不完全な宣言は syntax diagnostic として分類される
  - _Requirements: 1.2, 1.3, 1.4, 1.5_
  - _Boundary: KeParser_

- [ ] 2. Core: スコープ付き定義モデルと収集規則を実装する
- [x] 2.1 定義種別とスコープ階層の semantic model を用意する
  - variable、function、class、enum、enum member、actor、parameter、class field、class method を区別できる
  - module、class、function、method、block の scope kind と親子関係を表せる
  - module-level compatibility symbol view の入力に必要な module / file / location 情報を保持する
  - 完了時には、semantic model の単体テストで scope identity、parent identity、definition kind を検証できる
  - _Requirements: 2.5, 4.2, 4.3, 4.4_
  - _Boundary: Definition Models_

- [x] 2.2 module scope の主要定義を収集する
  - syntax-valid script の top-level `actor`、`fn`、`class`、`enum`、`var` を module scope に登録する
  - 既存の tag 定義収集と競合しない形で主要定義を扱う
  - module-level compatibility symbol view から既存名前解決が主要定義を参照できるようにする
  - 完了時には、module scope の定義表と compatibility symbol view が単体テストで確認できる
  - _Requirements: 2.1, 4.1, 4.2, 4.3, 4.4_
  - _Boundary: DefinitionCollector_

- [x] 2.3 class scope の member 定義を収集する
  - class ごとに独立した class scope を作成する
  - member `var` を class field、member `fn` を class method として登録する
  - 異なる class 間の同名 member は別 scope として扱う
  - 完了時には、複数 class の同名 member が許容されることを単体テストで確認できる
  - _Requirements: 2.2, 2.5, 3.4, 4.4_
  - _Boundary: DefinitionCollector_

- [x] 2.4 function / method / block scope の定義を収集する
  - function と method の parameter を function-or-method scope に登録する
  - function / method 直下の local `var` を同じ function-or-method scope に登録する
  - nested block 内の local `var` を block scope に登録し、親 scope との関係を保持する
  - 完了時には、parameter、local `var`、nested block scope の親子関係を単体テストで確認できる
  - _Requirements: 2.3, 2.4, 2.5, 4.1_
  - _Boundary: DefinitionCollector_

- [x] 2.5 重複定義とシャドーイングを診断する
  - 同一 scope 内の同名定義を duplicate compile diagnostic として報告する
  - outer scope の visible definition と同名の内側定義を shadowing compile diagnostic として報告する
  - module scope の `actor`、`fn`、`class`、`enum`、`var` は同じ名前空間として衝突させる
  - 完了時には、diagnostic の file、line、column、code、message が単体テストで確認できる
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 4.5_
  - _Boundary: DefinitionCollector_

- [ ] 3. Integration: semantic stage と CLI check-only に接続する
- [x] 3.1 定義収集結果を semantic analysis result に保持する
  - import 解決成功後に scoped definition collection を実行する
  - 収集成功時には後続 semantic validation が定義表を参照できる状態にする
  - 収集失敗時には name resolution へ進まず compile error として分類する
  - 完了時には、semantic analyzer の単体テストで成功結果と失敗時の stage gating を確認できる
  - _Depends: 2.5_
  - _Requirements: 4.1, 4.2, 4.5, 5.1, 5.2, 5.3_
  - _Boundary: SemanticAnalyzer_

- [x] 3.2 既存 name resolution との互換接続を維持する
  - scoped definition result から existing name resolver が使う module-level symbol view を渡す
  - imported module definitions と local module definitions を区別できる状態を維持する
  - 既存 import/name/tag resolution の成功・失敗分類を変えない
  - 完了時には、既存 import-resolution と tag-resolution の semantic tests が回帰なく成功する
  - _Depends: 3.1_
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_
  - _Boundary: SemanticAnalyzer, NameResolver_

- [x] 3.3 `kes build --check-only` の診断と終了コードに反映する
  - valid major definitions を含む project が成功終了することを統合する
  - duplicate / shadowing diagnostics が compile error exit code になることを統合する
  - text と JSON Lines の両方で既存 diagnostic fields と順序を保持する
  - 完了時には、CLI 統合テストで success、compile error、text output、JSON Lines output を確認できる
  - _Depends: 3.2_
  - _Requirements: 3.5, 5.1, 5.2, 5.3, 5.4, 5.5_
  - _Boundary: CLI Check-only Flow_

- [ ] 4. Validation: 要求範囲と回帰をテストで固定する
- [x] 4.1 (P) parser 宣言構文の回帰テストを完成させる
  - top-level 主要宣言、class member、parameter、local `var` の source location を検証する
  - malformed declaration が syntax diagnostic として分類されることを検証する
  - 既存 command、LESS、text block、select、label、jump の parser tests が成功することを確認する
  - 完了時には、parser 関連テストだけを実行して全件成功した結果を提示できる
  - _Depends: 1.4_
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_
  - _Boundary: KeParserTests_

- [x] 4.2 (P) scoped definition collection の単体テストを完成させる
  - definition kind、scope kind、parent-child scope、compatibility symbol view を検証する
  - duplicate と shadowing の compile diagnostics を検証する
  - 異なる class 間の同名 member が許容されることを検証する
  - 完了時には、definition collector 関連テストだけを実行して全件成功した結果を提示できる
  - _Depends: 2.5_
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.2, 3.3, 3.4, 3.5, 4.1, 4.2, 4.3, 4.4, 4.5_
  - _Boundary: DefinitionCollectorTests_

- [x] 4.3 semantic / CLI 統合テストを完成させる
  - semantic analyzer が import 後、name resolution 前に definition collection を実行することを検証する
  - collection diagnostics で name resolution が消費されないことを検証する
  - `kes build --check-only` の text と JSON Lines diagnostics を検証する
  - 完了時には、semantic analyzer と build check-only の統合テストが全件成功した結果を提示できる
  - _Depends: 3.3_
  - _Requirements: 4.1, 4.2, 4.5, 5.1, 5.2, 5.3, 5.4, 5.5_
  - _Boundary: SemanticAnalyzerTests, BuildCheckOnlyCommandTests_

- [x] 4.4 全体回帰と差分品質チェックを実行する
  - 既存 lexer/parser/semantic/build tests が主要定義収集追加後も通ることを確認する
  - `dotnet test` と差分空白チェックを実行し、失敗があれば原因を修正する
  - 実装範囲が parser、semantics、CLI 統合、tests に収まっていることを確認する
  - 完了時には、全体テスト結果と差分品質チェック結果を implementation evidence として提示できる
  - _Depends: 4.1, 4.2, 4.3_
  - _Requirements: 1.5, 3.5, 5.1, 5.2, 5.3, 5.4, 5.5_
  - _Boundary: Regression Validation_
