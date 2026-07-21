# Koromo Event Script 言語仕様書

KoromoEventScript (KES) は、RPG・ADV・ノベルゲーム向けのシナリオDSLです。
本仕様は、吉里吉里/KAG 系のノベルゲームスクリプトで一般的に必要になる機能群を参考にしつつ、KSE 独自の方針として「シナリオライターが流れるように書けること」「パーサ・LSP・ローカライズ・Git 管理に強いこと」を重視して設計します。
KAG は本文中にタグを埋め込む方式ですが、KSE はタグ埋め込み型には寄せず、ブロック構文と命令構文を明確に分けます。

## 基本要件と設計思想

- シナリオライターがそのまま簡易的な演出を組める、脚本に寄った文法
- 日本語などのIME切り替えを考慮した文法と、補助ツール
- 読みやすさを優先した文法（記号や省略をできるだけ避ける）
- 空行には意味を持たせず、ブロックはインデントで表現する
- ローカライズ（多言語対応）を意識した文法
- 直列と並列命令を明示的に書き分けやすい文法
- cliコマンドを通じて、シナリオライティング→スクリプティング→デバッグまで高速に検証できるワークフロー
- ノベルゲームに必要十分な組み込み命令を実装する
- macro分を備え、独自拡張がしやすい構造
- Parser / LSP / Tree-sitter が実装しやすいIDEライクな言語仕様
- Unity / UnrealEngine 向けのエディタ拡張

## シーン構造

シーンはレイヤー構造になっており、
zが少ないほうが手前、多いほうが奥となる

```txt
z:2     -2  -1  0  +1  +2
z:1     -2  -1  0  +1  +2
z:0     -2  -1  0  +1  +2

         camera ⇑ 
```

## オートフォーカスシステム

```kes
camera_autofocus true
```

上記コマンドを入れた行から、カメラフォーカスシステムがONになる。
カメラフォーカスシステムでは、そのシーンに内に配置されている全アクターのバウンディングボックスを計算し、
それにフィットするよう、自動的にカメラのFOVと位置が調整される。

例：
※黒丸の位置にアクターがいる想定

デフォルトで0の位置だけにアクターがいる

```txt
z:2     -2  -1  0  +1  +2
z:1     -2  -1  0  +1  +2
z:0     -2  -1  ●  +1  +2
              \   /
         camera ⇑
```

1,2の位置にアクターを追加すると、1の位置にカメラが移動する
FOVも3体が収まるように広がる

```txt
z:2     -2  -1  0  +1  +2
z:1     -2  -1  0  +1  +2
z:0     -2  -1  ●   ●   ●
              \           /
             camera ⇑
```

## 目次

- スクリプト概要
- ファイル構造
- ブロック構文
- 字句・記法
- コメント
- エラー分類
- import
- 変数定義
- 式と四則演算
- 配列
- enum定義
- トップレベル関数
- class定義
- スコープ規則
- 命令構文
- LESS (List Expansion Syntax Sugar)
- actor構文
- cast と actor
- テキスト構文 (`say`)
- 選択肢構文
- `if` 構文
- ループ構文
- `using` 構文
- 主要組み込み命令
- サンプルコード

## スクリプト概要

KES のソースファイルは、上から順に評価される命令列で構成される。
基本単位は1行1命令であり、空行は無視される。

本言語は python ライクなインデントベースのブロック構造を持つ。
詳細は「ブロック構文」で定義する。

命令には以下の2種類がある。

- 通常の関数呼び出しとして解釈される命令
- `say`、`using`、`if`、`while`、`for` のような組み込み構文

通常命令は原則として1行で完結するが、LESS により複数行の糖衣構文へ展開できる。
一方、組み込み構文はそれぞれ固有の展開規則と実行規則を持つ。

## ファイル構造

1つの KES ファイルは、先頭に `import` 文群をまとめて記述し、その後ろにその他の命令や定義を置く。

`import` 文はファイル先頭に連続して並んでいなければならず、`import` 文より前に `import` 以外の記述を置くことはできない。
いったん `var`、初期化命令、ロード命令、シナリオ命令などの非 `import` 文が現れた後は、それ以降に `import` 文を書くことはできない。

一方で、`import` 以外の記述順には言語仕様上の制約はない。
`var`、`fn`、`class`、`enum`、`actor`、初期化命令、ロード命令、シナリオ命令は任意の順序で記述できる。
実行時に処理を行う文は、ファイルに記述された順で処理される。
`fn`、`class`、`enum`、`actor` の宣言は実行時命令ではなく、コンパイル時に名前と型を登録する。

典型例:

```kes
import Common

var _bg_jitaku="bg_自宅"
var _bg_living="bg_リビング"

cast:
    Riku
    Amane

bg _bg_jitaku
say Riku:
    俺は、かぐやに連絡してみる
```

`fn`、`class`、`enum`、`actor` などの宣言構文はファイル内に記述できる。
少なくとも本仕様書のサンプルコードで使用している範囲では、ファイルは逐次実行される命令列として読めばよい。

## ブロック構文

本言語では、python ライクなブロックの仕組みを持つ。
ある行に続くスペースによる字下げ行群を、ひとつのブロックとして認識する。
インデント幅にも意味があり、スペース数によってブロックのネストを表現する。

### ブロックの開始

ブロックを開始できる行は、原則として末尾が `:` で終わる行である。
その直後の行が、親行より深いインデントを持つ場合、それらはその親行に属する子ブロックとなる。

```kes
cast:
    Riku
    Amane

using change_scene "crossfade":
    bg _bg_jitaku_focus
    show Noa 0 bustup=true face="normal"
```

### ブロックの終了

ブロックは、親行より浅いインデントに戻った地点で終了する。
同じ深さのインデントに戻った場合は、同一階層の次の行として扱う。

```kes
using change_scene "circle":
    bg _bg_living

show Kurumi -1 "normal"
say Kurumi:
    やーほー。
```

この例では、`show Kurumi -1 "normal"` は `using` ブロックの外側にある新しい命令である。

### ネスト

ネストしたブロックは、より深いインデントで表現する。

```kes
change_scene:
    bg _bg_living
    show:
        Kurumi 0 "normal"
        Noa 1 "normal"
```

この例では、`show:` は `change_scene:` の子ブロックであり、`Kurumi 0 "normal"` と `Noa 1 "normal"` の2行は `show:` に属する同一ブロックである。

### インデント規則

インデントにはスペースのみを用いるものとし、タブ文字は使用しない。
同一ブロック内では、すべての行が同じインデント幅でそろっていなければならない。
たとえば4スペースで開始したブロックの途中に2スペースや6スペースの行を混在させることはできない。

ブロックの深さは相対的に解釈されるため、1段を2スペースにするか4スペースにするかは実装・プロジェクト規約で定めてよい。
ただし、ひとつのファイル内では同一の段数表現を維持することを推奨する。

### 空行

空行はブロック構造に影響しない。
空行を挟んでも、次の非空行が同じインデント深度を持つなら同じブロックに属する。

### エラー条件

インデントが親行以下になっているにもかかわらずブロック本文を書いた場合や、同一階層でインデント幅が不一致な場合は構文エラーとする。

## 字句・記法

### 識別子

識別子は、関数名、変数名、actor 名、モジュール名などに用いる。

識別子には日本語を含む Unicode 文字を使用できる。
使用できる記号は `_` のみとし、それ以外の記号は識別子に含められない。
識別子の先頭文字に数字は使用できない。
識別子は大文字小文字を区別する。

例:

- `Common`
- `Riku`
- `_bg_jitaku`
- `change_scene`

有効な例:

- `くるみ`
- `背景_自宅`
- `Noa`
- `noa`

無効な例:

- `123abc` : 先頭が数字
- `bg-jitaku` : `_` 以外の記号を含む
- `say!` : `_` 以外の記号を含む

### 予約語

次の語は予約語であり、識別子として使用できない。

```txt
import var fn class enum actor public private
if else while for in break continue using as return new
select case label jump
true false null
```

予約語は構文上の曖昧さを避けるため、大文字小文字を区別したうえで予約する。
たとえば `if` は予約語だが、`If` は通常の識別子として扱える。

### 予約内部名

`__systemcall__` は標準ライブラリ実装から VM/runtime 側の機能を呼び出すための予約内部名である。
通常のシナリオ `.kc` から直接呼び出すことはできない。
また、ユーザー定義関数、変数、class、enum、actor 名として使用できない。

`__systemcall__` の詳細は `kes-language-stl-spec.md` で定義する。

### 文字列

文字列リテラルはダブルクオートで囲む。
ダブルクオートそのものを出したい場合は\"のようにエスケープ記法を使う。
KES言語では、シングルクオートによる囲いはサポートしない。

```kes
trans "crossfade"
face Amane "通常"
```

### 引数

引数は空白区切りで記述する。
また、`key=value` 形式の名前付き引数を用いてもよい。

```kes
show Noa 0 bustup=true face="normal"
face exp="eye_open" no_wait=true:
    Orie
    Kurumi
    Kaguya
```

位置引数と名前付き引数は同一命令内で併用できる。
引数に演算式や関数呼び出し結果を渡したい場合は、式中の関数呼び出しと同じく値全体を丸括弧で囲む。

```kes
show Noa (basePosition + 1)
face actor=(get_current_actor()) exp="normal"
```

## コメント

1行コメントと複数行コメントをサポートする。

```kes
// 1行コメント

/*
    複数行コメント
*/
```

コメントは構文解析上無視される。

## エラー分類

KES のエラーは、構文エラー、コンパイルエラー、実行時エラー、警告に分類する。

構文エラーは、字句解析または構文解析の段階で検出できる誤りである。
インデント不一致、閉じていない文字列、予約語の不正使用、ブロック開始行の `:` 欠落などが該当する。
構文エラーがあるファイルはコンパイルを継続しない。

コンパイルエラーは、構文としては読めるが、名前解決、型検査、スコープ規則、制御フロー検査に違反する誤りである。
未定義名の参照、型不一致、異なる型同士の演算、戻り値不足、ループ外の `break` / `continue`、関数外の `return` などが該当する。
コンパイルエラーがあるファイルは実行できない。

実行時エラーは、コンパイル時には確定できず、実行中に検出される誤りである。
配列の範囲外アクセス、初期化前トップレベル変数の参照、存在しないリソースへのアクセスなどが該当する。

警告は、実行を継続できるが確認が必要な状態である。
ボイスIDや画像IDに対応するリソースが見つからない場合などが該当する。
警告をエラーとして扱うかどうかは、CLI やプロジェクト設定で切り替えられるものとする。

## import

外部モジュールの読み込みには `import` 文を用いる。

```kes
import Common
```

`import` 文は必ずファイル先頭にまとまって並ばなければならない。
`import` 以外の文が1つでも出現した後に `import` 文を書いた場合は構文エラーとする。

モジュール名はファイル名から拡張子を除いた名前として解決する。
パスは解決に含めないため、異なるディレクトリに同名モジュールを複数置くことは想定しない。

## 変数定義

変数定義には `var` を用いる。

型を明示する場合は、変数名の後ろに後置で `: <type>` を書く。

```kes
var hoge: string = ""
var actor: Actor = null
```

```kes
var _bg_jitaku="bg_自宅"
var _bg_living="bg_リビング"
var message: string = "こんにちは"
```

変数は後続の命令から参照できる。
文字列、数値、真偽値、式評価結果などを格納できる想定とする。

初期代入式がある場合、その式から型推論できるため、型注釈は省略可能である。

```kes
var name = "Noa"
var index = 10
var enabled = true
```

一方、初期代入式がない変数定義では型推論ができないため、型指定を必須とする。

```kes
var currentActor: Actor
var title: string
```

プリミティブ型は次の2種類のみを持つ。

- `number`
- `bool`

string型は組み込みの参照型である。
リテラルを代入する場合は、内部的に新しいstringオブジェクトが作られる。
(コピーコンストラクタが呼ばれる)

```kes
var strA = "hogehoge"
var strB = strA //strAの参照先とstrBの参照先は同じ
print strA //hogehoge
print strB //hogehoge

// 参照型だが、リテラルを代入するとコピーコンストラクタで新しいオブジェクトが作られるので、
// strAとstrBは別参照を持つことになる
strB = "fugafuga"
print strA //hogehoge
print strB //fugafuga
```

特殊値として `null` を持つ。
`null` は参照が存在しないことを表す値であり、`string`、`Actor`、ユーザー定義クラス、配列などの参照型に代入できる。
`number`、`bool`、`enum` などの値型には代入できない。

`Actor` はアクター型として特別扱いされる。
それ以外のユーザー定義型(クラス)は参照型として扱う。

`_` は特殊な無名変数名として扱う。
値を一時的に受け取り、表示せず破棄したい場合に用いる。

```kes
@var _ = sum 1 2 3
```

## 式と四則演算

KES の式は、変数定義の初期値、代入、命令引数、名前付き引数の値、`say` / `nar` 本文中の `{...}`、`@` から始まる式行で使用できる。

```kes
var base = 10
var count = base + 2 * 3
show Noa count

say Noa:
    合計は {count + 1} だよ
```

### 四則演算子

数値型 `number` に対して、次の四則演算子をサポートする。

| 演算子 | 意味 | 例 |
|---|---|---|
| `+` | 加算 | `1 + 2` |
| `-` | 減算 | `10 - 3` |
| `*` | 乗算 | `2 * 4` |
| `/` | 除算 | `8 / 2` |

演算子の優先順位は一般的な算術式と同じく、`*` と `/` が `+` と `-` より強い。
同じ優先順位の演算子は左結合で評価する。
丸括弧 `(` `)` によって評価順を明示できる。

```kes
var a = 1 + 2 * 3      // 7
var b = (1 + 2) * 3    // 9
var c = 10 - 3 - 2     // 5
```

`+` は両辺が `number` の場合は加算として扱う。
文字列連結は専用関数または標準ライブラリで扱うことを想定し、算術演算子 `+` による暗黙の文字列連結は行わない。

四則演算を含む二項演算では、左右の型が演算子の要求する型と一致していなければならない。
異なる型同士の演算、または演算子が対応しない型の組み合わせはコンパイルエラーとする。

```kes
var n = 10 + 2       // OK
var x = 10 + "2"     // コンパイルエラー
var y = true * 3     // コンパイルエラー
```

### 単項演算子

数値式の前には単項 `+`、単項 `-` を付けられる。

```kes
var x = -10
var y = -(1 + 2)
```

### 関数呼び出し式

式中でも通常命令と同じく、空白区切りで関数呼び出しを書ける。
式中の関数呼び出しではカンマ区切りの引数リストは用いない。

```kes
var total = sum 1 2 3
say Noa:
    今日の日付は {format_date currentDate} だよ
```

引数に演算式を渡したい場合は、その式を丸括弧で囲む。
また、関数呼び出しの評価結果にさらに演算を適用したい場合も、呼び出し全体を丸括弧で囲む。

```kes
var total = sum (1 + 2) (base * 3)
var doubled = (sum 1 2 3) * 2

say Noa:
    合計は {(sum 1 2 3) + bonus} だよ
```

名前付き引数も通常命令と同じく `key=value` 形式で書く。
名前付き引数の値に演算式や関数呼び出しを渡す場合は、値全体を丸括弧で囲む。

```kes
var message = format_score value=(score + bonus) unit="pt"
```

式中の関数呼び出しは、引数を1つ以上持つ場合に関数呼び出しとして解釈する。
識別子だけを単独で書いた場合は変数参照として扱う。
引数を持たない関数を式中で呼び出す場合は、変数参照と区別するため `name()` と書く。

```kes
var nowText = current_time()
```

### 代入式

既存の変数には `=` で再代入できる。
代入は文として扱い、表示値を返さない。

```kes
var score = 0
score = score + 10
```

### 比較演算子

`if` 構文などの条件式で使うため、次の比較演算子をサポートする。

| 演算子 | 意味 | 対象型 |
|---|---|---|
| `==` | 等しい | 同一型同士 |
| `!=` | 等しくない | 同一型同士 |
| `<` | より小さい | `number` |
| `<=` | 以下 | `number` |
| `>` | より大きい | `number` |
| `>=` | 以上 | `number` |

比較演算の結果は `bool` である。
`==` と `!=` は左右が同一型の場合のみ使用できる。
ただし、参照型の値は `null` と `==` / `!=` で比較できる。
異なる型同士の比較は、参照型と `null` の比較を除きコンパイルエラーとする。

### 論理演算子

`bool` 型に対して、次の論理演算子をサポートする。

| 演算子 | 意味 | 例 |
|---|---|---|
| `&&` | 論理積 | `enabled && score >= 70` |
| `\|\|` | 論理和 | `hasTicket \|\| isDebug` |
| `!` | 否定 | `!enabled` |

`!` は単項演算子であり、`&&` は `||` より優先順位が高い。
`&&` と `||` は短絡評価を行う。
左辺だけで結果が確定する場合、右辺は評価されない。

```kes
if actor != null && actor.isVisible:
    show actor 0
```

論理演算子の対象は `bool` のみである。
`number`、`string`、参照型などを暗黙に真偽値へ変換することはせず、`bool` 以外に論理演算子を適用した場合はコンパイルエラーとする。

## 配列

配列は同じ型の値を順序付きで保持する参照型である。
配列リテラルは角括弧 `[` `]` で書き、要素はカンマで区切る。

```kes
var points = [10, 20, 30]
var names: string[] = ["Riku", "Noa", "Amane"]
```

配列型は `T[]` と書く。
`T` には `number`、`bool`、`string`、`Actor`、ユーザー定義クラス型、または配列型を指定できる。

```kes
var actors: Actor[]
var matrix: number[][] = [[1, 2], [3, 4]]
```

空配列 `[]` は要素型を推論できないため、型注釈を必須とする。

```kes
var queue: string[] = []
```

### 要素アクセス

配列要素には `array[index]` でアクセスする。
インデックスは `0` 始まりで、範囲外アクセスは実行時エラーとする。

```kes
var names = ["Riku", "Noa", "Amane"]
say Noa:
    先頭は {names[0]} だよ

names[1] = "Kurumi"
```

配列の長さは標準関数 `array_len array` で取得する。

実行時に決まる長さの配列は `new T[count]` で生成する。`T` は `number`、`bool`、`string` のいずれかとし、全要素はそれぞれ `0`、`false`、空文字列で初期化する。初期値を明示する場合は `new T[count](value)` と書く。`count` は0以上の整数でなければならず、負数、小数、実行環境の上限を超える値は実行時エラーとする。

```kes
var flags: bool[] = new bool[candidate + 1](true)
var scores: number[] = new number[player_count]
```

```kes
var count = array_len names
```

## enum定義

`enum` 構文は、取りうる値が決まっている列挙型を定義する。
表情、演出状態、選択肢結果など、文字列で書くと間違いやすい値を型として扱いたい場合に用いる。

```kes
enum Mood:
    normal
    smile
    angry
```

`enum` 名は型名として使用できる。
列挙子は `<enum名>.<列挙子名>` で参照する。
列挙子名は識別子規則に従い、同一 `enum` 内で重複してはならない。

```kes
var mood: Mood = Mood.normal

if mood == Mood.smile:
    face Noa "smile"
```

`enum` の値同士は、同一の `enum` 型である場合のみ `==` と `!=` で比較できる。
異なる `enum` 型同士の比較や、`enum` と `string` / `number` など異なる型との比較はコンパイルエラーとする。

```kes
enum Route:
    common
    noa

var mood = Mood.normal
var route = Route.common

var ok = mood == Mood.normal      // OK
var ng = mood == route            // コンパイルエラー
var ng2 = mood == "normal"        // コンパイルエラー
```

列挙子は暗黙に数値や文字列へ変換しない。
表示名や外部IDが必要な場合は、標準ライブラリまたはプロジェクト側の関数で変換する。

## トップレベル関数

トップレベル関数は `fn` で定義する。
クラスに属さない共通処理、条件判定、計算、演出補助などを定義するために用いる。

```kes
fn calc_score(base: number, bonus: number): number:
    return base + bonus

var score = calc_score 70 10
```

引数は `name: type` 形式で書き、複数ある場合はカンマで区切る。
戻り値型を書く場合は引数リストの後ろに `: <type>` を付ける。
戻り値型を省略した場合は `void` とする。

```kes
fn setup_actor(actor: Actor, faceName: string):
    show actor 0
    face actor faceName
```

トップレベル関数は宣言であり、ファイルの逐次実行時に関数本体は実行されない。
関数本体は、その関数が呼び出された時点で実行される。
同一モジュール内のトップレベル関数は、定義位置より前から呼び出せる。

関数呼び出しは、式中でも通常命令と同じく空白区切りで書く。
呼び出し結果にさらに演算を行う場合は、呼び出し全体を丸括弧で囲む。

```kes
var total = (calc_score 70 10) + 5
```

### `return`

`return` は関数またはメソッドの実行を終了し、呼び出し元へ戻る。
戻り値型が `void` の場合、値付き `return` はコンパイルエラーとする。
戻り値型が `void` 以外の場合、`return` の値は宣言された戻り値型と一致しなければならない。

```kes
fn is_high_score(score: number): bool:
    if score >= 80:
        return true

    return false
```

戻り値型が `void` 以外の関数では、すべての実行経路で値付き `return` に到達しなければならない。
到達しない可能性がある場合はコンパイルエラーとする。
`return` を関数またはメソッドの外で使用した場合もコンパイルエラーとする。

## class定義

`class` 構文はユーザー定義の参照型を定義する。
クラス定義は実行時オブジェクトの設計図であり、`new` 式または `using` 構文によってインスタンス化できる。

```kes
class change_scene:
    private var _fx: string

    fn __init__(fx: string):
        _fx = fx
        rt_back

    fn dispose():
        rt_front
        trans _fx
```

クラス名は識別子規則に従う。
`class` で定義した名前は型名として使用できる。

```kes
var scene: change_scene
```

### メンバー変数

クラスブロック内では `var` によってメンバー変数を定義する。
メンバー変数にはアクセス修飾子 `public` または `private` を付けられる。
省略時は `public` とする。

```kes
class Counter:
    private var _value: number = 0
    var name: string = "counter"
```

### メソッド

メソッドは `fn` で定義する。
引数は `name: type` 形式で書き、複数ある場合はカンマで区切る。
戻り値型を書く場合は引数リストの後ろに `: <type>` を付ける。
戻り値型を省略した場合は `void` とする。

```kes
class Counter:
    private var _value: number = 0

    fn add(value: number): number:
        _value = _value + value
        return _value
```

メソッド本体では、同じクラスのメンバー変数とメソッドを名前で参照できる。
ローカル変数や引数がメンバー名と衝突する場合はコンパイルエラーとする。

### コンストラクタ、dispose、デストラクタ

`__init__` はコンストラクタであり、`new` 式または `using` による生成時に呼び出される。
`dispose` は `using` ブロック終了時に自動で呼び出される。
`__destroy__` は実行系がオブジェクト破棄時に呼び出す特殊メソッドとして予約する。

```kes
class ScopedEffect:
    fn __init__():
        rt_back

    fn dispose():
        rt_front

    fn __destroy__():
        skip
```

### インスタンス生成とメンバーアクセス

通常のインスタンス生成には `new ClassName <arg>*` を用いる。
メンバーアクセスは `.` を用いる。

```kes
var counter = new Counter
var value = counter.add 1
```

`private` メンバーは定義クラスの外側から参照できない。

## スコープ規則

KES の名前解決は、モジュールスコープ、関数/メソッドスコープ、ブロックスコープ、クラススコープに基づいて行う。
名前は内側のスコープから外側のスコープへ順に探索する。

### モジュールスコープ

ファイルのトップレベルに定義された `var`、`fn`、`class`、`enum`、`actor` はモジュールスコープに属する。
`fn`、`class`、`enum`、`actor` は宣言として扱われ、同一モジュール内では定義位置より前から参照できる。

トップレベルの `var` は、ファイルの逐次実行中にその行へ到達した時点で初期化される。
初期化前のトップレベル変数を実行時に参照した場合は実行時エラーとする。

```kes
fn print_title():
    print title

var title = "Chapter 1"
print_title
```

### 関数/メソッドスコープ

関数またはメソッドの引数と、その直下で定義されたローカル変数は、関数/メソッドスコープに属する。
関数/メソッドスコープの名前は、その関数またはメソッドの外から参照できない。

```kes
fn calc(base: number): number:
    var bonus = 10
    return base + bonus

print bonus // コンパイルエラー
```

### ブロックスコープ

`if`、`else if`、`else`、`while`、`for`、`using`、`say`、`nar` の構文ブロックは、それぞれ新しいブロックスコープを作る。
ブロック内で定義した `var` は、そのブロックと子ブロックの中でのみ参照できる。
LESS ブロックは関数呼び出しの展開として扱い、それ自体は新しいスコープを作らない。

```kes
if enabled:
    var message = "OK"
    print message

print message // コンパイルエラー
```

`for` のループ変数と `using ... as <name>` のインスタンス名も、そのブロック内だけで有効である。

```kes
for actor in actors:
    show actor 0

show actor 0 // コンパイルエラー
```

### クラススコープ

クラスのメンバー変数とメソッドはクラススコープに属する。
メソッド本体からは、同じクラスのメンバー変数とメソッドを名前で参照できる。
クラス外から参照する場合は、インスタンスに対して `.` でアクセスする。

```kes
class Counter:
    private var _value: number = 0

    fn add(value: number): number:
        _value = _value + value
        return _value
```

### 重複定義とシャドーイング

同一スコープ内で同じ名前を複数定義した場合はコンパイルエラーとする。
また、内側のスコープで外側のスコープと同じ名前を再定義するシャドーイングもコンパイルエラーとする。
これは、シナリオ記述時の意図しない名前衝突を早期に検出するためである。

```kes
var score = 0

if enabled:
    var score = 10 // コンパイルエラー
```

ただし、クラスメンバーはクラススコープに属するため、異なるクラス間で同じメンバー名を持つことはできる。
関数名、クラス名、enum名、actor名、トップレベル変数名は同じモジュールスコープを共有するため、互いに重複できない。

## 命令構文

通常の命令は、関数名に引数を並べた1行の呼び出しとして記述する。

```kes
bg _bg_jitaku
show Amane 0
action_jump Amane
```

この形式は、概念的には以下と等価である。

```txt
<function-name> <arg0> <arg1> ...
```

組み込み構文ではない限り、命令はすべて「関数呼び出し」として扱われる。

一行に複数命令書く場合は;で区切る

```kes
bg _bg_jitaku; show Amane 0; action_jump Amane
```

## LESS (List Expansion Syntax Sugar)

### 概要

LESS は、同じ関数を複数回呼び出すための糖衣構文である。

```kes
cast:
    Riku
    Amane
    Noa
```

これは内部的に以下へ展開される。

```kes
cast Riku
cast Amane
cast Noa
```

### 共通引数付き LESS

関数名と `:` の間に空白区切りで書かれた内容は、各展開先に共通引数として付与される。

```kes
face exp="eye_open" no_wait=true:
    Orie
    Kurumi
    Kaguya
```

これは概念的に以下へ展開される。

```kes
face exp="eye_open" no_wait=true Orie
face exp="eye_open" no_wait=true Kurumi
face exp="eye_open" no_wait=true Kaguya
```

### 適用範囲

LESS はあくまで関数呼び出しの糖衣構文である。
`if`、`while`、`for`、`using` など、専用の意味論を持つ構文は LESS ではない。

また、`say`および`nar` は組み込み構文であり、LESS そのものではない。
ただし、複数行の本文を順に処理する点では、見た目上 LESS と似た展開を行う。

## actor構文

`actor` 構文は `Actor` 型を定義するための構文である。

```kes
actor Riku:
    // Actor定義
```

`actor` 構文で定義された名前は `Actor` 型として扱われる。
`Actor` は通常のクラス型とは異なる特別な組み込み型であり、シナリオ実行系におけるアクターを表す。

`actor` 構文で定義した `Actor` は、`cast` 命令によってロードした後、定義名で直接参照できる。
本仕様では、1つの actor 定義名に対して実行時インスタンスは常に1つである。

## cast と actor

`cast` は actor をロードする組み込み関数である。

```kes
cast:
    Riku
    Amane
    Noa
```

actor は通常の `class` とは異なる、シナリオ実行系における特殊オブジェクトである。
actor は `actor` 構文で定義される想定とし、`cast` によりロードされた後は actor 定義名を通じて直接参照できる。

本仕様では、1つの actor 定義名に対して実行時インスタンスは常に1つであることを前提とする。

## テキスト構文 (`say`, `nar`)

### 基本形

`say` はキャラクターの台詞を記述するための組み込み構文である。
`nar` はナレーションを記述するための組み込み構文である。

```kes
say Riku:
    俺は、かぐやに連絡してみる

nar:
    LIMEでメッセージを送ると、すぐに既読が付いた
```

`say <actor_identifier>:` に続くブロックは、シナリオテキストとして解釈される。
`say` の直後には `Actor` 型の識別子のみを指定できる。
式、関数呼び出し、メンバーアクセス、配列要素アクセスなどは指定できない。
指定した識別子が `Actor` 型でない場合はコンパイルエラーとする。
ブロック内の通常行は、表示対象の本文行となる。

`nar:` は `say` からアクター指定を除いた構文であり、それ以外の仕様は `say` と同一である。

### 複数行の台詞

```kes
say Riku:
    前世帰りみたいな大きな変化はなくても、
    何か気づいたこととかあるかもしれないしな

nar:
    思い起こすと、
    いくつかの心当たりがあった
```

複数行を記述した場合、それぞれの行は同一の話者文脈に属するテキスト行として順に処理される。
`say` では同一 actor による連続した発話単位となり、`nar` では連続したナレーション行となる。

### タグ付きテキスト

`say` と `nar` には `#id` 形式のタグを付与できる。

```kes
say Amane #sy_sample_0001:
    わかった
    {vo}
    待ってるわ

nar #na_sample_0002:
    その日のことを、私はまだよく覚えている。
```

このタグは、ローカライズキー、ボイス検索キー、デバッグ用識別子などに利用できる。

### 自動ボイス再生

タグ付き `say` および `nar` は、タグを基点として自動的にボイスIDを組み立てて再生する。

上記の例では、1行目で `sy_sample_0001_1` が再生される。
`{vo}` のように明示的なボイス再生命令を本文中に書いた場合、連番は進み、次のボイスは `sy_sample_0001_2` となる。

`say` または `nar` のブロックを抜けると連番はリセットされる。
該当するボイスが存在しない場合は警告を出し、再生しない。

### テキスト構文の実行規則

`say` および `nar` 中のテキスト表示は、ほかの命令実行を妨げない。
本文表示、ボイス再生、表情変更などは並行して実行されうるが、ボイス再生自体は順次処理する。

`say` と `nar` は標準でクリック待ちを行う。
クリックされた時点で、そのブロック内で進行中の命令は即時終了し、次の処理へ進む。
音声再生中であれば音も停止する。

### 本文中の式埋め込み

`say` および `nar` の本文中では `{...}` による式埋め込みを行える。

```kes
I have {plural value one="an" other=value} apple{plural value other="s"}
```

波括弧内は式として評価され、その結果を文字列へ埋め込む。
標準ライブラリの `p` 命令は改ページを表す。
`{p}` と書くことで、同一 `say` / `nar` 文脈内にページ区切りを挿入できる。
標準ライブラリの `r` 命令は改行を表す。
`{r}` と書くことで、同一ページ内に行区切りを挿入できる。
標準ライブラリの `l` 命令は行内クリック待ちを表す。
`{l}` と書くことで、同一ページ内の途中で入力待ちを挿入できる。
標準ライブラリの `cm` 命令はメッセージウィンドウの非表示を表す。
`{cm}` または `@cm` と書くことで、現在のメッセージウィンドウを非表示にできる。

### `@` から始まる式行

`say` および `nar` のブロック内で `@` から始まる行は、その行全体を式として扱う糖衣構文である。

```kes
say Noa #sy_sample_0003:
    詳しい話を確認できるよう、
    @vf Noa "eye_close"
    一度集まってもらってもいいかもしれないね

nar:
    @format_date currentDate
```

上記の `@vf Noa "eye_close"` は、概念的には `{vf Noa "eye_close"}` と同等である。

### 複数式と表示値

式は `;` 区切りで複数記述できる。

```kes
{1+2+3; print "hoge"}
@1+2+3; print "hoge"
```

複数式を記述した場合、本文上に表示される値は最後に評価された式の結果とする。
`void` 関数や代入文のように値を返さない式は表示されない。

## 選択肢構文

`select` はノベルゲームの選択肢を表示し、選ばれた項目に対応するタグへ制御を移すための組み込み構文である。
`select:` または `select <tag>:` に続くブロックには `case` 行のみを書ける。

```kes
select #se_sample_0001:
    case "かぐやに意見を聞く" #se_sample_0002
    case "オリエに意見を聞く" #se_sample_0003
    case "乃愛に意見を聞く" #se_sample_0004
```

`case` は1行の構文であり、必ず `case <文字列リテラル> <タグ>` の順に書く。
文字列リテラルは画面上に表示される選択肢テキストである。
タグは、その選択肢が選ばれたときのジャンプ先を表す。
`select` 自体にも任意でタグを付けられる。`select` に付いたタグは、その選択 UI 自体を識別するためのタグである。

`select` は実行時にすべての `case` を画面に表示し、プレイヤーの選択を待つ。
プレイヤーが選択したら、対応するタグへジャンプする。
`select` ブロックが `case` を1つも持たない場合はコンパイルエラーとする。
`case` を `select` ブロックの外に書いた場合もコンパイルエラーとする。
`case` の選択肢文字列は、ローカライズ辞書の自動抽出対象である。

### ラベルとジャンプ

`label` はジャンプ先を定義する構文である。
`jump` は指定したタグへ無条件でジャンプする構文である。

```kes
label #se_sample_0002

say Riku:
    かぐやはどう思う？

jump #end_choice
```

`label` と `jump` はどちらも `#id` 形式のタグを1つ取る。
同じファイル内で同一タグを複数のジャンプ先として定義した場合はコンパイルエラーとする。
未定義タグを `case` または `jump` から参照した場合もコンパイルエラーとする。

ジャンプ先として使えるタグは、`label` に付けたタグ、またはタグ付きの `say` / `nar` に付けたタグである。
タグ付き `say` / `nar` に直接ジャンプした場合、その `say` / `nar` から実行を開始する。

```kes
say Riku #choice3:
    乃愛はどう思う？
```

`jump` は構造化制御構文ではなく、シナリオ進行位置を移すための命令である。
関数/メソッドの外側にあるラベルへ、関数/メソッドの内側からジャンプしてはならない。
関数/メソッドの内側にあるラベルへ、外側からジャンプしてはならない。
このようなスコープ境界をまたぐジャンプはコンパイルエラーとする。

## `if` 構文

`if` は条件に応じて実行するブロックを切り替える組み込み構文である。
条件式は `bool` 型でなければならない。
`number` や `string` などを暗黙に真偽値へ変換することはせず、`bool` 以外の式を条件に指定した場合はコンパイルエラーとする。

```kes
var score = 80

if score >= 70:
    say Noa:
        合格だよ
```

`else` を付けると、条件が `false` の場合に実行するブロックを指定できる。

```kes
if hasTicket:
    jump concert_hall
else:
    jump ticket_counter
```

複数条件を分岐したい場合は `else if` を用いる。
条件は上から順に評価され、最初に `true` になったブロックだけを実行する。
どの条件も `true` にならず、`else` がある場合は `else` ブロックを実行する。

```kes
if score >= 90:
    rank "S"
else if score >= 70:
    rank "A"
else:
    rank "B"
```

`if`、`else if`、`else` の各ブロックは通常のブロック構文に従う。
`else if` と `else` は、対応する `if` と同じインデント階層に書かなければならない。

## ループ構文

KES は `while` と `for` の2種類のループ構文を持つ。
どちらも通常のブロック構文に従い、ブロック内には命令、変数定義、`if`、`using`、さらにネストしたループを書ける。

### `while`

`while` は条件式が `true` の間、ブロックを繰り返し実行する。
条件式は `bool` 型でなければならない。
`if` と同様に、`number` や `string` を暗黙に真偽値へ変換することはしない。

```kes
var count = 0

while count < 3:
    action_jump Noa
    count = count + 1
```

条件式は各反復の先頭で評価される。
最初の評価が `false` の場合、ブロックは一度も実行されない。

### `for`

`for` は配列などの反復可能な値を先頭から順に取り出して実行する。
構文は `for <変数名> in <式>:` とする。

```kes
var actors = [Noa, Amane, Kurumi]

for actor in actors:
    face actor "normal"
```

`in` の右辺は反復可能な型でなければならない。
本仕様では、少なくとも配列型 `T[]` を反復可能とする。
ループ変数の型は反復対象の要素型から推論される。
ループ変数のスコープは `for` ブロック内に限定する。

数値範囲を反復したい場合は、標準関数 `range start end` を使う。
`range start end` は `start` 以上 `end` 未満の `number[]` を返す。

```kes
for i in range 0 3:
    print (number_to_string i)
```

### `break` と `continue`

ループ内では `break` と `continue` を使用できる。
`break` は最も内側のループを終了する。
`continue` は現在の反復を終了し、次の反復へ進む。

```kes
for actor in actors:
    if actor == null:
        continue

    if actor == target:
        break

    show actor 0
```

`break` と `continue` をループの外で使用した場合はコンパイルエラーとする。

## `using` 構文

`using` は、指定したクラスのインスタンスをスコープ付きで生成し、ブロック終了時に自動的に後始末を行う組み込み構文である。

```kes
using change_scene "crossfade":
    bg _bg_jitaku_focus
    show Noa 0 bustup=true face="normal"
```

構文は次の通り。

```txt
using <class_type_name> <constructor_arg>*:
    ...
```

`using` に続く引数は対象クラスのコンストラクタへ渡される。
生成されたインスタンスをブロック内で参照したい場合は `as` で名前を付ける。

```kes
using change_scene "crossfade" as scene:
    bg _bg_jitaku_focus
```

`using` ブロックを抜ける際には `dispose` メソッドを自動で呼び出す。
これにより、リソースの解放や、`rt_front`・`trans` のような対になる命令の呼び忘れを防げる。

また、実装側では必要に応じてコンストラクタ、`dispose`、デストラクタを定義できる。

## 主要組み込み命令

本サンプルに登場する代表的な命令を以下に示す。

| 命令 | 概要 |
|---|---|
| `rt_back` | 描画先を裏画面へ切り替える |
| `rt_front` | 描画先を表画面へ切り替える |
| `bg` | 背景を設定する |
| `show` | actor を表示する |
| `trans` | 画面遷移を実行する |
| `face` | actor の表情を変更する |
| `action_jump` | actor にジャンプ動作を実行する |
| `vo` | ボイス再生を明示的に行う |

これらはすべて通常命令としても、LESS を通じても利用できる。
ただし、`say` や `using` のような構文専用キーワードは通常命令とは異なる。

## サンプルコード

```kes
import Common

cast:
    Riku
    Amane
    Noa
    Kurumi
    Kaguya
    Orie

var _bg_jitaku="bg_自宅"
var _bg_jitaku_focus="bg_自宅_フォーカス"
var _bg_living="bg_リビング"
var _bg_living_focus="bg_リビング_フォーカス"

rt_back
bg _bg_jitaku
show Amane 0
rt_front

trans "crossfade"

say Riku:
    俺は、かぐやに連絡してみる
say Riku:
    前世帰りみたいな大きな変化はなくても、
    何か気づいたこととかあるかもしれないしな

face Amane "通常"
action_jump Amane

var hero_name = localize.get("proper_name_hero")
say Amane #sy_sample_0001:
    わかった
    {vo}
    待ってるわ
using change_scene "crossfade":
    bg _bg_jitaku_focus
    show noa 0 bustup=true face="normal"

say Noa #sy_sample_0003:
    詳しい話を確認できるよう、
    @vf Noa "eye_close"
    一度集まってもらってもいいかもしれないね

using change_scene "circle":
    bg _bg_living

show Kurumi -1 "normal"
say Kurumi:
    やーほー。
    @vf "smile"
    参上つかまつったよー

show Kaguya 0 "smile"
say Kaguya:
    こんにちは

show Orie 1 "eye_close"
say Orie:
    お邪魔いたします

say Riku:
    一緒だったのか

face exp="eye_open" no_wait=true:
    Orie
    Kurumi
    Kaguya

say Orie:
    こちらで来る途中で一緒に

say Riku:
    改めて確認するけど、小雲雀さんには何の変化もないんだよな？

change_scene:
    bg _bg_living_focus
    show Kurumi

face Kurumi "kyoton"
say Kurumi:
    まったくない

face Kurumi "think"
say Kurumi:
    海では楽しく過ごしたし、
    @vf "kyoton"
    家に帰ってからもごくごく普通。{p}
    @vf "niya"
    いつもより、
    @vf "eye_close"
    ぐっすり眠れたくらい

change_scene:
    bg _bg_living
    show:
        Kurumi 0 "normal"
        Noa 1 "normal"

say Noa:
    起きた時に、何か違和感なんかは？

face Kurumi "think"
say Kurumi:
    そっちも別に
select #se_sample_0001:
    case "かぐやに意見を聞く" #se_sample_0002
    case "オリエに意見を聞く" #se_sample_0003
    case "乃愛に意見を聞く" #se_sample_0004
label #se_sample_0002

say Riku:
    かぐやはどう思う？

face Kaguya "think"
say Kaguya:
    そうね…、心当たりはないわ
jump #end_choice

label #se_sample_0003
say Orie:
    私に聞かれましても困ります
jump #end_choice

say Riku #sy_sample_0005:
    乃愛はどう思う？

face Noa "bikkuri"
say Noa:
    ボク！？　えっと、そういわれてもなあ

label #end_choice

say Riku:
    そうか…

```

## 現状文法BNF

このBNFは、本仕様書で説明している現状の構文をまとめたものである。
字句解析、インデント解釈、コメント除去、空行除去は構文解析の前段で行われるものとする。

```bnf
<script> ::= <import_section>? <top_level_item>*

<import_section> ::= <import_stmt>+
<import_stmt> ::= "import" <identifier> <newline>

<top_level_item> ::=
      <class_decl>
    | <enum_decl>
    | <fn_decl>
    | <actor_decl>
    | <stmt>

<block> ::= <indent> <block_item>+ <dedent>
<block_item> ::= <stmt>

<stmt> ::=
      <var_decl> <newline>
    | <assignment> <newline>
    | <return_stmt> <newline>
    | <say_stmt>
    | <nar_stmt>
    | <select_stmt>
    | <label_stmt> <newline>
    | <jump_stmt> <newline>
    | <if_stmt>
    | <while_stmt>
    | <for_stmt>
    | <loop_control_stmt> <newline>
    | <using_stmt>
    | <less_stmt>
    | <command_line> <newline>

<var_decl> ::= "var" <identifier> <type_annotation>? ("=" <expr>)?
<type_annotation> ::= ":" <type>
<type> ::= <base_type> "[]"*
<base_type> ::= <identifier> | <primitive_type>
<return_type> ::= <type> | "void"
<primitive_type> ::= "number" | "bool" | "string" | "Actor"

<assignment> ::= <assignable> "=" <expr>
<assignable> ::= <identifier> | <member_access> | <index_access>
<return_stmt> ::= "return" <expr>?

<command_line> ::= <command_stmt> (";" <command_stmt>)*
<command_stmt> ::= <call_target> <command_arg>*
<command_arg> ::= <simple_arg_expr> | <named_arg>
<named_arg> ::= <identifier> "=" <simple_arg_expr>

<less_stmt> ::= <identifier> <command_arg>* ":" <newline> <less_block>
<less_block> ::= <indent> <less_item>+ <dedent>
<less_item> ::= <command_arg>+ <newline> | <command_stmt> <newline> | <less_stmt>

<say_stmt> ::= "say" <actor_identifier> <tag>? ":" <newline> <text_block>
<actor_identifier> ::= <identifier>
<nar_stmt> ::= "nar" <tag>? ":" <newline> <text_block>
<tag> ::= "#" <identifier>
<text_block> ::= <indent> <text_item>+ <dedent>
<text_item> ::= <text_line> <newline> | <text_expr_line> <newline>
<text_line> ::= raw scenario text, with <expr_list> expression interpolation allowed
<text_expr_line> ::= "@" <inline_stmt_list>
<expr_list> ::= "{" <inline_stmt_list> "}"
<inline_stmt_list> ::= <inline_stmt> (";" <inline_stmt>)*
<inline_stmt> ::= <expr> | <var_decl> | <assignment> | <command_stmt>

<select_stmt> ::= "select" <tag>? ":" <newline> <select_block>
<select_block> ::= <indent> <case_stmt>+ <dedent>
<case_stmt> ::= "case" <string_literal> <tag> <newline>
<label_stmt> ::= "label" <tag>
<jump_stmt> ::= "jump" <tag>

<if_stmt> ::= "if" <expr> ":" <newline> <block> <else_if_clause>* <else_clause>?
<else_if_clause> ::= "else" "if" <expr> ":" <newline> <block>
<else_clause> ::= "else" ":" <newline> <block>

<while_stmt> ::= "while" <expr> ":" <newline> <block>
<for_stmt> ::= "for" <identifier> "in" <expr> ":" <newline> <block>
<loop_control_stmt> ::= "break" | "continue"

<using_stmt> ::= "using" <identifier> <command_arg>* ("as" <identifier>)? ":" <newline> <block>

<fn_decl> ::= "fn" <identifier> "(" <param_list>? ")" <return_type_annotation>? ":" <newline> <block>
<return_type_annotation> ::= ":" <return_type>

<enum_decl> ::= "enum" <identifier> ":" <newline> <enum_block>
<enum_block> ::= <indent> <enum_member>+ <dedent>
<enum_member> ::= <identifier> <newline>

<class_decl> ::= "class" <identifier> ":" <newline> <class_block>
<class_block> ::= <indent> <class_member>+ <dedent>
<class_member> ::= <field_decl> <newline> | <method_decl>
<field_decl> ::= <access_modifier>? <var_decl>
<access_modifier> ::= "public" | "private"
<method_decl> ::= "fn" <identifier> "(" <param_list>? ")" <return_type_annotation>? ":" <newline> <block>
<param_list> ::= <param> ("," <param>)*
<param> ::= <identifier> ":" <type>

<actor_decl> ::= "actor" <identifier> ":" <newline> <block>

<expr> ::= <assignment_expr>
<assignment_expr> ::= <logical_or_expr>
<logical_or_expr> ::= <logical_and_expr> ("||" <logical_and_expr>)*
<logical_and_expr> ::= <equality_expr> ("&&" <equality_expr>)*
<equality_expr> ::= <relational_expr> (("==" | "!=") <relational_expr>)?
<relational_expr> ::= <additive_expr> (("<" | "<=" | ">" | ">=") <additive_expr>)?
<additive_expr> ::= <multiplicative_expr> (("+" | "-") <multiplicative_expr>)*
<multiplicative_expr> ::= <unary_expr> (("*" | "/") <unary_expr>)*
<unary_expr> ::= ("+" | "-" | "!") <unary_expr> | <postfix_expr>
<postfix_expr> ::= <primary_expr> <postfix_op>*
<postfix_op> ::= "." <identifier> | "[" <expr> "]"
<primary_expr> ::=
      <literal>
    | <call_expr>
    | <identifier>
    | <array_literal>
    | <new_expr>
    | "(" <expr> ")"

<call_expr> ::= <call_target> <space_call_arg>+ | <call_target> "(" ")"
<call_target> ::= <identifier> ("." <identifier>)*
<space_call_arg> ::= <simple_arg_expr> | <named_space_arg>
<named_space_arg> ::= <identifier> "=" <simple_arg_expr>
<simple_arg_expr> ::=
      <literal>
    | <signed_number_literal>
    | <identifier>
    | <array_literal>
    | <new_expr_no_args>
    | <member_access>
    | <index_access>
    | "(" <expr> ")"
<array_literal> ::= "[" (<expr> ("," <expr>)*)? "]"
<new_expr> ::= "new" <identifier> <space_call_arg>*
<new_expr_no_args> ::= "new" <identifier>
<member_access> ::= <postfix_expr> "." <identifier>
<index_access> ::= <postfix_expr> "[" <expr> "]"

<identifier> ::= Unicode identifier starting with a non-digit character; "_" is allowed; reserved words and reserved internal names are excluded
<literal> ::= <number_literal> | <string_literal> | <bool_literal> | "null"
<bool_literal> ::= "true" | "false"
<number_literal> ::= implementation-defined unsigned numeric literal
<signed_number_literal> ::= ("+" | "-") <number_literal>
<string_literal> ::= double quoted string literal
<newline> ::= line break
<indent> ::= increased indentation
<dedent> ::= decreased indentation
```
