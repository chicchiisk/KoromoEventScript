# .k 中間表現仕様

この文書は、KoromoEventScript の `.ke` から生成される VM 実行用中間表現 `.k` の公開契約を定義する仕様である。

## 目的

`.k` は CLI が `.ke` を解析・検証した後に生成し、VM と runtime がイベント実行時に参照する中間表現である。
本仕様は、CLI、VM、runtime、debug tooling の実装者とレビュー担当者が、`.k` の責務境界と隣接仕様との関係を同じ前提で確認できるようにする。

この文書は段階的に拡張する。現時点では、`.k` 中間表現仕様が扱う範囲、扱わない範囲、参照すべき隣接仕様、現行用語と旧称の関係に加えて、基本 file format と compatibility policy を定義する。instruction schema、value model、source mapping、manifest 参照契約の詳細は後続タスクでこの文書へ追記する。

## 基本ファイル形式

_Requirements: 1.1, 1.3_

`.k` は、`.ke` から生成される VM 実行用の中間表現ファイルである。拡張子は `.k` とし、runtime package や build output の中では `manifest.json` などの隣接成果物から参照される script artifact として扱う。

`.k` の基本ファイル形式は次の通りである。

| 項目 | 仕様 |
|------|------|
| 目的 | CLI / compiler が検証済み `.ke` を VM/runtime が読み込める実行契約へ正規化する。 |
| 拡張子 | `.k`。旧称 `.klib` は新規仕様では使用しない。 |
| 文字エンコーディング | UTF-8。BOM なしを正規形とする。VM/runtime は UTF-8 として復号できない `.k` を format load error として読み込み失敗にする。 |
| 改行 | LF を正規形とする。CRLF は読み込み時に LF と同等に扱ってよいが、golden test や正規化出力では LF を用いる。 |
| top-level document identification | top-level object の `format` に固定値 `koromo.k` を持つ。VM/runtime は `format` が存在しない、文字列でない、または `koromo.k` でない場合、format load error として読み込み失敗にする。 |

`.k` document は top-level object として識別される。少なくとも compatibility 判定に必要な `format`、`version`、`features` を持つ。

```json
{
  "format": "koromo.k",
  "version": { "major": 1, "minor": 0, "patch": 0 },
  "features": []
}
```

`version` は `.k` document contract の互換性判定情報であり、`major`、`minor`、`patch` を非負整数として表す。`features` は、この `.k` を正しく読み込み、実行前検証するために VM/runtime が対応している必要がある feature identifier の配列である。feature identifier は ASCII の安定した文字列とし、具体的な feature 名は各 feature を導入する仕様更新で定義する。

## 互換性ポリシー

_Requirements: 1.1, 1.3_

VM/runtime は `.k` の命令実行を開始する前に、少なくとも `format`、`version`、`features` を検証する。この pre-load check に失敗した `.k` は実行してはならない。

| 検証対象 | 読み込み側の期待動作 |
|----------|----------------------|
| `format` | `koromo.k` 以外、欠落、型不一致、top-level object でない document は format load error として読み込み失敗にする。 |
| `version.major` | VM/runtime が対応する major version と一致しない未知 major version は compatibility load error として読み込み失敗にする。 |
| `version.minor` / `version.patch` | 対応 major の範囲内では後方互換を前提とする。ただし、読み込み側が必要な minor/patch 契約を満たせない場合は compatibility load error として読み込み失敗にし、必要 version を診断へ含める。 |
| `features` | 配列内に未対応 feature が 1 つでも含まれる場合、unsupported feature の compatibility load error として読み込み失敗にする。診断には未対応 feature identifier を含める。 |

未知 major version は、同名 field が存在しても意味論、命令 schema、値表現、source mapping、manifest 参照契約が互換とは限らないため、VM/runtime は推測して実行してはならない。unsupported feature も、読み込み側が該当 feature の検証規則または実行前提を保証できないことを意味するため、feature を無視して実行してはならない。

Format errors と compatibility errors はどちらも load error であり、VM/runtime の命令実行前に発生する。Format errors は `.k` document として識別または復号できない問題、compatibility errors は document は識別できるが `version` または `features` の契約を読み込み側が満たせない問題として区別する。

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
