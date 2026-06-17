# Requirements Document

## Introduction

この feature は、KoromoEventScript の CLI 利用者が `kes correct` を実行し、`.kel` から参照される `.kc` を解析して、ローカライズ用タグの自動採番と `.kc` への書き戻し整形を行えるようにすることを目的とする。これにより、利用者はタグを手動で採番せずに、公開仕様に沿ったタグ付け済みスクリプトを準備できる。

## Boundary Context (Optional)

- **In scope**: `kes correct [PROJECT_DIR] [options]` のコマンド受付、`kes.xml` と entry `.kel` の解決、参照 `.kc` の解析、`say` / `nar` / `select` への自動タグ補完、公開仕様に沿ったタグ採番規則、`.kc` への書き戻し整形、`--check-only` による差分確認、診断と終了コード。
- **Out of scope**: ローカライズ辞書 `.csv` の生成、`.klib` / `.klibtxt` の生成、`kes build` / `kes loc` / `kes run` / `kes publish` の挙動変更、翻訳文の解決、ランタイム実行。
- **Adjacent expectations**: `.kel` / `.kc` の構文・意味解析、タグの公開仕様、ローカライズ辞書側が前提とするタグ規則、標準の CLI 診断形式と終了コード契約は既存仕様に従う。

## Requirements

### Requirement 1: CLI 利用者は `kes correct` を実行して対象プロジェクトを解決できる

**Objective:** As a CLI 利用者, I want `kes correct` を実行して対象プロジェクトを解決したい, so that 自動タグ補完の処理対象を明示または既定規則で指定できる

#### Acceptance Criteria

1. When `kes correct [PROJECT_DIR]` が実行されたとき, the CLI shall `PROJECT_DIR` を対象プロジェクトとして扱う。
2. When `PROJECT_DIR` を指定せずに `kes correct` が実行されたとき, the CLI shall 現在のディレクトリまたは親ディレクトリから `kes.xml` を探索して対象プロジェクトを解決する。
3. When `--entry <PATH_TO_EVENT_LIST>` が指定されたとき, the CLI shall プロジェクト既定の entry の代わりにその `.kel` を解析起点として扱う。
4. If `kes correct` に対するコマンドライン引数が不正な場合, the CLI shall コマンドライン診断を報告して非 0 の終了コードを返す。
5. If 対象プロジェクト、`kes.xml`、または `--entry` で指定された `.kel` を解決できない場合, the CLI shall ファイル診断を報告して非 0 の終了コードを返す。

### Requirement 2: CLI 利用者は entry から参照される `.kc` を解析対象として自動補完できる

**Objective:** As a CLI 利用者, I want `kes correct` が必要なスクリプトだけを解析してタグ補完したい, so that 実際に利用されるシナリオ資産を一貫した規則で整形できる

#### Acceptance Criteria

1. When `kes correct` が有効なプロジェクトに対して実行されたとき, the CLI shall entry `.kel` から参照される `.kc` ファイルを解決する。
2. When 参照 `.kc` が見つかったとき, the CLI shall `import` を解決して依存関係を構築したうえで解析対象を確定する。
3. When 解析対象が確定したとき, the CLI shall 字句解析、構文解析、型検査、名前解決を行ってからタグ補完可否を判定する。
4. If 解析対象の `.kc` またはその依存に構文エラーまたは意味エラーがある場合, the CLI shall 標準 diagnostic 形式でエラーを報告し、タグ書き戻しを成功として扱ってはならない。

### Requirement 3: CLI 利用者は公開仕様に沿った自動採番タグを付与できる

**Objective:** As a CLI 利用者, I want `kes correct` が公開仕様どおりのタグを自動採番したい, so that ローカライズ辞書や後続ツールと整合するタグを手作業なしで得られる

#### Acceptance Criteria

1. When `kes correct` がタグ未設定のローカライズ対象文を検出したとき, the CLI shall `say`、`nar`、`select` の3種類だけを自動タグ補完対象として扱う。
2. When `say` 構文へ自動タグを付与するとき, the CLI shall `sy_<normalized-script-file-name>_<number>` 形式のタグを生成する。
3. When `nar` 構文へ自動タグを付与するとき, the CLI shall `na_<normalized-script-file-name>_<number>` 形式のタグを生成する。
4. When `select` 構文へ自動タグを付与するとき, the CLI shall `se_<normalized-script-file-name>_<number>` 形式のタグを生成する。
5. When 自動タグに含まれる `<normalized-script-file-name>` を決定するとき, the CLI shall 拡張子を除いたスクリプトファイル名から空白を除去し、小文字化し、`_` 以外の記号類を除去した文字列を使う。
6. When 自動タグに含まれる `<number>` を採番するとき, the CLI shall 同一 `.kc` ファイル内の `say`、`nar`、`select` で番号空間を共有し、出現順に `0001` から始まる共通連番を使う。
7. When 採番結果が `9999` を超えるとき, the CLI shall `10000`、`10001` のように桁数を増やして採番を継続する。
8. If 既存の自動採番パターンに一致するタグと番号が衝突する場合, the CLI shall その番号を再利用せず、衝突しない次の番号を採番する。
9. While 既存タグが自動採番パターンに一致しない場合, the CLI shall そのタグを自動採番の衝突回避対象として扱わなくてよい。

### Requirement 4: CLI 利用者は自動採番結果を `.kc` へ書き戻すか、差分だけを確認できる

**Objective:** As a CLI 利用者, I want 実際の書き戻しと差分確認を使い分けたい, so that 変更を安全に確認しながらスクリプトを整形できる

#### Acceptance Criteria

1. When `--check-only` が指定されていない `kes correct` が成功したとき, the CLI shall 必要なタグ補完と整形を `.kc` に反映する。
2. When `--check-only` が指定されたとき, the CLI shall 実際の `.kc` 書き戻しを行わず、追記または更新予定のタグ一覧を出力する。
3. While `--check-only` が有効な間, the CLI shall `.kc` の内容を変更してはならない。
4. The CLI shall 既存の適切なタグを保持しつつ、不足しているタグだけを補完できなければならない。

### Requirement 5: CLI 利用者は `kes correct` の成否を診断と終了コードで判断できる

**Objective:** As a CLI 利用者, I want `kes correct` の成否を診断と終了コードで判断したい, so that 手元確認や CI で自動補完処理の結果を扱える

#### Acceptance Criteria

1. When `kes correct` がエラーなく成功したとき, the CLI shall exit code `0` を返す。
2. If コマンドライン引数エラーにより `kes correct` が失敗した場合, the CLI shall exit code `2` を返す。
3. If 構文検証エラーにより `kes correct` が失敗した場合, the CLI shall exit code `3` を返す。
4. If compile stage の diagnostics により `kes correct` が失敗した場合, the CLI shall exit code `4` を返す。
5. If ファイルまたはディレクトリの入出力エラーにより `kes correct` が失敗した場合, the CLI shall exit code `6` を返す。
6. When `kes correct` が diagnostics を出力するとき, the CLI shall 公開仕様で定義された標準 diagnostic 形式で出力する。
