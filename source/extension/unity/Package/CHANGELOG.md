# Changelog

## [0.1.0] - 2026-07-12

- Unity 6000.5.3f1 と URP を対象とする package scaffold を追加。
- Runtime、Editor、Edit Mode Test、Play Mode Test の assembly 境界を追加。
- `.klib`を`KesKlibAsset`へ、`manifest.kson`を`KesBuildAsset`へ変換するScriptedImporterを追加。
- `KesManager`へKES Build Asset参照を追加。
- Unity packageと.NET Runtime CoreでKlibモデル・loader・診断ソースの共有を開始。
- Klib importerを完全なsection構造検証へ対応し、メモリ上のKlib assetを直接読み込めるようにした。
