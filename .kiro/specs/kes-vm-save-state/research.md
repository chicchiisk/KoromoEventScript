# Research & Design Decisions

## Summary

- **Feature**: `kes-vm-save-state`
- **Discovery Scope**: Extension
- **Key Findings**:
  - 既存 `HeadlessVmSession` / `HeadlessVmState` は待機理由と観測結果は保持するが、変数状態、call/continuation state、評価スタックをまだ保持していない。
  - `.klib` 仕様と Windows runtime 仕様は save/load の安定参照として `scriptId`、実行位置、`variableState`、`continuationState` をすでに要求している。
  - serializer、保存先、完全な runtime セーブデータは別責務なので、この spec では「保存可能な VM snapshot 契約」に責務を限定するのが最小である。

## Research Log

### 既存 headless VM 実装で何が足りないか

- **Context**: save 対象の設計を既存 `Execution` 層の延長でまとめられるか確認するため。
- **Sources Consulted**:
  - `source/cli/KoromoEventScript.Cli/Execution/HeadlessVmState.cs`
  - `source/cli/KoromoEventScript.Cli/Execution/HeadlessVmSession.cs`
  - `source/cli/KoromoEventScript.Cli/Execution/HeadlessVmExecutor.cs`
- **Findings**:
  - `HeadlessVmState` は `Kind`、`StopReason`、`ScriptId`、`InstructionOffset`、pending choices、fault だけを持つ。
  - `HeadlessVmExecutor` の評価スタックはローカル変数であり、待機境界を越えて永続化されない。
  - 変数、call frame、continuation payload を保存対象として取り出す公開契約がまだ存在しない。
- **Implications**:
  - save/load は `HeadlessVmState` の単純保存では足りず、保存専用 snapshot モデルが必要である。
  - session の live state と persistence state を分離しないと、runtime 表示状態やテスト観測状態が save 契約へ漏れやすい。

### 既存公開仕様は save/load に何を期待しているか

- **Context**: 新しい save state 契約が既存の `.klib` / runtime 仕様と矛盾しないことを確認するため。
- **Sources Consulted**:
  - `docs/spec/k-intermediate-representation-spec.md`
  - `docs/spec/windows-runtime-spec.md`
  - `.kiro/specs/kes-k-intermediate-representation-spec/design.md`
- **Findings**:
  - `.klib` 仕様は save/load 境界として `scriptId`、`bytecodeOffset`、`variableState`、`continuationState` を定義している。
  - `.klib` 仕様は「一時値（オペランドスタック上）は原則として保存対象に含めない」としている。
  - Windows runtime 仕様は完全セーブデータに VM 状態、制御状態、画面状態、音声状態、既読情報、メタ情報を含める。
- **Implications**:
  - 本 spec は Windows runtime の完全セーブデータではなく、その一部である VM snapshot だけを定義すべきである。
  - 保存対象の「スタック」は raw operand stack 全量より、call/continuation を復元可能にする最小 snapshot として設計するのが既存仕様と整合する。

### テストと実装の配置先

- **Context**: 実装タスクへ落とす際の物理境界を確認するため。
- **Sources Consulted**:
  - `tests/KoromoEventScript.Cli.Tests/Execution/HeadlessVmExecutionTests.cs`
  - `tests/KoromoEventScript.Cli.Tests/Execution/HeadlessVmTestHelper.cs`
  - `docs/testing-strategy.md`
- **Findings**:
  - `Execution` 名前空間と `tests/.../Execution` には headless VM の責務がすでに集約されている。
  - テスト戦略は runtime state test を「描画に依存しないランタイム挙動」の層として位置付けている。
  - 既存 helper は `KlibDocument` 生成済み session を再利用できるため、save snapshot の export/import 検証も同じテスト群に置ける。
- **Implications**:
  - save state 実装は `Execution/` 配下へ閉じ、テストは `Execution/` 配下へ追加するのが自然である。
  - 新しい外部依存や永続化ライブラリ導入は不要である。

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| `HeadlessVmState` に保存対象を直接追加 | live state をそのまま save 契約にする | 実装箇所が少ない | 観測状態や runtime 向け待機情報が混ざりやすい | 不採用 |
| 保存専用 snapshot record 群を導入 | session から export/import する persistence 向け契約 | 責務分離が明確、serializer 非依存、テストしやすい | 変換責務が増える | 採用 |
| runtime 完全セーブデータまで同時定義 | VM と UI/音声をまとめて定義 | 将来の実装像が見えやすい | Issue #29 の範囲を超える | 不採用 |

## Design Decisions

### Decision: live session state と save snapshot を分離する

- **Context**: `HeadlessVmState` は待機理由や pending choices を持つが、保存契約としては画面状態と混ざりやすい。
- **Alternatives Considered**:
  1. `HeadlessVmState` をそのまま保存対象にする
  2. 保存専用 `HeadlessVmSaveState` aggregate を新設する
- **Selected Approach**: `HeadlessVmSaveState` を aggregate とし、その配下に execution position、variable snapshots、call frames、continuation snapshot、schema version を持たせる。
- **Rationale**: save/load 契約を runtime 観測状態から切り離せ、serialize 可能性も明示しやすい。
- **Trade-offs**: 変換責務は増えるが、境界の明快さと将来の serializer 選定自由度が上回る。
- **Follow-up**: 実装時に session から export/import する API と不変条件をテストで固定する。

### Decision: raw operand stack ではなく continuation 中心で保存する

- **Context**: 要件には「スタック」が含まれる一方、`.klib` 仕様は operand stack を原則 save 対象外としている。
- **Alternatives Considered**:
  1. 常に operand stack 全量を保存する
  2. call stack と continuation snapshot を主契約にし、必要時だけ serializable operand value を補助保存する
- **Selected Approach**: call frame / continuation を基本契約とし、復元不能になる場合だけ serializable operand values を `ContinuationState` の一部として保持できる形にする。
- **Rationale**: 既存 `.klib` save/load 境界と整合しつつ、`SELECT` や入力待ちの復元も表現できる。
- **Trade-offs**: continuation の種類ごとのモデル化が必要だが、保存対象が過剰に広がらない。
- **Follow-up**: 実装時に wait kind ごとの payload 最小集合を洗い出す。

### Decision: serializer 実装と保存先はこの spec から外す

- **Context**: Issue #29 は保存対象状態の定義が主目的であり、永続化方式は要求されていない。
- **Alternatives Considered**:
  1. JSON など具体形式まで固定する
  2. save state contract だけを定義し、serializer は別 Issue に委ねる
- **Selected Approach**: contract は record/value-object と schema version だけ定義し、JSON/XML/binary などの永続化方式は後続仕様へ委譲する。
- **Rationale**: WHAT と HOW を分離でき、runtime ごとの保存戦略差分も吸収しやすい。
- **Trade-offs**: 直ちに保存ファイル互換性は固定されない。
- **Follow-up**: serializer 導入 Issue で schema versioning と互換性方針を再検証する。

## Risks & Mitigations

- save state が runtime 画面状態まで抱え込むリスク — `Boundary Commitments` と data model で VM snapshot 専用であることを明記する。
- continuation payload が不足して復元不能になるリスク — wait kind ごとに pre/post conditions を契約化し、integration test で restore 後の wait reason を固定する。
- 将来の opcode 拡張で保存対象が不足するリスク — `Revalidation Triggers` に opcode / call state / dependency 変更を含める。

## References

- `docs/spec/k-intermediate-representation-spec.md` — `.klib` の save/load 境界と安定識別子の根拠
- `docs/spec/windows-runtime-spec.md` — 完全セーブデータと VM 状態の境界
- `source/cli/KoromoEventScript.Cli/Execution/HeadlessVmState.cs` — 現行 live state 契約
- `source/cli/KoromoEventScript.Cli/Execution/HeadlessVmSession.cs` — session の開始・再開 API
- `docs/testing-strategy.md` — runtime state test の位置づけ
