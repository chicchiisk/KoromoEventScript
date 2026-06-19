# Requirements Document

## Introduction

この feature は、KoromoEventScript の CLI 利用者が `kes loc` を実行し、`.kel` から参照される `.kc` を解析してローカライズ辞書テンプレート `.csv` を生成できるようにすることを目的とする。現在は翻訳作業へ渡すための辞書テンプレートを CLI から書き出せないため、利用者はタグ、原文、翻訳列を手作業で管理しなければならない。`kes loc` は公開仕様に沿って `kes correct` 相当の処理を先行実行し、タグ付け済みのスクリプトから原文を抽出し、既存辞書がある場合は翻訳列を保持しながら不足行と不足列を補った辞書を出力できる必要がある。

## Boundary Context

- **In scope**: `kes loc [PROJECT_DIR] [options]` のコマンド受付、対象プロジェクトの解決、`kes correct` 相当の前処理、entry `.kel` から参照される `.kc` の解析、ローカライズ辞書 `.csv` の生成、`--locale` / `--out` の解釈、既存辞書の引き継ぎ、診断と終了コード。
- **Out of scope**: `.klib` / `.klibtxt` の生成、`kes build --loc` の言語別コンパイル、翻訳文そのものの作成、ランタイムでのローカライズ解決、辞書を利用した実行時 UI。
- **Adjacent expectations**: タグ採番と書き戻し規則は `kes correct` の仕様に従う。辞書列構成、UTF-8 BOM、言語タグ列、既存翻訳の保持規則はローカライズ辞書仕様書に従う。CLI の診断形式と終了コードの流儀は既存コマンドと整合していることを期待する。

## Requirements

### Requirement 1: CLI 利用者は `kes loc` を実行して対象プロジェクトと出力先を解決できる

**Objective:** CLI利用者として、`kes loc` を実行して対象プロジェクトと辞書の出力先を明示または既定規則で決めたい。これにより、翻訳テンプレート生成を迷わず開始できる。

#### Acceptance Criteria

1. When `kes loc [PROJECT_DIR]` が実行されたとき, the CLI shall `PROJECT_DIR` を対象プロジェクトとして扱う。
2. When `PROJECT_DIR` を指定せずに `kes loc` が実行されたとき, the CLI shall 現在のディレクトリまたは親ディレクトリから `kes.xml` を探索して対象プロジェクトを解決する。
3. When `--out <PATH_TO_LOCALIZATION_CSV>` が指定されたとき, the CLI shall そのパスをローカライズ辞書 `.csv` の出力先として扱う。
4. When `--out` が省略されたとき, the CLI shall プロジェクトルート直下をローカライズ辞書 `.csv` の既定出力先として扱う。
5. If `kes loc` に対するコマンドライン引数が不正な場合, the CLI shall コマンドライン診断を報告して非 0 の終了コードを返す。
6. If 対象プロジェクト、`kes.xml`、または出力先パスを解決できない場合, the CLI shall ファイル診断を報告して非 0 の終了コードを返す。

### Requirement 2: CLI 利用者は `kes correct` 相当の前処理を経たスクリプトから辞書生成対象を確定できる

**Objective:** CLI利用者として、`kes loc` に辞書生成前のタグ整備と解析を一貫して行ってほしい。これにより、未整備タグや解析漏れのない辞書を得られる。

#### Acceptance Criteria

1. When `kes loc` が有効なプロジェクトに対して実行されたとき, the CLI shall ローカライズ辞書生成の前に `kes correct` 相当の処理を行う。
2. When `kes loc` が解析対象を決定するとき, the CLI shall entry `.kel` から参照される `.kc` を解決し、必要な `import` 依存を含めて対象を確定する。
3. When 解析対象が確定したとき, the CLI shall 字句解析、構文解析、型検査、名前解決を行ってから辞書生成可否を判定する。
4. If 解析対象の `.kc` またはその依存に構文エラーまたは意味エラーがある場合, the CLI shall 標準 diagnostic 形式でエラーを報告し、辞書生成を成功として扱ってはならない。
5. If `kes correct` 相当の処理で必要なタグ補完または書き戻しが完了できない場合, the CLI shall 不完全な辞書を書き出してはならない。

### Requirement 3: CLI 利用者は公開仕様どおりのローカライズ辞書テンプレートを取得できる

**Objective:** CLI利用者として、翻訳作業へそのまま渡せる辞書テンプレートを公開仕様どおりに出力したい。これにより、タグ、原文、翻訳列の管理を統一できる。

#### Acceptance Criteria

1. When `kes loc` が辞書を生成するとき, the CLI shall ローカライズ辞書仕様書で定義された `.csv` フォーマットを出力する。
2. When `.csv` を出力するとき, the CLI shall UTF-8 BOM 付きで書き出す。
3. When ヘッダ行を出力するとき, the CLI shall 少なくとも `tag`、`say`、`original` をこの順序で含め、その後ろに出力対象言語の列を配置する。
4. When `.kc` から自動抽出対象を収集するとき, the CLI shall `say` 構文の本文、`nar` 構文の本文、`select` ブロック内の `case` 選択肢テキストを辞書行として抽出する。
5. When `say` 構文から辞書行を生成するとき, the CLI shall `say` 列に話者名を格納する。
6. When `nar` 構文または `select` ブロックから辞書行を生成するとき, the CLI shall `say` 列を翻訳支援用の補助情報として扱い、主キーとして扱ってはならない。
7. When `original` 列へ原文を格納するとき, the CLI shall 改行、改ページ、インラインマクロ、制御記法を含む基準言語の本文を保持する。
8. When 辞書行を識別するとき, the CLI shall `.kc` に付与されたローカライズタグを `tag` 列の安定キーとして用いる。

### Requirement 4: CLI 利用者は言語列を制御しつつ既存辞書の翻訳を保持できる

**Objective:** CLI利用者として、必要な言語列だけを追加しながら既存翻訳を失わずに辞書を更新したい。これにより、翻訳作業を継続的に積み上げられる。

#### Acceptance Criteria

1. When 既存のローカライズ辞書 `.csv` が存在する場合, the CLI shall 既存辞書を読み込み、既存の翻訳列と翻訳内容を引き継ぐ。
2. When `--locale <LOCALE_LIST>` が省略され、既存辞書が存在する場合, the CLI shall 既存辞書に含まれる言語列を出力対象として使う。
3. When `--locale <LOCALE_LIST>` が省略され、既存辞書が存在しない場合, the CLI shall `.kc` の基準言語だけを出力対象言語として扱う。
4. When `--locale <LOCALE_LIST>` が指定されたとき, the CLI shall 既存辞書の言語列に指定された言語タグをマージする。
5. While `--locale <LOCALE_LIST>` が指定されていても既存辞書に言語列が存在する間, the CLI shall 既存辞書の言語列を削除してはならない。
6. When 新しい抽出結果に既存辞書と同じ `tag` が存在するとき, the CLI shall その `tag` に対応する既存翻訳を保持する。
7. When 新しい抽出結果に既存辞書に存在しない `tag` または言語列があるとき, the CLI shall 不足する行または列を追加する。
8. If 既存辞書に必須カラム `tag`、`say`、`original` が存在しない場合, the CLI shall 辞書形式の診断を報告して非 0 の終了コードを返す。
9. If 既存辞書内で `tag` が一意でない場合, the CLI shall 辞書形式の診断を報告して非 0 の終了コードを返す。

### Requirement 5: CLI 利用者は `kes loc` の成否を診断と終了コードで判断できる

**Objective:** CLI利用者として、`kes loc` の成功と失敗を診断と終了コードで確実に判断したい。これにより、手元確認や自動化から安全に利用できる。

#### Acceptance Criteria

1. When `kes loc` がエラーなく成功したとき, the CLI shall exit code `0` を返す。
2. If コマンドライン引数エラーにより `kes loc` が失敗した場合, the CLI shall exit code `2` を返す。
3. If 構文検証エラーにより `kes loc` が失敗した場合, the CLI shall exit code `3` を返す。
4. If compile stage の diagnostics により `kes loc` が失敗した場合, the CLI shall exit code `4` を返す。
5. If ファイルまたはディレクトリの入出力エラーにより `kes loc` が失敗した場合, the CLI shall exit code `6` を返す。
6. When `kes loc` が diagnostics を出力するとき, the CLI shall 公開仕様で定義された標準 diagnostic 形式で出力する。
7. When `kes loc` が正常終了したとき, the CLI shall 生成または更新したローカライズ辞書の出力先を利用者へ示さなければならない。
