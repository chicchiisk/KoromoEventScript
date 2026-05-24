# Koromo Event List ファイル仕様書

この文書は、KoromoEventScript のイベントデータファイル `.kel` の構文を定義する。

`.kel` は JSON に近い key/value ベースのデータ記法を採用するが、区切り記号やクオート規則は KES 向けに簡略化する。
パーサの責務は、`.kel` を key/value とオブジェクトの木構造として読み取ることまでであり、各キーや値の意味を解釈することではない。
データをどう使うかは、パース後の処理系が決定する。

## 基本ルール

- ファイル拡張子は `.kel` とする。
- 文字エンコーディングは `utf-8` を標準とする。
- 空行は無視する。
- コメントは `.kc` と同様に `// ...` と `/* ... */` を使える。
- 相対パスは、必要になった場合に処理系側が解釈する。
- 構文上の予約キーワードは持たない。
- パース処理時点では、キー名に対する意味チェックや必須キー検証は行わない。

## 構造モデル

`.kel` は key/value の組を並べたドキュメントである。
値には次の型を使える。

- オブジェクト
- 文字列
- identifier
- 数値
- boolean

最上位もオブジェクト相当の連続した key/value 群として扱う。
同一階層で同じキーが複数回出現した場合、パース結果ではそのキーの値を配列として解釈する。
この配列は構文上の独立した値型ではなく、重複キーを集約した結果として得られる表現である。

## キーのルール

キーには次の文字だけを使用できる。

- 英字 `a-z`, `A-Z`
- 数字 `0-9`
- アンダースコア `_`
- ドット `.`

例:

```txt
event_001
flag.123
chapter.main
ui_style
```

## 値のルール

### オブジェクト

オブジェクトは `{ ... }` で囲む。
オブジェクトの中には、さらに key/value の組をネストできる。
同一オブジェクト内で同じキーを複数回定義してよい。
その場合、パーサは出現順を保ったまま、そのキーに対応する複数の値を配列として保持する。

```txt
trigger = {
    flag.123 = true
    nested.block = {
        value = 10
    }
}
```

```txt
option = {
    text = option.a
}

option = {
    text = option.b
}
```

上記のように同一階層で `option` が複数回出現した場合、パース結果では `option` は配列として解釈される。

### 文字列

文字列はダブルクオートで囲む。

```txt
title = "chapter001_intro.title"
chapter = "events/chapter001.kc"
```

### identifier

identifier はクオートなしの単一トークン値である。
キーと同様に、英数字、`_`、`.` を使えるものとして扱う。

```txt
type = story
route = branch.a
```

### 数値

数値は整数または小数を許可する。

```txt
priority = 100
weight = 0.75
```

### boolean

boolean 値は `true` と `false` を使う。

```txt
enabled = true
hidden = false
```

## 構文

```ebnf
kel-file      = { blank-line | comment-line | pair } ;

pair          = key, "=", value ;

key           = key-char, { key-char } ;

key-char      = letter | digit | "_" | "." ;

value         = object
              | string-literal
              | identifier
              | number-literal
              | boolean-literal ;

object        = "{", { blank-line | comment-line | pair }, "}" ;

identifier    = key ;

boolean-literal = "true" | "false" ;
```

## 例

```txt
entry = chapter001_intro

chapter001_intro = {
    type = story
    title = chapter001_intro.title
    desc = chapter001_intro.desc
    chapter = "events/chapter001.kc"
    trigger = {
        flag.123 = true
    }
    ui_style = modal
}
```

```txt
main = {
    type = story
    chapter = "events/chapter001.kc"
    trigger = {
        all = {
            flag.chapter0_finished = true
            route.current = noa
        }
    }
    metadata = {
        icon = story.main
        priority = 100
        skippable = false
    }
}
```

上記の `entry`、`type`、`chapter`、`trigger`、`metadata` は、この文書における予約語ではない。
これらは単なるキー名であり、意味づけは後段の処理系が行う。

## パーサの責務

`.kel` パーサは、少なくとも次を保証する。

- key と value の対応関係を保持できる。
- オブジェクトのネスト構造を保持できる。
- 値の型が object / string / identifier / number / boolean のいずれかとして判別できる。
- 同一階層で重複したキーを、出現順を保った配列として集約できる。
- 構文的に妥当なキー文字だけを受け入れる。

一方で、次の検証はパース処理の責務に含めない。

- `entry` が存在するかどうか
- `chapter` が存在するかどうか
- キー名が予約済みかどうか
- 特定キーに特定の値型が必要かどうか
- 複数のキーの意味関係が正しいかどうか

これらはパース後の処理系、ビルド処理、ランタイム、LSP などが必要に応じて行う。

## スコープ外

次の項目は、この文書では固定しない。

- キーごとの意味論
- 必須キー集合
- イベント選択順序や優先順位
- 条件式専用の演算子体系
- 他ファイル参照の解決規則

これらは、`.kel` を消費する各処理系の仕様で定義する。
