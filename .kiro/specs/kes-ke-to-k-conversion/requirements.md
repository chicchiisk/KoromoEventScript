# Requirements Document

## Introduction

この仕様は、KoromoEventScript の `.kc` AST から VM 実行用中間表現 `.klib` document への変換を定義する。
CLI / compiler 実装者と VM 実装者が、既存の `.klib` 中間表現仕様に沿った成果物を同じ前提で生成・検証できるように、`say`、`nar`、`select`、`jump`、通常命令の変換、制御先解決、最小 source mapping、golden test 可能な安定出力を要求として固定する。

## Boundary Context

- **In scope**: `.kc` AST から `.klib` document への変換、`say` / `nar` / `select` / `jump` / 通常命令の opcode 生成、label から instruction index への解決、最小 source mapping、deterministic output、golden test または snapshot test による検証。
- **Out of scope**: VM interpreter、runtime 実装、manifest 全体の生成、asset / locale 実体解決、式評価・変数・関数・制御構文全体の完全 lowering、`.klib` schema validator、既存 `.kc` / `.klib` 表記の全面移行。
- **Adjacent expectations**: `.klib` の file format、instruction schema、opcode、manifest 参照契約は `docs/spec/k-intermediate-representation-spec.md` を正とする。既存 parser、AST、semantic diagnostics の公開挙動は、この仕様で不要に変更しない。

## Requirements

### Requirement 1: 変換対象と入力前提

**Objective:** As a CLI / compiler 実装者, I want `.kc` AST から `.klib` document を生成できる, so that VM 向け成果物生成を `.klib` 仕様に接続できる

#### Acceptance Criteria

1. When 検証済み `.kc` AST が変換対象として渡される, the `.kc` to `.klib` converter shall `.klib` 中間表現仕様に沿った `.klib` document を生成する
2. The `.kc` to `.klib` converter shall `say`、`nar`、`select`、`jump`、通常命令を変換対象として扱う
3. If 変換対象外の構文が入力に含まれる, the `.kc` to `.klib` converter shall その構文を暗黙に成功扱いせず、変換対象外であることを利用者が判別できる結果を返す
4. While 既存 semantic diagnostics が入力の名前解決、型、重複、未定義参照を扱う, the `.kc` to `.klib` converter shall それらの診断ルールを置き換えない

### Requirement 2: `.klib` document の基本契約

**Objective:** As a VM 実装者, I want 変換結果が `.klib` の基本 document 契約を満たす, so that VM loader が同じ前提で成果物を読める

#### Acceptance Criteria

1. When `.klib` document が生成される, the `.kc` to `.klib` converter shall `format`、`version`、`features`、`module`、`imports`、`instructions`、`labels`、`manifestRefs`、`debug` を含む成果物を出力する
2. The `.kc` to `.klib` converter shall `.klib` 中間表現仕様で定義された現行 format identifier と version 情報を成果物に含める
3. The `.kc` to `.klib` converter shall instruction index を 0 から始まる連続した順序として出力する
4. The `.kc` to `.klib` converter shall 同じ入力と同じ変換条件から同じ比較可能な `.klib` 出力を生成する
5. If source に import が含まれない, the `.kc` to `.klib` converter shall 空の import 情報を正規化された形で出力する

### Requirement 3: text と通常命令の変換

**Objective:** As a script author, I want `say`、`nar`、通常命令が `.klib` の命令列に反映される, so that 書いたイベントの表示命令と runtime action が VM 成果物に残る

#### Acceptance Criteria

1. When `say` statement が変換される, the `.kc` to `.klib` converter shall 話者と本文を保持した `say` instruction を生成する
2. When `nar` statement が変換される, the `.kc` to `.klib` converter shall 本文を保持した `nar` instruction を生成する
3. When `say` または `nar` に tag が付与されている, the `.kc` to `.klib` converter shall tag を実行開始または参照に利用できる label 情報として反映する
4. When 通常命令 statement が変換される, the `.kc` to `.klib` converter shall 命令名と引数を保持した command instruction を生成する
5. If text block が複数行を含む, the `.kc` to `.klib` converter shall 行順を保持した比較可能な text 表現を成果物に含める

### Requirement 4: 制御フロー変換

**Objective:** As a VM 実装者, I want `label`、`jump`、`select` の制御先が `.klib` 上で解決される, so that runtime が `.kc` の label 探索を再実行せずに分岐できる

#### Acceptance Criteria

1. When `label` statement が変換される, the `.kc` to `.klib` converter shall label 名と対応する instruction index を `labels` に出力する
2. When `jump` statement が変換される, the `.kc` to `.klib` converter shall jump 先を instruction index として参照できる instruction を生成する
3. When `select` statement が変換される, the `.kc` to `.klib` converter shall 各 case の表示 text と遷移先を保持した select instruction を生成する
4. If `jump` または `select` case の遷移先 label が解決できない, the `.kc` to `.klib` converter shall 未解決の制御先を成功した `.klib` 出力として残さない
5. While `.klib` document が実行順序を表す, the `.kc` to `.klib` converter shall 通常実行順序と明示的な制御移動を区別できる命令列を出力する

### Requirement 5: source mapping と debug 情報

**Objective:** As a CLI / debug tooling 実装者, I want `.klib` instruction が元 `.kc` 位置へ戻れる, so that golden test、runtime error、debug 表示で変換元を確認できる

#### Acceptance Criteria

1. When `.kc` statement が `.klib` instruction に変換される, the `.kc` to `.klib` converter shall 可能な範囲で元 `.kc` の file、line、column を参照できる source mapping を出力する
2. When 1 つの `.kc` statement が複数の `.klib` instruction に対応する, the `.kc` to `.klib` converter shall 各 instruction と元 statement の関係を debug 情報として追跡できるようにする
3. If source location が取得できない生成物が必要になる, the `.kc` to `.klib` converter shall VM 実行意味に影響しない fallback debug 情報または `null` source を出力する
4. The `.kc` to `.klib` converter shall source mapping の有無によって opcode、operand、制御フローの意味を変えない

### Requirement 6: manifest 参照と隣接成果物の境界

**Objective:** As a build pipeline 実装者, I want `.klib` が manifest と照合できる最小参照を持つ, so that 後続の package / runtime 作業が成果物を接続できる

#### Acceptance Criteria

1. When `.klib` document が生成される, the `.kc` to `.klib` converter shall 自身の script 参照を manifest と照合できる形で成果物に含める
2. If `.klib` instruction が asset ID または locale key を参照する表現を含む, the `.kc` to `.klib` converter shall 参照 ID または key を manifest 参照情報として追跡できるようにする
3. The `.kc` to `.klib` converter shall manifest が所有する artifact path、hash、asset 実体、locale 本文、runtime metadata の完全情報を `.klib` 成果物の必須情報として要求しない
4. Where manifest 生成が別機能として扱われる, the `.kc` to `.klib` converter shall `.klib` 単体で比較可能な変換結果を提供する

### Requirement 7: golden test 可能性

**Objective:** As a reviewer, I want 変換結果を安定して比較できる, so that `.kc` から `.klib` への変換の差分をレビューできる

#### Acceptance Criteria

1. When 代表的な `say`、`nar`、`select`、`jump`、通常命令を含む入力が変換される, the `.kc` to `.klib` converter shall golden test または snapshot test で比較できる出力を生成する
2. When 同じ test input が複数回変換される, the `.kc` to `.klib` converter shall timestamp、環境依存 path、非決定的順序によって差分を発生させない
3. If 変換結果が期待値と異なる, the test suite shall 差分を reviewer が確認できる形で失敗を報告する
4. The test suite shall Issue #25 の受け入れ条件である `say`、`nar`、`select`、`jump`、通常命令の変換を検証する
