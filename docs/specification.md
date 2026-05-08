# KoromoEventScript (KSE) 仕様書

KoromoEventScript (KSE) は、RPG・ADV・ノベルゲーム向けのシナリオDSLです。

大量のシナリオを、シナリオライターが自然な流れで執筆できることを目的としています。

また、以下を重視しています。

- IME切り替え負担の低減
- 記号過多な文法の回避
- Git diff の読みやすさ
- ローカライズしやすい構造
- LSP / Tree-sitter / Parser 実装容易性
- シーン単位のリソース先読み
- 明示的な並列実行モデル

---

# ファイル構造

- 1 event = 1 `.kse` ファイル
- ファイル名が event 名となる

例:

```txt
prologue_001.kse
```

---

# トップレベル構造

`.kse` ファイルには以下を記述できる。

- `use`
- `actor`
- `macro`
- `scene`

例:

```kse
use common

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

# use

```kse
use common
use battle_common
```

共通マクロ、actor定義、ライブラリ等を読み込む。

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

# scene

scene は、単一背景上で展開するシナリオ単位である。

各 scene は必ず先頭に `setup` ブロックを持つ。

```kse
scene arrival {
    setup {
        ...
    }

    ...
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

    bg: royal_gate_evening
    transition: fade 1.0

    init {
        A: left normal hidden
        G: right serious hidden
        camera: wide
        bgm: capital_evening
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
- scene解析
- 出演統計
- ビルドツール連携

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

---

## init

scene 開始時の静的状態を定義する。

```kse
init {
    A: left normal hidden
    G: right serious hidden

    camera: wide
    bgm: capital_evening
}
```

`init` はアニメーション用途ではなく、即時状態設定用途である。

---

# say

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

# nar

ナレーション。

```kse
nar #arrival_002 {
    王都の門には、夕陽が長い影を落としていた。
}
```

ナレーションは `text` ではなく `nar` を使用する。

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

# together

KSE は `cut` を持たない。

並列実行は `together` で明示する。

```kse
together {
    show A
    pan A 0.5
    se cloth
}
```

`together` 内の命令は同時開始される。

全命令完了後、次の命令へ進む。

---

# 実行命令

## キャラクター命令

```kse
show A
show A left normal
hide A
face A angry
move A center 0.5
```

---

## カメラ命令

```kse
pan A 0.5
zoom 1.2 0.5
shake 0.5 0.3
fade in 1.0
fade out 1.0
```

---

## 音声命令

```kse
bgm capital_evening
bgm stop
bgm stop 1.0
se armor
voice A alice_001
```

---

# 制御構文

## if

```kse
if has_pass {
    say A #arrival_010 {
        これでいい？
    }
} else {
    say G #arrival_011 {
        持っていないなら通すわけにはいかん。
    }
}
```

---

## scene 遷移

```kse
goto enter_city
```

---

## event 呼び出し

```kse
call sub_event_001
return
```

---

## end

```kse
end
```

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

# 文法概要

```ebnf
file        ::= use_decl* actor_def* macro_def* scene_def*
use_decl    ::= "use" IDENT

actor_def   ::= "actor" IDENT ":" IDENT "{" actor_body* "}"

scene_def   ::= "scene" IDENT "{" setup_block scene_body* "}"
setup_block ::= "setup" "{" cast_block bg_stmt transition_stmt? init_block? "}"
cast_block  ::= "cast" "{" IDENT* "}"
bg_stmt     ::= "bg" ":" IDENT
transition_stmt ::= "transition" ":" IDENT value*
init_block  ::= "init" "{" init_stmt* "}"

scene_body  ::= say_stmt
              | nar_stmt
              | together_block
              | if_stmt
              | goto_stmt
              | call_stmt
              | return_stmt
              | end_stmt
              | command
              | macro_call
```

---

# 設計思想

KSE は以下を明確に分離する。

- `setup`: scene メタ情報
- `init`: 初期状態
- `say` / `nar`: ローカライズ対象テキスト
- 実行命令: 演出
- `together`: 並列実行

これにより、

- シナリオライターが読みやすい
- Parser 実装が容易
- LSP 実装しやすい
- Git diff が読みやすい
- ローカライズしやすい

という特性を持つ。