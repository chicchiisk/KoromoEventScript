# Implementation Plan

- [x] 1. 型検査 stage の土台を作る
- [x] 1.1 MVP 型モデルと型検査結果を定義する
  - `number`、`bool`、`string`、`Actor`、配列、`null`、`void`、unknown、unsupported を区別できる型表現を追加する。
  - `null` の参照型代入可否と、`void` の値利用不可を判定できるようにする。
  - 型検査 stage の成功/失敗、診断、終了コードを保持する result を追加する。
  - 完了時には型モデルの unit test で assignability の代表ケースが観測できる。
  - _Requirements: 1.1, 1.2, 1.3, 3.4, 5.6, 6.2_
  - _Boundary: KesType Model_

- [x] 1.2 型検査対象の構文ノードを追加する
  - 代入、`if`、`else if`、`while`、`for` を token list と source location 付きで表現する。
  - 既存の `var`、通常命令、LESS、`say` の syntax contract と互換性を保つ。
  - 完了時には parser test で新しい statement が AST として識別され、既存 command / LESS が従来どおり parse される。
  - _Requirements: 2.4, 4.5, 4.6_
  - _Boundary: Parser Type Syntax Contract_

- [x] 2. 呼び出しと型環境の基礎を実装する
- [x] 2.1 (P) MVP 組み込み命令のシグネチャ表を追加する
  - core、actor、scene、text、audio、state、system の MVP command / function を最小シグネチャとして登録する。
  - 位置引数、名前付き引数、省略可能引数、`array_len` の配列引数、`range` の `number[]` 戻り値を表現する。
  - 完了時には registry test で代表 built-in の引数型と戻り値型を取得できる。
  - _Requirements: 5.2, 5.3_
  - _Boundary: BuiltInSignatureRegistry_

- [x] 2.2 型注釈と定義から型環境を構築する
  - 変数、引数、関数戻り値、actor 定義から scope ごとの型情報を構築する。
  - import 済み module の visible な関数、変数、actor を呼び出し元の型検査で参照できるようにする。
  - unknown / unsupported type annotation を区別し、unsupported type は compile diagnostic にできる。
  - 完了時には user-defined function と imported actor / variable の型が型検査入力として解決できる。
  - _Requirements: 1.1, 1.2, 1.4, 1.5, 5.1_
  - _Boundary: TypeChecker_

- [x] 2.3 token list 式の MVP 型評価を実装する
  - リテラル、識別子、括弧、単項演算、二項演算、関数呼び出し、配列リテラル、配列要素アクセスの型を評価する。
  - 算術、比較、等価、論理演算の型規則を適用し、不一致を診断できるようにする。
  - unknown 型では派生診断を抑制し、前段エラーに由来する重複診断を増やさない。
  - 完了時には式 evaluator の unit test で演算結果型と不一致診断が確認できる。
  - _Requirements: 1.3, 3.1, 3.2, 3.3, 3.4, 3.5, 4.1, 4.2, 4.3, 4.4, 5.6_
  - _Boundary: TypeChecker_

- [x] 3. statement ごとの型検査を実装する
- [x] 3.1 変数定義と代入の型検査を実装する
  - 型注釈付き initializer、型注釈なし initializer 推論、既知型 variable への代入を検査する。
  - 不一致診断には expected type と actual type を含める。
  - 完了時には `var name: string = 1` と `score = "bad"` が compile diagnostic になる。
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_
  - _Boundary: TypeChecker_

- [x] 3.2 制御構文と配列反復の型検査を実装する
  - `if`、`else if`、`while` の条件式が `bool` であることを検査する。
  - `for` の右辺が配列などの supported iterable であることを検査し、loop variable を要素型として扱う。
  - 完了時には非 `bool` 条件と非配列 `for` が compile diagnostic になり、配列の要素型が loop body で使える。
  - _Requirements: 4.5, 4.6_
  - _Boundary: TypeChecker_

- [x] 3.3 命令引数と関数呼び出しの型検査を実装する
  - user-defined function、built-in command/function、通常命令、式中関数呼び出しの引数を signature と照合する。
  - named arguments と optional arguments を signature に従って検査する。
  - 完了時には `print 1`、`show "Noa" 0`、`number_to_string true` が compile diagnostic になる。
  - _Requirements: 5.1, 5.2, 5.5, 5.6_
  - _Boundary: TypeChecker, BuiltInSignatureRegistry_

- [x] 3.4 LESS と `say` の型検査を実装する
  - LESS の shared arguments と item arguments を同一 command signature に対して検査する。
  - nested LESS を再帰的に検査する。
  - `say <actor_identifier>:` の話者が `Actor` 値であることを検査する。
  - 完了時には LESS 内の型不一致と非 actor 話者が参照位置付き compile diagnostic になる。
  - _Requirements: 5.3, 5.4, 5.5_
  - _Boundary: TypeChecker_

- [x] 4. semantic pipeline と CLI に統合する
- [x] 4.1 型検査 stage を semantic analyzer に接続する
  - 名前解決成功後にだけ型検査を実行する。
  - 型診断を semantic analysis result の diagnostics と exit code に反映する。
  - 完了時には型不一致のみの script が compile error になり、名前解決失敗時には型診断が出ない。
  - _Depends: 2.2, 2.3, 3.1, 3.2, 3.3, 3.4_
  - _Requirements: 6.1, 6.2, 6.5, 6.6_
  - _Boundary: SemanticAnalyzer Integration_

- [x] 4.2 CLI check-only の診断出力を確認する
  - 既存 text output と JSON Lines output の schema を変更せずに型診断を出力する。
  - `kes build --check-only` の success / compile error exit code を型検査結果に合わせる。
  - 完了時には CLI integration test で file、line、column、level、code、message が text と JSON Lines の両方で確認できる。
  - _Depends: 4.1_
  - _Requirements: 6.1, 6.2, 6.3, 6.4_
  - _Boundary: CLI Diagnostic Flow_

- [x] 5. 回帰テストと fixture を整備する
- [x] 5.1 型検査の成功・失敗 fixture を追加する
  - 成功系 project で MVP 型が正しく通る代表ケースを用意する。
  - 失敗系 project で変数、式、配列、制御構文、命令引数の型不一致を観測できるようにする。
  - 完了時には fixture を使った check-only test が成功系 exit code 0 と失敗系 compile error を確認できる。
  - _Depends: 4.2_
  - _Requirements: 2.1, 2.3, 3.2, 4.2, 4.5, 5.5, 6.1, 6.2_
  - _Boundary: CLI Diagnostic Flow_

- [x] 5.2 stage ordering と既存診断の回帰を固定する
  - syntax、import、definition、undefined reference の前段 failure が型診断より優先されることを確認する。
  - 既存の import/name/definition tests が型検査追加後も同じ診断順序を保つことを確認する。
  - 完了時には semantic analyzer test で前段 failure 時に type checking result が成功扱いまたは未実行相当であることが確認できる。
  - _Depends: 4.1_
  - _Requirements: 6.5, 6.6_
  - _Boundary: SemanticAnalyzer Integration_

- [x] 5.3 全体テストを実行し、タスク完了状態を確認する
  - `dotnet test KoromoEventScript.slnx` を実行し、既存テストと追加テストが通ることを確認する。
  - 失敗が出た場合は型検査 scope 内の原因に限定して修正する。
  - 完了時には全テスト成功と、`kes-minimal-type-checking` の全要求 ID が実装タスクでカバーされていることが確認できる。
  - _Depends: 5.1, 5.2_
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.2, 3.3, 3.4, 3.5, 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 6.1, 6.2, 6.3, 6.4, 6.5, 6.6_
  - _Boundary: TypeChecker, SemanticAnalyzer Integration, CLI Diagnostic Flow_
