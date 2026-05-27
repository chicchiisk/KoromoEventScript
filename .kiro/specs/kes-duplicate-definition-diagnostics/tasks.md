# Implementation Plan

- [x] 1. Foundation: 重複元位置を運べる診断 contract を整える
- [x] 1.1 診断が主位置とは別に関連位置を保持できるようにする
  - 既存診断は主位置、code、level、message をこれまで通り扱える状態を維持する
  - 重複定義診断では、重複先を主位置、重複元を関連位置として保持できるようにする
  - 関連位置がない診断では既存の診断生成コードが追加作業なしで動く
  - 完了時には、診断 model の単体テストまたは既存診断テストから、関連位置あり/なしの両方を型安全に生成できる
  - _Requirements: 2.2, 2.3, 2.5, 3.4_
  - _Boundary: Diagnostic_

- [x] 2. Core: 重複診断と出力を強化する
- [x] 2.1 (P) 関連位置を text と JSON Lines の診断出力に反映する
  - text 出力は既存の先頭形式を維持し、重複元位置を人が読める形で確認できるようにする
  - JSON Lines 出力は標準フィールドを維持し、関連位置がある場合だけ機械可読フィールドを出力する
  - 関連位置がない既存診断では JSON Lines の標準フィールドが変わらない
  - 完了時には、formatter 単体テストで related location あり/なしの text と JSON Lines が確認できる
  - _Depends: 1.1_
  - _Requirements: 3.1, 3.3, 3.4_
  - _Boundary: DiagnosticFormatter_

- [x] 2.2 (P) module scope の主要定義重複に重複元位置を付与する
  - module scope 内の `actor`、`fn`、`class`、`enum`、`var` の同名衝突を重複定義として扱う
  - 重複先の位置を主位置、最初の定義位置を関連位置として診断に含める
  - 診断メッセージから重複した名前を確認できるようにする
  - 完了時には、module scope の主要定義重複テストで `KES2009`、重複先位置、重複元位置、名前が確認できる
  - _Depends: 1.1_
  - _Requirements: 1.1, 2.1, 2.2, 2.3, 4.2_
  - _Boundary: DefinitionCollector_

- [x] 2.3 class / function / method / block scope の重複範囲を固定する
  - class scope の member `fn` と member `var` の同名衝突を重複定義として扱う
  - function、method、block scope の local `var` 同名衝突を重複定義として扱う
  - 異なる scope に属する同名定義は、名前一致だけでは重複定義診断にしない
  - 完了時には、class member、local `var`、異なる class scope の同名許可が単体テストで確認できる
  - _Depends: 2.2_
  - _Requirements: 1.2, 1.3, 1.4, 4.1, 4.3_
  - _Boundary: DefinitionCollector_

- [x] 2.4 複数重複、大小文字、ファイル位置の edge case を固定する
  - 3件以上の同名定義では、2件目以降の各診断が最初の定義を重複元として参照する
  - 名前比較は case-sensitive とし、大文字小文字だけが異なる名前は重複扱いにしない
  - 同じ module scope に寄与する入力で重複が見つかった場合、重複元と重複先の file 情報を失わない
  - 完了時には、複数重複、case-sensitive 比較、file 情報保持が単体テストで確認できる
  - _Depends: 2.3_
  - _Requirements: 1.5, 2.4, 2.5_
  - _Boundary: DefinitionCollector_

- [x] 3. Integration: semantic stage と check-only に接続する
- [x] 3.1 definition diagnostics の compile error 分類と stage ordering を維持する
  - import 成功後に重複定義診断がある場合、compile error として返る状態を維持する
  - 重複定義診断がある場合は name resolution へ進まない
  - syntax、file、import の earlier-stage failure がある場合は重複定義検証を実行しない
  - 完了時には、semantic analyzer テストで compile error、name resolution skip、earlier-stage 優先が確認できる
  - _Depends: 2.4_
  - _Requirements: 3.2, 3.5, 4.4_
  - _Boundary: SemanticAnalyzer_

- [x] 3.2 `kes build --check-only` で重複定義診断を出力できるようにする
  - check-only の結果に重複定義診断が既存の診断出力フローで渡ることを確認する
  - text 出力で診断 code、level、重複先位置、重複元位置、message を確認できる
  - JSON Lines 出力で重複先位置と重複元位置を機械可読に確認できる
  - 完了時には、CLI 統合テストで `KES2009` が compile error exit code と text / JSON Lines 出力に反映される
  - _Depends: 2.1, 3.1_
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_
  - _Boundary: BuildCheckOnlyCommand, DiagnosticSink_

- [x] 4. Validation: 要求範囲と回帰をテストで固定する
- [x] 4.1 duplicate / shadowing / name resolution の境界回帰を確認する
  - shadowing 診断は `KES2014` のまま維持し、この仕様の関連位置追加対象にしない
  - 型検査、overload resolution、式評価、runtime 実行なしで重複定義診断が出ることを確認する
  - 重複がない script では duplicate definition diagnostics が出ないことを確認する
  - 完了時には、既存 shadowing、import/name resolution、definition collector tests が新しい重複診断追加後も成功する
  - _Depends: 3.2_
  - _Requirements: 4.1, 4.2, 4.3, 4.4_
  - _Boundary: Regression Validation_

- [x] 4.2 全体テストと差分品質チェックを実行する
  - 診断、意味解析、check-only 統合の関連テストを実行する
  - 必要な全体 `dotnet test` を実行し、既存 CLI / parser / semantic tests の回帰がないことを確認する
  - 差分に不要な scope 外変更や空白問題がないことを確認する
  - 完了時には、実行したテスト結果と差分品質チェック結果を implementation evidence として提示できる
  - _Depends: 4.1_
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.2, 3.3, 3.4, 3.5, 4.1, 4.2, 4.3, 4.4_
  - _Boundary: Regression Validation_
