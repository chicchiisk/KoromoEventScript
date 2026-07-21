# .klib 中間表現仕様

この文書は、KoromoEventScript の `.kc` から生成される VM 実行用中間表現 `.klib` の公開契約を定義する仕様である。

## 目的

`.klib` は CLI が `.kc` を解析・検証した後に生成し、VM と runtime がイベント実行時に参照する中間表現である。
本仕様は、CLI、VM、runtime、debug tooling の実装者とレビュー担当者が、`.klib` の責務境界と隣接仕様との関係を同じ前提で確認できるようにする。

## 設計方針

`.klib` はコンパクトなバイナリ形式を採用する。runtime が読む正規成果物は `.klib` のみとし、テキスト形式（JSON 等）を代替実行形式として使わない。
一方で、デバッグ・レビュー・golden test 向けには、`.klib` の論理内容を人間可読な IL 風テキストへ射影した `.klibtxt` を補助成果物として併置できる。
設計上の主な方針は次の通りである。

- **コンパクト性**: 数千行規模の `.kc` を効率的に表現するため、リテラル値は定数プールに集約し、bytecode は統一幅オペランドで符号化する。
- **スタックマシン命令セット**: 命令列はスタックベースの VM を想定した bytecode として表現する。compiler は `.kc` の高水準構文をすべてスタック操作命令とシナリオ opcode へ正規化する。
- **セクション構造**: ファイルはヘッダーと複数のセクションで構成し、各セクションは型 ID とオフセットで識別する。debug セクションはリリースビルドで strip 可能とする。
- **compile-time 完全解決**: label は bytecode offset へ、変数は変数テーブル index へ、actor/asset/locale は定数プール entry へ、command/syscall は名前 ID へ、すべて compile-time に解決する。VM/runtime は `.klib` load 後に名前解決を再実行しない。
- **可読なテキスト射影**: `.klibtxt` は `.klib` が持つ論理情報をセクション単位で読みやすいテキストへ変換し、C# IL に近い assembler 風の見た目で表示する。

---

## エンコーディング規則

`.klib` は次のエンコーディング規則に従う。

| 項目 | 仕様 |
|------|------|
| バイトオーダー | リトルエンディアン（little-endian）。 |
| 文字エンコーディング | UTF-8。BOM なし。 |
| 整数型 | `i32`（符号付き 32 bit）のみを使用する。バイトコードのオペコード値自体は 1 バイトで表現するが、オペランドはすべて `i32`。 |
| 浮動小数点 | `f64`（IEEE 754 倍精度）。 |
| 文字列 | `i32` 長さプレフィックス（バイト数、0 以上）+ UTF-8 バイト列。長さ 0 は空文字列。 |
| bool | `i32`。0 = false、1 = true。それ以外は load error。 |

---

## ファイル構造

`.klib` ファイルは次の構成を持つ。

```
[File Header + Section Table]
[Section Data ...]
```

セクションデータはセクションテーブルが示すオフセットに配置し、配置順序は任意とする。

### File Header

```
magic:         4 bytes  "KLIB"（0x4B 0x4C 0x49 0x42）
version_major: i32
version_minor: i32
version_patch: i32
features:      i32      対応 feature の bitmask（現在は 0）
section_count: i32
sections[section_count]:
  type:        i32      セクション型 ID
  offset:      i32      ファイル先頭からのバイトオフセット
  size:        i32      セクションデータのバイトサイズ
```

- `magic` が `KLIB` でない場合: format load error
- `version_major` が未対応の場合: compatibility load error
- `features` に未対応ビットが立っている場合: compatibility load error

### セクション型 ID 一覧

| 型 ID  | 名称           | 必須       | 説明 |
|--------|----------------|------------|------|
| 0x0001 | Module Info    | 必須       | module 識別情報と entry label |
| 0x0002 | Constant Pool  | 必須       | 全リテラル値・参照値の集中テーブル |
| 0x0003 | Variable Table | 必須       | compile-time 解決済み変数の定義テーブル |
| 0x0004 | Import         | 省略可     | import がある場合は必須、ない場合は省略可 |
| 0x0005 | Instruction    | 必須       | スタックマシンバイトコード |
| 0x0006 | Label Map      | 必須       | label name → bytecode offset の解決済み mapping |
| 0x0007 | Debug          | 任意       | ソースマップ、シンボルテーブル（strip 可） |

未知の型 ID のセクションは `features` に対応ビットがなければ load error とする。

---

## `.klibtxt` テキスト表現

`.klibtxt` は `.klib` と同じ logical module を人間可読な形で表現する補助形式である。
binary の完全な byte dump ではなく、各セクションの論理内容を assembler 風に整形した表示用成果物として扱う。

- **用途**: debug、仕様レビュー、golden test、差分確認。
- **非用途**: runtime 入力、配布成果物、バイナリ `.klib` の代替。
- **生成契約**: CLI の `kes build --txt-il` により、対応する `.klib` と同じ build 出力ツリーに `.klibtxt` を併置する。
- **可読性優先**: 命令行は `IL_0000` のような表示用ラベルを持つ。これは読者の追跡用であり、binary `.klib` の byte offset そのものとは限らない。

### 基本構造

`.klibtxt` は UTF-8（BOM なし）のテキストで、次の順にセクションを持つ。

1. `.module` / `.script` / `.source` などの module header
2. `.imports`
3. `.constants`
4. `.instructions`
5. `.debug`

### 表示規則

- 文字列は `"..."` で表示する。
- 参照系定数は `actorRef`、`assetRef`、`localeKey` などの kind 名を明示する。
- 命令は `IL_XXXX: OPCODE operands...` 形式で 1 行ずつ表示する。
- 行末コメントには source file と source position を表示してよい。
- `.debug` には、少なくとも symbol 情報と instruction → source map を表示する。

### `.klibtxt` サンプル

次の `.kc`:

```kc
label #start
jump #start
```

に対応する `.klibtxt` の一例:

```txt
.klibtxt 1.0
.module "chapter001"
.script "events/chapter001"
.source "events/chapter001.kc"
.imports.count 0

.imports
{
  // none
}

.constants
{
  [0] string "chapter001"
  [1] string "events/chapter001.kc"
  [2] string "#start"
}

.instructions
{
  IL_0000: LABEL #start // events/chapter001.kc:1:1
  IL_0001: JUMP #start  // events/chapter001.kc:2:1
}

.debug
{
  .symbols
  {
    // none
  }
  .source-map
  {
    IL_0000 -> "events/chapter001.kc":1:1
    IL_0001 -> "events/chapter001.kc":2:1
  }
}
```

binary `.klib` の byte offset、little-endian 配置、section size などの物理表現は `.klibtxt` へそのまま転写しない。
必要な場合は logical 情報としてコメントまたは directive へ再構成して示す。

---

## Module Info セクション（0x0001）

module の識別情報を保持する。

```
scriptId:    string    manifest の scripts entry ID
moduleId:    string    この .klib document 内の主 module を識別する安定 ID
sourcePath:  string    project root から見た生成元 .kc の正規化 path
has_entry:   i32       0 = entry label なし、1 = あり
entryLabel:  string    has_entry = 1 の場合のみ。manifest entry/chapter が参照する開始 label
```

VM/runtime は `module.scriptId` が manifest の scripts entry と一致することを load 時に検証する。
`has_entry = 1` かつ `entryLabel` が Label Map に存在しない場合は manifest integration error として実行開始前に失敗する。

---

## 定数プールセクション（0x0002）

命令オペランドで使う全リテラル値と参照値を集中管理するテーブルである。
命令セクションは定数プール index（`i32`）で値を参照する。index は 0 始まり。

```
count: i32
entries[count]:
  type: i32
  data: ...   （type に応じた可変長データ）
```

### 定数プールエントリ型

| type | 名称      | data |
|------|-----------|------|
| 1    | string    | `i32` 長 + UTF-8 bytes |
| 2    | number    | `f64` |
| 3    | bool      | `i32`（0/1） |
| 4    | null      | なし（0 バイト） |
| 5    | actorRef  | string index `i32`（同プール内の string エントリへの参照） |
| 6    | assetRef  | string index `i32` |
| 7    | localeKey | string index `i32`（legacy / authoring 用。localized runtime `.klib` では非推奨） |
| 8    | classRef  | string index `i32`（compile-time 解決済みの class stable ID） |
| 9    | fieldRef  | string index `i32`（compile-time 解決済みの field stable ID） |
| 10   | methodRef | string index `i32`（compile-time 解決済みの method stable ID） |

actorRef / assetRef / localeKey / classRef / fieldRef / methodRef の `string index` は同じ定数プール内の string エントリ（type=1）を指す。
現在の公開 build では、ローカライズ対象本文は compile-time に解決された string 定数として `.klib` へ埋め込む。`localeKey` は旧来の authoring / 互換用表現として予約される。
stable ID 文字列の内部形式は compiler 実装依存でよいが、同一プロジェクト内で一意かつ build 間で安定していなければならない。
string エントリはこれらの参照型エントリより先に配置することを推奨する。

---

## 変数テーブルセクション（0x0003）

`.kc` で定義されたすべての変数を compile-time に解決したテーブルである。
VM は変数を変数テーブル index（`i32`）で参照し、名前で探索しない。

```
count: i32
variables[count]:
  id_idx:      i32   変数の stable ID（定数プール string index）
  name_idx:    i32   debug 用 source 変数名（定数プール string index）
  type:        i32   変数型 ID
  scope:       i32   スコープ種別 ID
  scope_id:    i32   スコープ識別子（0 = module-level）
  has_initial: i32   初期値の有無（0 = なし、1 = 定数プール参照）
  initial_idx: i32   has_initial = 1 の場合のみ。初期値の定数プール index
```

### 変数型 ID

| type | 名称      |
|------|-----------|
| 1    | number    |
| 2    | bool      |
| 3    | string    |
| 4    | Actor     |
| 5    | assetRef  |
| 6    | localeKey（legacy / authoring 用） |
| 7    | array     |
| 8    | classInstance |

### スコープ種別 ID

| scope | 名称    | 説明 |
|-------|---------|------|
| 1     | global  | プロジェクト全体で共有 |
| 2     | script  | .kc ファイル（module）スコープ |
| 3     | chapter | chapter / event entry スコープ |
| 4     | block   | if/while/for/using ブロックスコープ |
| 5     | local   | 関数/メソッドローカルスコープ |

---

## インポートセクション（0x0004）

import された他の `.klib` module への参照を保持する。import がない場合は省略可能。

```
count: i32
imports[count]:
  moduleId:   string    import 先 module の stable ID
  scriptId:   string    import 先 .klib artifact を manifest から解決するための ID
  sourcePath: string    import 解決後の .kc の正規化 path
  has_entry:  i32       0 = entry なし、1 = あり
  entryLabel: string    has_entry = 1 の場合のみ
```

VM/runtime は import 解決をやり直さず、`imports[].scriptId` が manifest に存在することだけを検証する。

---

## 命令セクション（0x0005）

スタックマシンバイトコードを格納するセクションである。

```
bytecode_size: i32
bytecode:      bytes[bytecode_size]
```

### スタックマシン実行モデル

VM は次の状態を保持する。

- **PC（プログラムカウンタ）**: bytecode 内の現在実行バイトオフセット。通常命令は PC を命令サイズ分進める。
- **オペランドスタック**: タグ付き値のスタック。型不一致は実行時エラー（compile-time 完全解決が前提のため、正常な `.klib` では発生しない）。配列および class instance は runtime 管理の参照ハンドルとして積まれる。
- **変数テーブル**: Variable Table セクションのエントリに対応するランタイム値の配列。`var_idx` で直接参照する。

`JUMP` / `JUMP_FALSE` の offset は符号付き 32 bit 相対値であり、**当該命令の次のバイト**を基点とする。
`SELECT` の各 case offset は **SELECT 命令の最終オペランドの次のバイト**を基点とする。

### オペコード一覧

#### スタック操作・定数ロード

| オペコード    | バイト | オペランド  | スタック変化      | 説明 |
|--------------|--------|-------------|-------------------|------|
| `PUSH_CONST` | 0x01   | `idx:i32`   | → val             | 定数プール entry idx の値をプッシュ |
| `PUSH_TRUE`  | 0x02   | -           | → true            | |
| `PUSH_FALSE` | 0x03   | -           | → false           | |
| `PUSH_NULL`  | 0x04   | -           | → null            | |
| `PUSH_INT`   | 0x05   | `val:i32`   | → val             | 小整数のインラインプッシュ |
| `POP`        | 0x06   | -           | val →             | スタックトップを破棄 |
| `DUP`        | 0x07   | -           | val → val val     | スタックトップを複製 |

#### 変数操作

| オペコード   | バイト | オペランド    | スタック変化 | 説明 |
|-------------|--------|---------------|--------------|------|
| `LOAD_VAR`  | 0x10   | `var_idx:i32` | → val        | 変数テーブル var_idx の値をプッシュ |
| `STORE_VAR` | 0x11   | `var_idx:i32` | val →        | スタックトップを変数テーブル var_idx に保存（pop） |
| `DEF_VAR`   | 0x12   | `var_idx:i32` | init →       | 変数定義。スタックトップを初期値として設定（pop） |

#### 算術演算

| オペコード | バイト | スタック変化 | 説明 |
|-----------|--------|--------------|------|
| `ADD`     | 0x20   | a b → a+b    | |
| `SUB`     | 0x21   | a b → a-b    | |
| `MUL`     | 0x22   | a b → a*b    | |
| `DIV`     | 0x23   | a b → a/b    | |
| `NEG`     | 0x24   | a → -a       | 単項マイナス |

#### 比較演算

| オペコード | バイト | スタック変化  | 説明 |
|-----------|--------|---------------|------|
| `EQ`      | 0x30   | a b → bool    | |
| `NEQ`     | 0x31   | a b → bool    | |
| `LT`      | 0x32   | a b → bool    | |
| `LE`      | 0x33   | a b → bool    | |
| `GT`      | 0x34   | a b → bool    | |
| `GE`      | 0x35   | a b → bool    | |

#### 論理演算

| オペコード | バイト | スタック変化 | 説明 |
|-----------|--------|--------------|------|
| `AND`     | 0x38   | a b → bool   | |
| `OR`      | 0x39   | a b → bool   | |
| `NOT`     | 0x3A   | a → bool     | |

#### 制御フロー

| オペコード     | バイト | オペランド                               | スタック変化 | 説明 |
|---------------|--------|-----------------------------------------|--------------|------|
| `JUMP`        | 0x40   | `offset:i32`                            | -            | 無条件ジャンプ。offset の基点は当該命令の次のバイト |
| `JUMP_FALSE`  | 0x41   | `offset:i32`                            | cond →       | スタックトップが false の場合ジャンプ |
| `LABEL`       | 0x42   | `name_idx:i32` `flags:i32`              | -            | label marker。実行時は PC を進めるだけ。flags bit0 = public（manifest entry から参照される label） |
| `SELECT`      | 0x43   | `n:i32` `[text_idx:i32 offset:i32] × n` | prompt →     | 選択肢表示。スタックから prompt（string/null）を pop し n 個の選択肢を runtime へ渡す。source 上で `select #tag:` が指定されている場合、runtime はその tag を選択 UI の識別子として利用してよい。ユーザー選択後、対応 offset へジャンプ |
| `END`         | 0x4F   | -                                       | -            | 実行完了 |

#### 関数・コマンド呼び出し

| オペコード      | バイト | オペランド                   | スタック変化     | 説明 |
|----------------|--------|------------------------------|------------------|------|
| `CALL`         | 0x50   | `cmd_idx:i32` `argc:i32`     | args... → result | compile-time 解決済みの command（定数プール string index cmd_idx）を argc 個の引数で呼び出す。戻り値をプッシュ |
| `CALL_VOID`    | 0x51   | `cmd_idx:i32` `argc:i32`     | args... →        | 戻り値を破棄するコマンド呼び出し |
| `SYSCALL`      | 0x52   | `sys_idx:i32` `argc:i32`     | args... → result | runtime syscall（定数プール string index sys_idx）。戻り値をプッシュ |
| `SYSCALL_VOID` | 0x53   | `sys_idx:i32` `argc:i32`     | args... →        | 戻り値を破棄する syscall |

引数はスタックに左から右の順で push し（最後の引数がスタックトップ）、opcode が argc 個分を pop する。
`say`、`nar`、演出表示などのシナリオ構文は専用 opcode を持たず、compiler が runtime 定義の syscall 名へ lower して `SYSCALL` / `SYSCALL_VOID` で呼び出す。

#### 配列・クラス操作

| オペコード           | バイト | オペランド                    | スタック変化            | 説明 |
|---------------------|--------|-------------------------------|-------------------------|------|
| `ARRAY_NEW`         | 0x54   | `count:i32`                   | elems... → array        | `count` 個の値から配列インスタンスを生成する。要素は source 順を保持する |
| `ARRAY_GET`         | 0x55   | -                             | array index → value     | 配列要素を読み取る。index は 0 始まりで、範囲外は実行時エラー |
| `ARRAY_SET`         | 0x56   | -                             | array index value →     | 配列要素を書き込む。範囲外は実行時エラー |
| `NEW`               | 0x57   | `class_idx:i32` `argc:i32`    | args... → instance      | classRef を参照してインスタンスを生成し、`__init__` があれば直ちに呼び出す |
| `GET_FIELD`         | 0x58   | `field_idx:i32`               | receiver → value        | fieldRef で指定したメンバー変数を読み取る |
| `SET_FIELD`         | 0x59   | `field_idx:i32`               | receiver value →        | fieldRef で指定したメンバー変数へ書き込む |
| `CALL_METHOD`       | 0x5A   | `method_idx:i32` `argc:i32`   | receiver args... → result | methodRef で指定したメソッドを呼び出し、戻り値をプッシュする |
| `CALL_METHOD_VOID`  | 0x5B   | `method_idx:i32` `argc:i32`   | receiver args... →      | 戻り値を破棄するメソッド呼び出し |
| `DISPOSE`           | 0x5C   | -                             | receiver →              | `using` ブロック終了時に `dispose` を呼び出すための命令。`dispose` 未定義なら no-op |
| `ADD_VAR`           | 0x5D   | `target_idx:i32` `source_idx:i32` | -                    | number変数 `target` に number変数 `source` を加算する融合命令 |
| `INCREMENT_VAR`     | 0x5E   | `var_idx:i32` `delta:i32`     | -                       | number変数へ整数定数を加算する融合命令 |
| `NUMBER_ARRAY_GET`  | 0x5F   | -                             | number[] index → number | 型特化された数値配列要素を読み取る |
| `NUMBER_ARRAY_SET`  | 0x60   | -                             | number[] index number → | 型特化された数値配列要素を書き込む |
| `ARRAY_NEW_FILLED`  | 0x61   | -                             | count fill → array      | 実行時に決まる長さと初期値で配列を生成する |
| `CALL_FUNCTION`     | 0x62   | `function_idx:i32` `argc:i32` | args... → result       | Function Tableのユーザー定義関数を呼び出す |
| `CALL_FUNCTION_VOID`| 0x63   | `function_idx:i32` `argc:i32` | args... →              | 戻り値を利用しないユーザー定義関数呼び出し |
| `RETURN_VALUE`      | 0x64   | -                             | value →                | 値を呼び出し元へ返す |
| `RETURN_VOID`       | 0x65   | -                             | -                      | 値を返さず呼び出し元へ戻る |

`ARRAY_NEW` の要素は左から右の順で push し、命令は `count` 個を pop して同じ順序で配列へ格納する。
`ADD_VAR`、`INCREMENT_VAR`、`NUMBER_ARRAY_GET`、`NUMBER_ARRAY_SET`、`ARRAY_NEW_FILLED` は version 1.1 で追加された。compiler は静的なnumber型を確認できる場合だけ型特化命令を出力し、型条件を満たさないbytecodeをruntime errorとする。
`NEW` の `class_idx` は classRef の定数プール index、`GET_FIELD` / `SET_FIELD` は fieldRef、`CALL_METHOD*` は methodRef を参照する。
`CALL_METHOD*` は receiver を先に push し、その後に通常の `CALL` と同様に引数を左から右へ push する。opcode は argc 個の引数を pop した後に receiver を pop する。
`using` 構文は compile-time に `NEW` とスコープ終端での `DISPOSE` に lower する。`__destroy__` は runtime のオブジェクト寿命管理に属し、専用 opcode は持たない。
クラス内での暗黙のメンバー参照は、compiler が hidden local の receiver 参照と `GET_FIELD` / `SET_FIELD` / `CALL_METHOD*` に lower する。

#### Function Table（0x0008）

ユーザー定義関数を含むmoduleは、次の任意sectionを持つ。

```txt
count: i32
functions[count]:
  name_idx: i32
  entry_offset: i32
  returns_value: i32
  parameter_count: i32
  parameter_slots[parameter_count]: i32
  local_count: i32
  local_slots[local_count]: i32
```

`entry_offset` は関数先頭命令のbyte offsetである。`parameter_slots` は引数順、`local_slots` は再帰呼び出し時に退避・復元する変数slotを表す。呼び出し元は引数を左から右へpushし、`CALL_FUNCTION*` がcall frameを生成して関数先頭へ移動する。`RETURN_*` はframeのreturn位置とlocal値を復元する。

#### シナリオ構文の lowering

シナリオ DSL の `say`、`nar`、将来的な UI/演出命令は VM 命令として固定せず、runtime syscall として表現する。

- `say <actor> #<tag>:` のような構文は、`actor`（actorRef）と compile-time に解決済みの本文 string を順に push して `SYSCALL_VOID sys_idx=<scenario.say> argc=2` に lower する。タグは voice 解決や debug のために compiler / build 系で利用されるが、表示本文そのものの runtime 解決には使わない。
- `say <actor>:` （タグなし）は、`actor` と本文 string を push して同様に `argc=2` で lower する。
- `nar #<tag>:` のような構文は、compile-time に解決済みの本文 string を push して `SYSCALL_VOID sys_idx=<scenario.nar> argc=1` に lower する。
- `nar:` （タグなし）は、本文 string を push して同様に `argc=1` で lower する。

syscall 名文字列の命名規約は runtime 仕様が所有するが、本仕様のサンプルでは `scenario.say`、`scenario.nar` を用いる。

---

## ラベルマップセクション（0x0006）

compile-time に解決済みの label name → bytecode offset mapping を保持する。
VM/runtime は manifest entry/chapter の entryLabel をこの map で offset に解決する。

```
count: i32
labels[count]:
  name_idx: i32   定数プール string index（label 名）
  offset:   i32   bytecode 内のバイトオフセット（LABEL opcode の位置）
  flags:    i32   bit0 = public（manifest から参照される entry label）
```

同じ `name_idx` を持つエントリが複数存在する場合は instruction schema violation とする。

---

## デバッグセクション（0x0007）

ソース位置マッピングとシンボルテーブルを保持する任意セクション。
リリースビルドでは strip 可能。debug セクションが欠落しても VM は同じ bytecode を同じ順序で実行する。

```
module_display_name_idx: i32   定数プール string index（0 = 使用しない）
file_display_name_idx:   i32   定数プール string index（0 = 使用しない）
mapping_count: i32
source_mappings[mapping_count]:
  bytecode_offset: i32   対応する bytecode 内のオフセット
  file_idx:        i32   定数プール string index（生成元 .kc パス）
  line:            i32   1 始まりの行番号（0 = 不明）
  column:          i32   1 始まりの列番号（0 = 不明）
  end_line:        i32   終端行（0 = 単一点）
  end_column:      i32   終端列（0 = 単一点）
  kind:            i32   mapping 種別 ID（下表参照）
symbol_count: i32
symbols[symbol_count]:
  var_idx:          i32   変数テーブル index
  display_name_idx: i32   定数プール string index（表示用変数名）
```

### source mapping 種別 ID

| kind | 名称        | 説明 |
|------|-------------|------|
| 0    | statement   | 通常の命令文 |
| 1    | textBody    | say/nar 本文 |
| 2    | selectCase  | select/case 構文 |
| 3    | expression  | 式 |
| 4    | synthetic   | コンパイラ生成の人工命令 |

debug metadata の不備（mapping 欠落、line = 0 等）は load error にしてはならない。
opcode と bytecode offset が valid である限り VM は実行できる。

### runtime error と debug 表示

| 表示要素    | 優先順 |
|-------------|--------|
| module 名   | `debug.moduleDisplayName`、`module.moduleId`、`module.scriptId` の順 |
| file 名     | source mapping `file_idx`、`debug.fileDisplayName`、`module.sourcePath` の順 |
| line/column | source mapping の `line` / `column`。欠落時は表示しない |
| 実行位置    | `scriptId:bytecodeOffset` を常に表示可能な fallback とする |

---

## 互換性ポリシー

VM/runtime は命令実行を開始する前に File Header の `magic`、`version_major`、`features` を検証する。

| 検証対象                     | 読み込み側の期待動作 |
|------------------------------|----------------------|
| `magic`                      | `KLIB` 以外、4 バイト未満は format load error |
| `version_major`              | 未対応 major は compatibility load error |
| `version_minor` / `version_patch` | 同一 major 内は後方互換前提。対応できない場合は compatibility load error |
| `features`                   | 未対応ビットが立っている場合は compatibility load error |
| 必須セクション               | 欠落は load error |
| 未知セクション               | `features` に対応ビットがなければ load error |

---

## 実行モデル

### スタックとプログラムカウンタ

VM は Instruction セクション bytecode 内のバイトオフセットを PC として保持する。
通常命令は命令サイズ分 PC を進める。`JUMP` / `JUMP_FALSE` は PC を相対 offset で更新する。
`SELECT` はユーザー入力待ちの後に選ばれた case の offset で PC を更新する。`END` は実行を完了する。

### save/load 境界

runtime の save data は `.klib` 本体を複製せず、次の安定識別子を参照することで実行状態を復元する。

| 参照                  | 仕様 |
|-----------------------|------|
| `scriptId`            | manifest の script entry と Module Info の `scriptId` に一致する安定 ID |
| `bytecodeOffset`      | Instruction セクション内の現在または再開対象バイトオフセット。`scriptId` と組で実行位置を一意に表す |
| `variableState`       | 保存対象 scope（global / script / chapter）に属する variable index と値の集合。一時値（オペランドスタック上）は原則として保存対象に含めない |
| `continuationState`   | `SELECT` や runtime 入力待ちで一時停止した場合の再開情報。`scriptId`、`bytecodeOffset`、pending result target を参照する |

VM/runtime は load 時に `scriptId` が manifest と `.klib` に存在し、`bytecodeOffset` が有効範囲内であることを検証する。
debug セクションのソースマップを使って save/load を復元してはならない。

---

## manifest 参照契約

runtime manifestはruntime packageの目録であり、scripts、assets、locale、entryなどを所有する。ファイル名と完全なschemaは[ランタイムマニフェスト仕様](runtime-manifest-spec.md)が所有する。
`.klib` はそれらを定数プールと bytecode opcode に含まれる安定 ID/key で参照する。

| manifest が所有する情報       | `.klib` での参照方法 |
|-------------------------------|---------------------|
| scripts entry（artifact path）| Module Info `scriptId`、Import `scriptId` |
| entry / chapter の開始位置   | Label Map で entryLabel → bytecode offset に解決 |
| asset（path、variant）        | 定数プール `assetRef` エントリ内の string ID |
| 表示テキスト（翻訳文字列）    | 定数プール string エントリ内の localized UTF-8 text |
| runtime capabilities          | File Header `features` bitmask と manifest の runtime 要件を照合 |

ローカライズ本文は build 時に `.csv` と `.kc` から compile-time 解決され、対象言語向け `.klib` の string 定数として埋め込まれる。
翻訳作業用の source locale `.csv` は build-time 入力であり、runtime や VM は直接読み込まない。
source locale `.csv` の列構成は [ローカライズ辞書仕様書](localization-dictionary-spec.md)が所有する。

---

## `.kc` スクリプトサンプルとコンパイル後 `.klib` バイトコード表現

次の `.kc` ファイルは、`actor` 定義、`cast`、`var` 定義、`label`、`say`、`select/case`、代入式、`jump`、`nar`、インラインマクロを含む
小規模プロローグシナリオの例である。

```kes
// events/prologue.kc
actor Hero
cast Hero
var score: number = 0

label start
say Hero #sy_prologue_0001:
    旅はまだ{vo}続く
select #se_prologue_0002:
    case "街へ向かう" #town
    case "森へ向かう" #forest
label town
score = score + 10
jump end
label forest
nar #na_prologue_0003:
    静かな森に{p}足を踏み入れた。
label end
```

compiler はこの `.kc` を解析し、定数プール・変数テーブル・bytecode・ラベルマップ・デバッグ情報を生成する。
`{vo}` や `{p}` のようなインラインマクロを含む本文も、compile-time に解決された localized string として `.klib` へ埋め込まれる。
以下はその人間可読なアセンブリ表現である。

### 定数プール（コンパイル後）

```
[0]  string    "script.prologue"
[1]  string    "module.prologue"
[2]  string    "events/prologue.kc"
[3]  string    "start"
[4]  string    "actor.Hero"
[5]  actorRef  → [4]               ; actor.Hero
[6]  string    "sy_prologue_0001"
[7]  string    "旅はまだ{vo}続く"     ; say text（localized）
[8]  string    "街へ向かう"
[9]  string    "森へ向かう"
[10] string    "na_prologue_0003"
[11] string    "静かな森に{p}足を踏み入れた。" ; nar text（localized）
[12] string    "v.score"           ; 変数 stable ID
[13] string    "score"             ; 変数 source 名
[14] number    0.0                 ; var 初期値
[15] number    10.0                ; 加算値
[16] string    "cast"              ; command 名
[17] string    "town"              ; label 名
[18] string    "forest"
[19] string    "end"
[20] string    "prologue"          ; debug module 表示名
[21] string    "prologue.kc"       ; debug file 表示名
[22] string    "scenario.say"      ; syscall 名
[23] string    "scenario.nar"      ; syscall 名
```

### 変数テーブル（コンパイル後）

```
[0]  id=cp[12]"v.score"  name=cp[13]"score"  type=number(1)  scope=script(2)  initial=cp[14]=0.0
```

### バイトコード（アセンブリ表現）

```
offset  size  opcode        operands                          備考
────────────────────────────────────────────────────────────────────────────
0x0000  5     PUSH_CONST    cp[5]                             ; actorRef:Hero
0x0005  9     CALL_VOID     cmd=cp[16]"cast" argc=1           ; cast Hero
0x000E  5     PUSH_CONST    cp[14]                            ; number:0.0
0x0013  5     DEF_VAR       var[0]                            ; var score = 0
0x0018  9     LABEL         cp[3]"start" flags=1              ; label start（public）
0x0021  5     PUSH_CONST    cp[5]                             ; actor:Hero
0x0026  5     PUSH_CONST    cp[7]                             ; string:"旅はまだ{vo}続く"
0x002B  9     SYSCALL_VOID  sys=cp[22]"scenario.say" argc=2   ; actor + text
0x0034  1     PUSH_NULL                                       ; prompt = null
0x0035  21    SELECT        n=2                               ; 2 選択肢
                            cp[8]"街へ向かう" offset=+0x0000  ; → town（base=0x004A）
                            cp[9]"森へ向かう" offset=+0x001E  ; → forest（base=0x004A, +30）
0x004A  9     LABEL         cp[17]"town"   flags=0
0x0053  5     LOAD_VAR      var[0]                            ; score をプッシュ
0x0058  5     PUSH_CONST    cp[15]                            ; number:10.0
0x005D  1     ADD                                             ; score + 10
0x005E  5     STORE_VAR     var[0]                            ; score = 結果
0x0063  5     JUMP          offset=+0x0017                    ; → end（base=0x0068）
0x0068  9     LABEL         cp[18]"forest" flags=0
0x0071  5     PUSH_CONST    cp[11]                            ; string:"静かな森に{p}足を踏み入れた。"
0x0076  9     SYSCALL_VOID  sys=cp[23]"scenario.nar" argc=1   ; voice なし
0x007F  9     LABEL         cp[19]"end"    flags=0
0x0088  1     END
────────────────────────────────────────────────────────────────────────────
bytecode_size: 0x89（137 バイト）
```

### ラベルマップ（コンパイル後）

```
name=cp[3]"start"   offset=0x0018  flags=1  （public）
name=cp[17]"town"   offset=0x004A  flags=0
name=cp[18]"forest" offset=0x0068  flags=0
name=cp[19]"end"    offset=0x007F  flags=0
```

### `.kc` → `.klib` バイトコード対応表

| `.kc` 構文                          | `.klib` バイトコード                                       | 備考 |
|-------------------------------------|------------------------------------------------------------|------|
| `cast Hero`                         | `PUSH_CONST cp[5]` + `CALL_VOID cmd=cp[16] argc=1`         | actorRef を push してコマンド呼び出し |
| `var score: number = 0`             | `PUSH_CONST cp[14]` + `DEF_VAR var[0]`                     | 初期値を push してから変数定義 |
| `label start`                       | `LABEL cp[3] flags=1`                                      | flags=1 で public（manifest entry から参照可） |
| `say Hero #sy_prologue_0001: ...`   | `PUSH_CONST`×2 + `SYSCALL_VOID sys=cp[22] argc=2`         | actor/text を積んで `scenario.say` を呼ぶ。本文中の `{vo}` などのインラインマクロは localized string に保持される |
| `select #se_prologue_0002: case "..." case "..."` | `PUSH_NULL` + `SELECT n=2 [text offset]×2`       | case テキストと jump offset をインライン。`select` の tag は source 識別子として保持してよい |
| `score = score + 10`                | `LOAD_VAR` + `PUSH_CONST` + `ADD` + `STORE_VAR`           | load → push → 演算 → store |
| `jump end`                          | `JUMP offset=+23`                                          | Label Map から resolve した相対 offset |
| `nar #na_prologue_0003: ...`        | `PUSH_CONST cp[11]` + `SYSCALL_VOID sys=cp[23] argc=1`     | localized string を push して `scenario.nar` を呼ぶ。本文中の `{p}` などのインラインマクロも string に保持される |

---

## 対象読者

- `.kc` から `.klib` を生成する CLI / compiler 関連機能の設計者と実装者。
- `.klib` を読み込んでバイトコードを実行するスタックマシン VM の実装者。
- `manifest.json` と `.klib` を組み合わせて実行資産を解決する runtime 実装者。
- runtime error、debug 表示、golden test、仕様レビューを担当する開発者。

## 適用範囲

本仕様は、`.klib` 中間表現の公開契約を所有する。

- `.klib` ファイルの目的、バイナリ形式、セクション構造、version、feature compatibility。
- `.kc` 入力、`.kel` entry、`manifest.json`、runtime が読む成果物との関係。
- スタックマシン VM が参照するバイトコード命令セット、値表現、制御フロー、実行位置の契約。
- 定数プール、変数テーブル、ラベルマップ、デバッグセクションの契約。
- asset ID、locale key、script path など、manifest が所有する情報への参照関係。

## 非対象範囲

- compiler 実装、`.klib` emitter 実装、serializer 実装、バイナリ schema validator 実装。
- スタックマシン VM 実装、命令ディスパッチや save/load の実装詳細。
- runtime 実装、描画、音声、入力、UI、プラットフォーム固有の配布処理。
- asset manifest 全体、source locale `.csv`、ローカライズ済み `.klib` バリアントの manifest 表現、runtime package manifest の完全な schema。
- 配布時の圧縮、暗号化、署名、改ざん検出。
- テキスト形式（JSON 等）での `.klib` 表現（debug tooling が独自に定義してよい）。

## 現行用語と旧称

| 種別                 | 現行用語 | 扱い |
|----------------------|----------|------|
| イベントスクリプト入力 | `.kc`   | KoromoEventScript 言語で記述された現在の正規入力拡張子。 |
| VM 実行用中間表現      | `.klib` | `.kc` から生成される現在の正規中間表現拡張子。バイナリ形式。 |
| 旧イベントスクリプト表記 | `.ke` | 旧称。新規仕様では `.kc` を正とする。 |
| 旧中間表現表記         | `.k`   | 旧称。新規仕様では `.klib` を正とする。 |

## 隣接仕様

本仕様は、次の仕様を参照する。詳細責務は各仕様に委譲し、`.klib` 仕様は VM 実行用中間表現の契約に集中する。

| 仕様 | 本仕様から見た関係 |
|------|--------------------|
| `docs/spec/cli-tool-spec.md`       | `kes build`、`kes run`、`kes publish` が `.kc` / `.kel` を扱い、`.klib` と `manifest.json` を生成または runtime に渡す成果物契約を定義する。 |
| `docs/spec/kes-language-spec.md`   | `.kc` の構文、名前、型、変数、制御構文、source position の前提を定義する。 |
| `docs/spec/kes-language-stl-spec.md` | `__systemcall__`、STL、runtime call、asset ID、actor、tag など、`.klib` に反映される語彙の前提を定義する。 |
| `docs/spec/kel-file-spec.md`       | `.kel` の entry、chapter、script path 参照の前提を定義する。 |
| `docs/spec/windows-runtime-spec.md` | Windows runtime が `manifest.json` と VM 成果物を読み込む隣接仕様である。 |
| `docs/spec/unity-runtime-spec.md`  | Unity runtime が published data と VM 成果物を読み込む隣接仕様である。 |
| `docs/spec/unreal-runtime-spec.md` | Unreal runtime が published data と VM 成果物を読み込む隣接仕様である。 |
| `docs/spec/overview.md`            | 読者が KoromoEventScript 全体像と各詳細仕様へ到達するための導線を持つ。 |

## 不整合の扱い

既存仕様と本仕様の間で用語または形式が異なる場合は、本仕様を正とする。
既存仕様の責務範囲をこの文書で直接変更しない。CLI、runtime、overview などの文書更新が必要な場合は、
それぞれの責務を持つ後続タスクまたは別 Issue で扱う。
