# .k 中間表現仕様

この文書は、KoromoEventScript の `.ke` から生成される VM 実行用中間表現 `.k` の公開契約を定義する仕様である。

## 目的

`.k` は CLI が `.ke` を解析・検証した後に生成し、VM と runtime がイベント実行時に参照する中間表現である。
本仕様は、CLI、VM、runtime、debug tooling の実装者とレビュー担当者が、`.k` の責務境界と隣接仕様との関係を同じ前提で確認できるようにする。

この文書は段階的に拡張する。現時点では、`.k` 中間表現仕様が扱う範囲、扱わない範囲、参照すべき隣接仕様、現行用語と旧称の関係に加えて、基本 file format、compatibility policy、document/module/import、instruction schema、主要 opcode 群を定義する。value model、source mapping、manifest 参照契約の詳細は後続タスクでこの文書へ追記する。

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
| `source` | 必須 | source mapping 参照。詳細 field は source mapping task が定義するが、instruction schema 上は `null` または source 参照 object を許容する。 |
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

temporary target は instruction sequence 内の後続 instruction が参照できる一時値である。temporary の lifetime、型表現、保存対象かどうかの詳細は value/variable/execution state task で定義する。ただし本 task の範囲では、値を生成する opcode は `result` に生成先を明示し、値を消費する opcode は `args` からその生成先を参照することを必須契約とする。

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

この文書だけを読んでも、次の作業が本仕様の対象外であることが分かるようにする。

- compiler 実装、`.k` emitter 実装、serializer 実装、schema validator 実装。
- VM 実装、VM interpreter 実装、命令ディスパッチや save/load の実装詳細。
- runtime 実装、描画、音声、入力、UI、プラットフォーム固有の配布処理。
- asset manifest 全体、locale dictionary、runtime package manifest の完全な schema。
- 配布時の圧縮、暗号化、署名、改ざん検出、binary format。
- 既存文書に残る `.kc` / `.klib` 表記の一括置換。

## 現行用語と旧称

現行の authoritative term は次の通りである。

| 種別 | 現行用語 | 扱い |
|------|----------|------|
| イベントスクリプト入力 | `.ke` | KoromoEventScript 言語で記述された現在の正規入力拡張子。 |
| VM 実行用中間表現 | `.k` | `.ke` から生成される現在の正規中間表現拡張子。 |
| 旧イベントスクリプト表記 | `.kc` | 旧称または移行前の表記。新規仕様では `.ke` を正とする。 |
| 旧中間表現表記 | `.klib` | 旧称または移行前の表記。新規仕様では `.k` を正とする。 |

既存仕様に `.kc` / `.klib` が残っている場合でも、本仕様では `.ke` / `.k` を正とする。既存文書との用語差分は、必要に応じて別 Issue または後続タスクで移行する。

## 隣接仕様

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

`.ke` / `.k` と `.kc` / `.klib` のように、既存仕様と本仕様の間で用語または拡張子が異なる場合は、本仕様では `.ke` / `.k` を正とする。

ただし、既存仕様の責務範囲をこの文書で直接変更しない。CLI、runtime、overview などの文書更新が必要な場合は、それぞれの Boundary を持つ後続タスクまたは別 Issue で扱う。
