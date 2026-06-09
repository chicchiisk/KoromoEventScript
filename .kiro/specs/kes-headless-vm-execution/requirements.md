# Requirements Document

## Introduction

この仕様では、KoromoEventScript の VM 実装者とテスト整備担当が、UI や runtime に依存せず VM 実行用中間表現を読み込み、命令列を順に実行できるヘッドレス実行環境を利用できる状態を定義する。利用者は VM の進行、入力待ち、選択肢処理をテストコードから観測・制御し、描画や実ランタイムの存在に左右されずに VM 挙動を検証できる必要がある。

## Boundary Context

- **In scope**: VM 実行用中間表現の読込、命令の順次実行、入力待ちや選択肢での停止と再開、テストコードからの制御、runtime 非依存の VM テスト実行。
- **Out of scope**: Windows / Unity / Unreal runtime の描画、音声、入力デバイス処理、セーブ UI、バックログ UI、配布成果物の publish 契約変更、IR 命令体系そのものの再設計。
- **Adjacent expectations**: VM 実行用中間表現の正規仕様は `.klib` であり、`.k` は旧称または移行前表記として扱う。中間表現のファイル形式、opcode、source mapping は既存の `.klib` 仕様が所有し、この仕様はそれを headless 実行で消費できることを要求する。

## Requirements

### Requirement 1: ヘッドレス VM の順次実行

**Objective:** As a VM 実装者, I want VM 実行用中間表現を headless に読み込んで順に実行したい, so that runtime なしでもシナリオ命令の進行を検証できる

#### Acceptance Criteria

1. When テストコードが有効な VM 実行用中間表現を与える, the KES Headless VM shall その中間表現を読み込んで実行を開始する。
2. When headless 実行が開始される, the KES Headless VM shall 命令列を定義順に評価し、停止条件に達するまで実行を進める。
3. When `say`、`nar`、`jump`、`label`、`select` のような制御やシナリオ進行に関わる命令が現れる, the KES Headless VM shall それぞれの命令意味に従って実行位置と進行状態を更新する。
4. If 実行が `END` または等価な完了状態へ到達する, then the KES Headless VM shall 完了したことをテストコードから判定できる状態で終了する。

### Requirement 2: 入力待ちと選択肢のテスト制御

**Objective:** As a テスト整備担当, I want 入力待ちや選択肢をテストから制御したい, so that 対話的な VM 進行も自動テストで再現できる

#### Acceptance Criteria

1. When headless 実行が入力待ちに到達する, the KES Headless VM shall その場で進行を停止し、外部から再開入力を与えられる状態へ遷移する。
2. When headless 実行が `SELECT` 相当の選択肢待ちに到達する, the KES Headless VM shall 利用可能な選択肢をテストコードから取得できるようにする。
3. When テストコードが選択肢の決定結果を与える, the KES Headless VM shall 対応する遷移先に従って実行を再開する。
4. If テストコードが必要な入力や選択結果をまだ与えていない, then the KES Headless VM shall 自動で先へ進まず待機状態を維持する。

### Requirement 3: 観測可能な実行状態

**Objective:** As a VM テスト作成者, I want headless 実行中の状態を観測したい, so that 命令実行結果と停止理由をテストで検証できる

#### Acceptance Criteria

1. The KES Headless VM shall 実行中、待機中、完了、失敗のような実行状態をテストコードから判定できるようにする。
2. When headless 実行がテキスト表示、地の文、選択肢、ジャンプ先変更などの観測可能な変化を起こす, the KES Headless VM shall その結果をテストコードから確認できる状態として保持する。
3. When headless 実行が停止する, the KES Headless VM shall その停止理由が完了・入力待ち・選択待ち・エラーのどれかを区別できるようにする。
4. If headless 実行がエラーで停止する, then the KES Headless VM shall テストコードが失敗原因を識別できる情報を提供する。

### Requirement 4: runtime 非依存の VM テスト実行

**Objective:** As a CI 利用者, I want runtime を起動せずに VM テストを実行したい, so that VM の回帰を軽量かつ安定して検証できる

#### Acceptance Criteria

1. The KES CLI test suite shall runtime や UI の起動を前提にせず headless VM テストを実行できる。
2. When headless VM テストが実行される, the KES CLI test suite shall 描画結果ではなく VM の進行状態と観測結果に基づいて合否を判定する。
3. When CI 環境で headless VM テストが実行される, the KES CLI test suite shall 対話的な手動入力を要求しない。
4. Where runtime 固有の表示や入力機能が必要なシナリオが存在する, the KES CLI test suite shall それらを headless VM の必須前提にしない。
