# .k 中間表現仕様

この文書は、KoromoEventScript の `.ke` から生成される VM 実行用中間表現 `.k` の公開契約を定義する仕様である。

## 目的

`.k` は CLI が `.ke` を解析・検証した後に生成し、VM と runtime がイベント実行時に参照する中間表現である。
本仕様は、CLI、VM、runtime、debug tooling の実装者とレビュー担当者が、`.k` の責務境界と隣接仕様との関係を同じ前提で確認できるようにする。

この文書の初期骨格では、`.k` 中間表現仕様が扱う範囲、扱わない範囲、参照すべき隣接仕様、現行用語と旧称の関係を定義する。具体的な file format、instruction schema、value model、source mapping、manifest 参照契約は後続タスクでこの文書へ追記する。

## 対象読者

- `.ke` から `.k` を生成する CLI / compiler 関連機能の設計者と実装者。
- `.k` を読み込んで命令列を実行する VM 実装者。
- `manifest.json` と `.k` を組み合わせて実行資産を解決する runtime 実装者。
- runtime error、debug 表示、golden test、仕様レビューを担当する開発者。

## 適用範囲

本仕様は、`.k` 中間表現の公開契約を所有する。

- `.k` ファイルの目的、基本構造、version、feature compatibility。
- `.ke` 入力、`.kel` entry、`manifest.json`、runtime が読む成果物との関係。
- VM が参照する命令表現、値表現、制御フロー、実行位置、source mapping の契約。
- asset ID、locale key、script path など、manifest が所有する情報への参照関係。
- 既存仕様と用語または拡張子が不整合な場合に、現行の正とする用語を示す導線。

## 非対象範囲

この文書だけを読んでも、次の作業が本仕様の対象外であることが分かるようにする。

- compiler 実装、`.k` emitter 実装、serializer 実装、schema validator 実装。
- VM 実装、VM interpreter 実装、命令ディスパッチや save/load の実装詳細。
- runtime 実装、描画、音声、入力、UI、プラットフォーム固有の配布処理。
- asset manifest 全体、locale dictionary、runtime package manifest の完全な schema。
- 配布時の圧縮、暗号化、署名、改ざん検出、binary format。
- 既存文書に残る `.kc` / `.klib` 表記の一括置換。

## 現行用語と旧称

現行の authoritative term は次の通りである。

| 種別 | 現行用語 | 扱い |
|------|----------|------|
| イベントスクリプト入力 | `.ke` | KoromoEventScript 言語で記述された現在の正規入力拡張子。 |
| VM 実行用中間表現 | `.k` | `.ke` から生成される現在の正規中間表現拡張子。 |
| 旧イベントスクリプト表記 | `.kc` | 旧称または移行前の表記。新規仕様では `.ke` を正とする。 |
| 旧中間表現表記 | `.klib` | 旧称または移行前の表記。新規仕様では `.k` を正とする。 |

既存仕様に `.kc` / `.klib` が残っている場合でも、本仕様では `.ke` / `.k` を正とする。既存文書との用語差分は、必要に応じて別 Issue または後続タスクで移行する。

## 隣接仕様

本仕様は、次の仕様を参照する。詳細責務は各仕様に委譲し、`.k` 仕様は VM 実行用中間表現の契約に集中する。

| 仕様 | 本仕様から見た関係 |
|------|--------------------|
| `docs/spec/cli-tool-spec.md` | `kes build`、`kes run`、`kes publish` が `.ke` / `.kel` を扱い、`.k` と `manifest.json` を生成または runtime に渡す成果物契約を定義する。 |
| `docs/spec/kes-language-spec.md` | `.ke` の構文、名前、型、変数、制御構文、source position の前提を定義する。 |
| `docs/spec/kes-language-stl-spec.md` | `__systemcall__`、STL、runtime call、asset ID、actor、tag など、`.k` に反映される語彙の前提を定義する。 |
| `docs/spec/kel-file-spec.md` | `.kel` の entry、chapter、script path 参照の前提を定義する。 |
| `docs/spec/windows-runtime-spec.md` | Windows runtime が `manifest.json` と VM 成果物を読み込む隣接仕様である。 |
| `docs/spec/unity-runtime-spec.md` | Unity runtime が published data と VM 成果物を読み込む隣接仕様である。 |
| `docs/spec/unreal-runtime-spec.md` | Unreal runtime が published data と VM 成果物を読み込む隣接仕様である。 |
| `docs/spec/overview.md` | 読者が KoromoEventScript 全体像と各詳細仕様へ到達するための導線を持つ。 |

## 不整合の扱い

`.ke` / `.k` と `.kc` / `.klib` のように、既存仕様と本仕様の間で用語または拡張子が異なる場合は、本仕様では `.ke` / `.k` を正とする。

ただし、既存仕様の責務範囲をこの文書で直接変更しない。CLI、runtime、overview などの文書更新が必要な場合は、それぞれの Boundary を持つ後続タスクまたは別 Issue で扱う。
