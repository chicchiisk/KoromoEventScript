# .k 中間表現仕様

この文書は、KoromoEventScript の `.ke` から生成される VM 実行用中間表現 `.k` の公開契約を定義する仕様である。

## 目的

`.k` は CLI が `.ke` を解析・検証した後に生成し、VM と runtime がイベント実行時に参照する中間表現である。
本仕様は、CLI、VM、runtime、debug tooling の実装者とレビュー担当者が、`.k` の責務境界と隣接仕様との関係を同じ前提で確認できるようにする。

この文書は段階的に拡張する。現時点では、`.k` 中間表現仕様が扱う範囲、扱わない範囲、参照すべき隣接仕様、現行用語と旧称の関係に加えて、基本 file format、compatibility policy、document/module/import、instruction schema、主要 opcode 群、value model、variable/scope、execution state reference、source mapping と debug metadata、manifest 参照契約、最小正規化例を定義する。

## 基本ファイル形式

_Requirements: 1.1, 1.3_

`.k` は、`.ke` から生成される VM 実行用の中間表現ファイルである。拡張子は `.k` とし、runtime package や build output の中では `manifest.json` などの隣接成果物から参照される script artifact として扱う。

`.k` の基本ファイル形式は次の通りである。

| 項目 | 仕様 |
|------|------|
| 目的 | CLI / compiler が検証済み `.ke` を VM/runtime が読み込める実行契約へ正規化する。 |
| 拡張子 | `.k`。旧称 `.klib` は新規仕様では使用しない。 |
| 文字エンコーディング | UTF-8。BOM なしを正規形とする。VM/runtime は UTF-8 として復号できない `.k` を format load error として読み込み失敗にする。 |
| 改行 | LF を正規形とする。CRLF は読み込み時に LF と同等に扱ってよいが、golden test や正規化出力では LF を用いる。 |
| top-level document identification | top-level object の `format` に固定値 `koromo.k` を持つ。VM/runtime は `format` が存在しない、文字列でない、または `koromo.k` でない場合、format load error として読み込み失敗にする。 |

`.k` document は top-level object として識別される。少なくとも compatibility 判定に必要な `format`、`version`、`features` を持つ。

```json
{
  "format": "koromo.k",
  "version": { "major": 1, "minor": 0, "patch": 0 },
  "features": []
}
```

`version` は `.k` document contract の互換性判定情報であり、`major`、`minor`、`patch` を非負整数として表す。`features` は、この `.k` を正しく読み込み、実行前検証するために VM/runtime が対応している必要がある feature identifier の配列である。feature identifier は ASCII の安定した文字列とし、具体的な feature 名は各 feature を導入する仕様更新で定義する。

## 互換性ポリシー

_Requirements: 1.1, 1.3_

VM/runtime は `.k` の命令実行を開始する前に、少なくとも `format`、`version`、`features` を検証する。この pre-load check に失敗した `.k` は実行してはならない。

| 検証対象 | 読み込み側の期待動作 |
|----------|----------------------|
| `format` | `koromo.k` 以外、欠落、型不一致、top-level object でない document は format load error として読み込み失敗にする。 |
| `version.major` | VM/runtime が対応する major version と一致しない未知 major version は compatibility load error として読み込み失敗にする。 |
| `version.minor` / `version.patch` | 対応 major の範囲内では後方互換を前提とする。ただし、読み込み側が必要な minor/patch 契約を満たせない場合は compatibility load error として読み込み失敗にし、必要 version を診断へ含める。 |
| `features` | 配列内に未対応 feature が 1 つでも含まれる場合、unsupported feature の compatibility load error として読み込み失敗にする。診断には未対応 feature identifier を含める。 |

未知 major version は、同名 field が存在しても意味論、命令 schema、値表現、source mapping、manifest 参照契約が互換とは限らないため、VM/runtime は推測して実行してはならない。unsupported feature も、読み込み側が該当 feature の検証規則または実行前提を保証できないことを意味するため、feature を無視して実行してはならない。

Format errors と compatibility errors はどちらも load error であり、VM/runtime の命令実行前に発生する。Format errors は `.k` document として識別または復号できない問題、compatibility errors は document は識別できるが `version` または `features` の契約を読み込み側が満たせない問題として区別する。

## document、module、import、実行単位

_Requirements: 1.2, 2.5, 5.1, 5.2_

`.k` document は 1 つの VM 実行単位を表す。基本方針として、1 つの `.k` document は 1 つの `.ke` 入力から生成され、その `.ke` を主 module として持つ。複数ファイル project では、import された `.ke` は主 module と同じ `.k` document に埋め込まれるのではなく、各 import 元 `.ke` から生成された別の `.k` document として扱う。VM/runtime は `manifest.json` が列挙する script 情報と `.k` document 内の module/import 情報を照合し、実行開始前に必要な module 群を解決する。

### 単一 `.ke` 入力

単一ファイル project では、`.k` document の `module` は生成元 `.ke` と 1 対 1 に対応する。

| 項目 | 仕様 |
|------|------|
| `module.moduleId` | `.k` document 内で主 module を識別する安定 ID。compiler が生成し、同一 build 内で一意でなければならない。 |
| `module.scriptId` | manifest の scripts 一覧が `.k` artifact を参照するための ID。VM/runtime は manifest 側の script entry と `.k` 側の `module.scriptId` が一致することを確認する。 |
| `module.sourcePath` | project root から見た生成元 `.ke` の正規化 path。表示、diagnostic、manifest 照合に使う。 |
| `module.entryLabel` | `.kel` entry または chapter がこの `.k` を開始する場合の既定 label。entry を持たない通常 module では `null` を許容する。 |
| `imports` | import 先がない場合は空配列。省略せず空配列を正規形とする。 |
| `labels` | この `.k` document 内で実行開始点または jump 先になり得る label から instruction index への解決済み mapping。 |

単一ファイル project の VM 実行開始位置は、manifest の entry が参照する `scriptId` と任意の `entryLabel` を `.k` の `module.scriptId`、`labels` に照合して決定する。manifest entry/chapter が明示的な `entryLabel` を指定する場合、`labels[entryLabel]` は有効な instruction index を指さなければならない。manifest entry/chapter が label を指定しない場合、既定開始位置は参照先 `scriptId` の `.k` document における instruction index `0` とする。`module.entryLabel` は entry を持たない通常 module のために `null` を許容するが、manifest が明示した label の解決失敗を隠す fallback として使ってはならない。

### 複数ファイル project と import

複数ファイル project では、各 `.ke` 入力ごとに 1 つの `.k` document を生成する。import された `.ke` は import 元 `.k` の命令列へ暗黙に連結されず、`imports` によって別 module として参照される。これにより、VM の instruction index は `.k` document ごとに局所的に安定し、save/debug/manifest 照合は `scriptId` と instruction index の組で実行位置を特定できる。

`imports` の各要素は次の情報を持つ。

| 項目 | 仕様 |
|------|------|
| `moduleId` | import 先 module の安定 ID。import 元 `.k` の `module.moduleId` と異なる値でなければならない。 |
| `scriptId` | import 先 module を含む `.k` artifact を manifest から解決するための ID。 |
| `sourcePath` | import 宣言が解決した `.ke` の正規化 path。 |
| `importPath` | `.ke` 内の import 宣言で使われた path または module specifier。正規化前の読者向け情報として保持してよい。 |
| `entryLabel` | import 先を VM が直接開始できる場合の label。単なる共有定義 module では `null` を許容する。 |

compiler は `.k` 生成前に import graph を解決し、循環 import、未解決 path、重複 module ID などの compile error を処理する。VM/runtime は import 解決をやり直さず、`.k` の `imports[].scriptId` が manifest に存在し、該当 `.k` の `module.moduleId` と `module.scriptId` が `imports` の参照と一致することだけを検証する。

import 先 module の label を参照する実行開始や cross-module jump が許可される場合、その参照は `scriptId` と `label` の組で表す。runtime は manifest から `scriptId` に対応する `.k` artifact を読み込み、その `.k` の `labels[label]` を instruction index へ解決する。未解決 label name を runtime が探索したり、source path 文字列だけで module を推測したりしてはならない。

### manifest と VM が共有する識別子

`manifest.json` は build output に含まれる `.k` artifact の列挙、entry/chapter、asset、locale、runtime metadata を所有する。一方 `.k` は VM 実行に必要な module、imports、labels、instruction index を所有する。両者の共有点は ID/key/path の参照に限定し、同じ metadata を重複して完全複製しない。

| 識別子 | 所有元 | `.k` での扱い | manifest との関係 |
|--------|--------|---------------|-------------------|
| `moduleId` | `.k` | `.k` document 内の主 module と import 先 module の識別に使う。 | manifest が module 単位の表示や診断情報を持つ場合は同じ値を参照してよい。 |
| `scriptId` | manifest | `.k` の `module.scriptId` と `imports[].scriptId` として参照する。 | manifest の scripts 一覧が `.k` artifact path と entry/chapter の参照先として所有する。 |
| `sourcePath` | compiler / `.k` | 生成元 `.ke` または import 解決後 `.ke` の正規化 path。 | manifest の script path と照合できるが、artifact 配置 path の所有元は manifest とする。 |
| `entryLabel` | `.kel` / manifest | `.k` の `module.entryLabel` または `imports[].entryLabel` として開始 label を示す。`null` の場合は label なしの module を表す。 | manifest の entry/chapter は `scriptId` と任意の `entryLabel` の組で VM 開始位置を参照する。label が省略された場合は instruction index `0` から開始する。 |
| `imports` | `.k` | import graph の解決済み参照を保持する。 | manifest は `imports[].scriptId` の `.k` artifact を列挙していなければならない。 |
| `labels` | `.k` | label name から instruction index への mapping を保持する。 | manifest は label の命令位置を複製せず、entry/chapter から label name を参照する。 |

manifest entry から VM 実行単位への解決順序は次の通りである。

1. runtime は manifest の entry/chapter から `scriptId` と任意の `entryLabel` を読む。
2. runtime は manifest の scripts 一覧から `scriptId` に対応する `.k` artifact を読み込む。
3. VM/runtime は `.k` の `module.scriptId` が manifest の `scriptId` と一致することを検証する。
4. manifest entry/chapter が明示的な `entryLabel` を持つ場合、VM/runtime はその `entryLabel` が `.k` の `labels` に存在し、有効な instruction index を指すことを検証する。明示された `entryLabel` が `labels` に存在しない場合は manifest integration error として実行開始前に失敗する。
5. manifest entry/chapter が `entryLabel` を指定しない場合、VM/runtime は instruction index `0` を開始位置として使う。
6. VM は `scriptId` と instruction index の組を実行開始位置として扱う。

この契約により、単一 `.ke` project では 1 つの `scriptId` と 1 つの `.k` artifact だけで実行単位を説明できる。複数ファイル project では、manifest の scripts 一覧が複数の `.k` artifact を列挙し、各 `.k` の `imports` が import 先 `scriptId` を参照することで、import 済み module と VM 実行単位の関係を説明できる。

## instruction schema と主要 opcode

_Requirements: 2.1, 2.2, 2.3, 2.4_

`.k` document の `instructions` は、VM が実行する instruction sequence である。`instructions` は配列で表し、配列順が基本の実行順序である。各 instruction は `index` を持ち、`index` は `instructions` 内で 0 から始まる連続した整数でなければならない。VM は通常、現在の instruction を実行した後、明示的に制御を移す opcode でない限り `index + 1` へ進む。

instruction index は `.k` document 内で局所的に安定した位置識別子である。save/debug/manifest entry は `scriptId` と instruction index の組で実行位置を参照する。compiler は `.k` 出力時点で `index` を確定し、VM/runtime は load 時に重複、欠番、配列位置との不一致を instruction schema violation として扱う。

### instruction 共通 schema

_Requirements: 2.1_

各 instruction は次の共通 field を持つ。

| Field | 必須 | 仕様 |
|-------|------|------|
| `index` | 必須 | 0 から始まる連続整数。`instructions[index]` の配列位置と一致しなければならない。 |
| `op` | 必須 | opcode を表す安定文字列。VM は未知 opcode を load error として拒否する。 |
| `args` | 必須 | opcode ごとの operand。名前付き object を正規形とする。operand がない opcode では空 object `{}` を使う。 |
| `result` | 必須 | instruction が値を生成または書き込む先。戻り値を持たない opcode では `null` を使う。 |
| `source` | 必須 | source mapping 参照。`null` または `source mapping と debug metadata` 節で定義する source 参照 object を許容する。 |
| `flags` | 任意 | VM が opcode semantics を変えずに参照できる補助 metadata。未知 flag は load 時 validation の対象にしてよい。 |

`args` と `result` は opcode contract の一部である。`args` には入力 operand、literal、参照、解決済み target index を置く。`result` には一時値、変数、または runtime call の戻り値利用先を置く。戻り値を破棄する場合も `result: null` として明示する。

```json
{
  "index": 0,
  "op": "say",
  "args": {
    "speaker": { "kind": "actorRef", "id": "hero" },
    "text": { "kind": "string", "value": "こんにちは" }
  },
  "result": null,
  "source": { "mappingId": "main.ke:1:1" }
}
```

VM の dispatch は `op` で行う。compiler は `.ke` の高水準構文を、VM が直接解釈できる opcode と operand に正規化する。VM は `.ke` の構文解析、名前解決、label 文字列探索、syscall signature 推測を実行時に行ってはならない。

### 基本実行順序

_Requirements: 2.1, 2.3_

VM は次の規則で program counter を更新する。

| opcode 種別 | 次の実行位置 |
|-------------|--------------|
| 通常 opcode | `index + 1`。次の index が存在しない場合は `.k` document の実行完了。 |
| `jump` | `args.targetIndex`。 |
| `select` | runtime/user input の選択結果に対応する `args.cases[].targetIndex`。 |
| `case` | case body の開始 marker として扱い、通常は `index + 1`。 |
| `return` 相当 | この task では opcode を固定しない。将来導入する場合は call/continuation state と戻り先 index を別途定義する。 |

`label` は debug と entry 解決のための marker opcode として出力してよいが、VM の jump 解決は `labels` mapping または `jump` / `select` operand 内の instruction index で完了していなければならない。runtime は未解決 label name を実行時に探索しない。

### text opcode: `say` と `nar`

_Requirements: 2.2_

`say` は話者付き台詞を表す。`nar` は話者を持たない narration を表す。どちらも VM が text progression を runtime へ渡すための instruction であり、表示、音声、入力待ちの具体実装は runtime 仕様が所有する。

| opcode | `args` | `result` | 実行契約 |
|--------|--------|----------|----------|
| `say` | `speaker`、`text`、任意の `voice` / `tags` / `style` | `null` | `speaker` は compile-time 解決済み actor reference または `null`。`text` は string literal または locale key reference。VM は runtime に話者付き text event を発行し、通常は完了後 `index + 1` へ進む。 |
| `nar` | `text`、任意の `voice` / `tags` / `style` | `null` | narration text event を runtime へ発行する。話者欄を表示するかどうかは runtime に委譲するが、`.k` 上では speaker を持たない opcode として扱う。 |

`say` / `nar` の本文が locale key に置換される場合、`.k` は key 参照を operand に保持し、実際の locale dictionary 本体は manifest または隣接成果物が所有する。文字列を直接持つ場合も、VM は文字列連結や式展開が必要な状態を残さず、compiler が必要な `eval` instruction と text operand に分解しておく。

### command、式、変数、代入 opcode

_Requirements: 2.2_

通常 command は `.ke` の command 構文を VM が実行できる runtime action に正規化した instruction である。command 名の解決、引数数、型検査は compile-time に完了している前提とする。VM は `command` opcode の `args.commandId` と typed args を runtime または VM 内 command dispatcher へ渡す。

| opcode | `args` | `result` | 実行契約 |
|--------|--------|----------|----------|
| `command` | `commandId`、`typedArgs`、任意の `target` | `null` または戻り値 target | 通常命令を表す。戻り値を使う command の場合は `result` に temporary または variable target を置く。戻り値を破棄する場合は `null`。 |
| `eval` | `exprId`、`kind`、`operands` | temporary target | 式評価を表す。`kind` は演算子、literal load、変数読み取り、配列構築などの評価種別を表す。型検査済み operand を読み、`result` に値を置く。 |
| `var.def` | `variable`、`scope`、`type`、`initializer` | variable target | 変数定義を表す。`initializer` は literal、temporary、または `null`。初期値が式の場合は先行する `eval` の `result` を参照する。 |
| `assign` | `target`、`value`、任意の `operator` | variable target または `null` | 代入を表す。`target` は compile-time 解決済み variable reference。`value` は literal、temporary、variable reference。複合代入の場合は `operator` に正規化済み演算子を持つ。 |

temporary target は instruction sequence 内の後続 instruction が参照できる一時値である。temporary の lifetime、型表現、保存対象かどうかは `value、variable、scope、execution state reference` 節の operand reference と save/load 境界に従う。値を生成する opcode は `result` に生成先を明示し、値を消費する opcode は `args` からその生成先を参照することを必須契約とする。

式が副作用を持つ runtime call を含む場合、compiler は副作用境界が分かるように `__systemcall__` または `runtime.call` instruction と `eval` instruction を分離する。VM は `eval` を純粋な値計算として扱い、runtime interaction は runtime call opcode に集約する。

### control-flow opcode: `label`、`jump`、`select`、`case`

_Requirements: 2.3_

control-flow opcode は、`.ke` の label、jump、select/case を VM が instruction index で実行できるように正規化する。compiler は `.k` 出力時点ですべての jump target を同一 `.k` document 内の instruction index、または cross-module 参照が許可される場合は `scriptId` と instruction index の組へ解決する。

| opcode | `args` | `result` | 実行契約 |
|--------|--------|----------|----------|
| `label` | `name`、任意の `public` | `null` | label marker。`labels[name]` はこの instruction の `index` または label 直後の実行可能 instruction index を指す。どちらを採用するかは `.k` emitter が一貫させ、`labels` の値を正とする。 |
| `jump` | `targetIndex`、任意の `targetScriptId`、任意の `label` | `null` | 無条件 jump。`targetIndex` は解決済み instruction index。`label` は debug metadata として残してよいが、VM は `targetIndex` を実行先として使う。 |
| `select` | `prompt`、`cases`、任意の `defaultCase` | `null` または selection temporary | 選択肢開始。`cases` は case ID、表示 text または locale key、条件、`targetIndex` を持つ配列。runtime/user input の結果で対応 target へ進む。 |
| `case` | `caseId`、任意の `selectIndex`、任意の `endIndex` | `null` | case body の marker。`select` の `cases[].targetIndex` は対応する `case` または case body 先頭 instruction index を指す。case body 終了後の合流は明示的な `jump` または通常順序で表す。 |

`select` の各 case は、選択肢 text と制御先を分離して持つ。case 表示条件がある場合、条件式は先行する `eval` result または `cases[].condition` の typed operand として表し、VM は条件が false の case を runtime へ提示しない。`select` の解決先はすべて load 時に検証し、存在しない instruction index、範囲外 index、case ID 重複は instruction schema violation とする。

```json
[
  {
    "index": 10,
    "op": "select",
    "args": {
      "prompt": { "kind": "string", "value": "どちらへ行く？" },
      "cases": [
        { "caseId": "town", "text": { "kind": "string", "value": "街" }, "targetIndex": 11 },
        { "caseId": "forest", "text": { "kind": "string", "value": "森" }, "targetIndex": 20 }
      ]
    },
    "result": { "kind": "temp", "id": "$choice0" },
    "source": { "mappingId": "main.ke:12:1" }
  },
  {
    "index": 11,
    "op": "case",
    "args": { "caseId": "town", "selectIndex": 10, "endIndex": 30 },
    "result": null,
    "source": { "mappingId": "main.ke:13:3" }
  }
]
```

### runtime call opcode: `__systemcall__`

_Requirements: 2.4_

STL や runtime 機能への呼び出しは、`.ke` 上の `__systemcall__` または同等の runtime call を `.k` 上の runtime call instruction として表す。正規 opcode 名は `__systemcall__` とし、将来別名を導入する場合も同じ field 契約を満たす runtime-call equivalent として扱う。

| Field | 仕様 |
|-------|------|
| `op` | `__systemcall__`。 |
| `args.syscallId` | compile-time に解決済みの syscall ID。人間向け名ではなく、STL/runtime contract 上の安定 ID を使う。 |
| `args.typedArgs` | typed arg の配列。各要素は `name`、`type`、`value` または `ref` を持つ。引数順が意味を持つ syscall では配列順を正とする。 |
| `args.effects` | 任意。表示、音声、asset load、state read/write など、VM が load validation または debug に使える効果分類。 |
| `result` | 戻り値を使う場合は temporary または variable target。戻り値を破棄する場合は `null`。複数戻り値が必要な syscall は `result` に tuple/array temporary を置くか、将来の opcode 拡張で定義する。 |

`__systemcall__` は runtime interaction の境界である。compiler は syscall ID、引数数、引数型、戻り値型を `.k` 生成前に検証する。VM/runtime は load 時に `syscallId` が対応 runtime で利用可能か、`typedArgs` が syscall signature と一致するかを検証し、未対応 syscall または型不一致を load error または compatibility error として扱う。

```json
{
  "index": 42,
  "op": "__systemcall__",
  "args": {
    "syscallId": "runtime.audio.playBgm",
    "typedArgs": [
      {
        "name": "asset",
        "type": "assetRef",
        "value": { "kind": "assetRef", "id": "bgm.opening" }
      },
      {
        "name": "fadeSeconds",
        "type": "number",
        "value": { "kind": "number", "value": 1.5 }
      }
    ],
    "effects": ["audio"]
  },
  "result": null,
  "source": { "mappingId": "main.ke:20:1" }
}
```

戻り値を式で使う場合、`__systemcall__` の `result` に temporary target を置き、後続の `eval`、`assign`、`command` がその temporary を `args` から参照する。これにより、VM 実装者は runtime call の戻り値利用を instruction と operand の依存関係として読み取れる。

## value、variable、scope、execution state reference

_Requirements: 3.1, 3.2, 3.3, 3.4_

この節は、instruction の `args`、`result`、`typedArgs`、`initializer`、`value`、`ref` に現れる値と参照の共通契約を定義する。`.k` は VM が実行時に読む契約であり、runtime の save data そのものではない。save/load は `.k` に含まれる安定識別子を参照して実行状態を復元する。

### value model

_Requirements: 3.1, 3.4_

値は tagged object を正規形とし、`kind` で値種別を識別する。VM/runtime は未知の `kind` を load error として扱う。literal value は `.k` に直接含め、reference value は manifest、module、または runtime contract が所有する実体を安定 ID/key で参照する。

| `kind` | 必須 field | 仕様 |
|--------|------------|------|
| `number` | `value` | JSON number として表す。整数/小数の言語上の型差が必要な場合は `type` で補足する。NaN、Infinity、文字列化された数値は正規形ではない。 |
| `bool` | `value` | `true` または `false`。 |
| `string` | `value` | UTF-8 文字列 literal。locale 置換対象ではない固定文字列として扱う。 |
| `null` | なし | 値が存在しないことを表す。`value` field は持たない。 |
| `array` | `items` | 値の配列。各要素は本表の value object、temporary reference、variable reference のいずれかである。 |
| `actorRef` | `id` | compile-time に解決済みの actor reference。表示名や立ち絵 metadata は `.k` ではなく manifest/runtime 側が所有する。 |
| `tagRef` | `id` | compile-time に解決済みの tag reference。未解決 tag name を runtime が探索してはならない。 |
| `assetRef` | `id` | manifest が所有する asset ID への参照。asset path、hash、platform variant は `.k` に複製しない。 |
| `localeKey` | `key` | locale dictionary が所有する key への参照。fallback text が必要な場合は debug metadata として扱い、VM 実行意味に使わない。 |
| `runtimeDynamic` | `source`、`type` | runtime call、user input、platform state など、実行時に値が確定することを表す境界値。`source` は `syscall`、`selection`、`runtimeState` などの分類であり、VM は compile-time literal として畳み込まない。 |

```json
{ "kind": "array", "items": [
  { "kind": "number", "value": 1 },
  { "kind": "bool", "value": true },
  { "kind": "assetRef", "id": "bgm.opening" }
] }
```

compiler は `.k` 生成前に、名前解決、型検査、actor/tag/asset/locale key の存在検証を可能な範囲で完了する。`.k` に残る actor/tag/asset/locale は人間向け名前ではなく、manifest または runtime contract と照合できる安定 ID/key でなければならない。

runtime dynamic value は、runtime の現在状態、ユーザー入力、外部 platform 情報、`__systemcall__` の戻り値のように build 時点で値を確定できない情報を表す。runtime dynamic value であっても、`type`、発生元、保存対象かどうかを VM が判断できる metadata は `.k` の operand または opcode contract に含める。

### operand reference と target

_Requirements: 3.1, 3.2_

instruction が値を読む場合、operand は literal value、temporary reference、variable reference のいずれかで表す。instruction が値を書き込む場合、`result` は temporary target または variable target を表す。戻り値を破棄する場合は `result: null` とする。

| 種別 | 形 | 仕様 |
|------|----|------|
| literal value | `{ "kind": "...", ... }` | `.k` に直接埋め込まれた値。 |
| temporary reference | `{ "kind": "tempRef", "id": "t1" }` | 同一 `.k` document の先行 instruction が生成した一時値を読む。 |
| temporary target | `{ "kind": "temp", "id": "t1", "type": "number" }` | 後続 instruction から参照できる一時値の生成先。 |
| variable reference | `{ "kind": "varRef", "id": "v.score", "scope": "script", "type": "number" }` | compile-time 解決済み variable を読む。 |
| variable target | `{ "kind": "var", "id": "v.score", "scope": "script", "type": "number" }` | `var.def`、`assign`、runtime call result が variable を書き込む先。 |

temporary は式分解と runtime call 戻り値の受け渡しに使う VM 内部値であり、save data の安定対象ではない。save/load 境界を越えて値を保持する必要がある場合、compiler は variable state として保存できる variable target に書き込む命令列を生成する。

### variable と scope

_Requirements: 3.2, 3.4_

変数は compile-time に宣言と参照が解決され、`.k` では stable variable identifier と scope を持つ。VM は未解決の変数名を lexical scope から探索せず、`var.def`、`eval`、`assign`、`command`、`__systemcall__` の operand に含まれる variable reference をそのまま使う。

| 項目 | 仕様 |
|------|------|
| declaration | `var.def` の `args.variable` が stable variable identifier、source name、type、scope、任意の storage class を持つ。 |
| read | `eval` または opcode operand の `varRef` が variable identifier、scope、type を持つ。読み取り前に初期化が必要な変数は VM load または実行時 validation の対象にする。 |
| write | `assign` の `args.target` または戻り値を持つ opcode の `result` が variable target を持つ。型不一致や read-only variable への書き込みは compile-time error または load error とする。 |
| scope | `global`、`script`、`chapter`、`block`、`temporary` などの安定 scope kind と、必要に応じて `scopeId` を持つ。scope kind の追加は feature compatibility の対象にする。 |
| initial values | `var.def.args.initializer` は literal value、temporary reference、variable reference、または `null`。初期値が式の場合は先行する `eval` の temporary を参照する。 |

```json
{
  "index": 4,
  "op": "var.def",
  "args": {
    "variable": {
      "kind": "var",
      "id": "v.score",
      "sourceName": "score",
      "scope": "script",
      "type": "number"
    },
    "initializer": { "kind": "number", "value": 0 }
  },
  "result": { "kind": "var", "id": "v.score", "scope": "script", "type": "number" },
  "source": { "mappingId": "main.ke:3:1" }
}
```

`sourceName` は debug 表示用 metadata であり、VM の変数解決には使わない。save/load と debug 表示で安定して参照する識別子は `id` と `scope` の組である。同名変数が異なる scope に存在する場合、compiler は異なる `id` または `scopeId` を割り当てて衝突を解消する。

### execution state reference と save/load 境界

_Requirements: 3.3, 3.4_

`.k` は save data ではない。runtime の save data は、`.k` document 本体や命令列を複製せず、次の安定識別子を参照することで実行状態を復元できる形式にする。

| 参照 | 仕様 |
|------|------|
| `scriptId` | manifest の script entry と `.k` の `module.scriptId` に一致する安定 ID。 |
| `instructionIndex` | `.k` document 内の現在または再開対象 instruction index。`scriptId` と組で実行位置を一意に表す。 |
| `callState` | script/module 呼び出しを導入する opcode がある場合の call frame 識別情報。少なくとも呼び出し元 `scriptId`、戻り先 `instructionIndex`、引数/戻り値 target を参照できること。 |
| `continuationState` | `select`、runtime input wait、非同期 runtime call など、VM が一時停止して再開する位置の識別情報。待機中 opcode の `scriptId`、`instructionIndex`、pending result target を参照する。 |
| `variableState` | 保存対象 scope に属する variable identifier と値の集合。temporary は原則として保存対象に含めない。 |
| `branchReturnPosition` | branch、case、call、runtime wait から戻る位置が必要な場合の `scriptId` と instruction index。label name ではなく解決済み index を使う。 |

VM/runtime は load 時に、save data が参照する `scriptId` が manifest と `.k` に存在し、`instructionIndex` が対象 `.k` の有効範囲内にあることを検証する。参照先が存在しない場合は save/load integration error として扱い、runtime が近い label name や source path から推測して復元してはならない。

branch return position と call/continuation state は、将来 opcode が追加されても `.k` 上の安定位置参照として `scriptId` と instruction index を使う。source mapping は debug fallback に使えるが、save/load の正規復元キーではない。

## source mapping と debug metadata

_Requirements: 4.1, 4.2, 4.3, 4.4_

source mapping は、`.k` の instruction または instruction group を生成元 `.ke` の file、line、column へ戻すための補助情報である。runtime error、debug 表示、golden test、仕様レビューの可読性に使うが、VM の opcode dispatch、operand 評価、program counter 更新、save/load 復元キー、分岐先解決の実行意味を変えてはならない。

### source mapping schema

`.k` document は top-level `debug` field を持ってよい。`debug.sourceMappings` は mapping ID から source range への辞書であり、各 instruction の `source` はその mapping を参照する。

| Field | 必須 | 仕様 |
|-------|------|------|
| `debug.moduleDisplayName` | 任意 | debug 表示で使う module 名。省略時は `module.moduleId` を使う。 |
| `debug.fileDisplayName` | 任意 | debug 表示で使う file 名。省略時は `module.sourcePath` の末尾要素を使う。 |
| `debug.sourceMappings` | 任意 | mapping ID から source range object への辞書。存在する場合、instruction の `source.mappingId` はこの辞書内の key を参照する。 |

source range object は次の field を持つ。

| Field | 必須 | 仕様 |
|-------|------|------|
| `file` | 必須 | project root から見た生成元 `.ke` の正規化 path。主 module では原則として `module.sourcePath` と一致する。 |
| `line` | 必須 | 1 始まりの行番号。行を特定できない synthetic instruction では `null` を許容する。 |
| `column` | 必須 | 1 始まりの列番号。列を特定できない場合は `null` を許容する。 |
| `endLine` | 任意 | source range の終端行。省略時は単一点または compiler が range を保持していないことを表す。 |
| `endColumn` | 任意 | source range の終端列。省略時は単一点または compiler が range を保持していないことを表す。 |
| `kind` | 任意 | `statement`、`expression`、`textBody`、`selectCase`、`lessExpansion`、`synthetic` など、debug tooling が表示を補助する分類。 |
| `displayText` | 任意 | runtime error/debug 表示で短く示す source 断片。VM 実行意味には使わない。 |

各 instruction の `source` は `null` または次の source 参照 object とする。

| Field | 必須 | 仕様 |
|-------|------|------|
| `mappingId` | 必須 | `debug.sourceMappings` 内の primary source mapping ID。`debug.sourceMappings` を省略する小規模 document では、互換性のために `file`、`line`、`column` を直接持つ inline object を許容する。 |
| `related` | 任意 | 関連 source mapping ID の配列。1 つの source construct が複数 instruction へ展開された場合や、instruction が複数 source range の結果である場合に使う。 |
| `generatedBy` | 任意 | `less`、`textSplit`、`selectCaseLowering`、`expressionLowering` など、compiler が行った展開分類。debug metadata であり VM は実行判断に使わない。 |

`source: null` は、compiler が source position を持たない synthetic instruction、互換性維持のため source mapping を省略した instruction、または生成元 `.ke` 位置が存在しない metadata-only instruction に限って許容する。source mapping が欠落しても VM は同じ opcode と operand を同じ順序で実行する。

```json
{
  "debug": {
    "moduleDisplayName": "main",
    "fileDisplayName": "main.ke",
    "sourceMappings": {
      "main.ke:12:1": {
        "file": "events/main.ke",
        "line": 12,
        "column": 1,
        "kind": "statement"
      },
      "main.ke:12:5-12:18": {
        "file": "events/main.ke",
        "line": 12,
        "column": 5,
        "endLine": 12,
        "endColumn": 18,
        "kind": "textBody"
      }
    }
  }
}
```

### primary source と related source

primary source は、runtime error や debug step 表示で最初に示す生成元位置である。related source は、同じ instruction の理解に必要な追加位置、または同じ source construct から派生した別 range を表す。debug tooling は primary source を主表示にし、related source は展開詳細、hover、trace、golden test 差分などの補助表示に使う。

1 つの source construct が複数 instruction へ展開される場合、compiler は次の方針で source を割り当てる。

| source construct | primary source | related source |
|------------------|----------------|----------------|
| LESS 展開 | 展開元 LESS 構文の開始位置。生成された各 instruction は同じ primary source を共有してよい。 | 展開後 instruction が対応する式、引数、tag、text などの詳細 range。展開元全体と詳細 range の両方が必要な場合は詳細 range を related に含める。 |
| `say` / `nar` 本文 | 表示 text event を発生させる `say` / `nar` instruction は本文 range を primary source とする。speaker や style の解析で生成された補助 instruction は、その token range を primary source にしてよい。 | speaker token、locale key、文字列補間式、style/tag range。本文を式分解した `eval` instruction は本文全体または該当補間式を related に含める。 |
| `select` / `case` | `select` instruction は `select` 構文または prompt の開始位置を primary source とする。各 `case` instruction は対応する case label または選択肢 text の開始位置を primary source とする。 | `select` 全体、各 case の条件式、選択肢 text、case body 先頭 range。`select` から case target へ進む debug 表示では `select` と選ばれた `case` の両方を related に含めてよい。 |
| 式分解 | 生成された `eval` instruction は、該当する最小式 range を primary source とする。 | 親 statement、operand token、元の複合式 range。 |
| compiler generated marker | 生成元が明確な marker は元 statement を primary source とする。生成元がない marker は `source: null` とする。 | 生成理由を示す元 construct、隣接する label/case/select range。 |

primary source と related source は VM の実行順序を決める情報ではない。複数 instruction が同じ primary source を共有しても、VM は `instructions[].index` と opcode contract だけで実行順序を決める。

### runtime error と debug 表示

runtime error、debug step、breakpoint、trace、golden test の位置表示は、実行位置を `scriptId` と `instructionIndex` で特定し、表示用に source mapping を重ねる。

| 表示要素 | 優先順 |
|----------|--------|
| module 名 | `debug.moduleDisplayName`、`module.moduleId`、`module.scriptId`。 |
| file 名 | primary source の `file`、`debug.fileDisplayName`、`module.sourcePath`、`module.scriptId`。 |
| line / column | primary source の `line` / `column`。欠落時は表示しない。 |
| instruction position | `scriptId:instructionIndex` を常に表示可能な fallback とする。 |
| source 断片 | primary source の `displayText`、related source の `displayText`。欠落時は表示しない。 |

source mapping が有効な場合の推奨表示は `file:line:column (module moduleDisplayName, scriptId:instructionIndex)` である。line または column が欠落する場合は、存在する要素だけを使い、少なくとも `scriptId:instructionIndex` を表示する。file も解決できない場合の fallback 表示は `scriptId:<scriptId> instruction:<instructionIndex>` とする。

breakpoint や trace filter が source position を入力として受ける場合でも、VM 実行時の正規位置は `scriptId` と instruction index である。debug tooling は source position から候補 instruction を逆引きしてよいが、VM/runtime は source path や line/column から実行位置を推測して save/load を復元してはならない。

debug metadata の不備は、`.k` の opcode、operand、result、control-flow target が valid である限り load error にしてはならない。`source.mappingId` が `debug.sourceMappings` に存在しない、line/column が `null`、related source が欠落しているなどの問題は warning または fallback 表示で扱う。ただし schema validator が strict debug validation mode を持つ場合は、golden test や compiler 品質検証として別途失敗扱いにしてよい。

### compile-time と runtime の境界

_Requirements: 3.4_

次の情報は compile-time に解決済みであることを `.k` の前提とする。

- variable、label、actor、tag、command、syscall、type の名前解決。
- jump/select/case の制御先 instruction index。
- actor/tag/asset/locale key の参照 ID/key。
- `var.def`、`assign`、`eval`、`__systemcall__` 引数と戻り値の型。

次の情報は runtime dynamic value として残してよい。

- user input、select の選択結果、runtime UI/音声/入力待ちの結果。
- `__systemcall__` または runtime call の戻り値。
- platform state、保存済み variable state、runtime が所有する asset/locale の実体。

この境界により、`.k` は build 時に検証できる名前、型、タグ、参照を VM 実行用 ID へ正規化し、実行時にしか確定しない値だけを runtime dynamic value として残す。VM/runtime は `.k` load 後に compiler の名前解決を再実行しない。

## manifest 参照契約と最小正規化例

_Requirements: 1.4, 5.1, 5.2, 5.3, 5.4_

`manifest.json` は runtime package の目録であり、entry、scripts、assets、locale、runtime metadata、build metadata を所有する。`.k` は VM が実行する document であり、instruction、labels、module/import、source mapping、operand に含まれる参照 ID/key を所有する。`.k` は manifest の詳細 schema を複製せず、`manifestRefs` と operand reference によって、manifest が所有する実体を安定 ID/key/path で指す。

| manifest が所有する情報 | `.k` が所有する情報 | 参照契約 |
|--------------------------|--------------------|----------|
| `entry` / `chapters` | `module.scriptId`、`labels`、instruction index | manifest は開始対象の `scriptId` と任意の `entryLabel` を持つ。VM/runtime は `.k` の `module.scriptId` と `labels[entryLabel]` を照合し、開始位置を `scriptId` と instruction index に正規化する。 |
| `scripts` | `module.sourcePath`、`module.scriptId`、`imports[].scriptId` | manifest は script ID、生成元 `.ke` の script path、`.k` artifact path、hash などの配布 metadata を所有する。`.k` は `module.scriptId` と `module.sourcePath` を持ち、artifact path や hash を所有しない。 |
| `assets` | `assetRef` operand、`manifestRefs.assets[]` | manifest は asset ID、実ファイル path、hash、platform variant、preload 方針を所有する。`.k` は asset ID だけを参照し、asset path や variant 選択を複製しない。 |
| `locale` | `localeKey` operand、`manifestRefs.localeKeys[]` | manifest または locale dictionary は locale key と翻訳文字列、fallback、利用可能 locale を所有する。`.k` は locale key を参照し、表示文字列本体を必須契約として複製しない。 |
| `runtime` metadata | `__systemcall__`、`runtimeDynamic`、runtime call operand | manifest は対象 runtime、platform、runtime package version、capability などを所有する。`.k` は syscall ID、typed args、runtime dynamic value の境界を持ち、runtime package metadata を所有しない。 |
| `build` metadata | `format`、`version`、`features`、`debug` | manifest は build ID、compiler version、target、artifact hash、生成時刻などを所有する。`.k` は VM load に必要な format/version/features と debug metadata を持つが、build provenance 全体は所有しない。 |

`manifestRefs` は human review、strict validation、将来の golden test で manifest との照合対象を読みやすくするための top-level field である。正規形では、`.k` が参照する script、asset、locale key の集合を重複なしの配列として持つ。VM 実行意味は各 instruction の operand と `module` / `imports` / `labels` から決まるため、`manifestRefs` は参照一覧であり、asset path、locale text、runtime/build metadata の実体を含めてはならない。

| Field | 必須 | 仕様 |
|-------|------|------|
| `manifestRefs.entry` | 任意 | この `.k` が manifest entry/chapter から直接開始される場合の `scriptId` と任意の `entryLabel`。entry を持たない library-like module では `null` を許容する。 |
| `manifestRefs.scripts` | 必須 | `.k` が直接照合する script 参照の配列。少なくとも自身の `module.scriptId` を含め、import がある場合は `imports[].scriptId` も含める。 |
| `manifestRefs.assets` | 必須 | operand に現れる asset ID の重複なし配列。asset を参照しない場合は空配列を正規形とする。 |
| `manifestRefs.localeKeys` | 必須 | operand に現れる locale key の重複なし配列。locale key を参照しない場合は空配列を正規形とする。 |
| `manifestRefs.runtime` | 任意 | runtime capability や syscall namespace の照合に必要な最小参照。runtime package の完全 metadata は manifest が所有する。 |

`.k` 内の script path、asset ID、locale key は次のように manifest と対応する。

- `.k` の `module.sourcePath` と `imports[].sourcePath` は、manifest の scripts entry が持つ生成元 script path と照合する。`.k` artifact の出力 path は manifest の scripts entry が所有するため、`.k` は artifact path を正規契約として持たない。
- `.k` の `module.scriptId`、`imports[].scriptId`、cross-module target の `targetScriptId` は、manifest の scripts entry ID と一致しなければならない。存在しない `scriptId` は manifest integration error として実行開始前に失敗する。
- `.k` の `{ "kind": "assetRef", "id": "..." }` と `manifestRefs.assets[]` は、manifest の assets entry ID と一致しなければならない。runtime は asset ID から path、variant、load policy を manifest 側で解決する。
- `.k` の `{ "kind": "localeKey", "key": "..." }` と `manifestRefs.localeKeys[]` は、manifest または locale dictionary が所有する locale key と一致しなければならない。runtime は locale key から表示文字列を解決し、`.k` 内の debug text を fallback 実行意味として使ってはならない。

次は human review と将来の golden test に使える最小の正規化 `.k` 例である。field order は `format`、`version`、`features`、`module`、`imports`、`instructions`、`labels`、`manifestRefs`、`debug` を正規形とし、空の `imports`、`manifestRefs.assets` は省略しない。

```json
{
  "format": "koromo.k",
  "version": { "major": 1, "minor": 0, "patch": 0 },
  "features": [],
  "module": {
    "moduleId": "module.main",
    "scriptId": "script.main",
    "sourcePath": "events/main.ke",
    "entryLabel": "start"
  },
  "imports": [],
  "instructions": [
    {
      "index": 0,
      "op": "label",
      "args": { "name": "start", "public": true },
      "result": null,
      "source": { "mappingId": "events/main.ke:1:1" }
    },
    {
      "index": 1,
      "op": "say",
      "args": {
        "speaker": { "kind": "actorRef", "id": "actor.hero" },
        "text": { "kind": "localeKey", "key": "main.opening.hello" },
        "voice": { "kind": "assetRef", "id": "voice.hero.hello" }
      },
      "result": null,
      "source": { "mappingId": "events/main.ke:2:1" }
    }
  ],
  "labels": {
    "start": 0
  },
  "manifestRefs": {
    "entry": {
      "scriptId": "script.main",
      "entryLabel": "start"
    },
    "scripts": [
      {
        "scriptId": "script.main",
        "sourcePath": "events/main.ke"
      }
    ],
    "assets": [
      "voice.hero.hello"
    ],
    "localeKeys": [
      "main.opening.hello"
    ],
    "runtime": {
      "requiredCapabilities": []
    }
  },
  "debug": {
    "moduleDisplayName": "main",
    "fileDisplayName": "main.ke",
    "sourceMappings": {
      "events/main.ke:1:1": {
        "file": "events/main.ke",
        "line": 1,
        "column": 1,
        "kind": "statement",
        "displayText": "label start"
      },
      "events/main.ke:2:1": {
        "file": "events/main.ke",
        "line": 2,
        "column": 1,
        "kind": "textBody",
        "displayText": "hero: main.opening.hello"
      }
    }
  }
}
```

この例では、manifest の scripts entry が `script.main`、`events/main.ke`、`.k` artifact path、hash を所有する。`.k` は `script.main` と `events/main.ke` を照合用に持つだけで、artifact path と hash を複製しない。同様に、`voice.hero.hello` のファイル path や platform variant、`main.opening.hello` の翻訳文字列、runtime/build metadata の詳細は manifest または隣接成果物が所有する。

## 対象読者

- `.ke` から `.k` を生成する CLI / compiler 関連機能の設計者と実装者。
- `.k` を読み込んで命令列を実行する VM 実装者。
- `manifest.json` と `.k` を組み合わせて実行資産を解決する runtime 実装者。
- runtime error、debug 表示、golden test、仕様レビューを担当する開発者。

## 適用範囲

本仕様は、`.k` 中間表現の公開契約を所有する。

- `.k` ファイルの目的、基本構造、version、feature compatibility。
- `.ke` 入力、`.kel` entry、`manifest.json`、runtime が読む成果物との関係。
- VM が参照する命令表現、値表現、制御フロー、実行位置、source mapping の契約。
- asset ID、locale key、script path など、manifest が所有する情報への参照関係。
- 既存仕様と用語または拡張子が不整合な場合に、現行の正とする用語を示す導線。

## 非対象範囲

_Requirements: 6.3_

この文書だけを読んでも、次の作業が本仕様の対象外であることが分かるようにする。

- compiler 実装、`.k` emitter 実装、serializer 実装、schema validator 実装。
- VM 実装、VM interpreter 実装、命令ディスパッチや save/load の実装詳細。
- runtime 実装、描画、音声、入力、UI、プラットフォーム固有の配布処理。
- asset manifest 全体、locale dictionary、runtime package manifest の完全な schema。
- 配布時の圧縮、暗号化、署名、改ざん検出、binary format。
- 既存文書に残る `.kc` / `.klib` 表記の一括置換。

## 現行用語と旧称

_Requirements: 6.2_

現行の authoritative term は次の通りである。

| 種別 | 現行用語 | 扱い |
|------|----------|------|
| イベントスクリプト入力 | `.ke` | KoromoEventScript 言語で記述された現在の正規入力拡張子。 |
| VM 実行用中間表現 | `.k` | `.ke` から生成される現在の正規中間表現拡張子。 |
| 旧イベントスクリプト表記 | `.kc` | 旧称または移行前の表記。新規仕様では `.ke` を正とする。 |
| 旧中間表現表記 | `.klib` | 旧称または移行前の表記。新規仕様では `.k` を正とする。 |

既存仕様に `.kc` / `.klib` が残っている場合でも、本仕様では `.ke` / `.k` を正とする。既存文書との用語差分は、必要に応じて別 Issue または後続タスクで移行する。

## 隣接仕様

_Requirements: 6.1_

本仕様は、次の仕様を参照する。詳細責務は各仕様に委譲し、`.k` 仕様は VM 実行用中間表現の契約に集中する。

| 仕様 | 本仕様から見た関係 |
|------|--------------------|
| `docs/spec/cli-tool-spec.md` | `kes build`、`kes run`、`kes publish` が `.ke` / `.kel` を扱い、`.k` と `manifest.json` を生成または runtime に渡す成果物契約を定義する。 |
| `docs/spec/kes-language-spec.md` | `.ke` の構文、名前、型、変数、制御構文、source position の前提を定義する。 |
| `docs/spec/kes-language-stl-spec.md` | `__systemcall__`、STL、runtime call、asset ID、actor、tag など、`.k` に反映される語彙の前提を定義する。 |
| `docs/spec/kel-file-spec.md` | `.kel` の entry、chapter、script path 参照の前提を定義する。 |
| `docs/spec/windows-runtime-spec.md` | Windows runtime が `manifest.json` と VM 成果物を読み込む隣接仕様である。 |
| `docs/spec/unity-runtime-spec.md` | Unity runtime が published data と VM 成果物を読み込む隣接仕様である。 |
| `docs/spec/unreal-runtime-spec.md` | Unreal runtime が published data と VM 成果物を読み込む隣接仕様である。 |
| `docs/spec/overview.md` | 読者が KoromoEventScript 全体像と各詳細仕様へ到達するための導線を持つ。 |

## 不整合の扱い

_Requirements: 6.4_

`.ke` / `.k` と `.kc` / `.klib` のように、既存仕様と本仕様の間で用語または拡張子が異なる場合は、本仕様では `.ke` / `.k` を正とする。

ただし、既存仕様の責務範囲をこの文書で直接変更しない。CLI、runtime、overview などの文書更新が必要な場合は、それぞれの Boundary を持つ後続タスクまたは別 Issue で扱う。
