# Research & Design Decisions

## Summary

- **Feature**: `kes-headless-vm-execution`
- **Discovery Scope**: Extension
- **Key Findings**:
  - `.klib` は既存の公開仕様で VM/runtime 共通の実行契約として定義済みであり、headless 実行はその consumer として追加するのが自然である。
  - `docs/testing-strategy.md` は UI テストを headless 分離後に追加する方針を明示しており、今回の仕様は VM test 基盤の前提になる。
  - 現行コードベースには compiler と `.klib` artifact writer は存在するが、VM 実行責務はまだ独立した層として現れていない。

## Research Log

### `.klib` 実行契約の確認

- **Context**: headless VM が新しいファイル形式を持つべきか、既存 `.klib` を直接消費すべきかを判断するため。
- **Sources Consulted**:
  - `docs/spec/k-intermediate-representation-spec.md`
  - `source/cli/KoromoEventScript.Cli/Compilation/KlibModels.cs`
- **Findings**:
  - `.klib` は CLI が生成し、VM と runtime が参照する正規の中間表現として定義されている。
  - 命令セットには `LABEL`、`JUMP`、`SELECT`、`END` が含まれ、`SELECT` は待機と再開を伴う。
  - `KlibDocument` は instruction、label、constant、debug mapping を保持しており、headless 実行の入力モデルとして十分である。
- **Implications**:
  - 本仕様は compiler や IR 形式を拡張せず、`KlibDocument` を消費する実行レイヤーの追加に集中できる。
  - source mapping は失敗時診断や観測状態の補助情報として再利用できる。

### runtime 依存境界の確認

- **Context**: headless VM が UI 表示や Windows runtime の責務まで抱え込まないようにするため。
- **Sources Consulted**:
  - `docs/spec/windows-runtime-spec.md`
  - `docs/spec/kes-language-stl-spec.md`
- **Findings**:
  - Windows runtime は `say`、`nar`、`select` などを受けて UI 表示、クリック待ち、選択受付を担当する。
  - language / STL 仕様は `select` を runtime 連携上の構文として扱うが、描画方式までは固定していない。
- **Implications**:
  - headless VM は「何を表示すべきか」「なぜ停止したか」までを保持し、実際の描画や入力デバイス処理は runtime adapter に残す。
  - 後続の Windows / Unity / Unreal runtime は、headless VM の停止状態を消費する adapter として接続できる。

### テスト基盤との整合

- **Context**: 要件 4 の CI 実行可能性と、既存テストプロジェクトへの収まりを確認するため。
- **Sources Consulted**:
  - `docs/testing-strategy.md`
  - `tests/KoromoEventScript.Cli.Tests/Compilation/KlibCompilerTests.cs`
  - `tests/KoromoEventScript.Cli.Tests/Commands/BuildCommandTests.cs`
- **Findings**:
  - テスト戦略は `VM test` を独立分類として定義し、描画結果ではなく状態検証を前提にしている。
  - 既存 NUnit テストは `TemporaryProject` と build command を使って `.klib` / `.klibtxt` 生成までを既に自動化している。
- **Implications**:
  - headless VM test は既存 CLI test project に追加し、fixture は build 済み `KlibDocument` または build command 経由で取得できる。
  - 新しい UI テスト基盤を先に作る必要はない。

### 配置候補の確認

- **Context**: 新しい実行責務をどのディレクトリに置くかを決めるため。
- **Sources Consulted**:
  - `source/cli/KoromoEventScript.Cli/Compilation/`
  - `source/cli/KoromoEventScript.Cli/Commands/Build/BuildCommand.cs`
  - `tests/KoromoEventScript.Cli.Tests/`
- **Findings**:
  - 現在の `Compilation` 名前空間は build-time 責務に集中している。
  - 実行時責務を追加するなら `Execution` のような別ディレクトリで境界を分けた方が明快である。
- **Implications**:
  - `Compilation` から `Execution` への依存は許容し、逆方向依存は避ける。
  - テストも `Execution/` 配下でまとめ、golden test と VM test の責務を分離する。

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| Compiler に実行機能を直結 | `KlibCompiler` 周辺に実行 API を追加する | 追加ファイル数が少ない | build-time と run-time の責務が混ざる | 境界が不明瞭になるため不採用 |
| Headless VM core + ports | `KlibDocument` を入力に実行 core を作り、入力待ちや観測結果を port 化する | runtime 非依存、テスト容易、将来 adapter を足しやすい | 状態モデル設計が必要 | 要件 1-4 と最も整合 |
| runtime 先行実装の流用 | Windows runtime の都合に合わせて VM 実装を作る | 将来 runtime 実装と近い | UI 依存が core に漏れやすい | 現時点では時期尚早 |

## Design Decisions

### Decision: `KlibDocument` を headless 実行の唯一の入力契約にする

- **Context**: `.k` / `.klib` の二重表現や別形式の test fixture を避ける必要がある。
- **Alternatives Considered**:
  1. `.klibtxt` を parse して実行する
  2. `KlibDocument` を直接実行する
- **Selected Approach**: build 済み `KlibDocument` を `HeadlessVmSession` に渡して実行する。
- **Rationale**: 公開仕様と実装モデルが既に一致しており、golden test と VM test の責務も分けやすい。
- **Trade-offs**: binary loader が未実装の段階では compiler 経由 fixture に寄るが、contract は安定する。
- **Follow-up**: 実装時に binary `.klib` loader を追加する場合も同じ `KlibDocument` 契約へ正規化する。

### Decision: 停止理由を状態型で表現し、入力と選択を resume API へ分離する

- **Context**: headless 実行は手動入力なしで待機を制御できる必要がある。
- **Alternatives Considered**:
  1. 例外で待機を表現する
  2. 実行結果に停止理由と pending payload を持たせる
- **Selected Approach**: `Running` / `WaitingForAdvance` / `WaitingForSelection` / `Completed` / `Faulted` の状態を持つ実行セッションを採用する。
- **Rationale**: テストが停止理由を直接 assert でき、runtime adapter も同じ状態を読める。
- **Trade-offs**: 状態遷移の明示実装が必要になる。
- **Follow-up**: 実装時に resume 前提条件を unit test で固定する。

### Decision: 観測結果は UI コマンドではなく scene event と transcript で保持する

- **Context**: headless VM でも `say` / `nar` / `select` の結果をテストで確認したい。
- **Alternatives Considered**:
  1. runtime 用の UI コマンド列をそのまま露出する
  2. headless 専用の観測状態モデルを定義する
- **Selected Approach**: 直近イベント、累積 transcript、現在の choice prompt を持つ観測モデルを定義する。
- **Rationale**: UI フレームワーク非依存で、テスト期待値を安定させやすい。
- **Trade-offs**: runtime adapter 側に変換層が必要になる。
- **Follow-up**: `show` や `bg` など将来の runtime 命令拡張では event 種別追加で表現できるようにする。

## Risks & Mitigations

- 停止状態の粒度が粗すぎると runtime adapter で再判定が必要になる — design で停止理由と pending payload を分離しておく。
- compiler fixture 依存のテストだけだと loader 不具合を拾えない — 実装時は compiler 直結 test と document-level test を併用する。
- `say` / `nar` を自動進行させると対話待ち要件が曖昧になる — headless VM では明示 resume があるまで待機維持を原則にする。

## References

- `docs/spec/k-intermediate-representation-spec.md` — `.klib` の公開契約
- `docs/spec/windows-runtime-spec.md` — runtime 側責務と VM 連携境界
- `docs/spec/kes-language-stl-spec.md` — `say` / `nar` / `select` の runtime 連携意味
- `docs/testing-strategy.md` — VM test と UI test の役割分担
- `source/cli/KoromoEventScript.Cli/Compilation/KlibModels.cs` — 現行の IR 実装モデル
