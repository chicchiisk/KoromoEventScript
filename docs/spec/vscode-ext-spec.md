# KoromoEventScript VS Code 拡張仕様書

KoromoEventScript VS Code Extension は、VS Code 上で KoromoEventScript の編集、検証、ナビゲーション、整形を支援する拡張機能である。

本仕様書では、拡張の対象ファイル、言語機能、設定項目、Language Server 連携、診断表示、フォーマット規則を定義する。

## 基本方針

- `.ke` と `.kel` を KoromoEventScript 関連ファイルとして扱う。
- シナリオライターが本文を崩さず書けることを優先する。
- 構文色分けなどの軽量機能は拡張単体で提供する。
- 定義ジャンプ、補完、診断、フォーマットなど意味解析が必要な機能は Language Server で提供する。
- CLI の `kes build --check-only` と同じ解析・検証規則を Language Server でも利用できる設計とする。
- 未保存ファイルにも可能な範囲で診断、補完、フォーマットを提供する。
- 大きなプロジェクトでも入力を妨げないよう、解析はインクリメンタルに行う。

## 対象ファイル

| 拡張子 | 言語ID | 用途 |
|---|---|---|
| `.ke` | `koromo-event-script` | イベントスクリプトファイル |
| `.kel` | `koromo-event-list` | イベントマスタファイル |

`kes.xml` は XML ファイルとして扱い、構成検証は `docs/spec/kes-config.xsd` に委ねる。
VS Code 拡張は `kes.xml` を読み込み、プロジェクトルート、イベントディレクトリ、素材ディレクトリ、エントリポイントの解決に利用する。

## 拡張構成

拡張は次の構成を標準とする。

```txt
vscode-extension/
    package.json
    syntaxes/
        koromo-event-script.tmLanguage.json
        koromo-event-list.tmLanguage.json
    language-configuration.json
    client/
        extension.ts
    server/
        languageServer.ts
```

| 構成要素 | 役割 |
|---|---|
| TextMate Grammar | シンタックスハイライトを提供する |
| Language Configuration | コメント、括弧、インデント、折りたたみ規則を提供する |
| Extension Client | VS Code API と Language Server を接続する |
| Language Server | 定義ジャンプ、補完、フォーマット、診断を提供する |

Language Server は LSP (Language Server Protocol) を用いる。
将来的に CLI と同じパーサ・名前解決器・型検査器を共有できるよう、解析処理は VS Code 固有 API から分離する。

## アクティベーション

拡張は次の条件で起動する。

```json
{
  "activationEvents": [
    "onLanguage:koromo-event-script",
    "onLanguage:koromo-event-list",
    "workspaceContains:kes.xml"
  ]
}
```

`.ke` または `.kel` を開いた時点で Language Server を起動する。
ワークスペースに `kes.xml` が存在する場合は、プロジェクト単位の解析を有効化する。

## シンタックスハイライト

シンタックスハイライトは TextMate Grammar で提供する。
意味解析を待たず、ファイルを開いた直後から色分けできることを必須とする。

### `.ke` のハイライト対象

| 種別 | 対象 |
|---|---|
| 予約語 | `import`, `var`, `fn`, `class`, `enum`, `actor`, `public`, `private`, `if`, `else`, `while`, `for`, `in`, `break`, `continue`, `using`, `as`, `return`, `new`, `select`, `case`, `label`, `jump`, `true`, `false`, `null` |
| 組み込み構文 | `say`, `nar`, `using`, `select`, `case`, `label`, `jump` |
| 組み込み型 | `number`, `bool`, `string`, `Actor`, `void` |
| 文字列 | ダブルクオート文字列 |
| 数値 | 数値リテラル |
| コメント | `// ...` と `/* ... */` |
| タグ | `#choice1`, `#ch001_s01110` など |
| 式埋め込み | `say` / `nar` ブロック内の `{...}` |
| 式行 | `say` / `nar` ブロック内の `@...` |
| 演算子 | `=`, `+`, `-`, `*`, `/`, `==`, `!=`, `<`, `<=`, `>`, `>=`, `&&`, `\|\|`, `!` |
| 関数・命令名 | 行頭またはセミコロン直後の識別子 |
| 型注釈 | `: string`, `: Actor[]` など |

### `say` / `nar` ブロックの扱い

`say` / `nar` の本文は通常のシナリオテキストとして扱う。
本文全体を文字列色に寄せすぎず、読み物として自然に見える配色を想定する。

ただし、次の部分は通常のコードとしてハイライトする。

- `{...}` 内の式
- `@` で始まる式行
- `//` と `/* ... */` コメント

### `.kel` のハイライト対象

`.kel` は予約キーワードを持たない key/value ベース文法として扱う。
VS Code 拡張は、少なくとも次の最小ハイライトを提供する。

| 種別 | 対象 |
|---|---|
| コメント | `// ...` と `/* ... */` |
| 文字列 | ダブルクオート文字列 |
| キー | 英数字、`_`、`.` からなる key |
| identifier 値 | クオートされていない値 |
| 数値 | 数値リテラル |
| boolean | `true`, `false` |
| 区切り・構造 | `=`, `{`, `}` |

意味論に基づく特定キーの色分けは、後段の処理系仕様が確定してから追加する。

## 定義ジャンプ

定義ジャンプは Language Server の `textDocument/definition` で提供する。

### `.ke` でのジャンプ対象

| カーソル位置 | ジャンプ先 |
|---|---|
| `import Common` の `Common` | 解決された `Common.ke` |
| 変数参照 | 対応する `var` 定義または代入可能な宣言 |
| 関数呼び出し | `fn` 定義または組み込み命令の仕様定義 |
| クラス名 | `class` 定義 |
| enum名 | `enum` 定義 |
| enumメンバー | 対応する enum メンバー定義 |
| actor名 | `actor` 定義、または `cast` により有効化された actor 定義 |
| メンバーアクセス | 対応するフィールドまたはメソッド定義 |
| `jump #tag` のタグ | `label #tag`、タグ付き `say`、タグ付き `nar` |
| `case "..." #tag` のタグ | `label #tag`、タグ付き `say`、タグ付き `nar` |
| 素材ID | 解決可能な素材ファイル |

組み込み命令の定義ジャンプは、実ファイルが存在しない場合、仮想ドキュメントを開いて説明を表示してよい。

### `.kel` でのジャンプ対象

| カーソル位置 | ジャンプ先 |
|---|---|
| 文字列または identifier のパス値 | 対応する `.kc` ファイル |
| identifier 値 | 対応する `.kel` 内定義 |

`.kel` では予約キーを前提にせず、値の形と処理系ルールに応じてジャンプ先を解決する。

### 解決範囲

定義解決は次の順で行う。

1. 現在のファイル内スコープ
2. `import` された `.ke` ファイル
3. 同一プロジェクトの actor / class / enum / fn インデックス
4. 組み込み命令・組み込み型
5. `kes.xml` の `Paths.Assets` 配下の素材

同名定義が複数ある場合は、言語仕様のスコープ規則に従う。
曖昧で一意に決められない場合は、複数候補を `LocationLink[]` として返す。

## キーワード補完

補完は Language Server の `textDocument/completion` で提供する。
補完候補は文脈に応じて絞り込む。

### 補完対象

| 文脈 | 候補 |
|---|---|
| 行頭 | 予約語、組み込み構文、組み込み命令、ユーザー定義関数 |
| `import` の後 | import 可能な `.ke` モジュール名 |
| `var` の後 | スニペット、型注釈候補 |
| 型注釈位置 | `number`, `bool`, `string`, `Actor`, ユーザー定義 class / enum、配列型候補 |
| `new` の後 | class 名 |
| `using` の後 | `dispose` 可能な class 名、組み込み using 対応命令 |
| `say` の actor 位置 | actor 名、cast 済み actor 名 |
| `case` / `jump` のタグ位置 | `label`、タグ付き `say`、タグ付き `nar` のタグ |
| 命令引数 | 変数、actor、enum メンバー、素材ID |
| 名前付き引数 | 対象命令・関数の引数名 |
| `.` の後 | メンバー、メソッド |
| `say` / `nar` の `{...}` 内 | 式で使える変数、関数、演算子スニペット |
| `say` / `nar` の `@` 後 | 式、命令、補助スニペット |

### キーワード候補

最低限、次の候補を提供する。

```txt
import var fn class enum actor public private
if else while for in break continue using as return new
select case label jump
say nar
true false null
number bool string Actor void
```

### 組み込み命令候補

言語仕様に定義された主要組み込み命令を候補に含める。

```txt
rt_back
rt_front
bg
show
trans
face
action_jump
vo
camera_autofocus
```

### スニペット

代表的な構文にはスニペットを提供する。

```kes
say ${1:Actor}:
    ${0}
```

```kes
select:
    case "${1:選択肢}" #${2:tag}
```

```kes
using ${1:change_scene} "${2:crossfade}":
    ${0}
```

```kes
if ${1:condition}:
    ${0}
```

スニペットは明示的な候補として表示し、通常のキーワード補完より過度に優先しない。

## 自動フォーマット

自動フォーマットは Language Server の `textDocument/formatting` と `textDocument/rangeFormatting` で提供する。
保存時フォーマットにも対応する。

### 基本規則

| 項目 | 規則 |
|---|---|
| インデント | スペースのみを使用する |
| 既定インデント幅 | 4スペース |
| 改行コード | VS Code のファイル設定に従う |
| 文字エンコーディング | UTF-8 を推奨する |
| 行末空白 | 削除する |
| ファイル末尾 | 1つの改行を置く |
| 空行 | 連続空行は最大2行までに圧縮する |

インデント幅は VS Code の `editor.tabSize` を優先する。
`editor.insertSpaces` が `false` の場合でも、KES ファイルではスペースを使用する。

### ブロック整形

末尾が `:` の行の直後にあるブロックは、親行より1段深く整形する。

```kes
using change_scene "crossfade":
    bg _bg_jitaku
    show Noa 0 face="normal"
```

`if` / `else if` / `else`、`while`、`for`、`using`、`select`、`say`、`nar`、LESS 構文、`fn`、`class`、`enum`、`actor` のブロックを対象とする。

### スペース整形

| 対象 | 規則 |
|---|---|
| 代入演算子 | `name = value` のように前後へ1スペースを置く |
| 比較・算術・論理演算子 | `a + b`, `x == y` のように前後へ1スペースを置く |
| 名前付き引数 | `key=value` のように `=` 前後へスペースを置かない |
| 型注釈 | `name: string` のように `:` 後へ1スペースを置く |
| ブロック開始 | `say Riku:` のように `:` 直前へ余分なスペースを置かない |
| コメント | `// comment` のように `//` 後へ1スペースを推奨する |
| セミコロン | `cmd1; cmd2` のように `;` 後へ1スペースを置く |

名前付き引数と代入文は構文上区別して整形する。
たとえば `show Noa 0 face = "normal"` は `show Noa 0 face="normal"` に整形する。

### `say` / `nar` 本文の保護

シナリオ本文は文章としての意図を優先し、次の整形のみ行う。

- ブロックとしてのインデントをそろえる
- 行末空白を削除する
- `{...}` 内の式をコードとして整形する
- `@...` 行をコードとして整形する

本文の句読点、全角スペース、文中の連続スペース、改行位置は変更しない。

### フォーマットできない場合

構文エラーがある場合でも、可能な範囲で整形する。
ただし、インデント構造を安全に復元できない場合はフォーマットを中止し、診断のみ表示する。

## 文法エラーチェック

文法エラーチェックは Language Server の `textDocument/publishDiagnostics` で提供する。
保存時だけではなく、編集中にも遅延実行する。

### 診断タイミング

| タイミング | 内容 |
|---|---|
| ファイルを開いた時 | 対象ファイルを解析して診断する |
| 入力中 | 300ms から 800ms 程度の debounce 後に対象ファイルを再解析する |
| 保存時 | 対象ファイルと依存ファイルを再解析する |
| `kes.xml` 変更時 | プロジェクト全体の解決情報を再構築する |
| コマンド実行時 | ワークスペース全体を検証する |

### 診断分類

CLI と同じ診断コード体系を使用する。

| 範囲 | VS Code severity | 分類 |
|---|---|---|
| `KES1xxx` | Error | 構文エラー |
| `KES2xxx` | Error | コンパイルエラー |
| `KES3xxx` | Error | 実行時エラー相当の静的検出 |
| `KES4xxx` | Warning | 警告 |
| `KES9xxx` | Error | ツール・設定エラー |

VS Code の Problems パネルには、ファイル、行、列、診断コード、メッセージを表示する。
診断コードにはドキュメントリンクを関連付けられるようにする。

### 構文エラー

最低限、次の構文エラーを検出する。

| 例 | 分類 |
|---|---|
| 閉じていない文字列 | `KES1xxx` |
| 閉じていないブロックコメント | `KES1xxx` |
| タブによるインデント | `KES1xxx` |
| 同一ブロック内のインデント不一致 | `KES1xxx` |
| `:` が必要な構文で `:` がない | `KES1xxx` |
| `import` 文がファイル先頭以外にある | `KES1xxx` |
| 予約語を識別子として使用している | `KES1xxx` |
| `say` / `nar` ブロックが空 | `KES1xxx` |
| `{...}` の式埋め込みが閉じていない | `KES1xxx` |
| `case` の構文が不完全 | `KES1xxx` |
| `label` / `jump` のタグ形式が不正 | `KES1xxx` |

### コンパイルエラー

プロジェクト解析が可能な場合、次のエラーも表示する。

| 例 | 分類 |
|---|---|
| 未定義の変数、関数、class、enum、actor | `KES2xxx` |
| 重複定義 | `KES2xxx` |
| 未定義タグへの `jump` / `case` | `KES2xxx` |
| 型不一致 | `KES2xxx` |
| 引数数または名前付き引数の不一致 | `KES2xxx` |
| ループ外の `break` / `continue` | `KES2xxx` |
| 関数外の `return` | `KES2xxx` |
| 存在しない import 先 | `KES2xxx` |

### 警告

次の状態は警告として表示する。

| 例 | 分類 |
|---|---|
| 未使用の import | `KES4xxx` |
| 未使用の変数 | `KES4xxx` |
| 到達不能なシナリオ行 | `KES4xxx` |
| 存在しない可能性のある素材ID | `KES4xxx` |
| ボイスIDに対応する音声ファイルが見つからない | `KES4xxx` |

警告をエラーとして扱うかどうかは `kes.xml` の `Build.WarningsAsErrors` と拡張設定で切り替える。

## コマンド

拡張は VS Code のコマンドパレットに次のコマンドを提供する。

| コマンド名 | 内容 |
|---|---|
| `KoromoEventScript: Restart Language Server` | Language Server を再起動する |
| `KoromoEventScript: Check Workspace` | ワークスペース全体を検証する |
| `KoromoEventScript: Format Document` | 現在のファイルを整形する |
| `KoromoEventScript: Build` | `kes build` 相当の処理を実行する |
| `KoromoEventScript: Run` | `kes run` 相当の処理を実行する |
| `KoromoEventScript: Show Output` | 拡張の出力チャンネルを表示する |

`Build` と `Run` は CLI が利用可能な場合のみ有効化する。
CLI が見つからない場合は、設定 `koromoEventScript.cli.path` を案内する。

## 設定項目

| 設定キー | 型 | 既定値 | 意味 |
|---|---|---|---|
| `koromoEventScript.cli.path` | string | `kes` | 使用する CLI 実行ファイル |
| `koromoEventScript.languageServer.enabled` | boolean | `true` | Language Server を有効化する |
| `koromoEventScript.diagnostics.enable` | boolean | `true` | 診断を有効化する |
| `koromoEventScript.diagnostics.debounceMs` | number | `500` | 入力後に診断を開始するまでの待ち時間 |
| `koromoEventScript.diagnostics.workspace` | boolean | `true` | プロジェクト全体診断を有効化する |
| `koromoEventScript.format.enable` | boolean | `true` | フォーマッタを有効化する |
| `koromoEventScript.format.indentSize` | number | `4` | KES ファイルの既定インデント幅 |
| `koromoEventScript.completion.enableSnippets` | boolean | `true` | スニペット補完を有効化する |
| `koromoEventScript.trace.server` | string | `off` | Language Server 通信ログを出力する |

VS Code 標準の `editor.formatOnSave` が有効な場合、`.ke` / `.kel` の保存時にフォーマットを実行する。

## プロジェクト解決

Language Server は、現在のファイルから親ディレクトリへ向かって `kes.xml` を探索する。
見つかったディレクトリをプロジェクトルートとする。

`kes.xml` が見つからない場合は単一ファイルモードで動作する。
単一ファイルモードでは、現在のファイルのディレクトリを基準に import と素材参照を解決する。

### インデックス

プロジェクトモードでは、次の情報をインデックス化する。

- `.ke` ファイル一覧
- `.kel` ファイル一覧
- import 可能なモジュール名
- `fn` 定義
- `class` 定義
- `enum` 定義
- `actor` 定義
- `label`、タグ付き `say`、タグ付き `nar`
- 素材ファイル

インデックスはファイル保存、作成、削除、リネーム時に更新する。

## 出力とログ

拡張は `KoromoEventScript` 出力チャンネルを持つ。
次の情報を出力する。

- Language Server の起動・停止
- `kes.xml` の検出結果
- CLI 実行コマンドと終了コード
- 解析不能ファイルの概要
- 内部エラー

通常の構文エラーやコンパイルエラーは Problems パネルに表示し、出力チャンネルには大量出力しない。

## 受け入れ条件

初期リリースでは、次を満たすことを必須とする。

1. `.ke` を開くと予約語、コメント、文字列、タグ、`say` 本文、式埋め込みがハイライトされる。
2. `jump #tag` と `case "..." #tag` から、同一ファイル内の `label #tag` またはタグ付き `say` へジャンプできる。
3. 行頭で主要キーワードと組み込み命令が補完される。
4. `say Actor:`、`select:`、`using ...:`、`if ...:` のスニペット補完が使える。
5. ドキュメントフォーマットでインデント、演算子前後、行末空白が整形される。
6. 閉じていない文字列、インデント不一致、`:` 欠落、未定義タグ参照が Problems に表示される。
7. `kes.xml` が存在するワークスペースで、プロジェクトルートを認識できる。

## 将来拡張

初期仕様には含めないが、次の機能を追加できる設計とする。

- ホバー表示
- シンボル一覧
- 参照検索
- リネーム
- CodeLens によるボイスID、素材ID、分岐先表示
- Code Action による import 追加、未使用 import 削除、タグ生成
- `.kel` の完全な文法サポート
- Tree-sitter による高精度な構文ハイライト
- CLI と Language Server の解析キャッシュ共有
