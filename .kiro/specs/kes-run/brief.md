# Brief: kes-run

## Problem

KoromoEventScript 利用者は、`kes init` で作成したプロジェクトをそのまま `kes run` で起動したい。CLI の実行入口が `.kc` 単体や `.kel` 直接指定も受け付ける形だと、プロジェクト設定、素材、ローカライズ、ビルド成果物の契約が分散し、Windows ランタイムへ渡す入力も曖昧になる。

`kes run` はプロジェクト前提に統一し、`kes.xml` と `Project.Entry` を唯一の実行起点にする必要がある。

## Current State

現行実装は `source/cli/KoromoEventScript.Cli/Commands/Run/RunCommand.cs` に runtime 起動の骨格を持つ。

- `--no-build` がない場合は常に `windows` build を実行する。
- `--no-build` の場合は `build/windows/manifest.json` を推定して runtime へ渡す。
- `--locale`、`--start`、`--fullscreen`、`--width`、`--height`、`--debug`、`--profile`、`--` 以降の runtime arguments は転送している。
- Windows runtime は同梱 exe、リポジトリ内 csproj、既存 bin 配下 exe を探索して起動する。

一方で、最新の `docs/spec/cli-tool-spec.md` とは次の差分がある。

- `kes run [PROJECT_DIR]` をプロジェクト実行として扱う境界が明確でない。
- `kes.xml` の `Project.Entry` を唯一の `.kel` 起点として扱う契約が `RunCommand` に閉じていない。
- `.kc` 単体や `.kel` 直接指定を廃止する方針が未実装である。
- `--build` が明示的なオプションとして整理されていない。
- 既定動作は「必要なときだけ自動 build」だが、現行は常に build する。
- `--target windows` と未知 target の診断が整理されていない。
- runtime 起動失敗の終了コードと診断が仕様どおりに固定されていない。

## Desired Outcome

`kes run` がプロジェクト単位の実行コマンドとして一貫して動作する。

- `kes run` は現在ディレクトリまたは親ディレクトリから `kes.xml` を探索して実行する。
- `kes run path/to/project` は指定されたプロジェクトルートの `kes.xml` を使って実行する。
- 実行対象 `.kel` は常に `kes.xml` の `Project.Entry` から解決する。
- `.kc` 単体や `.kel` 直接指定はサポートせず、廃止済み入力として診断する。
- `--build` は必ず build してから実行する。
- `--no-build` は既存成果物だけを使い、不足時は診断する。
- 既定動作は manifest / `.klib` / asset などが不足または入力より古い場合だけ build する。
- Windows runtime 起動失敗は runtime launch error として終了コード 7 を返す。
- runtime の終了コードは CLI の終了コードへ反映する。
- 仕様差分は CLI テストで固定する。

## Approach

既存 `RunCommand` の起動処理を活かしつつ、入力解決、build 方針、鮮度判定、runtime 起動を分離して実装する。

`RunCommand` の入口では `PROJECT_DIR` のみをプロジェクト指定として扱う。指定がない場合は現在ディレクトリから `kes.xml` を探索する。指定値が `.kc` または `.kel` ファイルの場合は、プロジェクト実行へ暗黙変換せずエラーにする。

build 方針は `AlwaysBuild`、`NoBuild`、`BuildIfStale` の 3 種類として扱う。`BuildIfStale` では `kes.xml`、`Project.Entry` の `.kel`、参照 `.kc`、素材、ローカライズ辞書と `build/windows/manifest.json` / `.klib` を比較し、必要な場合だけ `BuildPipelineService` を呼ぶ。

## Scope

- **In**:
  - `kes run [PROJECT_DIR]` のプロジェクトルート解決
  - `kes.xml` 読み込みと `Project.Entry` 解決
  - `.kc` / `.kel` 直接指定廃止の診断
  - `--target windows` と未知 target 診断
  - `--build` / `--no-build` の排他制御
  - 既定時の build stale 判定
  - `build/windows/manifest.json` と `.klib` の存在検証
  - Windows runtime exe / csproj 解決
  - runtime 起動失敗時の終了コード 7 と診断
  - runtime 終了コードの反映
  - `--locale`、`--start`、`--fullscreen`、`--width`、`--height`、`--debug`、`--profile`、`--` 以降の引数転送
  - CLI unit / integration tests
- **Out**:
  - `.kc` 単体実行や `.kel` 直接実行の互換維持
  - Windows runtime 内部の VM / UI / audio / save-load 挙動
  - Unity / Unreal runtime 起動
  - `kes publish` の配布物構成
  - `.kel` フォーマット変更

## Boundary Candidates

- `RunInputResolver`: `PROJECT_DIR`、project root、`kes.xml`、`Project.Entry`、build output path を解決する。
- `RunBuildPolicy`: `--build`、`--no-build`、既定自動 build の方針を表現する。
- `RunStalenessChecker`: 入力ファイル群と manifest / `.klib` の更新時刻を比較する。
- `RuntimeCommandResolver`: Windows runtime exe / csproj の探索を担う。
- `RuntimeLaunchAdapter`: `ProcessLauncher` に渡す command / arguments / working directory を構築する。

## Out of Boundary

- ランタイムが `manifest.json` を読んだ後のイベント遷移、STL 実行、描画、音声再生は扱わない。
- Unity / Unreal の `run` target は将来拡張とする。
- build pipeline の成果物仕様そのものは `kes build` が所有する。
- `--profile` の runtime 内部ログ仕様は Windows runtime 側が所有し、CLI は引数を転送するだけにする。

## Upstream / Downstream

- **Upstream**:
  - `docs/spec/cli-tool-spec.md`
  - `kes.xml` project config loader
  - `BuildPipelineService`
  - `BuildManifestDocument` / `manifest.json`
  - Windows runtime の起動引数
- **Downstream**:
  - `full-command-sample` を `kes run` で起動する開発者導線
  - CI / smoke test での runtime 起動確認
  - 将来の `kes publish` 後の実行確認
  - 将来の Unity / Unreal target run 拡張

## Existing Spec Touchpoints

- **Extends**: `docs/spec/cli-tool-spec.md` の `kes run`
- **Adjacent**:
  - `.kiro/specs/windows-runtime`
  - `docs/spec/windows-runtime-spec.md`
  - `docs/spec/k-intermediate-representation-spec.md`
  - `docs/spec/kel-file-spec.md`

## Constraints

- 実装言語は C# / .NET 10。
- 既存 CLI の `BuildPipelineService` と `ProcessLauncher` を再利用する。
- Windows runtime 初期 target は `windows` のみ。
- docs/spec 配下および Kiro spec 文書は日本語で記述する。
- 新規実装には NUnit テストを追加し、`dotnet test` で検証できる状態にする。
- 既存の未コミット変更を巻き戻さず、`kes run` に必要な範囲へ変更を閉じる。
