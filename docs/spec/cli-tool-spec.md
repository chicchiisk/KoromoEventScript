# KoromoEventScript CLI ツール仕様書

KoromoEventScript CLI Tool (`kes.exe`) は、KoromoEventScript プロジェクトの作成、検証、ビルド、実行、配布物生成を行うコマンドラインツールである。

本仕様書では、CLI のコマンド体系、共通オプション、入出力、終了コード、各コマンドの挙動を定義する。

## 基本方針

- CLI は開発時の標準入口として機能する。
- `.kc` ファイルと `.kel` ファイルを入力として扱う。
- プロジェクト単位の操作を基本とする。
- 実行環境に依存する処理は、CLI から各ランタイムへ明示的に委譲する。
- 標準出力には通常ログ、標準エラー出力には警告・エラーを出力する。
- CI で利用しやすいよう、終了コードと機械可読ログ出力を定義する。

## 用語

| 用語 | 意味 |
|---|---|
| プロジェクトルート | `kes.xml` が置かれているディレクトリ |
| イベントスクリプトファイル | KoromoEventScript 言語で記述された `.kc` ファイル |
| イベントマスタファイル | イベントの一覧・遷移・エントリポイントを定義する `.kel` ファイル |
| 中間表現ファイル | `.kc` をコンパイルして生成する VM 実行用の `.klib` ファイル |
| VM | `.klib` ファイルを読み取り、イベントスクリプトを実行する仮想マシン |
| ビルド成果物 | CLI が生成する `.klib`、検証結果、ローカライズ辞書、ランタイム用出力 |
| パッケージ成果物 | `publish` が生成する配布用ディレクトリまたはアーカイブ |

## コマンド体系

```txt
kes -v|--version
kes -h|--help
kes <COMMAND> [-h|--help] [command-options] [arguments]

kes init [PROJECT_DIR] [options]
kes correct [PROJECT_DIR] [options]
kes loc [PROJECT_DIR] [options]
kes build [PROJECT_DIR] [options]
kes clean [PROJECT_DIR] [options]
kes run [PROJECT_DIR] [options] [-- runtime-arguments]
kes publish [PROJECT_DIR] [options]
```

`COMMAND` には次を指定できる。

| コマンド | 概要 |
|---|---|
| `init` | 新しい KES プロジェクトを作成する |
| `correct` | プロジェクト内の `.kc` / `.kel` を解析し、ローカライズタグの補完や書き戻し整形を行う |
| `loc` | プロジェクト内の `.kc` / `.kel` を解析し、ローカライズ辞書テンプレート `.csv` を生成する |
| `build` | プロジェクト内の `.kc` / `.kel` を解析・検証し、必要な書き戻しを行ってから `.kc` を VM 向け `.klib` にコンパイルする |
| `clean` | ビルド成果物と一時ファイルを削除する |
| `run` | `kes.xml` を起点に、単体実行ランタイムでプロジェクトを実行する |
| `publish` | 配布用成果物を生成する |

## 共通オプション

| オプション | 意味 |
|---|---|
| `-h`, `--help` | ヘルプを表示して終了する |
| `-v`, `--version` | CLI のバージョンを表示して終了する |
| `--verbose` | 詳細ログを出力する |

ログ形式は `text` のみをサポートする。ログ形式を切り替える CLI オプションは提供しない。

## プロジェクト構成

`kes init` が生成する標準構成は次の通りとする。

```txt
MyProject/
    kes.xml
    events/
        main.kel
        chapter001.kc
    assets/
        bg/
        actor/
        voice/
        se/
        bgm/
    locale/
    build/
    dist/
```

| パス | 用途 |
|---|---|
| `kes.xml` | アプリケーション設定 |
| `events/` | `.kc` / `.kel` の標準配置先 |
| `assets/` | 画像、音声などの素材配置先 |
| `locale/` | ローカライズ辞書の配置先 |
| `build/` | ビルド成果物の出力先 |
| `dist/` | 配布成果物の出力先 |

### `kes.xml`

`kes.xml` はアプリケーション設定ファイルである。
XML 宣言の文字エンコーディングは `utf-8` を標準とする。

```xml
<?xml version="1.0" encoding="utf-8"?>
<KoromoEventScript
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
    xsi:noNamespaceSchemaLocation="kes-config.xsd">
    <Project
        Name="MyProject"
        Version="0.1.0"
        Entry="events/main.kel" />

    <Paths
        Events="events"
        Assets="assets"
        Locale="locale"
        Build="build"
        Dist="dist" />

    <Build
        Target="windows"
        WarningsAsErrors="false" />

    <Runtime
        WindowWidth="1280"
        WindowHeight="720" />
</KoromoEventScript>
```

XML Schema は `docs/spec/kes-config.xsd` に置く。
`kes.xml` と同じディレクトリに `kes-config.xsd` を配置することで、XML エディタや CI から設定ファイルを検証できる。

| 項目 | 意味 |
|---|---|
| `Project.Name` | プロジェクト名 |
| `Project.Version` | プロジェクトバージョン |
| `Project.Entry` | 既定の `.kel` エントリポイント |
| `Paths.Events` | イベントファイルの配置先 |
| `Paths.Assets` | 素材の配置先 |
| `Paths.Locale` | ローカライズ辞書の配置先 |
| `Paths.Build` | ビルド成果物の出力先 |
| `Paths.Dist` | 配布成果物の出力先 |
| `Build.Target` | 既定のビルドターゲット |
| `Build.WarningsAsErrors` | 警告をエラーとして扱うか |
| `Runtime.WindowWidth` | 単体実行時の既定ウィンドウ幅 |
| `Runtime.WindowHeight` | 単体実行時の既定ウィンドウ高さ |

## 診断メッセージ

CLI は構文エラー、コンパイルエラー、実行時エラー、警告を診断メッセージとして出力する。

テキスト形式の例:

```txt
events/chapter001.kc:12:5 error KES1001: 未定義の識別子 'Noaa'
```

診断コードの分類は次の通りとする。

| 範囲 | 分類 |
|---|---|
| `KES1xxx` | 構文エラー |
| `KES2xxx` | コンパイルエラー |
| `KES3xxx` | 実行時エラー |
| `KES4xxx` | 警告 |
| `KES9xxx` | CLI 自体のエラー |

## 終了コード

| 終了コード | 意味 |
|---|---|
| `0` | 正常終了 |
| `1` | 一般エラー |
| `2` | コマンドライン引数エラー |
| `3` | 構文エラー |
| `4` | コンパイルエラー |
| `5` | 実行時エラー |
| `6` | ファイルまたはディレクトリの入出力エラー |
| `7` | ランタイム起動エラー |
| `8` | 配布成果物生成エラー |
| `9` | 警告をエラーとして扱ったことによる失敗 |

複数のエラー分類が同時に発生した場合は、最も早い処理段階のエラーを終了コードとして採用する。

## `kes init`

新しい KES プロジェクトを作成する。

```txt
kes init [PROJECT_DIR] [options]
```

`PROJECT_DIR` を省略した場合は、現在のディレクトリにプロジェクトを作成する。

### オプション

| オプション | 意味 |
|---|---|
| `--name <NAME>` | プロジェクト名を指定する |
| `--template <basic\|empty>` | 生成テンプレートを指定する。既定値は `basic` |
| `--force` | 既存ファイルの上書きを許可する |
| `--no-sample` | サンプル `.kc` / `.kel` を生成しない |

### 挙動

1. `PROJECT_DIR` を作成する。
2. `kes.xml` を生成する。
3. 標準ディレクトリを生成する。
4. `--template basic` の場合は、最小構成の `events/main.kel` と `events/chapter001.kc` を生成する。
5. 既存ファイルがあり `--force` が指定されていない場合はエラーとする。

### 例

```txt
kes init MyGame --name "MyGame"
kes init . --template empty
```

## `kes correct`

`.kel` から参照される `.kc` を解析し、ローカライズタグの補完と書き戻し整形を行う。

```txt
kes correct [PROJECT_DIR] [options]
```

`PROJECT_DIR` を省略した場合は、現在のディレクトリまたは親ディレクトリから `kes.xml` を探索する。

### オプション

| オプション | 意味 |
|---|---|
| `--entry <PATH_TO_EVENT_LIST>` | エントリポイントとなる `.kel` を指定する |
| `--check-only` | 実際には書き戻さず、追記または更新予定のタグ一覧を出力する |

### 挙動

1. `kes.xml` を読み込む。
2. `--entry` が指定されていればその `.kel` を、指定がなければ `Project.Entry` を起点に使う。
3. `.kel` から参照される `.kc` ファイルを解決する。
4. `import` を解決し、依存関係を構築する。
5. 字句解析、構文解析、型検査、名前解決を行う。
6. 各セリフまたはローカライズ対象文に対応するローカライズタグを補完する。
7. `--check-only` が指定された場合、CLI は実際の書き戻しを行わず、追記または更新予定のタグ一覧を出力する。
8. `--check-only` が指定されていない場合、CLI は必要な書き戻しと整形を `.kc` へ反映する。

### 自動タグ規則

`kes correct` が自動で補完する対象は次の3種類とする。

- `say` 構文のタグ
- `nar` 構文のタグ
- `select` 構文に属する `case` のタグ

自動補完タグの命名規則は次の通りとする。

- `say` : `sy_<normalized-script-file-name>_<number>`
- `nar` : `na_<normalized-script-file-name>_<number>`
- `select-case` : `se_<normalized-script-file-name>_<number>`

`<normalized-script-file-name>` は、拡張子を除いたスクリプトファイル名に対して次の正規化を行った文字列とする。

- 空白を除去する
- 小文字化する
- `_` 以外の記号類を除去する

`<number>` は 4 桁ゼロ埋めの連番とし、`0001` から開始する。
`9999` を超える場合は `10000`、`10001` のように桁数を増やしてよい。

同一 `.kc` ファイル内では、`say`、`nar`、`select-case` の3種類で番号空間を共有し、共通の連番として採番する。
出現順に `0001`、`0002`、`0003` と進み、接頭辞が異なっても同じ番号を再利用してはならない。

ユーザーが、手動で採番した場合、あるいはスクリプトの修正などによって、既存のタグが存在し、番号が重複する場合は、その番号を避けて採番する。
例えば、既存のタグとして sy_sample_1234 というタグがすでにあり、自動採番によって na_sample_1234 というタグを、作ろうとする場合は、1234を避けて、 na_sample_1235 とつけなければならない。
ただし、自動採番のパターンに当てはまらないタグの場合はこの限りではない。

例:

- `chapter 01.kc` の `say` : `sy_chapter01_0001`
- その後に現れる `nar` : `na_chapter01_0002`
- その後に現れる `select-case` : `se_chapter01_0003`

### 例

```txt
kes correct
kes correct --entry events/main.kel
kes correct --check-only
```

## `kes loc`

`kes correct` 相当の処理を行った後、ローカライズ辞書テンプレート `.csv` を生成する。

```txt
kes loc [PROJECT_DIR] [options]
```

`PROJECT_DIR` を省略した場合は、現在のディレクトリまたは親ディレクトリから `kes.xml` を探索する。

### オプション

| オプション | 意味 |
|---|---|
| `--locale <LOCALE_LIST>` | 出力する言語一覧を `jp,en,fr` のようなカンマ区切りで指定する |
| `--out <PATH_TO_LOCALIZATION_CSV>` | ローカライズ辞書 `.csv` の出力先を指定する |

言語タグは任意の英数字とハイフンを使えるものとし、各要素は `[0-9a-zA-Z-]` の範囲で構成する。
言語タグは自由に定義してよいが、相互運用性のため RFC 5646 / BCP 47 に従うことを推奨する。
言語タグは短いものを推奨する。たとえば日本語は通常 `ja-jp` より `ja` を推奨する。
`--locale` を省略した場合、既存の辞書があればその言語一覧を使う。既存の辞書がない場合は、`.kc` の基準言語だけを出力対象とする。
`--locale` を指定した場合でも、既存の辞書に含まれる言語は必ず出力対象に含める。指定された言語が既存の辞書に存在しない場合、CLI はその言語列を追加する。
`--out` を省略した場合、CLI はプロジェクトルート直下へローカライズ辞書 `.csv` を出力する。

### 挙動

1. `kes correct` 相当の処理を実行する。
2. 既存のローカライズ辞書があれば読み込み、言語一覧と既存翻訳を引き継ぐ。
3. `--locale` が指定された場合、既存辞書の言語一覧に指定言語をマージする。
4. 各タグについて、原文列と出力対象言語ごとの翻訳列を持つローカライズ辞書テンプレート `.csv` を生成する。
5. 既存辞書に翻訳済みテキストがある場合、CLI は対応するセルを保持したまま不足行または不足列だけを補う。
6. 生成した `.csv` を `--out` または既定の出力先へ書き出す。

### 例

```txt
kes loc
kes loc --locale jp,en,fr
kes loc --out translations/messages.csv
```

## `kes build`

プロジェクト内の `.kc` / `.kel` を解析・検証し、`.kc` ファイルを VM が解釈しやすい中間表現 `.klib` ファイルへコンパイルする。

`.klib` は VM 実行用の中間表現ファイルである。
`.klibtxt` は `.klib` の論理内容を人間可読な IL 風テキストへ射影した補助成果物であり、runtime 入力には使わない。
中間表現の命令体系、instruction schema、データ構造、バイナリ形式および `.klibtxt` テキスト形式の詳細は、[`.klib` 中間表現仕様](k-intermediate-representation-spec.md)で定義する。
本仕様書では、`kes build` が `.klib` を生成し、必要に応じて `.klibtxt` を併せて出力できること、およびランタイムは `.klib` を VM で読み取って実行する方式であることを定義する。

```txt
kes build [PROJECT_DIR] [options]
```

`PROJECT_DIR` を省略した場合は、現在のディレクトリまたは親ディレクトリから `kes.xml` を探索する。

### オプション

| オプション | 意味 |
|---|---|
| `--target <windows\|unity\|unreal>` | 出力ターゲットを指定する |
| `--entry <PATH_TO_EVENT_LIST>` | エントリポイントとなる `.kel` を指定する |
| `--out-dir <DIR>` | ビルド成果物の出力先を指定する |
| `--loc <LOCALE>` | ビルド対象の言語タグを指定する |
| `--warnings-as-errors` | 警告をエラーとして扱う |
| `--txt-il` | `.klib` と同じ論理内容を人間可読な `.klibtxt` としても出力する |
| `--check-only` | 成果物を生成せず検証のみ行う |

`--txt-il` は成果物を出力するオプションであるため、`--check-only` と同時指定してはならない。
`--check-only` が指定された場合、CLI は `.kc` へのタグ書き戻し、`.klib`、`.klibtxt` の生成を行ってはならない。

### 挙動

1. `kes.xml` を読み込む。
2. `.kel` から参照される `.kc` ファイルを解決する。
3. `import` を解決し、依存関係を構築する。
4. 字句解析、構文解析、型検査、名前解決を行う。
5. ボイスID、画像ID、音声IDなどのリソース参照を検証する。
6. `kes build` は内部的に `kes correct` 相当の処理を実行し、各セリフまたはローカライズ対象文に対応するタグを補完する。
7. `--check-only` が指定されていない場合、CLI は `kes correct` 相当で必要になった書き戻しを `.kc` へ反映する。
8. `--loc <LOCALE>` が指定された場合、CLI はプロジェクトルート直下のローカライズ辞書 `.csv` を読み込み、指定された言語タグ列が存在することを検証する。
9. `--loc <LOCALE>` が指定されていない場合、CLI は基準言語の build として扱う。
10. `--loc <LOCALE>` が指定された場合、CLI はローカライズ辞書 `.csv` と `.kc` を突き合わせ、表示テキストを compile-time に解決した言語別 `.klib` を生成する。
11. 言語別 `.klib` は `build/<target>/events/loc/<language-tag>/` 配下に出力する。たとえば `--loc en` の場合、`build/<target>/events/loc/en/chapter001.klib` に出力する。
12. `--loc` が指定されていない基準言語 build では、`.klib` を従来どおり `build/<target>/events/` 配下に出力する。
13. CLI は `.kc` ごとに VM 向けの中間表現へ変換する。
14. `--txt-il` が指定された場合、各 `.klib` と同じ論理内容を `.klibtxt` として整形出力する。
15. `--check-only` が指定されていない場合、CLI は `.klib`、必要に応じて `.klibtxt`、診断結果、マニフェストをビルド成果物として出力する。
16. `--target unity`ではマニフェストを`manifest.kson`として出力する。それ以外のtargetでは`manifest.json`として出力する。両者の内容は[ランタイムマニフェスト仕様](runtime-manifest-spec.md)に従うUTF-8 JSONである。

### 成果物

標準の出力先は `build/<target>/` とする。

```txt
build/
    windows/
        events/
            chapter001.klib
            chapter001.klibtxt
            loc/
                en/
                    chapter001.klib
                    chapter001.klibtxt
        diagnostics.json
        manifest.json
    unity/
        events/
        diagnostics.json
        manifest.kson
```

`.klib` のファイル名は、原則として入力 `.kc` のベース名を引き継ぐ。
たとえば `events/chapter001.kc` は `build/<target>/events/chapter001.klib` に出力する。
`--txt-il` を指定した場合は、同じ場所に `build/<target>/events/chapter001.klibtxt` も出力する。

runtime manifestには、入力`.kc` / `.kel`、生成された`.klib`、必要に応じて対応する`.klibtxt`、素材参照、ローカライズ情報、CLI versionを含める。正式なプロパティ、型、必須性、target別ファイル名は[ランタイムマニフェスト仕様](runtime-manifest-spec.md)と[runtime-manifest.schema.json](runtime-manifest.schema.json)に従う。
CLIは`.klib`を生成し、必要に応じて`.klibtxt`を併置しつつ、runtime manifestから`.klib`を参照できる成果物構成を作る責務を持つ。
ローカライズ辞書テンプレート `.csv` の生成は `kes loc` が担当する。CSV の列構成、文字コード、言語タグ規則は [ローカライズ辞書仕様書](localization-dictionary-spec.md)が所有する。`kes build --loc <language-tag>` はプロジェクトルートの `.csv` を取り込み、指定言語の表示テキストを compile-time に解決した `.klib` を `build/<target>/events/loc/<language-tag>/` 配下へ生成する。
`.klib` / `.klibtxt` ファイル内部の instruction schema、命令体系、source mapping、manifest 参照契約の詳細は [`.klib` 中間表現仕様](k-intermediate-representation-spec.md)が所有する。
ランタイムはtargetに対応する`manifest.json`または`manifest.kson`と、対象言語向けに生成済みの`.klib`を読み込み、VMに`.klib`を渡してイベントを実行する。`.klibtxt`は人間向けの補助成果物であり、runtimeは読み込まない。

### 例

```txt
kes build
kes build --target windows --warnings-as-errors
kes build --entry events/main.kel
kes build --loc en
kes build --txt-il
```

## `kes clean`

ビルド成果物と一時ファイルを削除する。

```txt
kes clean [PROJECT_DIR] [options]
```

### オプション

| オプション | 意味 |
|---|---|
| `--target <windows\|unity\|unreal>` | 指定ターゲットの成果物のみ削除する |
| `--dist` | `dist/` も削除対象に含める |
| `--dry-run` | 削除対象を表示するだけで削除しない |

### 挙動

1. プロジェクトルートを解決する。
2. `Paths.Build` 配下の成果物を削除する。
3. `--target` が指定された場合は `Paths.Build/<target>/` のみを削除する。
4. `--dist` が指定された場合は `Paths.Dist` 配下も削除する。
5. ソースファイル、素材ファイル、`kes.xml` は削除しない。

### 例

```txt
kes clean
kes clean --target windows
kes clean --dist --dry-run
```

## `kes run`

`kes.xml` を起点に、単体実行ランタイムでプロジェクトを実行する。

```txt
kes run [PROJECT_DIR] [options] [-- runtime-arguments]
```

`PROJECT_DIR` を省略した場合は、現在のディレクトリまたは親ディレクトリから `kes.xml` を探索してプロジェクトルートを解決する。
`kes run` は `.kc` ファイル単体や `.kel` ファイルを直接指定する実行をサポートしない。
実行対象のイベントマスタは常に `kes.xml` の `Project.Entry` で指定する。

### オプション

| オプション | 意味 |
|---|---|
| `--target <windows>` | 使用する単体実行ランタイムを指定する。初期仕様では `windows` のみ |
| `--build` | 実行前に `kes build` 相当の処理を行う |
| `--no-build` | 既存のビルド成果物を使用して実行する |
| `--debug` | デバッグ情報を有効にする |
| `--locale <LOCALE>` | 実行ロケールを指定する |
| `--start <TAG>` | 指定ラベルまたはタグから開始する |
| `--fullscreen` | フルスクリーンで起動する |
| `--width <NUMBER>` | ウィンドウ幅を指定する |
| `--height <NUMBER>` | ウィンドウ高さを指定する |

### 挙動

1. プロジェクトルートを解決し、`kes.xml` を読み込む。
2. `kes.xml` の `Project.Entry` から実行対象の `.kel` を解決する。
3. `--build` が指定されている場合、実行前にビルドを行う。
4. `--no-build` が指定されていない場合、ビルド成果物が存在しない、または入力ファイルより古ければ自動的にビルドする。
5. 単体実行ランタイムを起動し、`manifest.json`、`.klib` ファイル、実行オプションを渡す。
6. ランタイム内の VM が `.klib` ファイルを読み取り、イベントを実行する。
7. ランタイムの終了コードを CLI の終了コードへ反映する。

`kes run` が `--build` または自動ビルドで生成する `.klib` は `kes build` と同じ成果物契約に従う。
`.klib` の読み取り時に VM が検証する instruction schema と manifest 参照契約は [`.klib` 中間表現仕様](k-intermediate-representation-spec.md)を参照する。

`--` 以降の引数は CLI では解釈せず、ランタイムへそのまま渡す。

### 例

```txt
kes run
kes run . --debug
kes run testdata/projects/full-command-sample --start "#se_sample_0002" -- --profile
```

## `kes publish`

配布用成果物を生成する。

```txt
kes publish [PROJECT_DIR] [options]
```

### オプション

| オプション | 意味 |
|---|---|
| `--target <windows\|unity\|unreal>` | 配布対象ターゲットを指定する |
| `--configuration <debug\|release>` | 配布構成を指定する。既定値は `release` |
| `--out-dir <DIR>` | 配布成果物の出力先を指定する |
| `--archive <none\|zip>` | アーカイブ形式を指定する。既定値は `zip` |
| `--include-source` | `.kc` / `.kel` を配布物に含める。`windows` 向けのみ有効 |
| `--locale <LOCALE>` | 配布対象ロケールを指定する |
| `--clean` | publish 前に `clean` を実行する |

### 挙動

1. 必要に応じて `clean` を実行する。
2. `kes build` 相当の検証と `.klib` 生成を行う。
3. ターゲットに応じた配布用ファイルを収集する。
4. `dist/<target>/` に配布用ディレクトリを生成する。
5. `--archive zip` の場合は zip アーカイブを生成する。

`--target windows` の場合は、単体実行ランタイム、`.klib`、`manifest.json`、必要素材、ローカライズ済み `.klib`、ライセンス情報を収集する。
配布物に含める `.klib` は `kes build` が生成した VM 実行用中間表現であり、ファイル形式、instruction schema、命令体系、manifest 参照契約は [`.klib` 中間表現仕様](k-intermediate-representation-spec.md)に従う。

`--target unity` または `--target unreal` の場合は、エンジン組み込み拡張から読み込むためのデータフォルダを生成する。
このフォルダには、生成済みの `.klib` ファイルと、イベントマスタファイル `.kel` のみを含める。
Unity / Unreal 側のランタイム、VM、素材管理、ローカライズ辞書、ライセンス情報は、それぞれのエンジン組み込み拡張またはプロジェクト側で管理する。

Unity / Unreal 向け publish では `.kc` を含めない。
`--target unity` または `--target unreal` と `--include-source` が同時に指定された場合はエラーとする。

### 成果物

Windows 向け成果物:

```txt
dist/
    windows/
        MyProject/
            MyProject.exe
            data/
            licenses/
        MyProject-0.1.0-windows.zip
```

Unity 向け成果物:

```txt
dist/
    unity/
        MyProject.kesdata/
            main.kel
            events/
                chapter001.klib
```

Unreal Engine 向け成果物:

```txt
dist/
    unreal/
        MyProject.kesdata/
            main.kel
            events/
                chapter001.klib
```

`.kesdata` ディレクトリ名は、エンジン組み込み拡張が読み込む KES データフォルダを表す。
Unity / Unreal プロジェクトでは、このフォルダを任意のアセット配置先へ取り込み、エンジン側の KoromoEventScript 実行機構から `.kel` を起点に読み込む。

Windows 向けで `--include-source` が指定されていない場合、配布物には `.kc` / `.kel` の生ファイルを含めない。

### 例

```txt
kes publish
kes publish --target windows --configuration release
kes publish --target unity
kes publish --target unreal --out-dir Content/KoromoEventScript
kes publish --out-dir releases --archive zip
```

## コマンド別入力ファイル

| コマンド | 主入力 | 補助入力 |
|---|---|---|
| `init` | なし | テンプレート |
| `correct` | `kes.xml`, `.kel` | `.kc` |
| `loc` | `kes.xml`, `.kel` | `.kc`, 既存ローカライズ辞書 |
| `build` | `kes.xml`, `.kel` | `.kc`, 素材, ローカライズ辞書 |
| `clean` | `kes.xml` | なし |
| `run` | `kes.xml` | `.kel`, `.kc`, `.klib`, `manifest.json`, 素材 |
| `publish` | `kes.xml`, `.kel` | `.kc`, `.klib`, `windows` 向け素材・ランタイム |

## パス解決

- 相対パスは、原則としてプロジェクトルートから解決する。
- `kes.xml` が必要なコマンドでプロジェクトルートを解決できない場合はエラーとする。

## バージョン表示

```txt
kes --version
```

出力形式:

```txt
kes 0.1.0
```

## ヘルプ表示

```txt
kes --help
kes build --help
```

ヘルプには、概要、構文、引数、オプション、代表例を含める。
