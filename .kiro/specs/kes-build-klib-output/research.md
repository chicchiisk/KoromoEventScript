# Research & Design Decisions

## Summary

- **Feature**: `kes-build-klib-output`
- **Discovery Scope**: Extension
- **Key Findings**:
  - 現在の `BuildCommand` は `.klib` / `.klibtxt` の基本出力だけを持ち、`kes correct` 連携、`--out-dir`、`--loc`、`manifest.json` を扱っていない。
  - `BuildPreparationService` と `ScriptPreparationService` により、project 解決、entry 解決、semantic diagnostics の基盤は既にある。
  - `kes loc` 実装で追加された `LocalizationDictionaryCsvRepository` と `LocalizationTextExtractor` は、`--loc` の辞書入力側でも再利用可能な既存境界になっている。

## Research Log

### Build command の現状責務

- **Context**: `kes build` の設計で、既存実装の再利用可能範囲と不足責務を明確にする必要があった。
- **Sources Consulted**:
  - `source/cli/KoromoEventScript.Cli/Commands/Build/BuildCommand.cs`
  - `source/cli/KoromoEventScript.Cli/Commands/Build/BuildCommandOptions.cs`
  - `source/cli/KoromoEventScript.Cli/Build/BuildPreparationService.cs`
  - `source/cli/KoromoEventScript.Cli/Compilation/KlibArtifactWriter.cs`
- **Findings**:
  - 既存 `BuildCommand` は `BuildPreparationService`、`KlibCompiler`、`KlibArtifactWriter` を直列に呼ぶだけの最小実装である。
  - 出力先は `config.BuildPath` と `options.Target` に固定され、`--out-dir` の上書きや `manifest.json` の生成を扱っていない。
  - `BuildCommandOptions` は `ProjectDirectory`、`WarningsAsErrors`、`EntryPath`、`CheckOnly`、`EmitTextIr`、`Target` のみを持ち、公開仕様の `--loc`、`--out-dir`、`--no-incremental` を表現していない。
- **Implications**:
  - `BuildCommand` へ直接責務を積み増すのではなく、出力計画とローカライズ適用を分離した orchestration が必要になる。
  - `BuildCommandOptions` の拡張が CLI parse と実行境界の両方の起点になる。

### Check-only と diagnostics 契約

- **Context**: 既存の `build --check-only` 契約を壊さずに、正規 build へ拡張する必要があった。
- **Sources Consulted**:
  - `source/cli/KoromoEventScript.Cli/Commands/Build/BuildCheckOnlyCommand.cs`
  - `tests/KoromoEventScript.Cli.Tests/Commands/BuildCheckOnlyCommandTests.cs`
  - `source/cli/KoromoEventScript.Cli/Commands/CliApplication.cs`
- **Findings**:
  - `check-only` は非破壊であること、warning の扱い、JSON Lines diagnostics、終了コード契約が既にテストで固定されている。
  - 現状の parse では `--target windows` のみ受理し、それ以外は command line error になる。
  - `check-only` 時は build artifact だけでなく `.kc` の書き戻しも行ってはならない。
- **Implications**:
  - 設計では `check-only` を build pipeline の分岐ではなく、前処理後に artifact generation を抑止する第一級ルールとして扱う必要がある。
  - 新しい `kes build` 実装でも diagnostics 出力は `CliApplication` / `DiagnosticSink` の既存境界を維持する。

### ローカライズ入力と既存資産

- **Context**: `--loc` の実装責務をどこまで build spec に含めるかを決める必要があった。
- **Sources Consulted**:
  - `docs/spec/cli-tool-spec.md`
  - `source/cli/KoromoEventScript.Cli/Localization/LocalizationDictionaryCsvRepository.cs`
  - `source/cli/KoromoEventScript.Cli/Localization/LocalizationDictionaryExportService.cs`
  - `tests/KoromoEventScript.Cli.Tests/Localization/LocalizationDictionaryCsvRepositoryTests.cs`
- **Findings**:
  - 公開仕様上、`kes build --loc <language-tag>` は source locale `.csv` を読み、対象言語の表示テキストを compile-time に解決した `.klib` を生成する。
  - 辞書テンプレート生成は `kes loc` の責務であり、build 側は既存 `.csv` の検証と読込のみを担えばよい。
  - CSV repository は必須列、重複 tag、UTF-8 BOM などの契約を既に内包している。
- **Implications**:
  - `--loc` の実装は新しい CSV parser を作るのではなく、既存 repository を入力境界として再利用するのが最も小さい設計になる。
  - build spec は「辞書生成」ではなく「辞書適用」に責務を限定できる。

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| BuildCommand へ責務を集約 | parse 後の build 分岐を 1 クラスに寄せる | 変更ファイルが少ない | 責務肥大化、テスト境界が曖昧 | 不採用 |
| 既存 service を保ったまま build orchestration を追加 | 前処理、ローカライズ、出力計画、manifest 生成を補助 service に分離 | 既存パターンに沿う、テストしやすい | 新規型が増える | 採用 |
| `kes loc` と build を統合して辞書生成も build が担う | コマンド数は減る | CLI 境界が単純 | 公開仕様と矛盾、責務混線 | 不採用 |

## Design Decisions

### Decision: build orchestration を command から分離する

- **Context**: `kes build` は parse・correct・compile・localize・emit の複数責務を持つ。
- **Alternatives Considered**:
  1. `BuildCommand` へ直接すべて実装する
  2. build orchestration service を新設し、周辺責務を分離する
- **Selected Approach**: `BuildCommand` は CLI 境界を維持し、内部で build orchestration service 群を呼ぶ。
- **Rationale**: 既存の `CorrectCommand`、`LocCommand` と同じく command は orchestration 入口に留めるほうが、責務が明確でテストもしやすい。
- **Trade-offs**: 型数は増えるが、manifest・output layout・localization を独立に検証できる。
- **Follow-up**: タスク生成時に service ごとの unit test と command-level integration test を分ける。

### Decision: `--loc` は compile-time text projection として扱う

- **Context**: 公開仕様では runtime での辞書解決ではなく、言語別 `.klib` の生成が求められている。
- **Alternatives Considered**:
  1. VM/runtime で locale key を解決する
  2. build 時に `.csv` を読み、対象言語の string 定数へ置き換える
- **Selected Approach**: build 時に対象言語の本文へ射影した `KlibDocument` を生成する。
- **Rationale**: 中間表現仕様の「compile-time 完全解決」と整合し、runtime 側の責務を増やさない。
- **Trade-offs**: 言語ごとに成果物数は増えるが、runtime は単純になる。
- **Follow-up**: 実装では source text と localized text の差し替え位置を compiler か前段変換のどちらで持つかを検証する。

### Decision: `manifest.json` を build spec の第一級成果物に含める

- **Context**: 公開仕様は `manifest.json` を build 成果物として要求し、run/publish もこれを前提にしている。
- **Alternatives Considered**:
  1. 今回は `.klib` のみを出力し、manifest は後回しにする
  2. build spec の範囲で最小限の manifest 出力契約まで含める
- **Selected Approach**: build spec は `manifest.json` を含む最小ビルド成果物契約まで所有する。
- **Rationale**: `kes build` 単体で「実行に渡せる成果物」を作れることがユーザー価値であり、run/publish の前提にもなる。
- **Trade-offs**: manifest writer の新規責務が増える。
- **Follow-up**: manifest schema の最小構成を設計とタスクで固定する。

## Risks & Mitigations

- `--loc` の本文差し替え位置を誤ると compiler と source mapping の責務が曖昧になる — localized projection の責務を 1 コンポーネントへ固定する。
- `check-only` と通常 build の分岐が散ると非破壊契約が崩れる — artifact generation gate を 1 箇所へ集約する。
- manifest 契約を曖昧にすると run/publish へ波及する — build spec 内で最小 required fields を設計に明記する。

## References

- `docs/spec/cli-tool-spec.md` — `kes build` の公開 CLI 契約
- `docs/spec/k-intermediate-representation-spec.md` — `.klib` / `.klibtxt` / manifest との関係
- `source/cli/KoromoEventScript.Cli/Commands/Build/BuildCommand.cs` — 現行 build 実装
- `source/cli/KoromoEventScript.Cli/Localization/LocalizationDictionaryCsvRepository.cs` — locale 辞書入力の既存境界
