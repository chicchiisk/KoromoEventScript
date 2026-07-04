# Research & Design Decisions: kes-run

## Summary

- **Feature**: `kes-run`
- **Discovery Scope**: Extension
- **Key Findings**:
  - 既存の `RunCommand` は Windows runtime 起動の骨格を持つが、`--manifest` 直接指定、常時 build、runtime 起動失敗の終了コード `6` など、最新 CLI 仕様との差分が残っている。
  - `BuildPipelineService` は `manifest.json`、`.klib`、`inputs`、`scripts`、`assets` を生成済みであり、`kes run` は build 成果物の所有者にならず、成果物利用と検証に責務を限定できる。
  - `CliExitCode` には仕様上の runtime launch error `7` がまだ存在しないため、起動失敗を既存の file error と区別する必要がある。

## Research Log

### 既存 `kes run` 実装

- **Context**: requirements では `kes run [PROJECT_DIR]` をプロジェクト前提に統一する必要がある。
- **Sources Consulted**:
  - `source/cli/KoromoEventScript.Cli/Commands/Run/RunCommand.cs`
  - `source/cli/KoromoEventScript.Cli/Commands/Run/RunCommandOptions.cs`
  - `source/cli/KoromoEventScript.Cli/Commands/Run/ProcessLauncher.cs`
  - `source/cli/KoromoEventScript.Cli/Commands/CliApplication.cs`
- **Findings**:
  - `RunCommand` は `--no-build` がない限り常に `BuildPipelineService.Run` を呼ぶ。
  - `RunCommandOptions.ManifestPath` と `--manifest` は `.kc` / `.kel` 直接指定廃止後の project-first 契約と噛み合わない。
  - runtime executable / csproj 探索、引数組み立て、manifest 解決、build 実行が `RunCommand` に集中している。
  - `ProcessLauncher` は `ProcessStartInfo.ArgumentList` を使っており、通常 exe 起動時の引数境界は保たれている。
- **Implications**:
  - `RunCommand` は orchestration に寄せ、入力解決、ビルド方針、stale 判定、runtime コマンド解決を分離する。
  - `--manifest` は CLI parse から除外し、既存テストは project-first の期待値へ更新する。

### Build 成果物と manifest

- **Context**: `--no-build` と既定自動 build で成果物の存在・鮮度を判定する必要がある。
- **Sources Consulted**:
  - `source/cli/KoromoEventScript.Cli/Build/BuildPipelineService.cs`
  - `source/cli/KoromoEventScript.Cli/Build/BuildOutputPlanner.cs`
  - `source/cli/KoromoEventScript.Cli/Build/BuildManifestDocument.cs`
  - `source/cli/KoromoEventScript.Cli/Build/BuildManifestWriter.cs`
- **Findings**:
  - `BuildOutputPlanner` は `build/<target>/manifest.json` と `build/<target>/<EventsPath>/**/*.klib` を決める。
  - `BuildManifestDocument` は `Inputs`、`Scripts`、`Assets` を持ち、runtime に渡す `.klib` の manifest 相対パスを保持する。
  - `BuildPipelineService` は assets を manifest へ列挙するが、既存 manifest にない新規 asset は manifest だけでは検出できない。
- **Implications**:
  - `RunArtifactValidator` は manifest を読み、`scripts[].klibPath` の存在を検証する。
  - `RunStalenessChecker` は manifest に加え、現在の `kes.xml`、entry `.kel`、`EventsPath` 配下 `.kc`、`AssetsPath`、`LocalePath` 配下ファイルを保守的に入力として扱う。

### ProjectSystem と CLI parse

- **Context**: `PROJECT_DIR` 解決、`.kc` / `.kel` 直接指定診断、`--build` / `--no-build` 排他を CLI 境界で扱う必要がある。
- **Sources Consulted**:
  - `source/cli/KoromoEventScript.Cli/ProjectSystem/ProjectRootResolver.cs`
  - `source/cli/KoromoEventScript.Cli/ProjectSystem/ProjectConfigLoader.cs`
  - `source/cli/KoromoEventScript.Cli/Commands/CliApplication.cs`
- **Findings**:
  - `ProjectRootResolver` は明示ディレクトリとカレント上位探索に対応済みだが、ファイル指定の診断は汎用的である。
  - `ProjectConfigLoader` は `Project.Entry` 欠落を invalid `kes.xml` として扱える。
  - `ParseRun` は現在 `--target` と `--build` を持たず、`--manifest` と `--log-format` を持つ。
- **Implications**:
  - `ParseRun` に `--target windows`、`--build`、`--no-build` の仕様を反映する。
  - `.kc` / `.kel` ファイル指定の診断は `RunProjectInputResolver` で明確化し、`ProjectRootResolver` は既存利用者向けに維持する。

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| `RunCommand` 集中型 | 既存 `RunCommand` に条件分岐を追加する | 変更ファイルが少ない | build 方針、入力解決、runtime 起動の責務が混ざりテストが粗くなる | 不採用 |
| 小型サービス分割 | `RunCommand` を orchestration とし、周辺責務を `Commands/Run` 配下へ分離する | 境界が明確で unit test が書きやすい | ファイル数は増える | 採用 |
| BuildPipeline 拡張型 | stale 判定や成果物検証を `BuildPipelineService` に寄せる | build と成果物の近接性が高い | `kes run` 固有の runtime 起動判断が build 境界へ漏れる | 不採用 |

## Design Decisions

### Decision: `kes run` 固有の入力解決を `Commands/Run` に置く

- **Context**: `.kc` / `.kel` 直接指定廃止は `kes run` 固有の user-facing 診断である。
- **Alternatives Considered**:
  1. `ProjectRootResolver` を拡張する。
  2. `RunProjectInputResolver` を新設し、`ProjectRootResolver` を呼び出す。
- **Selected Approach**: `RunProjectInputResolver` を新設し、ファイル拡張子診断、project root 解決、`Project.Entry` 存在確認をまとめる。
- **Rationale**: `ProjectRootResolver` は他コマンドも使うため、run 固有の廃止済み入力診断を混ぜない。
- **Trade-offs**: run 用 resolver が増えるが、責務が明確になる。
- **Follow-up**: `.kc` / `.kel` の診断文を CLI test で固定する。

### Decision: Stale 判定は保守的な再ビルドを許容する

- **Context**: 新規 asset や新規 `.kc` は既存 manifest だけでは検出できない。
- **Alternatives Considered**:
  1. manifest の `inputs` と `scripts` のみを見る。
  2. project root の関連入力ディレクトリを列挙して既存成果物と比較する。
- **Selected Approach**: `kes.xml`、entry `.kel`、`EventsPath` 配下 `.kc`、`AssetsPath`、`LocalePath` 配下ファイルを入力候補として列挙する。
- **Rationale**: 不要な build より、古い成果物で runtime を起動するほうが利用者影響が大きい。
- **Trade-offs**: 未参照 `.kc` 変更でも自動 build される可能性がある。
- **Follow-up**: 必要なら将来 `BuildPipelineService` から import graph metadata を manifest に追加して精度を上げる。

### Decision: runtime 起動失敗を専用終了コードへ分離する

- **Context**: CLI 仕様では runtime launch error が終了コード `7` と定義されている。
- **Alternatives Considered**:
  1. 既存の `FileOrDirectoryError = 6` を継続する。
  2. `RuntimeLaunchError = 7` を `CliExitCode` に追加する。
- **Selected Approach**: `RuntimeLaunchError = 7` を追加する。
- **Rationale**: CI と smoke test が runtime 起動失敗を file error と区別できる。
- **Trade-offs**: 既存テストの期待値更新が必要。
- **Follow-up**: runtime process の戻り値そのものは変換せず、そのまま CLI 終了コードへ反映する。

## Risks & Mitigations

- Stale 判定が過剰に build する — 初期実装では安全側に倒し、後続で manifest metadata を増やせる設計にする。
- manifest JSON 読み取りが writer とずれる — `BuildManifestDocument` と対応する reader を用意し、テストで writer 出力を round-trip する。
- runtime project 起動時の `dotnet run -- --args` 引数境界が崩れる — 既存 `SerializeArguments` のテストを維持し、runtime arguments を含むケースを追加する。

## References

- `docs/spec/cli-tool-spec.md` — `kes run` の公開仕様と終了コード表。
- `.kiro/steering/product.md` — CLI と runtime 連携を段階的に育てる方針。
- `.kiro/steering/tech.md` — C# / .NET 10、NUnit、型安全性の標準。
- `.kiro/steering/structure.md` — `Commands` / `Build` / `ProjectSystem` の責務境界。
