# Brief: windows-runtime

## Problem

KoromoEventScript の制作者は、CLI で生成した Windows 向けビルド成果物を、一般ユーザーがそのまま起動して遊べる配布用プレイヤーとして提供したい。現状は `.klib`、`.klibtxt`、`manifest.json` を生成する CLI と headless VM の実装が中心であり、Windows 11 上で描画、音声、入力、標準 UI、セーブ、診断を統合して実行する runtime がまだ存在しない。

## Current State

`docs/spec/windows-runtime-spec.md` には Windows 11 / C# / WinUI 3 / Windows App SDK / Win2D を前提とした runtime 仕様が定義されている。CLI 側は `kes build` で `build/windows/` 配下へ `.klib`、必要に応じて `.klibtxt`、`manifest.json` を出力し、`kes run` と `kes publish --target windows` は runtime に成果物を渡す契約を持つ。

実装上は `source/cli/KoromoEventScript.Cli/Execution/` に headless VM と save-state 関連の基盤があり、`source/runtime/` は Windows runtime 実装の受け皿として存在するが、具体的な runtime アプリケーションはまだ未実装である。

## Desired Outcome

Windows 11 上で、`kes publish --target windows` の成果物に含まれる実行ファイルを起動し、`manifest.json` と `.klib` を読み込んでシナリオをプレイできる。開発時には `kes run` から manifest と runtime 引数を渡して起動でき、配布時には zip 展開後の `MyProject.exe` からプレイを開始できる。

初期リリースでは、1920x1080 の制作座標系を維持した描画、背景・actor・音声素材の読み込み、主要 VM 命令の反映、テキスト送り、選択肢、バックログ、スキップ、オート、通常セーブ、ロード、オートセーブ、既読情報、ユーザー設定保存、`--debug` / `--profile` による診断を満たす。

## Approach

Windows runtime を `source/runtime/` 配下の独立した Windows アプリケーションとして実装し、CLI 実装とはプロジェクト境界を分ける。runtime は `manifest.json` を唯一の素材・スクリプト解決入口とし、`.klib` を VM 実行契約に従って読み込む。UI と描画は WinUI 3 / Windows App SDK / Win2D に寄せ、VM 状態から runtime の画面状態、音声状態、入力待ち状態へ変換する adapter 層を設ける。

CLI 側の責務は、runtime が読める manifest と配布成果物を作ること、`kes run` で runtime を起動して終了コードを受け取ることに限定する。Windows runtime 側は `.kc` / `.kel` の生ソースを直接読まず、素材欠落、読み込み失敗、VM 状態不整合など実行時にしか検出できない問題を扱う。

## Scope

- **In**: Windows 11 向け runtime アプリケーションの起動制御、manifest 読み込み、`.klib` 読み込み、VM 連携、Win2D 描画、音声再生、入力操作、標準 UI、セーブ/ロード、ユーザー設定、デバッグ表示、ログ、終了コード、`kes run` / `kes publish --target windows` との接続に必要な最小 CLI 連携。
- **Out**: Windows 10 以前、macOS、Linux、Unity runtime、Unreal runtime、VS Code 拡張、UI スキン差し替え、キーコンフィグ、正式なゲームパッド対応、追加トランジション、MSIX 配布、クラウドセーブ、runtime プラグイン、生 `.kc` / `.kel` の runtime 直接実行。

## Boundary Candidates

- runtime 起動制御: コマンドライン引数、既定 manifest 探索、デバッグ/プロファイル設定、終了コード変換。
- manifest / resource 管理: manifest 基準の `.klib`、画像、音声、ローカライズ済み `.klib` 解決。
- VM adapter: headless VM の停止理由、観測ログ、選択肢、save-state を runtime 表現へ変換する境界。
- 描画/演出: 1920x1080 制作座標系、表示スケーリング、レイヤー順、fade / crossfade / none。
- 音声: BGM、SE、Voice チャンネル、音量、クリック進行時の Voice 停止。
- 標準 UI / 入力: メッセージ、選択肢、バックログ、スキップ、オート、システムメニュー、設定。
- 永続化: 通常セーブ、オートセーブ、既読情報、ユーザー設定、Windows ユーザーデータ領域への保存。
- CLI 接続: `kes run` の runtime 起動と引数引き渡し、`kes publish --target windows` の runtime 同梱と zip 生成。

## Out of Boundary

- `.klib` instruction schema や binary format の再設計。
- `.kc` / `.kel` の構文、意味解析、タグ補完、ローカライズ辞書生成。
- CLI の一般的な build / publish 仕様全体の再設計。
- Unity / Unreal 向け runtime の挙動定義。
- 配布用 UI の高度なカスタマイズ機構。
- OS 固有のストア配布、インストーラー、MSIX パッケージング。

## Upstream / Downstream

- **Upstream**: `docs/spec/windows-runtime-spec.md`、`docs/spec/cli-tool-spec.md`、`docs/spec/k-intermediate-representation-spec.md`、`docs/spec/kes-config.xsd`、CLI の `kes build` / manifest 生成、headless VM、save-state mapper。
- **Downstream**: `kes run` による開発時確認、`kes publish --target windows` による配布、将来の UI スキン差し替え、キーコンフィグ、ゲームパッド対応、追加トランジション、MSIX 配布、runtime プラグイン。

## Existing Spec Touchpoints

- **Extends**: 既存 `.kiro/specs/` に該当 spec はないため、新規 spec として作成する。
- **Adjacent**: CLI コマンド仕様、`.klib` 中間表現仕様、ローカライズ辞書仕様、Unity / Unreal runtime 仕様。特に CLI は成果物生成と runtime 起動に留め、Windows runtime は manifest 以降のプレイヤー挙動を所有する。

## Constraints

Windows 11 のみを対象とし、実装言語は C#、runtime UI 基盤は WinUI 3 と Windows App SDK、描画は Win2D とする。プロジェクト全体は .NET 10 系、NUnit テストを標準とする。ドキュメントは日本語で記述し、実装は公開仕様と矛盾させない。runtime は `.klibtxt` を入力に使わず、`.klib` と `manifest.json` を正の runtime 入力として扱う。
