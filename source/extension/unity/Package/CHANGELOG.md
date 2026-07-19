# Changelog

- `KesManager`に、命令実行前のevent source位置とbytecode命令をUnity Consoleへ出力する`Log Execution Source`オプションを追加。
- SampleProjectのGlobal Light 2DがKES背景・actorを含む全sorting layerを照らすように修正。

## [0.1.0] - 2026-07-12

- Unity 6000.5.3f1 と URP を対象とする package scaffold を追加。
- Runtime、Editor、Edit Mode Test、Play Mode Test の assembly 境界を追加。
- `.klib`を`KesKlibAsset`へ、`manifest.kson`を`KesBuildAsset`へ変換するScriptedImporterを追加。
- `KesManager`へKES Build Asset参照を追加。
- Unity packageと.NET Runtime CoreでKlibモデル・loader・診断ソースの共有を開始。
- Klib importerを完全なsection構造検証へ対応し、メモリ上のKlib assetを直接読み込めるようにした。
- VM、STL syscall、演出、セーブスナップショット、トリガー評価のRuntime CoreソースをUnity packageへ共有した。
- Unity Runtime Testへ最小Klibの算術実行検証を追加した。
- `KesManager`へBuild AssetからのVM開始、入力待ちの継続、選択肢決定、演出・診断通知を追加した。
- `KesManager`へロケールの完全一致選択と、未収録ロケールから既定ロケールへの警告付きフォールバックを追加した。
- `KesPresentation`へ背景・actor・会話・選択肢のeffect反映と1920x1080基準の座標変換を追加した。
- AddressablesによるSprite・AudioClipの非同期解決、キャッシュ、参照解放を行うresolverを追加した。
- actorの表情をSpriteロード完了前に切り替えた場合も、置き換えられたAddressables参照を解放するようにした。
- SampleProjectへ素材、Addressables登録、`KesSystem`プレハブ・SampleScene生成用セットアップを追加した。
- Input System用の入力ソースと一元入力ルーターを追加し、テキスト送り、選択肢、メニュー、スキップ、オートを入力コンテキストごとに重複なく処理できるようにした。
- manifestのentry event、trigger、ゲーム変数を使ったイベント遷移をUnity Runtimeへ追加した。
- 選択肢UIを行単位のオブジェクトへ分離し、選択アイコン、Vertical Layout Group、Content Size Fitterによる伸縮レイアウトへ変更した。
- VMへ`WaitingForHost`、`Faulted`、`Stopped`を追加し、Addressables、演出、音声、unscaled timer、save/load callbackの完了後にだけ1回再開するhost operation境界を追加した。
- `scene`、`actor`、`audio`、`text`、`state`、`system`のUnity向けSTL実行を追加し、非同期完了、取消し、診断、Addressables参照解放、タイプライター、BGM fade、SE・Voice再生へ対応した。
