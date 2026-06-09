# Implementation Plan

- [x] 1. Foundation: headless VM の共有状態モデルを固める
- [x] 1.1 実行状態、停止理由、fault payload の共通モデルを定義する
  - `NotStarted`、`Running`、`WaitingForAdvance`、`WaitingForSelection`、`Completed`、`Faulted` を区別できる状態表現を追加する。
  - 選択待ちで必要な choice 一覧、再開基点、fault 時の script id と source 情報の保持方法を統一する。
  - session と executor が同じ state 契約を参照し、停止理由をテストから判定できる状態になっている。
  - *Requirements: 1.4, 2.1, 2.2, 2.4, 3.1, 3.3, 3.4*

- [x] 1.2 観測ログと選択肢 payload の共通モデルを定義する
  - `say` の話者名と本文、`nar` の本文、`select` の選択肢一覧を保持する観測モデルを追加する。
  - 直近イベントと累積 transcript のどちらも確認できる形に整理する。
  - runtime UI を起動しなくても、テキスト進行と choice 内容を state から読み取れる状態になっている。
  - *Requirements: 1.3, 2.2, 3.2, 4.4*

- [x] 2. Core: `.klib` を headless に実行できるようにする
- [x] 2.1 (P) 命令列の逐次実行と分岐解決を実装する
  - `KlibDocument` を受け取り、命令ポインタを進めながら `label`、`jump`、`END` を正しい停止境界まで評価する。
  - 無効な offset、未知 opcode、欠落 label を fault 状態へ正規化する。
  - 有効な `.klib` を渡すと、完了または待機理由つき停止まで executor 単体で進行できる。
  - *Requirements: 1.1, 1.2, 1.4, 3.4*
  - *Boundary: HeadlessVmExecutor*

- [x] 2.2 (P) session の開始 API と再開 API を実装する
  - `Start`、`ResumeAdvance`、`ResumeSelection` から state 遷移を管理する session を追加する。
  - 再開不能な状態での API 誤用は実行 fault と混同せず拒否する。
  - テストコードから session の current state と observation を一貫して参照できる状態になっている。
  - *Requirements: 1.1, 2.1, 2.3, 2.4, 3.1, 3.3, 4.3*
  - *Boundary: HeadlessVmSession*

- [x] 2.3 text / select の待機と観測更新を executor と session に統合する
  - `say` と `nar` で観測ログを更新したうえで `WaitingForAdvance` へ遷移する処理を追加する。
  - `SELECT` で choice payload を生成し、選択結果に応じて対応先へ再開する処理を追加する。
  - 入力や選択結果が与えられない限り自動進行せず、待機状態が維持される。
  - *Requirements: 1.2, 1.3, 2.1, 2.2, 2.3, 2.4, 3.2, 3.3*

- [x] 3. Integration: CLI テストから headless VM を起動できる基盤をつなぐ
- [x] 3.1 build fixture から `KlibDocument` と session を生成する test helper を追加する
  - 既存 `TemporaryProject` と build 経路を使って、VM テストが再利用できる fixture helper を追加する。
  - broad-surface または最小専用シナリオから headless session を起動する入口を 1 つにまとめる。
  - CLI test project から runtime 起動なしで headless VM 実行を始められる状態になっている。
  - *Requirements: 1.1, 4.1, 4.3, 4.4*

- [x] 4. Validation: headless VM の受け入れ条件を自動テストで固定する
- [x] 4.1 session / executor の状態遷移と fault を検証するテストを追加する
  - `say` / `nar` の待機、`select` の待機、invalid selection、fault payload を個別に検証する。
  - 停止理由と観測結果を UI ではなく state / observation から判定する assertion を追加する。
  - 対象テストを実行すると、待機・完了・失敗の各状態が再現付きで確認できる。
  - *Requirements: 2.1, 2.2, 2.4, 3.1, 3.2, 3.3, 3.4, 4.2*
  - *Depends: 2.3, 3.1*

- [x] 4.2 build から resume 完了までの統合フローを検証するテストを追加する
  - build 済みシナリオから session を開始し、`ResumeAdvance` と `ResumeSelection` で `END` まで完了するフローを検証する。
  - choice に応じて分岐先が変わることと、手動入力なしでは先へ進まないことを固定する。
  - `dotnet test` で headless VM テストが CI 向けに完走し、runtime や UI 起動を要求しない。
  - *Requirements: 1.2, 1.3, 1.4, 2.3, 3.1, 4.1, 4.2, 4.3, 4.4*
  - *Depends: 2.3, 3.1*
