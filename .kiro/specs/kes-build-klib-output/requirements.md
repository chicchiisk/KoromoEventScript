# Requirements Document

## Introduction

この feature は、KES CLI 利用者が `kes build` を使って `.kc` / `.kel` を検証し、VM 実行形式である `.klib` を仕様どおり出力できるようにすることを目的とする。現状の CLI は check-only を中心とした検証機能は持つが、公開仕様に記載されたビルド成果物、ローカライズ済み成果物、補助成果物、出力先制御を完結に扱えていない。そのため、CLI 利用者はビルドフローを完走できず、実行や後続ランタイム連携へ進めない。`kes build` は公開仕様に沿って、必要なタグ補完、検証、`.klib` 出力、必要に応じた `.klibtxt` 出力、言語別ビルド、および終了コードと診断契約を満たす必要がある。

## Boundary Context

- **In scope**: `kes build [PROJECT_DIR] [options]` の引数受付、`kes.xml` に基づくプロジェクト解決、entry `.kel` と参照 `.kc` の解析、`kes correct` 相当のタグ補完と必要時の書き戻し、`.klib` / `.klibtxt` / `manifest.json` / 診断成果物の出力、`--check-only`、`--loc`、`--out-dir`、`--warnings-as-errors`、`--log-format`、`--target windows` の解釈、標準診断と終了コード。
- **Out of scope**: `unity` / `unreal` 向け成果物の実装、`kes clean` / `kes run` / `kes publish` の実装、ランタイムでの `.klib` 実行、翻訳辞書テンプレート `.csv` を生成する `kes loc` の再実装、配布アーカイブ生成。
- **Adjacent expectations**: タグ補完規則は `kes correct` の仕様に従う。ローカライズ辞書 `.csv` の列構成と言語タグ規則はローカライズ辞書仕様に従う。`.klib` と `.klibtxt` の論理内容、命令体系、manifest 参照契約は中間表現仕様に従う。

## Requirements

### Requirement 1: CLI 利用者は `kes build` を仕様どおりの引数で起動できる

**Objective:** As a CLI利用者, I want `kes build` の引数とオプションを仕様どおりに指定したい, so that ビルド対象と成果物の出力条件を意図どおりに切り替えられる

#### Acceptance Criteria

1. When `kes build [PROJECT_DIR]` が実行されたとき, the CLI shall `PROJECT_DIR` を対象プロジェクトとして扱う。
2. When `PROJECT_DIR` が省略されたとき, the CLI shall 現在のディレクトリまたは親ディレクトリから `kes.xml` を探索して対象プロジェクトを解決する。
3. When `--entry <PATH_TO_EVENT_LIST>` が指定されたとき, the CLI shall 指定された `.kel` をビルドのエントリポイントとして扱う。
4. When `--out-dir <DIR>` が指定されたとき, the CLI shall そのディレクトリをビルド成果物の出力先として扱う。
5. When `--target windows` が指定されたとき, the CLI shall `windows` 向けビルドとして扱う。
6. If `--target` に `windows` 以外の値が指定された場合, the CLI shall コマンドライン診断を報告して終了コード `2` で停止する。
7. If `--txt-il` と `--check-only` が同時に指定された場合, the CLI shall コマンドライン診断を報告して終了コード `2` で停止する。
8. If `kes build` に未対応オプション、値不足オプション、または不正な引数の組み合わせが指定された場合, the CLI shall 標準 diagnostic 形式でコマンドライン診断を出力して終了コード `2` を返す。

### Requirement 2: CLI 利用者は `kes build` で検証とタグ補完を伴うビルド前処理を完了できる

**Objective:** As a CLI利用者, I want `kes build` がコンパイル前に必要な検証とタグ補完を完了してほしい, so that 不完全なスクリプト状態のまま成果物が生成されることを防げる

#### Acceptance Criteria

1. When `kes build` が有効なプロジェクトに対して実行されたとき, the CLI shall `kes.xml`、entry `.kel`、参照される `.kc` を解決してビルド対象を確定する。
2. When ビルド対象が確定したとき, the CLI shall `import` 解決、字句解析、構文解析、名前解決、型検査、およびビルドに必要な診断生成を行う。
3. When `kes build` が `--check-only` なしで実行されたとき, the CLI shall `kes correct` 相当の処理を内部的に実行し、必要なタグ補完を書き戻してから成果物生成へ進む。
4. While `--check-only` が指定されている間, the CLI shall `.kc` への書き戻しを行ってはならない。
5. If `.kel`、`.kc`、またはその依存に構文エラーがある場合, the CLI shall 標準 diagnostic 形式でエラーを出力して終了コード `3` を返し、成果物を生成してはならない。
6. If 名前解決、型検査、import 解決、タグ重複、未定義ジャンプ先などの compile stage 診断が発生した場合, the CLI shall 標準 diagnostic 形式でエラーを出力して終了コード `4` を返し、成果物を生成してはならない。
7. If プロジェクトファイル、入力ファイル、または必要なディレクトリを読み書きできない場合, the CLI shall 標準 diagnostic 形式でファイル入出力エラーを出力して終了コード `6` を返す。

### Requirement 3: CLI 利用者は `kes build` で基準言語の `.klib` と補助成果物を取得できる

**Objective:** As a CLI利用者, I want 基準言語の `.klib` と必要な補助成果物を得たい, so that ランタイム実行や成果物確認へそのまま進める

#### Acceptance Criteria

1. When `kes build` が `--check-only` なしで成功したとき, the CLI shall 各入力 `.kc` に対応する `.klib` を生成する。
2. When 基準言語のビルドが成功したとき, the CLI shall 各 `.klib` を `build/<target>/events/` 配下、または `--out-dir` で指定された出力先の対応パス配下へ配置する。
3. When `--txt-il` が指定されたとき, the CLI shall 各 `.klib` と同じ論理内容を持つ `.klibtxt` を対応する出力先へ併せて生成する。
4. While `--check-only` が指定されている間, the CLI shall `.klib`、`.klibtxt`、`manifest.json`、およびその他のビルド成果物を生成してはならない。
5. When ビルド成果物の生成が成功したとき, the CLI shall 入力 `.kc` / `.kel`、生成された `.klib`、必要に応じて `.klibtxt`、およびローカライズ情報を参照可能な `manifest.json` をビルド出力へ含めなければならない。

### Requirement 4: CLI 利用者は `kes build --loc` で言語別 `.klib` を生成できる

**Objective:** As a CLI利用者, I want 指定言語向けに compile-time 解決済みの `.klib` を生成したい, so that ランタイムで生の翻訳辞書を読むことなく対象言語で実行できる

#### Acceptance Criteria

1. When `--loc <LOCALE>` が指定されたとき, the CLI shall プロジェクトルート直下のローカライズ辞書 `.csv` を読み込み、指定された言語タグ列が存在することを検証する。
2. If `--loc <LOCALE>` が指定され、対応するローカライズ辞書 `.csv` が存在しない場合, the CLI shall 標準 diagnostic 形式でエラーを出力して終了コード `6` を返し、言語別成果物を生成してはならない。
3. If `--loc <LOCALE>` が指定され、辞書内に指定言語タグ列が存在しない場合, the CLI shall 標準 diagnostic 形式でエラーを出力して終了コード `4` または `6` の適切な失敗として停止し、言語別成果物を生成してはならない。
4. When `--loc <LOCALE>` が指定されたビルドが成功したとき, the CLI shall ローカライズ辞書 `.csv` と `.kc` を突き合わせて表示テキストを compile-time に解決した `.klib` を生成する。
5. When `--loc <LOCALE>` が指定されたビルドが成功したとき, the CLI shall 生成した `.klib` を `build/<target>/events/loc/<language-tag>/` 配下、または `--out-dir` で指定された出力先の対応パス配下へ配置する。
6. When `--loc <LOCALE>` と `--txt-il` が同時に指定されているとき, the CLI shall 言語別 `.klib` に対応する `.klibtxt` も同じ出力ツリーへ生成する。
7. When `--loc` が指定されていないとき, the CLI shall 基準言語ビルドとして扱い、言語別出力ディレクトリを必須としてはならない。

### Requirement 5: CLI 利用者は `kes build` の成否を終了コードと診断で判断できる

**Objective:** As a CLI利用者, I want ビルドの成否を自動化しやすい形で判定したい, so that 手元利用でも CI でも同じ契約で扱える

#### Acceptance Criteria

1. When `kes build` がエラーなく成功したとき, the CLI shall 終了コード `0` を返す。
2. When `kes build --check-only` が警告のみで完了したとき, the CLI shall 警告診断を出力しても終了コード `0` を返す。
3. When `--warnings-as-errors` が指定されている、または `kes.xml` で `WarningsAsErrors="true"` が有効なとき, the CLI shall 警告を失敗として扱い終了コード `9` を返す。
4. When `--log-format json` が指定されたとき, the CLI shall diagnostics を JSON Lines 形式で標準エラー出力へ書き出す。
5. When `--log-format text` が指定された、または省略されたとき, the CLI shall diagnostics をテキスト形式で標準エラー出力へ書き出す。
6. While 正常終了している間, the CLI shall エラー診断を標準エラー出力へ書いてはならない。
