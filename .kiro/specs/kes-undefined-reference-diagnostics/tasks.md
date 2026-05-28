# Implementation Plan

- [x] 1. 参照名の位置情報を構文木に保持する
  - command、LESS、`say` の参照名 token 位置を構文木から参照できるようにする。
  - 既存の構文解析結果と手動構築テストが壊れない形で default location を扱えるようにする。
  - parser のテストで command 名、LESS 名、`say` 話者名の line / column が期待値として観測できる。
  - _Requirements: 2.5, 4.5_
  - _Boundary: Syntax Location Contract_

- [x] 2. 名前解決入力を定義種別と scope を参照できる形に切り替える
  - name resolution が定義収集済みの scope、definition kind、import graph を同時に参照できるようにする。
  - import 失敗、重複定義、シャドーイングがある場合は、既存どおり参照解決へ進まないことを維持する。
  - 既存の import 衝突とあいまい参照のテストが同じ診断コードと順序で通る。
  - _Requirements: 1.2, 1.3, 2.3, 2.4, 4.4, 6.1, 6.2, 6.3, 6.4_
  - _Boundary: NameResolver, SemanticAnalyzer Integration_

- [x] 3. 変数参照の未定義診断を実装する
  - 変数参照を現在 scope、親 scope、module scope、reachable import の順で解決する。
  - 不可視の同名定義は未定義として扱い、名前比較は case-sensitive にする。
  - 未定義変数の診断が参照 token の file / line / column と参照名を含んで観測できる。
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 6.5_
  - _Boundary: NameResolver_

- [x] 4. actor 参照の未定義診断を実装する
  - `say` 話者位置と actor として扱う command / expression 引数を actor 参照として分類する。
  - visible な actor 定義だけを解決成功とし、unreachable file の actor は未定義として扱う。
  - 未定義 actor の診断が actor identifier token の位置を指して観測できる。
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 6.5_
  - _Boundary: NameResolver_

- [x] 5. label 参照の未定義診断を同一 document の jump target に限定する
  - `jump` と `case` の tag 参照を同一 document 内の `label`、tagged `say`、tagged `nar` に照合する。
  - imported document の tag は local tag 参照の解決対象にしない。
  - 未定義 label の診断が `jump` / `case` の tag token 位置を指して観測できる。
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 6.5_
  - _Boundary: NameResolver_

- [x] 6. 関数参照の未定義診断を実装する
  - 通常命令、LESS 呼び出し、式中関数呼び出しを関数参照として分類する。
  - visible な module / imported / built-in callable は解決成功とし、それ以外は未定義として扱う。
  - 未定義関数の診断が command / LESS / expression の関数名 token 位置を指して観測できる。
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 6.5_
  - _Boundary: NameResolver_

- [x] 7. semantic validation と CLI check-only の統合を検証する
  - `kes build --check-only` が未定義参照を compile error exit code として返すことを確認する。
  - text 出力と JSON Lines 出力に file、line、column、level、code、message が含まれることを確認する。
  - 複数の未定義参照診断が semantic validation の deterministic ordering で出力されることを確認する。
  - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_
  - _Boundary: CLI Diagnostic Flow, SemanticAnalyzer Integration_

- [x] 8. 回帰テストと仕様境界の最終検証を行う
  - import failure、syntax failure、duplicate definition、shadowing が未定義参照より前に優先される既存動作を確認する。
  - 型検査、引数数検査、runtime 実行なしで未定義参照診断が完結することをテストで固定する。
  - 対象テストスイートが成功し、未定義参照の正常系と異常系が全要件を覆っていることが観測できる。
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.2, 3.3, 3.4, 3.5, 4.1, 4.2, 4.3, 4.4, 4.5, 5.1, 5.2, 5.3, 5.4, 5.5, 6.1, 6.2, 6.3, 6.4, 6.5_
  - _Boundary: NameResolver, SemanticAnalyzer Integration, CLI Diagnostic Flow_
