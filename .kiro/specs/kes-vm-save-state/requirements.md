# Requirements Document

## Introduction

この仕様では、KoromoEventScript のコンパイラ開発者と runtime 実装者が、セーブ/ロードで保存すべき VM 状態の範囲を一貫して扱える状態を定義する。利用者は、実行位置、変数、スタック、選択待ちなどの VM 実行継続に必要な状態を、画面表示や音声のような runtime 固有状態から分離して定義し、後続の save/load 実装でシリアライズ可能な契約として参照できる必要がある。

## Boundary Context (Optional)

- **In scope**: save/load が参照する VM 実行位置、変数状態、継続に必要なスタック状態、入力待ちや選択待ちの pending 状態、VM 状態の保存対象と除外対象の明確化、シリアライズ可能な保存契約の要件定義。
- **Out of scope**: Windows / Unity / Unreal runtime の画面描画状態、音声状態、既読情報、セーブ UI、保存先、暗号化方式、セーブスロット管理、save/load コマンドや serializer の実装詳細。
- **Adjacent expectations**: `.klib` 仕様は save/load が参照する `scriptId` と実行位置の安定識別子を所有し、runtime 仕様は画面状態や音声状態を含む完全なセーブデータを所有する。この仕様はそのうち VM 状態として保存すべき範囲だけを定義する。

## Requirements

### Requirement 1: 保存対象となる VM 継続状態の定義

**Objective:** As a コンパイラ開発者, I want セーブ対象となる VM の継続状態を明確にしたい, so that save/load 実装が復元に必要な情報を欠かさず参照できる

#### Acceptance Criteria

1. The KES VM Save State Contract shall セーブ対象の VM 状態として、少なくとも実行中 script の識別子と再開位置を含める。
2. The KES VM Save State Contract shall セーブ対象の VM 状態として、少なくとも保存対象スコープの変数状態を含める。
3. When VM 実行が関数呼び出し、分岐復帰、または評価途中の継続情報を保持している, the KES VM Save State Contract shall その実行継続に必要なスタックまたは継続状態を保存対象として含める。
4. When VM 実行が入力待ちまたは選択待ちで停止している, the KES VM Save State Contract shall 復元後に同じ待機状態へ戻るために必要な pending 状態を保存対象として含める。

### Requirement 2: runtime 固有状態との責務分離

**Objective:** As a コンパイラ開発者, I want VM 状態と runtime 固有状態の境界を分けたい, so that VM 保存契約を描画や音声の仕様変更から独立して維持できる

#### Acceptance Criteria

1. The KES VM Save State Contract shall 画面描画、音声再生、サムネイル、既読情報、セーブメタ情報を VM 保存対象そのものとしては扱わない。
2. When save/load の完全なユーザー体験に画面状態や音声状態が必要である, the KES VM Save State Contract shall それらを隣接する runtime 保存仕様の責務として区別できるようにする。
3. If ある状態が VM 実行継続ではなく runtime 表示復元のためだけに必要である, then the KES VM Save State Contract shall その状態を必須の VM 保存対象に含めない。
4. Where runtime 側で完全セーブデータが構成される, the KES VM Save State Contract shall その一部として組み合わせられても VM 部分だけを独立に識別できるようにする。

### Requirement 3: シリアライズ可能な保存契約

**Objective:** As a コンパイラ開発者, I want 保存対象の VM 状態をシリアライズ可能な契約として定義したい, so that 後続の save/load 実装が永続化形式を選んでも互換な情報を扱える

#### Acceptance Criteria

1. The KES VM Save State Contract shall 保存対象を値として表現できる情報で定義し、実行中プロセス内でしか意味を持たない参照を必須前提にしない。
2. When VM 状態が実行位置を表す, the KES VM Save State Contract shall 配布物上の一時パスやデバッグ専用情報ではなく、安定した識別子で参照できるようにする。
3. If ある VM 内部値が保存時点でシリアライズ不能である, then the KES VM Save State Contract shall その値を保存対象から除外するか、保存不能として識別できる扱いを要求する。
4. While save/load 実装方式が未確定である, the KES VM Save State Contract shall 特定のファイル形式、エンコーディング、serializer 実装に依存しない。

### Requirement 4: 復元可能性と検証前提

**Objective:** As a runtime 実装者, I want 保存された VM 状態が復元可能であることを確認したい, so that load 時に同じ実行継続点へ安全に戻せる

#### Acceptance Criteria

1. When 保存済みの VM 状態が load に渡される, the KES VM Save State Contract shall 同じ script と再開位置を特定できるだけの情報を含める。
2. When 保存済みの VM 状態が選択待ちや入力待ちから復元される, the KES VM Save State Contract shall 復元後の待機理由と再開条件を判定できる情報を含める。
3. If 保存済みの VM 状態が参照する script 識別子または再開位置が無効である, then the KES VM Save State Contract shall load 側が不正な保存状態として検出できる前提を持つ。
4. The KES VM Save State Contract shall 復元時に debug 用ソースマップだけへ依存せず、VM 実行継続に必要な情報を保存状態自身から判断できるようにする。
