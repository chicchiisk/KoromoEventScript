# Implementation Plan

- [ ] 1. Foundation: full opcode 対応の実行基盤を整える
- [x] 1.1 型付き runtime value と live runtime state を導入する
  - `object?` 直積み前提を置き換え、数値、bool、string、null、reference を表せる runtime value を追加する。
  - operand stack、variable values、call frames、object reference を `Execution` 層の live state として一元化する。
  - 実行中の値取得や stack underflow 検出を helper API へ寄せ、後続 opcode 実装が同じ fault 条件を使えるようにする。
  - 実装後は executor と save/load mapper が同じ runtime state 型を参照できる状態になる。
  - _Requirements: 1.2, 2.1, 2.3, 2.4_

- [x] 1.2 synthetic document と実行検証 helper を拡張する
  - compiler 駆動 fixture に加えて、任意 opcode 列を組み立てられる synthetic `KlibDocument` helper を整備する。
  - array、field、method、classRef を含む最小定数・命令データを安全に作れる補助 API を追加する。
  - 実装後は dormant opcode の happy path と failure path をテストから直接組み立てられる。
  - _Requirements: 5.4_

- [ ] 2. Core: opcode 実行と headless 効果を実装する
- [x] 2.1 stack、変数、演算、制御フロー opcode を executor で解釈する
  - `PUSH_*`、`POP`、`DUP`、`LOAD_VAR`、`STORE_VAR`、`DEF_VAR`、算術、比較、論理、`JUMP_FALSE` を既存 executor に追加する。
  - offset 規約に従って `JUMP`、`JUMP_FALSE`、`LABEL`、`END` を評価し、無効 offset や不正オペランドを fault に正規化する。
  - 実装後は compiler が現在 emit している式評価と分岐 opcode 群で unsupported fault が発生しなくなる。
  - _Requirements: 1.1, 1.2, 1.3, 2.1, 2.2, 2.3, 2.4, 3.1, 3.4, 5.3_

- [x] 2.2 (P) built-in call、syscall、method call の dispatcher を実装する
  - `CALL` / `CALL_VOID`、`SYSCALL` / `SYSCALL_VOID`、`CALL_METHOD` / `CALL_METHOD_VOID` の引数 pop、戻り値 push、fault 条件を dispatcher に集約する。
  - `scenario.say`、`scenario.nar`、runtime 連携命令を headless 観測イベントまたは継続可能な no-op へ正規化する。
  - built-in signature と戻り値利用条件に反した呼び出しを識別可能な fault にする。
  - 実装後は headless VM が一般 command と syscall を通して停止・継続・戻り値受け渡しを行える。
  - _Requirements: 1.1, 1.2, 4.1, 4.4, 5.1, 5.2_
  - _Boundary: HeadlessVmCallableDispatcher_

- [x] 2.3 (P) 配列とクラス参照を扱う object store を実装する
  - `ARRAY_NEW`、`ARRAY_GET`、`ARRAY_SET` を reference ベースの object store で処理する。
  - `NEW`、`GET_FIELD`、`SET_FIELD`、`DISPOSE`、method receiver 解決に必要な class instance storage を追加する。
  - 範囲外 index、未知 object ID、未定義 field/method を fault に正規化する。
  - 実装後は array/class opcode が raw object なしで継続実行でき、failure path も再現できる。
  - _Requirements: 1.1, 1.2, 4.2, 4.3, 4.4_
  - _Boundary: HeadlessVmObjectStore_

- [ ] 3. Integration: session、save/load、観測状態をつなぐ
- [x] 3.1 session と executor を expanded runtime state に接続する
  - session が runtime state、dispatcher、object store を束ねて開始・再開・完了・fault の既存契約を維持するよう更新する。
  - `SELECT` の待機理由、pending choices、`ResumeAdvance`、`ResumeSelection` を full opcode 実行と同時に維持する。
  - 実装後は `Start` / `Resume*` の公開 API 形状を変えずに full opcode 実行へ移行できる。
  - _Depends: 1.1, 2.1, 2.2, 2.3_
  - _Requirements: 1.4, 3.2, 3.3, 5.3_

- [x] 3.2 save/load mapper を expanded runtime state に追随させる
  - typed runtime value、variable state、call frame、object reference を snapshot 契約へ写像する。
  - restore 後の state が waiting/completed/running を再構築し、invalid snapshot は restore fault になるよう更新する。
  - 実装後は full opcode 対応後も save/export と restore round-trip が継続して成立する。
  - _Depends: 1.1, 2.3, 3.1_
  - _Requirements: 1.4, 2.3, 3.3_

- [ ] 4. Validation: full opcode coverage を回帰条件にする
- [x] 4.1 compiler 駆動の headless 実行テストを拡張する
  - `if`、`while`、`for`、組み込み command、式評価、変数更新、`say` / `nar` / `select` を含む scenario を追加する。
  - `SupportedOpCodes` ベースの unsupported 想定を廃止し、言語機能が最後まで進行することを期待値へ置き換える。
  - 実装後は compiler が emit する主要 opcode 群について headless VM 完了または待機が CI で確認できる。
  - _Depends: 1.2, 3.1_
  - _Requirements: 2.1, 2.2, 2.3, 3.1, 3.2, 3.3, 5.3, 5.4_

- [x] 4.2 synthetic opcode テストと save/load 回帰を追加する
  - compiler 未使用の dormant opcode、stack underflow、invalid field/method/index、unknown opcode を direct document で固定する。
  - expanded runtime state を含む save/load round-trip を更新し、array/class/call frame を含む復元パスを確認する。
  - 実装後は full opcode 対応の failure path と save/load 整合が自動テストで識別できる。
  - _Depends: 1.2, 2.2, 2.3, 3.2_
  - _Requirements: 1.3, 2.4, 4.2, 4.3, 4.4, 5.3_
