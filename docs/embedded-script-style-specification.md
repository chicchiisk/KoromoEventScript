# KoromoEventScript 埋め込みスクリプト型 仕様案

この文書は、従来の `scene/setup/say/nar/together` 型仕様とは別案として、**シナリオ本文の中にスクリプト命令を埋め込む思想**で設計した KSE 仕様案です。

既存仕様書 `docs/specification.md` は、構造化DSL型の仕様として残します。

本仕様案では、KAG 系ノベルスクリプトのように、本文を主役にして、演出命令を本文中に差し込む形式を採用します。

---

# 設計目標

- シナリオ本文を最優先に書ける
- 台詞・地の文の流れを止めない
- 命令は本文中に自然に埋め込む
- IME切り替え負担をできるだけ減らす
- 空行・インデントには意味を持たせない
- ローカライズキーを維持できる
- Git diff で本文の変更が追いやすい
- KAG的なノベルゲーム機能を実現できる
- ただし KAG の `[]` タグ構文をそのままコピーしない

---

# 基本方針

構造化DSL型では、本文は次のように明示的な命令だった。

```kse
say A #arrival_001 {
    ここが王都……。
}

nar #arrival_002 {
    王都の門には、夕陽が長い影を落としていた。
}
```

埋め込みスクリプト型では、本文を通常行として書く。

```kse
#arrival_001 A: ここが王都……。
#arrival_002 王都の門には、夕陽が長い影を落としていた。
```

演出命令は本文中にコマンド行として埋め込む。

```kse
@show A left normal
@pan A 0.5
@se cloth
#arrival_001 A: ここが王都……。
```

---

# ファイルモデル

- 1 event = 1 `.kse` ファイル
- ファイル名が event 名

```txt
prologue_001.kse
```

---

# トップレベル構造

```kse
@use common

@actor A : Alice {
    nameKey char.alice
    sprite alice
}

@actor G : Guard {
    nameKey char.guard
    sprite guard
}

@scene arrival {
    cast: A G
    bg: royal_gate_evening
    transition: fade 1.0
    init: A left normal hidden
    init: G right serious hidden
    init: camera wide
    init: bgm capital_evening
}

@together {
    show A left normal
    pan A 0.5
    se cloth
}

#arrival_001 王都の門には、夕陽が長い影を落としていた。
#arrival_002 A: ここが王都……。

@together {
    show G right serious
    se armor
}

#arrival_003 G: 止まれ。身分証を見せろ。
```

---

# 行の種類

KSE 埋め込み型では、各行は以下のいずれかになる。

| 行種別 | 例 | 意味 |
|---|---|---|
| コマンド行 | `@show A left` | スクリプト命令 |
| 台詞行 | `#id A: 本文` | actor 台詞 |
| ナレーション行 | `#id 本文` | ナレーション |
| 継続行 | `本文の続き` | 直前の本文に連結 |
| コメント行 | `// comment` | コメント |

---

# コマンド行

コマンド行は `@` で始まる。

```kse
@show A left normal
@se armor
@bgm capital_evening
```

本文行とコマンド行を字句レベルで分離できるため、パーサが簡潔になる。

---

# 台詞行

```kse
#arrival_001 A: ここが王都……。
```

構文:

```txt
#<textId> <actorId>: <本文>
```

`actorId:` の直後から行末までが本文になる。

---

# ナレーション行

```kse
#arrival_002 王都の門には、夕陽が長い影を落としていた。
```

構文:

```txt
#<textId> <本文>
```

`actorId:` がない textId 行はナレーションとして扱う。

---

# 複数行本文

長い本文は `{}` で囲む。

```kse
#arrival_010 A: {
    ここが王都……。
    思っていたより、ずっと大きい。
}

#arrival_011 {
    王都の門には、夕陽が長い影を落としていた。
    人々のざわめきが遠くから聞こえる。
}
```

この形式では空行にも意味を持たせない。

---

# textId 省略案

ライター負担を下げるため、textId を省略可能にする案。

```kse
A: ここが王都……。
王都の門には、夕陽が長い影を落としていた。
```

コンパイル時に textId を自動採番し、書き戻す。

```kse
#arrival_001 A: ここが王都……。
#arrival_002 王都の門には、夕陽が長い影を落としていた。
```

## 推奨

初期執筆時は省略可。

翻訳・収録・本番ビルド前には textId を固定してファイルに書き戻す。

---

# scene 宣言

本文埋め込み型でも scene は明示する。

```kse
@scene arrival {
    cast: A G
    bg: royal_gate_evening
    transition: fade 1.0
    init: A left normal hidden
    init: G right serious hidden
    init: camera wide
    init: bgm capital_evening
}
```

scene 宣言はコマンド行であり、本文とは区別される。

---

# scene ヘッダ

scene ヘッダは `{}` 内に key-value 形式で書く。

```kse
@scene arrival {
    cast: A G
    preload: se armor, se cloth
    bg: royal_gate_evening
    transition: fade 1.0
    init: A left normal hidden
    init: G right serious hidden
    init: camera wide
    init: messageWindow default visible
}
```

`bg` は scene ごとに必須、かつ1つだけ。

---

# actor 定義

```kse
@actor A : Alice {
    nameKey char.alice
    sprite alice
    voice alice
    color "#ffccdd"
}
```

コマンド行として `@actor` を使う。

---

# import

```kse
@use common
@use ui_common
```

---

# 演出命令

```kse
@show A left normal
@hide A
@face A angry
@move A center 0.5
@pan A 0.5
@zoom 1.2 0.5
@shake 0.5 0.3
@se armor
@bgm capital_evening
@bgm stop 1.0
```

---

# 並列演出

複数命令を同時実行する場合は `@together` を使う。

```kse
@together {
    show A left normal
    pan A 0.5
    se cloth
}
```

`@together` 内では `@` を省略する。

---

# 待機

```kse
@wait 0.5
@waitAnim A
@waitBgm
@waitMovie op_movie
```

---

# クリック待ち

本文行は、標準で表示完了後クリック待ちする。

クリック待ちしない場合:

```kse
#rush_001 A: 急いで！ @noWait
```

または属性形式:

```kse
#rush_001 A: 急いで！ { waitClick=false }
```

## 検討事項

末尾属性構文は本文と混ざりやすいため、採用するか要検討。

---

# インラインタグ

本文中には最小限のインラインタグを許可する。

```kse
#arrival_020 A: [ruby 王都 おうと]へようこそ。
```

推奨タグ:

| タグ | 意味 |
|---|---|
| `[ruby 王都 おうと]` | ルビ |
| `[br]` | 明示改行 |
| `[wait 0.5]` | テキスト中ウェイト |
| `[speed slow]` | 表示速度変更 |
| `[speed normal]` | 表示速度復帰 |
| `[color red]` | 文字色変更 |
| `[color default]` | 色復帰 |
| `[em]...[/em]` | 強調 |

---

# 選択肢

```kse
@choice #arrival_choice_001 {
    option #show_pass labelKey=choice.show_pass if=has_pass goto=show_pass
    option #ask labelKey=choice.ask goto=ask_guard
}
```

ブロック形式:

```kse
@choice #arrival_choice_001 {
    option #show_pass {
        labelKey choice.show_pass
        if has_pass
        goto show_pass
    }

    option #ask {
        labelKey choice.ask
        goto ask_guard
    }
}
```

---

# ラベル・ジャンプ

scene 内ラベル:

```kse
@label retry
@gotoLabel retry
```

scene 遷移:

```kse
@goto enter_city
```

event 呼び出し:

```kse
@call sub_event_001
@return
```

---

# if

```kse
@if has_pass {
    @goto show_pass
} else {
    @goto ask_guard
}
```

ブロック内ではコマンド行の `@` を省略してもよい案:

```kse
@if has_pass {
    goto show_pass
} else {
    goto ask_guard
}
```

## 推奨

ブロック内では `@` を省略可能とする。

ただし本文行を書く場合は textId 行を使う。

```kse
@if has_pass {
    #pass_001 A: これでいい？
    goto show_pass
}
```

---

# 変数・フラグ

```kse
@flag has_pass = true
@flag has_pass = false

@var trust_alice = 10
@var trust_alice += 5
@var route = "normal"
```

---

# セーブ・ロード

```kse
@savePoint #arrival_save_001
@quickSave
@quickLoad
```

---

# 履歴・既読・スキップ

```kse
@history show
@history clear
@history enable
@history disable

@skip enable
@skip disable
@auto enable
@auto disable

@record #arrival_seen
```

本文行は標準で履歴に追加される。

---

# メッセージウィンドウ

```kse
@messageWindow main visible
@messageWindow main hidden
@messageWindow main pos bottom
@messageWindow main skin default
@messageWindow main opacity 0.85
```

---

# 画像・レイヤ

```kse
@showImage cg_001 fullscreen
@showImage magic_circle center layer=effect alpha=0.8
@hideImage magic_circle
```

標準レイヤ:

| レイヤ | 用途 |
|---|---|
| `bg` | 背景 |
| `character` | 立ち絵 |
| `effect` | エフェクト |
| `ui` | UI |
| `message` | メッセージ |
| `movie` | 動画 |

---

# 動画

```kse
@movie preload op_movie
@movie play op_movie wait
@movie play op_movie async
@movie stop op_movie
```

---

# ボタン・クリック領域

```kse
@button menu_button {
    image ui_menu
    pos x=10 y=10
    action openMenu
}

@hotspot door {
    rect x=220 y=120 w=180 h=300
    action goto inspect_door
}
```

---

# macro

```kse
@macro enter(actor, pos, face) {
    show actor pos face
    se cloth
}
```

呼び出し:

```kse
@enter A left normal
```

本文中に自然に差し込める。

```kse
@enter A left normal
#arrival_001 A: ここが王都……。
```

---

# 完全サンプル

```kse
@use common

@actor A : Alice {
    nameKey char.alice
    sprite alice
    voice alice
    color "#ffccdd"
}

@actor G : Guard {
    nameKey char.guard
    sprite guard
    voice guard
}

@macro enter(actor, pos, face) {
    show actor pos face
    se cloth
}

@scene arrival {
    cast: A G
    preload: se armor, se cloth
    bg: royal_gate_evening
    transition: fade 1.0
    init: A left normal hidden
    init: G right serious hidden
    init: camera wide
    init: bgm capital_evening
    init: messageWindow default visible
}

@savePoint #arrival_save

@together {
    enter A left normal
    pan A 0.5
}

#arrival_001 王都の門には、夕陽が長い影を落としていた。
#arrival_002 A: ここが王都……。

@together {
    enter G right serious
    se armor
}

#arrival_003 G: 止まれ。身分証を見せろ。

@choice #arrival_choice_001 {
    option #show_pass labelKey=choice.show_pass if=has_pass goto=show_pass
    option #ask labelKey=choice.ask goto=ask_guard
}

@scene show_pass {
    cast: A G
    bg: royal_gate_evening
    transition: none
    init: A left normal visible
    init: G right serious visible
    init: bgm keep
}

#show_pass_001 A: これでいい？

@face G normal

#show_pass_002 G: 確認した。通ってよし。

@goto enter_city
```

---

# 構造化DSL型との比較

| 観点 | 構造化DSL型 | 埋め込みスクリプト型 |
|---|---|---|
| 本文の書きやすさ | やや重い | 軽い |
| パーサの単純さ | 高い | 中程度 |
| ローカライズ管理 | 強い | textId を必須にすれば強い |
| 演出の明示性 | 高い | 本文中に散らばる |
| KAG との近さ | 低い | 高い |
| Git diff | ブロック単位で見やすい | 本文差分が見やすい |
| ライター向け | 中 | 高 |
| エンジニア向け | 高 | 中 |

---

# 未決事項

以下は検討が必要。

## 1. コマンド接頭辞

現在案は `@`。

候補:

- `@show A left`
- `cmd show A left`
- `> show A left`

`@` は短く分かりやすいが、IME切り替えが必要になる可能性がある。

ただし、コマンド行は本文行より少ないため許容できる可能性が高い。

## 2. 台詞の話者区切り

現在案:

```kse
#id A: 本文
```

`:` 入力が負担になる可能性がある。

代替案:

```kse
#id A 本文
```

ただし actorId と本文の境界が曖昧になる。

## 3. textId の必須性

候補:

- 常に必須
- 初稿時は省略可、コンパイル時に自動付与
- 翻訳対象だけ必須

推奨は「初稿時は省略可、本番前に自動書き戻し」。

## 4. ブロック内 `@` 省略

```kse
@together {
    show A
    pan A 0.5
}
```

この案では省略可。

ただし、構文ルールが二重化するため、Parser の実装方針を決める必要がある。

---

# 質問事項

今後詰めるべき点:

1. コマンド行の接頭辞は `@` でよいか。
2. 台詞行は `A:` 形式でよいか。IME負担を避けて `A 本文` にするか。
3. textId は常に手書き必須にするか、自動採番・書き戻し前提にするか。
4. `@scene` のヘッダは1行 key-value でよいか、既存仕様同様 `setup` ブロックを残すか。
5. インラインタグは `[ruby ...]` のような KAG 寄りでよいか、それとも別記法にするか。

---

# 方向性

埋め込みスクリプト型は、本文量が非常に多いノベルゲームに向いている。

特に以下の用途に強い。

- 会話主体
- 地の文が多い
- 演出が本文の合間に少しずつ挟まる
- ライターがテキストエディタ上で長時間執筆する

一方で、複雑な演出や厳密な scene 初期化は、構造化DSL型の方が扱いやすい。

KSE としては、以下の2案を並行検討する価値がある。

- 構造化DSL型: `docs/specification.md`
- 埋め込みスクリプト型: 本文書
