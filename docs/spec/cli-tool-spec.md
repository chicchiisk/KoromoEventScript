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
kes build [PROJECT_DIR] [options]
kes clean [PROJECT_DIR] [options]
kes run [PATH_TO_EVENT_LIST] [options] [-- runtime-arguments]
kes publish [PROJECT_DIR] [options]
```

`COMMAND` には次を指定できる。

| コマンド | 概要 |
|---|---|
| `init` | 新しい KES プロジェクトを作成する |
| `build` | プロジェクト内の `.kc` / `.kel` を解析・検証し、`.kc` を VM 向け `.klib` にコンパイルする |
| `clean` | ビルド成果物と一時ファイルを削除する |
| `run` | `.kel` を起点に、単体実行ランタイムでイベントを実行する |
| `publish` | 配布用成果物を生成する |

## 共通オプション

| オプション | 意味 |
|---|---|
| `-h`, `--help` | ヘルプを表示して終了する |
| `-v`, `--version` | CLI のバージョンを表示して終了する |
| `--verbose` | 詳細ログを出力する |
| `--quiet` | エラー以外のログを抑制する |
| `--no-color` | ANSI カラー出力を無効化する |
| `--log-format <text\|json>` | ログ形式を指定する。既定値は `text` |
| `--project <PROJECT_DIR>` | プロジェクトルートを明示する |

`--verbose` と `--quiet` が同時に指定された場合はエラーとする。
`--log-format json` の場合、1行ごとに JSON オブジェクトを出力する JSON Lines 形式とする。

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

JSON 形式の例:

```json
{
    "level":"error",
    "code":"KES1001",
    "file":"events/chapter001.kc",
    "line":12,
    "column":5,
    "message":"未定義の識別子 'Noaa'"
}
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
| `--locale <LOCALE>` | 対象ロケールを指定する |
| `--warnings-as-errors` | 警告をエラーとして扱う |
| `--no-incremental` | インクリメンタルビルドを無効化する |
| `--emit-locale` | ローカライズ辞書を出力する |
| `--txt-il` | `.klib` と同じ論理内容を人間可読な `.klibtxt` としても出力する |
| `--check-only` | 成果物を生成せず検証のみ行う |

`--txt-il` は成果物を出力するオプションであるため、`--check-only` と同時指定してはならない。

### 挙動

1. `kes.xml` を読み込む。
2. `.kel` から参照される `.kc` ファイルを解決する。
3. `import` を解決し、依存関係を構築する。
4. 字句解析、構文解析、型検査、名前解決を行う。
5. ボイスID、画像ID、音声IDなどのリソース参照を検証する。
6. `.kc` ごとに VM 向けの中間表現へ変換する。
7. `--txt-il` が指定された場合、各 `.klib` と同じ論理内容を `.klibtxt` として整形出力する。
8. 必要に応じてローカライズ辞書を生成する。
9. `--check-only` が指定されていない場合、`.klib` ファイル、必要に応じて `.klibtxt`、診断結果、マニフェストをビルド成果物として出力する。

### 成果物

標準の出力先は `build/<target>/` とする。

```txt
build/
    windows/
        events/
            chapter001.klib
            chapter001.klibtxt
        diagnostics.json
        manifest.json
```

`.klib` のファイル名は、原則として入力 `.kc` のベース名を引き継ぐ。
たとえば `events/chapter001.kc` は `build/<target>/events/chapter001.klib` に出力する。
`--txt-il` を指定した場合は、同じ場所に `build/<target>/events/chapter001.klibtxt` も出力する。

`manifest.json` には、入力 `.kc` / `.kel`、生成された `.klib`、必要に応じて対応する `.klibtxt`、素材参照、ローカライズ情報、CLI バージョンを含める。
CLI は `.klib` を生成し、必要に応じて `.klibtxt` を併置しつつ、`manifest.json` から `.klib` を参照できる成果物構成を作る責務を持つ。
`.klib` / `.klibtxt` ファイル内部の instruction schema、命令体系、source mapping、manifest 参照契約の詳細は [`.klib` 中間表現仕様](k-intermediate-representation-spec.md)が所有する。
ランタイムは `manifest.json` と `.klib` を読み込み、VM に `.klib` を渡してイベントを実行する。`.klibtxt` は人間向けの補助成果物であり、runtime は読み込まない。

### 例

```txt
kes build
kes build --target windows --warnings-as-errors
kes build --entry events/main.kel --emit-locale
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

`.kel` を起点に、単体実行ランタイムでイベントを実行する。

```txt
kes run [PATH_TO_EVENT_LIST] [options] [-- runtime-arguments]
```

`PATH_TO_EVENT_LIST` を省略した場合は、`kes.xml` の `Project.Entry` を使用する。

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

1. `.kel` を解決する。
2. `--build` が指定されている場合、実行前にビルドを行う。
3. `--no-build` が指定されていない場合、ビルド成果物が存在しない、または入力ファイルより古ければ自動的にビルドする。
4. 単体実行ランタイムを起動し、`manifest.json`、`.klib` ファイル、実行オプションを渡す。
5. ランタイム内の VM が `.klib` ファイルを読み取り、イベントを実行する。
6. ランタイムの終了コードを CLI の終了コードへ反映する。

`kes run` が `--build` または自動ビルドで生成する `.klib` は `kes build` と同じ成果物契約に従う。
`.klib` の読み取り時に VM が検証する instruction schema と manifest 参照契約は [`.klib` 中間表現仕様](k-intermediate-representation-spec.md)を参照する。

`--` 以降の引数は CLI では解釈せず、ランタイムへそのまま渡す。

### 例

```txt
kes run
kes run events/main.kel --debug
kes run events/main.kel --start "#choice1" -- --profile
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

`--target windows` の場合は、単体実行ランタイム、`.klib`、`manifest.json`、必要素材、ローカライズ辞書、ライセンス情報を収集する。
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
| `build` | `kes.xml`, `.kel` | `.kc`, 素材, ローカライズ辞書 |
| `clean` | `kes.xml` | なし |
| `run` | `.kel` | `.klib`, `manifest.json`, 素材 |
| `publish` | `kes.xml`, `.kel` | `.kc`, `.klib`, `windows` 向け素材・ランタイム |

## パス解決

- 相対パスは、原則としてプロジェクトルートから解決する。
- `--project` が指定された場合、プロジェクトルート探索より優先する。
- `kes.xml` が必要なコマンドでプロジェクトルートを解決できない場合はエラーとする。

## バージョン表示

```txt
kes --version
```

出力形式:

```txt
kes 0.1.0
```

`--log-format json` が指定された場合でも、`--version` の出力はテキスト形式とする。

## ヘルプ表示

```txt
kes --help
kes build --help
```

ヘルプには、概要、構文、引数、オプション、代表例を含める。
