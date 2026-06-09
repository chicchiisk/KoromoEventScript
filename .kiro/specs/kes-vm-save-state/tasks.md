# Implementation Plan

- [x] 1. Foundation: save state 用の live VM 基盤を整える
- [x] 1.1 session から保存可能な live 実行状態を参照できるようにする
  - `HeadlessVmSession` と `HeadlessVmExecutor` の間で、save/export に必要な実行位置、待機理由、pending payload、内部継続状態を保持できる形へ整理する。
  - 既存の `ResumeAdvance` / `ResumeSelection` フローを壊さず、save/export 追加後も live 実行テストが同じ進行結果を維持する状態にする。
  - 完了時には、session だけを見て save snapshot へ必要な live VM 情報を取り出せる。
  - _Requirements: 1.1, 1.3, 1.4, 4.2_

- [x] 1.2 save snapshot の最小 aggregate と共通識別子を定義する
  - `HeadlessVmSaveState` を中心に、schema version、execution position、variable snapshots、call frames、continuation を保持する土台を追加する。
  - runtime 観測状態や画面専用状態を aggregate に含めない責務境界を constructor/factory レベルで固定する。
  - 完了時には、VM 部分だけを独立に識別できる save snapshot の骨格が `Execution` 層に存在する。
  - _Requirements: 1.1, 2.1, 2.4, 3.2, 3.4_

- [x] 2. Core: serializable な save state 契約を組み立てる
- [x] 2.1 (P) 保存可能な VM 値表現を定義する
  - `number`、`bool`、`string`、`null`、配列、安定参照 ID を保存可能な値として正規化する。
  - process-local object や serializer 不能値を silent drop せず、除外または識別可能な扱いを定義する。
  - 完了時には、保存対象変数と pending payload を serializer 非依存の値として表せる。
  - _Requirements: 1.2, 3.1, 3.3_
  - _Boundary: HeadlessVmValueSnapshot_

- [x] 2.2 (P) 復元可能な continuation / wait state 契約を定義する
  - `Running`、`WaitingForAdvance`、`WaitingForSelection` を区別できる continuation snapshot を追加する。
  - 選択待ちや入力待ちで必要な再開情報を保持し、debug source map なしで待機理由を再判定できるようにする。
  - 完了時には、save された wait state だけを見て復元後の再開条件を判断できる。
  - _Requirements: 1.3, 1.4, 4.2, 4.4_
  - _Boundary: HeadlessVmContinuationState_

- [x] 2.3 save aggregate に変数・継続・call frame を束ねる
  - variable snapshot、call frame snapshot、continuation snapshot を `HeadlessVmSaveState` の 1 つの aggregate として統合する。
  - save snapshot が runtime 完全セーブデータの一部として埋め込める一方で、VM 部分だけを単独で扱える構造にする。
  - 完了時には、1 つの save aggregate で VM 再開に必要な保存対象が過不足なくまとまる。
  - _Depends: 2.1, 2.2_
  - _Requirements: 1.1, 1.2, 1.3, 2.2, 2.4, 3.2_

- [x] 3. Integration: export / restore を session へ接続する
- [x] 3.1 save state mapper で export / restore と妥当性検証を実装する
  - live session state から save snapshot を export し、`KlibDocument` と snapshot から restore する変換責務を追加する。
  - 無効 `scriptId`、無効 offset、不整合 payload、unsupported value を識別可能な fault または明示的拒否へ正規化する。
  - 完了時には、`.klib` の安定識別子と snapshot 自身の情報だけで restore 可否を判定できる。
  - _Depends: 1.1, 1.2, 2.1, 2.2, 2.3_
  - _Requirements: 2.3, 3.3, 4.1, 4.3, 4.4_

- [x] 3.2 session の公開 save API と restore ライフサイクルを追加する
  - `ExportSaveState()` と `Restore(...)` を `HeadlessVmSession` に追加し、既存 `Start` / `Resume*` と矛盾しないライフサイクルに整理する。
  - restore 後に `ResumeAdvance` / `ResumeSelection` が従来どおり動き、observation を authoritative source にしないことを固定する。
  - 完了時には、runtime またはテストコードが session API だけで save/export と restore を呼べる。
  - _Depends: 3.1_
  - _Requirements: 1.4, 2.2, 4.1, 4.2_

- [x] 4. Validation: save snapshot の受け入れ条件を固定する
- [x] 4.1 save snapshot の単体検証を追加する
  - value snapshot と continuation snapshot の正常系 / 異常系を unit test で固定する。
  - unsupported value、欠落 payload、不正 wait kind の扱いを観測可能な assertion で検証する。
  - 完了時には、保存契約の基本不変条件がテストだけで追跡できる。
  - _Requirements: 1.2, 1.3, 3.1, 3.3, 4.2, 4.3_

- [x] 4.2 build fixture から export / restore の統合テストを追加する
  - waiting-for-advance、waiting-for-selection、invalid snapshot の各シナリオで export / restore を検証する。
  - restore 後に同じ wait kind を維持し、選択再開や fault 判定が `.klib` 安定識別子ベースで成立することを確認する。
  - 完了時には、headless VM の save snapshot が runtime 画面状態に依存せず往復可能であることを `dotnet test` で確認できる。
  - _Depends: 3.2, 4.1_
  - _Requirements: 1.1, 1.4, 2.1, 2.3, 3.2, 4.1, 4.2, 4.3, 4.4_
