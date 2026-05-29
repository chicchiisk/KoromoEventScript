# Requirements Document

## Introduction

この仕様は、`.ke` から生成される VM 実行用中間表現 `.k` の公開契約を定義する。
CLI、VM、Windows/Unity/Unreal runtime の実装者が同じ成果物契約を参照できるように、`.k` のファイル形式、命令表現、値表現、制御フロー、source mapping、manifest との関係、互換性方針を仕様化する。

## Boundary Context

- **In scope**: `docs/spec/k-intermediate-representation-spec.md` の追加、`.k` ファイル形式、命令 schema、VM が必要とする実行情報、source map 情報、manifest 参照契約、互換性/version 方針、最小サンプル。
- **Out of scope**: `.k` emitter 実装、VM 実装、runtime 描画/音声/入力実装、asset manifest の完全 schema、ローカライズ辞書の詳細形式、配布時の圧縮・暗号化。
- **Adjacent expectations**: 既存の CLI 仕様、言語仕様、STL 仕様、`.kel` 仕様、runtime 仕様と矛盾しない用語と参照関係を維持する。

## Requirements

### Requirement 1: `.k` ファイル形式の定義

**Objective:** As a CLI/VM 実装者, I want `.k` ファイルの基本形式が定義されている, so that 生成側と読み取り側が同じ成果物契約を検証できる

#### Acceptance Criteria

1. The `.k` 中間表現仕様 shall `.k` ファイルの目的、拡張子、文字エンコーディング、改行、version 情報、互換性方針を定義する
2. The `.k` 中間表現仕様 shall `.k` ファイルが単一 `.ke` 入力に対応するかどうか、および複数ファイル project での扱いを定義する
3. The `.k` 中間表現仕様 shall runtime または VM が未知の version または未対応の feature を見つけた場合の期待される扱いを定義する
4. The `.k` 中間表現仕様 shall 人間レビューと golden test に利用できる最小の正規化例を含める

### Requirement 2: 命令表現と実行順序の定義

**Objective:** As a VM 実装者, I want `.k` 内の命令表現と実行順序が定義されている, so that VM が `.ke` の実行意味を再現できる

#### Acceptance Criteria

1. The `.k` 中間表現仕様 shall 命令列、命令 ID または opcode、引数、戻り値、実行順序を表す項目を定義する
2. The `.k` 中間表現仕様 shall `say`、`nar`、通常命令、式評価、変数定義、代入を VM が解釈できる形で表現するために必要な情報を定義する
3. The `.k` 中間表現仕様 shall `label`、`jump`、`select`、`case` の制御フロー表現とジャンプ先解決後の表現を定義する
4. The `.k` 中間表現仕様 shall `__systemcall__` または runtime 呼び出しに相当する命令の syscall ID、引数、戻り値利用の表現を定義する
5. The `.k` 中間表現仕様 shall import された `.ke` が VM 実行単位または命令列へどのように反映されるかを定義する

### Requirement 3: 値、型、変数、実行状態に必要な情報の定義

**Objective:** As a VM 実装者, I want `.k` が VM 状態に必要な値と変数情報を表現する, so that セーブ、ロード、分岐、式評価が一貫して扱える

#### Acceptance Criteria

1. The `.k` 中間表現仕様 shall number、bool、string、null、array、actor 参照、tag 参照を含む値表現を定義する
2. The `.k` 中間表現仕様 shall 変数の宣言、読み取り、書き込み、スコープ、初期値に必要な情報を定義する
3. The `.k` 中間表現仕様 shall VM が保存すべき実行位置、命令 index、コール状態、変数状態を参照できる情報を定義する
4. The `.k` 中間表現仕様 shall コンパイル時に解決済みであるべき名前、型、タグと、runtime に残るべき動的値の境界を明記する

### Requirement 4: source mapping と診断/debug 情報の定義

**Objective:** As a CLI/debug tooling 実装者, I want `.k` が元 `.ke` 位置へ戻れる情報を持つ, so that runtime エラー、debug 表示、golden test が元ソースと対応できる

#### Acceptance Criteria

1. The `.k` 中間表現仕様 shall 各命令または関連する命令群から元 `.ke` の file、line、column を参照できる source mapping 情報を定義する
2. The `.k` 中間表現仕様 shall LESS、`say`/`nar` 本文、`select`/`case` のように 1 つの構文が複数命令へ展開される場合の位置情報方針を定義する
3. The `.k` 中間表現仕様 shall runtime エラーまたは debug 表示で参照できる module/file 名と命令位置の表現を定義する
4. Where source mapping is included, the `.k` 中間表現仕様 shall source mapping が VM 実行意味を変えない補助情報であることを明記する

### Requirement 5: manifest との関係の定義

**Objective:** As a runtime 実装者, I want manifest と `.k` の関係が定義されている, so that runtime が entry、script、asset、locale を一貫して解決できる

#### Acceptance Criteria

1. The `.k` 中間表現仕様 shall `manifest.json` が `.k` ファイルを列挙または参照するために必要な script 情報を定義する
2. The `.k` 中間表現仕様 shall `.kel` の entry または chapter 参照が manifest と `.k` 実行開始位置へどのように対応するかを定義する
3. The `.k` 中間表現仕様 shall `.k` 内の asset ID、locale key、script path が manifest 上の情報とどのように対応するかを定義する
4. The `.k` 中間表現仕様 shall manifest が所有する情報と `.k` が所有する情報の重複または参照関係を明記する

### Requirement 6: 既存仕様との整合と非対象範囲の明示

**Objective:** As a reviewer, I want `.k` 仕様の範囲と隣接仕様との関係が明確である, so that 後続 Issue が過不足なく設計できる

#### Acceptance Criteria

1. The `.k` 中間表現仕様 shall `docs/spec/cli-tool-spec.md`、`docs/spec/kes-language-spec.md`、`docs/spec/kes-language-stl-spec.md`、`docs/spec/kel-file-spec.md`、runtime 仕様との参照関係を明記する
2. The `.k` 中間表現仕様 shall `.ke` / `.k` と、古い文書に残る `.kc` / `.klib` 表記の扱いまたは移行上の注意を明記する
3. The `.k` 中間表現仕様 shall compiler 実装、VM 実装、runtime 実装、配布時の圧縮・暗号化が本仕様の対象外であることを明記する
4. If 既存仕様と `.k` 仕様の間に用語または拡張子の不整合がある, the `.k` 中間表現仕様 shall どちらを正とするか、または別 Issue で扱うべきことを明記する
