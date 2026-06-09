# Research & Design Decisions

## Summary

- **Feature**: `kes-headless-vm-full-opcodes`
- **Discovery Scope**: Extension
- **Key Findings**:
  - 現行 `HeadlessVmExecutor` は `PushConst`、`PushNull`、`Jump`、`Label`、`Select`、`End`、`SysCallVoid` だけを処理しており、opcode の大半が unsupported fault になる。
  - `KlibCompiler` はすでに変数、算術、比較、論理、`JumpFalse`、`Call`、`CallVoid`、`ArrayNew`、`ArrayGet` などを生成しており、headless VM 側の未実装が言語機能の主要なボトルネックになっている。
  - save/load 用の `HeadlessVmRuntimeState` は `object?` ベースで `HeadlessVmSaveStateMapper` 内部に閉じており、全 opcode 対応の実行状態としては責務と型表現が不足している。

## Research Log

### 現行 headless VM 実装の不足範囲

- **Context**: どこまで既存実装を拡張すれば全 opcode 対応に届くかを見積もるため。
- **Sources Consulted**:
  - `source/cli/KoromoEventScript.Cli/Execution/HeadlessVmExecutor.cs`
  - `tests/KoromoEventScript.Cli.Tests/Execution/HeadlessVmExecutionTests.cs`
  - `source/cli/KoromoEventScript.Cli/Compilation/KlibModels.cs`
- **Findings**:
  - `HeadlessVmExecutor` の switch は 7 opcode しか扱っていない。
  - 既存テストは `SupportedOpCodes` 配列を基準に unsupported fault を期待しており、現状の契約自体が「未実装前提」になっている。
  - `KlibOpCode` 列挙にはスタック、変数、演算、制御、呼び出し、配列、クラスまで一式が定義済みである。
- **Implications**:
  - 今回の設計は単なる opcode 追加ではなく、unsupported 前提のテスト戦略と runtime state 構造を同時に更新する必要がある。
  - `HeadlessVmExecutor` の巨大化を抑えるため、値、呼び出し、オブジェクト操作の補助責務を分離する設計が必要である。

### compiler が実際に生成している opcode 群

- **Context**: 「仕様に定義されている opcode」と「現在の compiler が emit する opcode」の差を把握し、テスト境界を決めるため。
- **Sources Consulted**:
  - `source/cli/KoromoEventScript.Cli/Compilation/KlibCompiler.cs`
  - `source/cli/KoromoEventScript.Cli/Semantics/BuiltInSignatureRegistry.cs`
  - `docs/spec/k-intermediate-representation-spec.md`
- **Findings**:
  - compiler は `DefVar`、`StoreVar`、`LoadVar`、`PushInt`、`PushTrue`、`PushFalse`、`Add`、`Sub`、`Mul`、`Div`、`Neg`、`Eq`、`Neq`、`Lt`、`Le`、`Gt`、`Ge`、`And`、`Or`、`Not`、`JumpFalse`、`Call`、`CallVoid`、`ArrayNew`、`ArrayGet` をすでに emit している。
  - `SysCallVoid` は `scenario.say` と `scenario.nar` に使われ、一般コマンドは `Call` / `CallVoid` に lower される。
  - `ArraySet`、`New`、`GetField`、`SetField`、`CallMethod*`、`Dispose` は opcode 定義済みだが、現時点では compiler から直接 emit されていない。
- **Implications**:
  - テストは compiler 生成ドキュメントで追える opcode と、synthetic `KlibDocument` で直接固定すべき opcode を分ける必要がある。
  - 設計上は dormant opcode も first-class に扱い、将来 compiler が emit し始めても executor を再設計しない形にするべきである。

### save/load 仕様との結合点

- **Context**: 全 opcode 対応で runtime state を広げる際に、直近で追加した save/load 契約と衝突しないかを確認するため。
- **Sources Consulted**:
  - `source/cli/KoromoEventScript.Cli/Execution/HeadlessVmSaveStateMapper.cs`
  - `source/cli/KoromoEventScript.Cli/Execution/HeadlessVmSaveState.cs`
  - `.kiro/specs/kes-vm-save-state/design.md`
- **Findings**:
  - `HeadlessVmRuntimeState` は現在 `VariableValues`、`CallFrames`、`OperandStack` だけを持つ内部型である。
  - save state は `HeadlessVmValueSnapshot` を持つが、live runtime は依然として `object?` を直接扱っている。
  - save/load 側は call frame と変数状態の保存責務を先行定義しており、runtime state の authoritative な所有場所を `Execution` 層に寄せる前提になっている。
- **Implications**:
  - 全 opcode 対応の設計では `HeadlessVmRuntimeState` を `SaveStateMapper` から独立させ、save/load と実行系が同じ runtime state 契約を共有する必要がある。
  - 値表現を typed runtime value に寄せると、save/load の `HeadlessVmValueSnapshot` への変換責務も明確になる。

### 既存仕様との責務境界

- **Context**: headless VM 完成仕様が `.klib` 仕様や runtime 仕様を食い破らないようにするため。
- **Sources Consulted**:
  - `.kiro/specs/kes-headless-vm-execution/design.md`
  - `docs/spec/k-intermediate-representation-spec.md`
  - `docs/spec/windows-runtime-spec.md`
- **Findings**:
  - `.klib` は opcode の意味、相対 offset、値表現、syscall lowering を所有している。
  - 既存 headless VM 仕様は「最小集合の runtime 非依存実行」にとどまり、runtime 固有演出の完全再現は out of scope としている。
  - Windows runtime 仕様は実描画・音声・UI を所有し、VM は命令進行と runtime 効果の要求を渡す立場である。
- **Implications**:
  - この設計は opcode 実行完了までを headless VM が所有し、描画や音声を直接実装しない。
  - runtime 連携命令は「headless で継続可能なイベントまたは no-op に正規化する」境界で統一する。

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| `HeadlessVmExecutor` の巨大 switch を拡張し続ける | 既存 executor にすべての opcode 実装を直書きする | ファイル追加が少ない | 値操作、呼び出し、オブジェクト状態、save/load 連携が密結合化する | 小規模修正には向くが今回の範囲では不採用 |
| stateful interpreter core + 補助 dispatcher | executor は進行制御に専念し、値/呼び出し/オブジェクト責務を補助コンポーネントへ分離する | 境界が明確で task 分解しやすい | ファイル追加は増える | 今回の設計に採用 |
| runtime adapter 先行型 | Windows runtime 相当の API を先に作り、その上で headless を実装する | 将来 runtime との契約を揃えやすい | headless 実行の独立性が落ちる | 現時点では過剰 |

## Design Decisions

### Decision: live runtime value を `object?` から型付き値モデルへ寄せる

- **Context**: 算術、比較、配列、クラス、save/load の整合を `object?` のまま増やすと、opcode ごとの型判定と fault 条件が散らばる。
- **Alternatives Considered**:
  1. `object?` を維持し、executor 内で都度型判定する
  2. `HeadlessVmRuntimeValue` と heap object を持つ型付き runtime 値へ寄せる
- **Selected Approach**: 実行中の値は型付き runtime value とし、配列・クラスインスタンスは参照 ID 経由で object store が所有する。
- **Rationale**: opcode 実装、save/load 変換、fault 生成の責務を安定化できる。
- **Trade-offs**: 既存 save/load 実装も追随が必要になる。
- **Follow-up**: `HeadlessVmValueSnapshot` との相互変換を一か所へ寄せる。

### Decision: executor は進行制御を担当し、呼び出しとオブジェクト操作を補助コンポーネントへ分離する

- **Context**: 全 opcode を単一 switch に押し込むと、`CALL*`、`SYSCALL*`、`ARRAY_*`、`NEW`、`CALL_METHOD*` の責務が見えなくなる。
- **Alternatives Considered**:
  1. すべて `HeadlessVmExecutor` に実装する
  2. `HeadlessVmCallableDispatcher`、`HeadlessVmObjectStore`、`HeadlessVmRuntimeState` に責務を分離する
- **Selected Approach**: executor は fetch/decode/branch/wait/fault の orchestration を持ち、複雑な opcode 群は補助コンポーネントへ委譲する。
- **Rationale**: task ごとの境界が明確になり、save/load や将来 runtime adapter とも整合しやすい。
- **Trade-offs**: 初期のファイル分割は増える。
- **Follow-up**: speculative abstraction を避けるため、委譲先は opcode 群単位の最小数にとどめる。

### Decision: テストは compiler 駆動と synthetic document の二層で固定する

- **Context**: compiler がまだ emit しない opcode まで headless VM 完成仕様が要求している。
- **Alternatives Considered**:
  1. compiler 生成シナリオだけで検証する
  2. compiler 生成シナリオに加え synthetic `KlibDocument` で dormant opcode を直接検証する
- **Selected Approach**: 言語機能の回帰は compiler 駆動テスト、dormant opcode と fault 条件は synthetic document テストで固定する。
- **Rationale**: 仕様 coverage と将来互換性を両立できる。
- **Trade-offs**: helper とテストデータのメンテナンスが増える。
- **Follow-up**: `SupportedOpCodes` ベースの unsupported test は opcode 別期待値テストへ置き換える。

## Risks & Mitigations

- runtime value モデルの変更で save/load テストが壊れる可能性がある — `HeadlessVmSaveStateTests` を同一変更セットで更新し、snapshot round-trip を回帰条件に含める。
- dormant opcode を no-op 的に雑実装すると将来 compiler emit 時に仕様逸脱が潜む — synthetic document テストで opcode ごとの最小意味を固定する。
- runtime 連携命令を全部観測イベント化すると責務が広がりすぎる — headless 継続に必要な最小イベントだけを持ち、描画・音声は no-op または state 更新に限定する。

## References

- `docs/spec/k-intermediate-representation-spec.md` — opcode と実行意味の正規仕様
- `.kiro/specs/kes-headless-vm-execution/design.md` — 現行 headless VM の最小設計
- `.kiro/specs/kes-vm-save-state/design.md` — runtime state/save state の境界
- `source/cli/KoromoEventScript.Cli/Execution/HeadlessVmExecutor.cs` — 現行 executor 実装
- `source/cli/KoromoEventScript.Cli/Compilation/KlibCompiler.cs` — compiler が emit している opcode 群
- `source/cli/KoromoEventScript.Cli/Semantics/BuiltInSignatureRegistry.cs` — built-in callable の公開集合
