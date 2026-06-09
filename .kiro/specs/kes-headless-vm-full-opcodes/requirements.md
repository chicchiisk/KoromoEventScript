# Requirements Document

## Introduction

この仕様では、KoromoEventScript のコンパイラ開発者と CLI 利用者が、仕様書で定義された言語機能をヘッドレス VM だけで実行・検証できる状態を定義する。現在のヘッドレス VM は対応 opcode が限定的であり、`.klib` に含まれる正当な命令でも unsupported fault になりうるため、言語機能の回帰確認や CLI ベースの自動検証を headless 環境だけで完結しにくい。利用者は、仕様に定義されたすべての opcode 群をヘッドレス VM が解釈し、式評価、制御フロー、関数呼び出し、配列、クラス、シナリオ進行を UI runtime なしで再現できることを必要としている。

## Boundary Context (Optional)

- **In scope**: `.klib` 仕様で定義された全 opcode の headless 実行、値スタックと変数状態の継続、制御フロー、関数/メソッド/配列/クラス操作、syscall ベースのシナリオ進行、待機と再開、headless 実行結果の観測、unsupported opcode 由来の制限解消。
- **Out of scope**: `.klib` 命令体系そのものの再設計、Windows / Unity / Unreal runtime の描画や音声再生の完全再現、公開言語仕様や STL 仕様の意味変更、save/load 契約の拡張、配布形式や manifest 仕様の変更。
- **Adjacent expectations**: opcode の正規意味、オペランド規約、値表現は `docs/spec/k-intermediate-representation-spec.md` が所有し、STL と runtime 仕様は syscall 名と演出効果の意味を所有する。この仕様は、それら既存契約に従う `.klib` を headless VM が実行できることを要求する。

## Requirements

### Requirement 1: 全 opcode 群を対象にした headless 実行

**Objective:** As a コンパイラ開発者, I want 仕様定義済み opcode を headless VM が一通り実行できるようにしたい, so that 有効な `.klib` を unsupported 制限なしで検証できる

#### Acceptance Criteria

1. When ヘッドレス VM が対象バージョンの `.klib` 仕様で定義された opcode を含む実行用中間表現を読み込む, the KES Headless VM shall その opcode を未実装扱いで拒否せず、仕様上の実行意味に従って評価する。
2. When 実行がスタック操作、変数操作、算術、比較、論理、制御フロー、呼び出し、配列、クラス、syscall の各 opcode 群に到達する, the KES Headless VM shall opcode 群ごとの実行結果を後続命令へ引き継げる状態に更新する。
3. If `.klib` に対象仕様バージョンで未定義の opcode 値が含まれる, then the KES Headless VM shall 仕様外命令であることを識別できる fault として停止する。
4. The KES Headless VM shall 対応 opcode の拡張後も、既存の start、resume、completed、faulted の headless 実行契約を維持する。

### Requirement 2: 値スタック、式評価、変数状態の再現

**Objective:** As a コンパイラ開発者, I want 式評価と変数更新が headless でも言語仕様どおり再現されてほしい, so that CLI ベースの検証で計算結果や状態遷移を信用できる

#### Acceptance Criteria

1. When `PUSH_*`、`POP`、`DUP` が実行される, the KES Headless VM shall 仕様どおりに値スタックを更新し、その結果を後続 opcode から利用できるようにする。
2. When `ADD`、`SUB`、`MUL`、`DIV`、`NEG`、`EQ`、`NEQ`、`LT`、`LE`、`GT`、`GE`、`AND`、`OR`、`NOT` が実行される, the KES Headless VM shall `.klib` と言語仕様に整合する評価結果を生成する。
3. When `DEF_VAR`、`LOAD_VAR`、`STORE_VAR` が実行される, the KES Headless VM shall `.klib` に記録された変数識別子、型、スコープに対応する値状態を更新し、その後の命令から同じ実行文脈として参照できるようにする。
4. If 式評価または変数操作に必要なオペランド、変数参照、または値形状が不正である, then the KES Headless VM shall 失敗原因を識別できる fault として停止する。

### Requirement 3: 制御フロー、待機、再開の完全性

**Objective:** As a CLI 利用者, I want 分岐や待機を含むシナリオ進行を headless で最後までたどりたい, so that UI runtime なしで実行経路を自動確認できる

#### Acceptance Criteria

1. When `JUMP`、`JUMP_FALSE`、`LABEL`、`END` が実行される, the KES Headless VM shall `.klib` の offset 規約に従って実行位置と完了状態を更新する。
2. When `SELECT` が実行される, the KES Headless VM shall 選択肢と待機理由を headless で観測可能な形で保持し、外部入力が与えられるまで自動進行しない。
3. When headless 実行が待機状態から再開される, the KES Headless VM shall 停止時点の実行位置と pending 情報に対応する次の実行地点から継続する。
4. If 分岐先 offset、選択肢遷移先、または再開条件が `.klib` と整合しない, then the KES Headless VM shall 無効な制御フローとして fault に正規化する。

### Requirement 4: 呼び出し、配列、クラス機能の言語実行

**Objective:** As a コンパイラ開発者, I want 関数呼び出しやデータ構造操作まで headless VM で扱いたい, so that 言語機能全体を runtime 非依存に回帰確認できる

#### Acceptance Criteria

1. When `CALL`、`CALL_VOID`、`SYSCALL`、`SYSCALL_VOID`、`CALL_METHOD`、`CALL_METHOD_VOID` が実行される, the KES Headless VM shall `.klib` の引数順と戻り値契約に従って呼び出し結果を後続命令へ反映する。
2. When `ARRAY_NEW`、`ARRAY_GET`、`ARRAY_SET` が実行される, the KES Headless VM shall 要素順、読取結果、更新結果を後続の式評価と命令実行から観測できるようにする。
3. When `NEW`、`GET_FIELD`、`SET_FIELD`、`DISPOSE` が実行される, the KES Headless VM shall クラスインスタンスとメンバー状態の変化を言語機能の継続実行に必要な範囲で再現する。
4. If 呼び出し対象、配列 index、フィールド参照、メソッド参照、または dispose 条件が実行時に不正である, then the KES Headless VM shall 原因を識別できる fault として停止する。

### Requirement 5: headless での観測可能性と runtime 非依存性

**Objective:** As a CLI 利用者, I want runtime 演出を伴う言語機能も headless で検証可能であってほしい, so that CI とローカル検証を同じ headless 実行基盤で回せる

#### Acceptance Criteria

1. When syscall ベースのシナリオ命令または runtime 連携命令が実行される, the KES Headless VM shall 実行継続に必要な結果、待機、または観測可能なイベントを UI runtime なしで扱える形に正規化する。
2. Where 命令効果が描画や音声再生のような headless 環境で直接再現できない機能を含む, the KES Headless VM shall その非再現性だけを理由に実行不能とせず、少なくとも後続の言語進行とテスト観測を成立させる。
3. When 有効な `.klib` が仕様で定義された言語機能だけを利用している, the KES Headless VM shall opcode 未対応を理由に fault せず、完了または仕様上の待機状態まで進行できる。
4. The KES CLI の headless 実行利用口 shall Windows / Unity / Unreal runtime を起動せずに、言語機能全体を使ったスクリプトの検証に利用できる。
