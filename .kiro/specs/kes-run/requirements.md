# Requirements Document

## Introduction

`kes run` は、`kes init` で作成された KoromoEventScript プロジェクトを `kes.xml` 起点で実行する CLI コマンドである。本仕様では、`.kc` ファイル単体や `.kel` ファイル直接指定での実行を廃止し、プロジェクトルート、`Project.Entry`、ビルド成果物、Windows ランタイム起動の契約を利用者にとって一貫した形に整理する。

## Boundary Context

- **In scope**: `kes run [PROJECT_DIR]` のプロジェクト解決、`kes.xml` と `Project.Entry` に基づく実行対象解決、Windows target の実行前ビルド方針、既存ビルド成果物の検証、Windows ランタイムへの実行オプション転送、診断と終了コード。
- **Out of scope**: `.kc` 単体実行、`.kel` 直接実行、Unity / Unreal runtime の起動、Windows ランタイム内部の描画・音声・VM 実行意味、`kes publish` の配布物構成、`.kel` フォーマット変更。
- **Adjacent expectations**: `kes run` が生成または利用する `.klib` と `manifest.json` は `kes build --target windows` と同じ成果物契約に従う。ランタイム内での `.klib` 読み取り、イベント遷移、STL 実行は Windows ランタイム側の仕様に従う。

## Requirements

### Requirement 1: プロジェクトルート解決

**Objective:** As a CLI 利用者, I want `kes run` をプロジェクトディレクトリ単位で実行できる, so that `kes init` で作成した構造をそのまま起動できる

#### Acceptance Criteria

1. When `PROJECT_DIR` が指定されずに `kes run` が実行された, the KES CLI shall 現在ディレクトリまたは親ディレクトリから最初に見つかる `kes.xml` を使ってプロジェクトルートを解決する
2. When `PROJECT_DIR` が指定されて `kes run` が実行された, the KES CLI shall 指定ディレクトリ直下の `kes.xml` を使ってプロジェクトルートを解決する
3. If 解決対象のディレクトリに `kes.xml` が存在しない, then the KES CLI shall ファイルまたはディレクトリの入出力エラーとして診断し、ランタイムを起動しない
4. If `kes.xml` を読み取れない、または `kes.xml` が不正である, then the KES CLI shall CLI 診断を出力し、ランタイムを起動しない

### Requirement 2: 実行エントリポイント

**Objective:** As a プロジェクト作成者, I want 実行対象を `kes.xml` の `Project.Entry` で一元管理できる, so that CLI 実行とビルド成果物の入口が分散しない

#### Acceptance Criteria

1. When プロジェクトルートが解決された, the KES CLI shall `kes.xml` の `Project.Entry` を実行対象のイベントマスタとして解決する
2. If `Project.Entry` が未指定または空である, then the KES CLI shall 設定エラーとして診断し、ランタイムを起動しない
3. If `Project.Entry` が指すファイルが存在しない, then the KES CLI shall ファイルまたはディレクトリの入出力エラーとして診断し、ランタイムを起動しない
4. The KES CLI shall `kes run` 専用の別エントリ指定によって `Project.Entry` を上書きしない

### Requirement 3: 廃止済み入力の診断

**Objective:** As a CLI 利用者, I want サポートされない実行指定が明確に拒否される, so that プロジェクト実行への移行理由を理解できる

#### Acceptance Criteria

1. If `kes run` の `PROJECT_DIR` に `.kc` ファイルが指定された, then the KES CLI shall `.kc` 単体実行はサポートされないことを診断し、ランタイムを起動しない
2. If `kes run` の `PROJECT_DIR` に `.kel` ファイルが指定された, then the KES CLI shall `.kel` 直接実行はサポートされないことを診断し、ランタイムを起動しない
3. If `PROJECT_DIR` が既存ファイルでありプロジェクトディレクトリではない, then the KES CLI shall プロジェクトルートを指定する必要があることを診断し、ランタイムを起動しない

### Requirement 4: Target とビルド方針

**Objective:** As a CLI 利用者, I want 実行前ビルドの有無を明示または自動で扱える, so that 開発中の起動と既存成果物の確認を使い分けられる

#### Acceptance Criteria

1. When `--target windows` または target 省略で `kes run` が実行された, the KES CLI shall Windows 単体実行ランタイムを実行対象として扱う
2. If `--target` に `windows` 以外の値が指定された, then the KES CLI shall 未対応 target として診断し、ランタイムを起動しない
3. When `--build` が指定された, the KES CLI shall 実行前に Windows target のビルドを行う
4. When `--no-build` が指定された, the KES CLI shall 実行前ビルドを行わず、既存の Windows target 成果物だけを使用する
5. If `--build` と `--no-build` が同時に指定された, then the KES CLI shall コマンドライン引数エラーとして診断し、ランタイムを起動しない
6. When `--build` と `--no-build` のどちらも指定されていない, the KES CLI shall 必要な Windows target 成果物が存在しない、または入力ファイルより古い場合だけ実行前ビルドを行う
7. If 実行前ビルドが失敗した, then the KES CLI shall ビルド時の診断と終了コードを返し、ランタイムを起動しない

### Requirement 5: ビルド成果物の検証

**Objective:** As a CLI 利用者, I want 実行前に必要な成果物不足を知りたい, so that ランタイム起動後の曖昧な失敗を避けられる

#### Acceptance Criteria

1. When `kes run` が Windows ランタイムを起動する, the KES CLI shall Windows target の `manifest.json` をランタイムに渡す
2. If `--no-build` 使用時に Windows target の `manifest.json` が存在しない, then the KES CLI shall 既存ビルド成果物が不足していることを診断し、ランタイムを起動しない
3. If `--no-build` 使用時に実行に必要な `.klib` が存在しない, then the KES CLI shall 既存ビルド成果物が不足していることを診断し、ランタイムを起動しない
4. When 自動ビルド判定を行う, the KES CLI shall `kes.xml`、`Project.Entry`、参照スクリプト、素材、ローカライズ入力が既存成果物より新しい場合に成果物を古いものとして扱う
5. The KES CLI shall `kes run` によるビルド成果物を `kes build --target windows` と同じ場所と形式で扱う

### Requirement 6: ランタイム起動オプション

**Objective:** As a CLI 利用者, I want 実行時の表示・開始位置・ロケール指定をランタイムへ渡せる, so that 開発と確認に必要な起動条件を CLI から制御できる

#### Acceptance Criteria

1. When `--locale <LOCALE>` が指定された, the KES CLI shall 指定ロケールを Windows ランタイムへ渡す
2. When `--start <TAG>` が指定された, the KES CLI shall 指定ラベルまたはタグを Windows ランタイムへ渡す
3. When `--fullscreen` が指定された, the KES CLI shall フルスクリーン起動要求を Windows ランタイムへ渡す
4. When `--width <NUMBER>` または `--height <NUMBER>` が指定された, the KES CLI shall 指定されたウィンドウサイズを Windows ランタイムへ渡す
5. When `--debug` が指定された, the KES CLI shall デバッグ有効化要求を Windows ランタイムへ渡す
6. When `--profile` が指定された, the KES CLI shall プロファイル有効化要求を Windows ランタイムへ渡す
7. When `--` 以降に runtime arguments が指定された, the KES CLI shall それらの引数を CLI では解釈せず Windows ランタイムへ渡す

### Requirement 7: ランタイム起動結果と終了コード

**Objective:** As a CI 利用者, I want `kes run` の失敗分類と終了コードが安定している, so that 自動テストやスモークテストで原因を判別できる

#### Acceptance Criteria

1. When Windows ランタイムが正常に終了した, the KES CLI shall ランタイムの終了コードを CLI の終了コードとして返す
2. When Windows ランタイムが非ゼロ終了した, the KES CLI shall ランタイムの終了コードを CLI の終了コードとして返す
3. If Windows ランタイムを起動できない, then the KES CLI shall ランタイム起動エラーとして診断し、終了コード `7` を返す
4. If ランタイム起動前の処理で CLI エラーが発生した, then the KES CLI shall CLI 仕様のエラー分類に従う終了コードを返し、ランタイムを起動しない
5. The KES CLI shall 複数のエラー分類が同時に検出される場合、CLI 仕様に従って最も早い処理段階の終了コードを採用する
