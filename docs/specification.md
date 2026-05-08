# KoromoEventScript (KSE) 仕様書

KoromoEventScript (KSE) は、RPG・ADV・ノベルゲーム向けのシナリオDSLです。

本仕様は、吉里吉里/KAG 系のノベルゲームスクリプトで一般的に必要になる機能群を参考にしつつ、KSE 独自の方針として「シナリオライターが流れるように書けること」「パーサ・LSP・ローカライズ・Git 管理に強いこと」を重視して設計します。

KAG は本文中にタグを埋め込む方式ですが、KSE はタグ埋め込み型には寄せず、ブロック構文と命令構文を明確に分けます。

---

# 設計目標

- 1 event = 1 `.kse` ファイル
- ファイル名が event 名になる
- 大量の会話文を書きやすい
- IME 切り替えをできるだけ減らす
- 記号過多な構文を避ける
- 空行・タブ・インデントに意味を持たせない
- ブロック構造は `{}` で明示する
- scene メタ情報と通常命令を分離する
- scene 単位でリソース先読みできる
- ローカライズキーを言語仕様として扱う
- 並列演出を安全に書ける
- ノベルゲームに必要な基本機能を一通り持つ
- Parser / LSP / Tree-sitter を実装しやすい

---

# 基本概念

| 概念 | 意味 |
|---|---|
| event | 1つのイベント。1 `.kse` ファイルに対応する |
| scene | 単一背景上で展開する場面単位 |
| setup | scene のメタ情報・初期状態定義 |
| cast | scene に登場する actor 一覧 |
| actor | シナリオ中で使う登場人物ID |
| say | キャラクター台詞 |
| nar | ナレーション |
| together | 並列演出ブロック |
| macro | 再利用可能な命令列 |
| label | scene 内のジャンプ位置 |
| choice | 選択肢 |

---

# ファイル構造

```txt
prologue_001.kse
```

このファイルは event `prologue_001` を表す。

ファイル内に `event prologue_001` のような宣言は書かない。

---

# トップレベル構造

`.kse` ファイルには以下を記述できる。

```txt
use
actor
macro
scene
```

例:

```kse
use common
use ui_common

actor A : Alice {
    nameKey char.alice
    sprite alice
}

actor G : Guard {
    nameKey char.guard
    sprite guard
}

scene arrival {
    setup {
        cast {
            A
            G
        }

        bg: royal_gate_evening
        transition: fade 1.0

        init {
            A: left normal hidden
            G: right serious hidden
            camera: wide
            bgm: capital_evening
            messageWindow: default visible
        }
    }

    together {
        show A
        pan A 0.5
        se cloth
    }

    nar #arrival_001 {
        王都の門には、夕陽が長い影を落としていた。
    }

    say A #arrival_002 {
        ここが王都……。
    }
}
```

---

# コメント

```kse
// 1行コメント

/*
    複数行コメント
*/
```

コメントは構文上無視される。

---

# use

```kse
use common
use battle_common
```

共通マクロ、actor 定義、UI 定義、位置プリセットなどを読み込む。

---

# actor

actor はシナリオ内で利用するローカルIDを定義する。

```kse
actor A : Alice {
    nameKey char.alice
    sprite alice
}
```

| 要素 | 意味 |
|---|---|
| `A` | シナリオ内ローカルID |
| `Alice` | ゲーム側マスターID |
| `nameKey` | 表示名ローカライズキー |
| `sprite` | 標準立ち絵セット |

---

## actor 詳細例

```kse
actor A : Alice {
    nameKey char.alice
    sprite alice
    voice alice
    color "#ffccdd"

    default {
        sprite normal
        face normal
        pos left
    }

    sprites {
        normal  = alice_normal
        uniform = alice_uniform
        battle  = alice_battle
    }

    faces {
        normal
        smile
        angry
        sad
        serious = angry_02
    }

    render {
        offset x:0 y:20
        scale 0.95
        layer character
        z 10
        lipSync true
        blink true
    }

    tags [heroine, party]
}
```

---

## actor の同一マスター参照

同じマスターIDを、別衣装・別状態として複数 localId に割り当ててもよい。

```kse
actor A : Alice {
    sprite alice_normal
}

actor AB : Alice {
    sprite alice_battle
}
```

---

# scene

scene は単一背景上で展開するシナリオ単位である。

各 scene は必ず先頭に `setup` ブロックを持つ。

```kse
scene arrival {
    setup {
        ...
    }

    ...scene body...
}
```

`setup` は scene ヘッダ専用構文であり、通常命令とは分離される。

---

# setup

`setup` は scene のメタ情報と初期状態を定義する。

```kse
setup {
    cast {
        A
        G
    }

    preload {
        image gate_emblem
        se armor
    }

    bg: royal_gate_evening
    transition: fade 1.0

    init {
        A: left normal hidden
        G: right serious hidden

        camera: wide
        bgm: capital_evening
        messageWindow: default visible
    }
}
```

---

## cast

scene に登場する actor 一覧。

```kse
cast {
    A
    G
}
```

用途:

- 立ち絵先読み
- ボイス先読み
- Live2D / Spine 準備
- scene 解析
- 出演統計
- ビルドツール連携

`cast` は明示的なロードヒントである。

コンパイラは本文中の `show` / `say` 等から自動推定してもよいが、明示 `cast` を優先する。

---

## preload

scene で必要な非 actor リソースを明示する。

```kse
preload {
    image gate_emblem
    image magic_circle
    se armor
    se door_open
    bgm capital_evening
    video op_movie
}
```

用途:

- ボタン画像
- 一枚絵
- カットイン
- SE
- BGM
- 動画
- クリックマップ

---

## bg

scene ごとに背景を1つだけ持つ。

```kse
bg: royal_gate_evening
```

scene 本文中で背景変更は禁止。

背景が変わる場合は scene を分割する。

---

## transition

scene 入場時トランジション。

```kse
transition: fade 1.0
transition: dissolve 0.8
transition: wipe left 0.5
transition: none
```

遷移は遷移先 scene が定義する。

```kse
goto enter_city
```

`goto` 側にトランジションを書かないことで、scene の入場表現を scene 側に閉じ込める。

---

## init

scene 開始時の静的状態を定義する。

```kse
init {
    A: left normal hidden
    G: right serious hidden

    camera: wide
    bgm: capital_evening
    messageWindow: default visible
}
```

`init` はアニメーション用途ではなく、即時状態設定用途である。

---

# テキスト表示

KSE の本文表示は以下を基本とする。

```txt
say actor textId { 本文 }
nar textId { 本文 }
```

---

## say

キャラクター台詞。

```kse
say A #arrival_001 {
    ここが王都……。
}
```

構文:

```txt
say <actorId> <textId> { <本文> }
```

---

## nar

ナレーション。

```kse
nar #arrival_002 {
    王都の門には、夕陽が長い影を落としていた。
}
```

ナレーションは `text` ではなく `nar` を使用する。

---

## 名前なし台詞

システムメッセージ、謎の声などは actor を用意する。

```kse
actor Unknown : SystemVoice {
    nameKey char.unknown
}

say Unknown #mysterious_001 {
    ……聞こえますか。
}
```

一時的な表示名を使う構文は原則用意しない。

理由:

- ローカライズ時に話者名も翻訳対象になる
- ボイス・色・ログ表示と紐付けやすい
- 表示名の揺れを避けられる

---

## テキスト制御タグ

本文中に最小限のインラインタグを許可する。

```kse
say A #arrival_010 {
    [ruby 王都 おうと]へようこそ。
}
```

推奨タグ:

| タグ | 用途 |
|---|---|
| `[ruby 親文字 ルビ]` | ルビ |
| `[br]` | 明示改行 |
| `[wait 0.5]` | テキスト中ウェイト |
| `[speed slow]` | 表示速度変更 |
| `[speed normal]` | 表示速度を戻す |
| `[color red]` | 文字色変更 |
| `[color default]` | 文字色を戻す |
| `[em]...[/em]` | 強調 |

インラインタグはローカライズ対象に含まれるため、壊れやすい。

多用せず、必要最小限にする。

---

## テキスト表示速度

```kse
textSpeed normal
textSpeed slow
textSpeed fast
textSpeed 30cps
```

`cps` は characters per second を表す。

---

## クリック待ち

通常の `say` / `nar` は、表示完了後にクリック待ちする。

明示制御:

```kse
say A #line_001 waitClick=false {
    急いで！
}

waitClick
```

---

## 改ページ・クリア

```kse
page
clearMessage
```

| 命令 | 意味 |
|---|---|
| `page` | メッセージを確定し、次ページへ進む |
| `clearMessage` | メッセージウィンドウをクリア |

---

# メッセージウィンドウ

ノベルゲームでは複数のメッセージレイヤが必要になる場合がある。

```kse
messageWindow main visible
messageWindow main hidden
messageWindow main pos bottom
messageWindow main skin default
messageWindow main opacity 0.85
```

---

## メッセージウィンドウ初期化

```kse
init {
    messageWindow: default visible
}
```

---

## 名前欄

```kse
nameBox visible
nameBox hidden
nameBox color actor
```

`actor` 指定時は actor の `color` を利用する。

---

# 履歴・バックログ

```kse
history show
history clear
history enable
history disable
```

`say` / `nar` は標準で履歴に追加される。

追加したくない場合:

```kse
say System #debug_001 history=false {
    デバッグ表示です。
}
```

---

# スキップ・オート・既読管理

```kse
skip enable
skip disable
auto enable
auto disable
readMark
```

| 命令 | 意味 |
|---|---|
| `skip enable` | スキップ可能にする |
| `skip disable` | スキップ禁止 |
| `auto enable` | オートモード開始 |
| `auto disable` | オートモード停止 |
| `readMark` | 既読到達点を記録 |

重要演出では一時的にスキップ禁止できる。

```kse
skip disable
movie play op_movie wait
skip enable
```

---

# レイヤモデル

KSE は論理レイヤを持つ。

標準レイヤ:

| レイヤ | 用途 |
|---|---|
| `bg` | 背景 |
| `character` | 立ち絵 |
| `effect` | エフェクト |
| `ui` | UI |
| `message` | メッセージ |
| `movie` | 動画 |

通常のシナリオでは直接レイヤ番号を扱わない。

必要な場合のみ明示する。

```kse
showImage magic_circle layer=effect center alpha=0.8
hideImage magic_circle
```

---

# 画像・立ち絵

## 表示

```kse
show A
show A left normal
show A center face=smile
show A right sprite=battle face=angry
```

---

## 非表示

```kse
hide A
hide A fade 0.3
```

---

## 表情変更

```kse
face A angry
face A smile
```

---

## 移動

```kse
move A center 0.5
move A x=100 y=0 duration=0.5 ease=easeOut
```

---

## 不透明度・拡大縮小

```kse
alpha A 0.5 0.3
scale A 1.1 0.3
rotate A 5 0.2
```

---

## 汎用画像

立ち絵以外の画像表示。

```kse
showImage item_sword center layer=effect
showImage cg_001 fullscreen
hideImage item_sword
```

---

# カメラ・画面演出

```kse
pan A 0.5
zoom 1.2 0.5
shake 0.5 0.3
flash white 0.2
fade in 1.0
fade out 1.0
```

---

# アニメーション

単純な演出は命令で書く。

```kse
move A center 0.5
alpha A 0 0.3
scale A 1.2 0.5
```

複雑なアニメーションは `anim` を使う。

```kse
anim A appear_bounce
anim magic_circle rotate_loop
animStop magic_circle
```

---

# 並列実行

KSE は `cut` を持たない。

並列実行は `together` で明示する。

```kse
together {
    show A left normal
    pan A 0.5
    se cloth
}
```

`together` 内の命令は同時開始される。

全命令完了後、次の命令へ進む。

---

## 待機しない命令

通常は命令完了を待つ。

待たずに開始だけしたい場合は `async` を付ける。

```kse
async bgm battle_theme fadeIn=1.0
```

`async` は多用しない。

基本は `together` で書く。

---

## 明示待機

```kse
wait 0.5
waitAnim A
waitBgm
waitSe armor
waitMovie op_movie
```

---

# 音声・BGM・SE

## BGM

```kse
bgm capital_evening
bgm battle_theme fadeIn=1.0
bgm stop
bgm stop 1.0
```

---

## SE

```kse
se armor
se door_open volume=0.8
se wind loop
se stop wind
```

---

## ボイス

`say` に voice を付ける。

```kse
say A #arrival_001 voice=alice_001 {
    ここが王都……。
}
```

または独立命令で再生する。

```kse
voice A alice_001
```

---

# 動画

```kse
movie preload op_movie
movie play op_movie wait
movie play op_movie async
movie stop op_movie
```

動画は `setup preload` に書いてもよい。

```kse
preload {
    video op_movie
}
```

---

# 選択肢

```kse
choice #arrival_choice_001 {
    option #help {
        labelKey choice.help
        goto help_route
    }

    option #leave {
        labelKey choice.leave
        goto leave_route
    }
}
```

選択肢文言もローカライズ対象とする。

簡略形:

```kse
choice #arrival_choice_001 {
    option #help labelKey=choice.help goto=help_route
    option #leave labelKey=choice.leave goto=leave_route
}
```

---

## 条件付き選択肢

```kse
choice #door_choice {
    option #open labelKey=choice.open if=has_key goto=open_door
    option #knock labelKey=choice.knock goto=knock_door
    option #leave labelKey=choice.leave goto=leave
}
```

---

## 選択後の変数操作

```kse
choice #trust_choice {
    option #trust labelKey=choice.trust {
        var trust_alice += 10
        goto trust_route
    }

    option #doubt labelKey=choice.doubt {
        var trust_alice -= 5
        goto doubt_route
    }
}
```

---

# ラベル・ジャンプ

scene 内の局所ジャンプには `label` を使う。

```kse
label retry

say A #retry_001 {
    もう一度試してみよう。
}

gotoLabel retry
```

scene 間遷移は `goto` を使う。

```kse
goto enter_city
```

別 event 呼び出し:

```kse
call sub_event_001
return
```

---

# 変数・フラグ

```kse
flag has_pass = true
flag has_pass = false

var trust_alice = 10
var trust_alice += 5
var route = "normal"
```

条件:

```kse
if has_pass {
    goto enter_city
}

if trust_alice >= 50 {
    goto good_route
} else {
    goto normal_route
}
```

---

# セーブ・ロード・スナップショット

ノベルゲームでは任意位置でのセーブ・ロードが必要になる。

```kse
savePoint
quickSave
quickLoad
```

---

## savePoint

```kse
savePoint #arrival_save_001
```

保存可能位置を明示する。

保存データには以下を含める。

- event 名
- scene 名
- 実行位置
- 変数
- フラグ
- 表示中 actor
- 背景
- BGM / SE 状態
- メッセージウィンドウ状態
- 履歴

---

## 一時スナップショット

演出巻き戻しや選択肢プレビュー用。

```kse
snapshot save temp1
snapshot load temp1
snapshot clear temp1
```

---

# UI・フォーム

名前入力などのために簡易フォーム命令を用意する。

```kse
input playerName #input_name {
    labelKey ui.input_name
    maxChars 12
    default ""
}
```

チェックボックス:

```kse
checkbox skipSeenOnly #skip_seen_only {
    labelKey ui.skip_seen_only
    default true
}
```

決定待ち:

```kse
commitForm
```

---

# ボタン・クリック領域

ノベルゲームでは画像ボタンやクリック可能領域が必要になる。

```kse
button menu_button {
    image ui_menu
    pos x=10 y=10
    action openMenu
}
```

クリック領域:

```kse
hotspot door {
    rect x=220 y=120 w=180 h=300
    action goto inspect_door
}
```

画像マスクによるクリック領域:

```kse
hotmap map_room_001 {
    image room_clickmap
    action red_area goto inspect_desk
    action blue_area goto inspect_door
}
```

---

# メニュー・システム操作

```kse
menu open
menu close
config open
saveMenu open
loadMenu open
history show
```

---

# 既読・通過記録

```kse
record #arrival_seen
if seen(#arrival_seen) {
    skip enable
}
```

用途:

- 既読スキップ
- 差分回収
- 実績
- ルート管理

---

# macro

再利用可能な命令列。

```kse
macro angry(actor) {
    face actor angry
    shake 0.3 0.2
}
```

使用:

```kse
angry A
```

macro は呼び出し時に即時実行される。

並列実行したい場合は `together` 内で呼ぶ。

```kse
together {
    enter A left normal
    look A
}
```

---

# ローカライズ

テキストを持つ命令には安定した textId を付与する。

```kse
say A #arrival_001 {
    ここが王都……。
}
```

```kse
nar #arrival_002 {
    王都の門には、夕陽が長い影を落としていた。
}
```

翻訳ツールは以下のようなキーを利用する。

```txt
prologue_001.arrival.arrival_001
```

---

## プレースホルダ

```kse
say A #arrival_003 {
    {playerName}、準備はいい？
}
```

翻訳側で語順変更できることを前提とする。

---

## 複数形

ICU MessageFormat 互換を推奨。

```kse
nar #item_count {
    {count, plural,
        one {ポーションを1個手に入れた。}
        other {ポーションを{count}個手に入れた。}
    }
}
```

---

# エラー方針

コンパイル時エラーにするもの:

- scene に `setup` がない
- `setup` が scene 先頭にない
- `bg` がない
- `bg` が複数ある
- scene 本文で背景変更している
- 未定義 actor を `say` / `show` している
- 未定義 scene に `goto` している
- textId が重複している
- `cast` に未定義 actor がいる

警告にするもの:

- `cast` にいるが本文で使われない actor
- 本文で使われるが `cast` にいない actor
- `preload` されているが使われないリソース
- voice 未指定の `say`
- textId 命名規則違反

---

# 完全サンプル

```kse
use common

actor A : Alice {
    nameKey char.alice
    sprite alice
    voice alice
    color "#ffccdd"
}

actor G : Guard {
    nameKey char.guard
    sprite guard
    voice guard
}

macro enter(actor, pos, face) {
    show actor pos face
    se cloth
}

macro look(actor) {
    pan actor 0.5
    zoom 1.08 0.5
}

scene arrival {
    setup {
        cast {
            A
            G
        }

        preload {
            se armor
            se cloth
        }

        bg: royal_gate_evening
        transition: fade 1.0

        init {
            A: left normal hidden
            G: right serious hidden
            camera: wide
            bgm: capital_evening
            messageWindow: default visible
        }
    }

    savePoint #arrival_save

    together {
        enter A left normal
        look A
    }

    nar #arrival_001 {
        王都の門には、夕陽が長い影を落としていた。
    }

    say A #arrival_002 voice=alice_001 {
        ここが王都……。
    }

    together {
        enter G right serious
        se armor
    }

    say G #arrival_003 voice=guard_001 {
        止まれ。身分証を見せろ。
    }

    choice #arrival_choice_001 {
        option #show_pass labelKey=choice.show_pass if=has_pass {
            goto show_pass
        }

        option #ask labelKey=choice.ask {
            goto ask_guard
        }
    }
}

scene show_pass {
    setup {
        cast {
            A
            G
        }

        bg: royal_gate_evening
        transition: none

        init {
            A: left normal visible
            G: right serious visible
            camera: wide
            bgm: keep
        }
    }

    say A #show_pass_001 {
        これでいい？
    }

    together {
        face G normal
    }

    say G #show_pass_002 {
        確認した。通ってよし。
    }

    goto enter_city
}

scene enter_city {
    setup {
        cast {
            A
        }

        bg: capital_street_evening
        transition: dissolve 0.8

        init {
            A: center normal visible
            camera: wide
            bgm: capital_theme
            messageWindow: default visible
        }
    }

    nar #enter_city_001 {
        アリスは王都へ足を踏み入れた。
    }

    record #entered_capital
    end
}
```

---

# 文法概要

```ebnf
file        ::= use_decl* actor_def* macro_def* scene_def*
use_decl    ::= "use" IDENT

actor_def   ::= "actor" IDENT ":" IDENT "{" actor_body* "}"
macro_def   ::= "macro" IDENT "(" param_list? ")" "{" command* "}"

scene_def   ::= "scene" IDENT "{" setup_block scene_body* "}"
setup_block ::= "setup" "{" cast_block preload_block? bg_stmt transition_stmt? init_block? "}"
cast_block  ::= "cast" "{" IDENT* "}"
preload_block ::= "preload" "{" preload_stmt* "}"
bg_stmt     ::= "bg" ":" IDENT
transition_stmt ::= "transition" ":" IDENT value*
init_block  ::= "init" "{" init_stmt* "}"

scene_body  ::= say_stmt
              | nar_stmt
              | together_block
              | choice_stmt
              | if_stmt
              | label_stmt
              | goto_stmt
              | goto_label_stmt
              | call_stmt
              | return_stmt
              | end_stmt
              | save_stmt
              | command
              | macro_call

together_block ::= "together" "{" scene_body* "}"
say_stmt       ::= "say" IDENT TEXT_ID attr* "{" raw_text "}"
nar_stmt       ::= "nar" TEXT_ID attr* "{" raw_text "}"
choice_stmt    ::= "choice" TEXT_ID "{" option_stmt* "}"
```

---

# KAG から取り込む考え方

KAG は、本文中にタグを埋め込み、レイヤ、メッセージ、BGM/SE、動画、ラベルジャンプ、フォーム、セーブ、履歴などの機能を提供する。

KSE では以下を参考にする。

- 背景・前景・メッセージなどのレイヤ概念
- メッセージ表示と文字属性
- ルビ、ウェイト、改ページ、履歴
- BGM / SE / 動画制御
- ラベル・ジャンプ・選択肢
- セーブ・ロード・一時スナップショット
- クリック領域・ボタン・フォーム
- 演出開始と待機の分離

ただし、KSE は KAG のタグ埋め込み構文をそのまま採用しない。

KSE では、通常命令・scene setup・本文ブロック・ローカライズキーを明確に分離する。

---

# 設計思想

KSE は以下を明確に分離する。

- `setup`: scene メタ情報
- `init`: 初期状態
- `say` / `nar`: ローカライズ対象テキスト
- 実行命令: 演出
- `together`: 並列実行
- `choice`: 選択肢
- `savePoint`: セーブ可能位置

これにより、

- シナリオライターが読みやすい
- Parser 実装が容易
- LSP 実装しやすい
- Git diff が読みやすい
- ローカライズしやすい
- 一般的なノベルゲーム制作に必要な機能を揃えやすい

という特性を持つ。
