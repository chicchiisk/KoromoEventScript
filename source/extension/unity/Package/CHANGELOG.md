# Changelog

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
