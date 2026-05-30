# Brief: kes-k-intermediate-representation-spec

## Problem

CLI、VM、Windows/Unity/Unreal runtime の実装者が、`.ke` から生成される中間表現 `.k` の契約を共有できていない。
現状の CLI 仕様は `.k` を VM 実行用の中間表現として扱うことだけを定義しており、ファイル形式、命令表現、manifest との接続が未定義であるため、compiler と runtime/VM を独立して実装しづらい。

## Current State

`docs/spec/cli-tool-spec.md` は `kes build` が `.ke` を `.k` に変換し、`manifest.json` とともに runtime へ渡す方針を定義している。
`docs/spec/windows-runtime-spec.md` は runtime が manifest と中間表現を読み、VM を通じて命令を実行することを前提としている。
一方で、`.k` の具体的なファイル形式、命令列、値表現、ラベル・ジャンプ・選択肢・syscall の表現、manifest 内での参照方法はまだ仕様化されていない。

## Desired Outcome

`.k` の仕様書が追加され、少なくとも次が明確になる。

- `.k` のファイル形式、文字エンコーディング、バージョン、安定性方針。
- VM が実行できる命令表現、制御フロー、値、変数、呼び出し、テキスト表示、選択肢、syscall の表現。
- debug/source map に必要な元 `.ke` の file/line/column 情報の扱い。
- `manifest.json` が `.k` をどのように列挙し、entry や asset/locale とどう関連付けるか。
- 仕様が CLI/VM/runtime 実装のテスト観点として使えること。

## Approach

新しい仕様書 `docs/spec/k-intermediate-representation-spec.md` を追加する方針とする。
最初の仕様では、人間がレビューしやすく golden test にも使いやすい JSON Lines または JSON 系のテキスト形式を候補として扱い、命令集合と manifest 参照契約を先に固定する。
バイナリ最適化、圧縮、暗号化は将来の publish/runtime 最適化として分離し、今回の仕様では VM 実行契約の安定性を優先する。

## Scope

- **In**: `.k` ファイル形式、命令表現、値表現、制御フロー、source mapping、manifest との関係、互換性/version 方針、最小サンプル。
- **Out**: `.k` を生成する compiler 実装、VM 実装、runtime 描画/音声実装、asset manifest の完全スキーマ、ローカライズ辞書の詳細形式、配布時の圧縮・暗号化。

## Boundary Candidates

- `.ke` AST/semantic model から VM 命令列へ落とす compiler contract。
- VM が読み込む `.k` の file format と instruction schema。
- `manifest.json` が `.k` と entry、asset、locale を関連付ける runtime input contract。
- source map と diagnostics/debugger が参照する位置情報 contract。

## Out of Boundary

- `kes build` が実際に `.k` を生成する処理の実装。
- VM が命令を実行する処理の実装。
- Windows/Unity/Unreal runtime 固有の画面・音声・入力処理。
- STL の全 syscall 実装詳細。
- `.ke` / `.kel` 言語仕様そのものの拡張。

## Upstream / Downstream

- **Upstream**: `docs/spec/kes-language-spec.md`、`docs/spec/kes-language-stl-spec.md`、`docs/spec/kel-file-spec.md`、`docs/spec/cli-tool-spec.md`、既存の semantic diagnostics specs。
- **Downstream**: `.k` emitter 実装、VM 実装、manifest 生成、Windows runtime、Unity runtime、Unreal runtime、CLI golden tests、debug tooling。

## Existing Spec Touchpoints

- **Extends**: `docs/spec/cli-tool-spec.md` の `kes build` 成果物説明に `.k` 仕様書への参照を追加する可能性がある。
- **Adjacent**: `docs/spec/windows-runtime-spec.md` の manifest/VM 連携、`docs/spec/kes-language-stl-spec.md` の `__systemcall__` と flow 構文、`docs/spec/kel-file-spec.md` の entry/chapter 参照。

## Constraints

`.kiro/specs/**` と `docs/` 配下のドキュメントは日本語で記述する。
既存コードと CLI 仕様で現在使われている `.ke` / `.k` の表記を優先し、古い overview/runtime 文書に残る `.kc` / `.klib` 表記とは互換性・移行観点として整合を取る。
初期仕様はレビュー容易性とテスト容易性を重視し、将来のバイナリ形式へ移行できる version/header 方針を含める。
