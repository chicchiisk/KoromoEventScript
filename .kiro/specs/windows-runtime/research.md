# Research: windows-runtime

## 調査範囲

Windows runtime は既存の CLI ビルド成果物を入力にする新規 runtime であり、Windows UI、描画、音声、VM、STL syscall、配布を横断するため Full Discovery として調査した。

## 既存コードベース

- `source/runtime` は現時点で `.gitkeep` のみで、runtime 用プロジェクトは未作成である。
- `KoromoEventScript.slnx` は CLI と CLI Tests を中心に構成されている。
- CLI は `net10.0`、Nullable と ImplicitUsings を有効にしている。
- `.klib` モデルは `source/cli/KoromoEventScript.Cli/Compilation/KlibModels.cs` にあり、runtime から直接参照するには CLI 実行ファイルへの依存が発生する。
- `source/cli/KoromoEventScript.Cli/Execution/HeadlessVmExecutor.cs` は VM 実行の既存実装として有用だが、syscall は headless 用の最小効果に偏っている。
- `BuildManifestDocument` は現状 `cliVersion`、`target`、`entryEventListPath`、`inputs`、`scripts`、`localizations` が中心であり、Windows runtime が必要とする runtime 設定、素材 catalog、配布情報は拡張が必要である。

## 参照仕様

- `docs/spec/windows-runtime-spec.md`: Windows runtime の起動、manifest、描画、音声、入力、標準 UI、セーブ、診断、配布の期待を定義している。
- `docs/spec/kes-language-stl-spec.md`: runtime 側で効果を持つ STL と syscall の範囲を定義している。
- `docs/spec/k-intermediate-representation-spec.md`: `.klib` 命令セットと VM 実行契約の根拠である。
- `docs/spec/cli-tool-spec.md`: `kes run`、`kes publish --target windows`、終了コード体系との接続を確認する対象である。

## 外部技術調査

- [Windows App SDK overview](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/) は、WinUI 3 と最新 Windows API を使う Windows desktop app の基盤として Windows App SDK を説明している。Stable channel を production 向けに使う前提が適切である。
- [Windows App SDK deployment overview](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/deploy-overview) は framework-dependent と self-contained deployment の選択肢を示している。本仕様では配布フォルダまたは zip 展開後に単体実行できることを優先し、unpackaged self-contained を第一候補にする。
- [Win2D overview](https://learn.microsoft.com/en-us/windows/apps/develop/win2d/) は GPU accelerated immediate-mode 2D graphics を提供する WinRT API として Win2D を説明している。ノベル runtime の 2D 合成、テキスト、スプライト、トランジションに適している。
- [Win2D quick start](https://learn.microsoft.com/en-us/windows/apps/develop/win2d/quick-start) では `CanvasControl` と `CanvasAnimatedControl` が示されている。runtime は継続描画と transition を扱うため `CanvasAnimatedControl` を採用候補にする。
- [Media playback docs](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/media-playback) と [MediaPlayerElement API](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.mediaplayerelement?view=windows-app-sdk-1.8) は Windows App SDK の media 再生制御を示している。runtime では UI 表示用 element に閉じず、BGM / SE / Voice のチャンネルサービスとして `MediaPlayer` を抽象化する。
- [Microsoft.WindowsAppSDK NuGet](https://www.nuget.org/packages/Microsoft.WindowsAppSDK/) は 2026-06-21 時点で 2.2.0 が安定版として公開されている。
- [Microsoft.Graphics.Win2D NuGet](https://www.nuget.org/packages/Microsoft.Graphics.Win2D/) は 2026-06-21 時点で 1.4.0 が公開され、Windows App SDK WinUI への依存を持つ。

## 採用判断

- Windows UI は WinUI 3 / Windows App SDK を採用する。
- 描画は Win2D `CanvasAnimatedControl` を採用候補にする。
- 音声は Windows App SDK / WinUI の media API を `IAudioChannelService` の背後に隠す。
- VM、manifest、STL syscall、save 形式は platform-neutral な `KoromoEventScript.Runtime.Core` に置き、Windows 固有実装を `KoromoEventScript.Runtime.Windows` に置く。
- 既存 CLI の `.klib` モデルと headless VM は Core へ移動または共有化し、CLI は Core を参照する形へ段階的に寄せる。
- Windows 配布は最初は unpackaged self-contained folder / zip を対象にし、MSIX は範囲外にする。

## 未解決事項と再検証トリガー

- Windows App SDK / Win2D の採用 version は実装開始時に再確認する。
- `manifest.json` の runtime 用 schema が CLI 側で変わる場合、runtime manifest reader と publish layout を再検証する。
- `.klib` opcode または STL syscall が追加・変更された場合、VM opcode coverage と syscall registry coverage を再生成する。
- save schema の version を上げる場合、互換読み込み方針を別途決める。
- self-contained single-file exe の採否は publish 実装時に成果物サイズ、起動性、デバッグ性を見て決める。
