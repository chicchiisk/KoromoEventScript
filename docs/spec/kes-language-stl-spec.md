# Koromo Event Script 標準ライブラリ仕様書

本仕様書は、KoromoEventScript (KES) の標準ライブラリ (STL) を定義する。
STL は `.kc` ファイルから暗黙に利用できる組み込み関数・命令群であり、ノベルゲーム MVP を成立させるために必要最小限の機能を提供する。

言語構文そのものは `kes-language-spec.md` が定義する。
本仕様書では、通常命令として呼び出せる標準関数、組み込み構文と連携する補助命令、runtime へ伝える実行時効果を定義する。
runtime 側の責務である機能は、STL 実装から内部命令 `__systemcall__` を通じて呼び出す。

## 基本方針

- STL はプロジェクト側の `import` なしで利用できる。
- STL の命令は、通常命令として1行呼び出しまたは LESS で呼び出せる。
- STL の実体は、`__systemcall__` の薄いラッパ、またはほかの STL 関数を組み合わせた関数として実装する。
- `__systemcall__` は STL 実装と VM/runtime の境界であり、通常のシナリオ `.kc` から直接呼び出してはならない。
- `say`、`nar`、`select`、`case`、`label`、`jump`、`using` は言語構文として扱い、STL では runtime 連携上の意味だけを補足する。
- 描画、音声、入力、保存先、素材読み込みの詳細は runtime 側の責務とする。
- KES 側は、runtime 実装に依存しない安定した命令インターフェースを定義する。
- 素材 ID は manifest により解決される。runtime はフォルダ規約から暗黙に素材探索してはならない。
- 背景、actor、BGM、SE など、命令が明示的に要求した素材が存在しない場合は原則として実行時エラーとする。
- `say` / `nar` の自動ボイスまたは `vo` が要求した Voice 素材が存在しない場合は警告を出し、実行は継続する。

## 型と表記

本仕様書では、命令シグネチャを次の表記で示す。

| 表記 | 意味 |
|---|---|
| `number` | 数値 |
| `bool` | 真偽値 |
| `string` | 文字列 |
| `Actor` | `actor` 構文で定義され、`cast` 済みのアクター |
| `T[]` | `T` 型の配列 |
| `void` | 表示値を返さない |

省略可能な引数には既定値を併記する。

```kes
trans effect="crossfade" duration=0.3
```

この例では、`effect` と `duration` は省略可能である。

タグ値を通常命令へ渡す場合は、`#se_sample_0002` 形式ではなく `"se_sample_0002"` のような文字列 ID を渡す。
`#id` 形式のタグは `say`、`nar`、`label`、`jump`、`case` の構文上でのみ使う。

## `__systemcall__` 内部命令

### 目的

`__systemcall__` は、STL から VM/runtime 側の機能を呼び出すための内部命令である。
通常のシナリオ作者は `bg`、`show`、`vo`、`save` などの STL 関数を使い、`__systemcall__` を直接書かない。

STL は次のどちらかとして実装する。

- `__systemcall__` を1回呼ぶ薄いラッパ
- 複数の STL 関数、または STL 関数と `__systemcall__` を組み合わせた関数

### 呼び出し形式

`__systemcall__` は通常関数ではなく、STL 実装内でのみ使える特別な内部命令である。
第1引数は syscall ID を表す `string` リテラルでなければならない。
第2引数以降の型と戻り値型は、syscall ID ごとの固定シグネチャで決まる。

```kes
__systemcall__ "scene.bg" id
__systemcall__ "scene.trans" effect duration
var count = __systemcall__ "core.array_len" values
```

syscall ID が文字列リテラルでない場合はコンパイルエラーとする。
未定義の syscall ID、または syscall ID に対応しない引数型・引数数・戻り値利用はコンパイルエラーとする。

### 公開範囲

- `__systemcall__` は予約された内部名であり、ユーザー定義関数、変数、class、enum、actor 名として使えない。
- プロジェクトの通常 `.kc` ファイルから `__systemcall__` を呼び出した場合はコンパイルエラーとする。
- STL 実装ファイル、またはコンパイラが内蔵する STL 定義内でのみ使用できる。
- LSP や補完では通常候補に出さない。STL 実装編集時のみ内部候補として扱ってよい。

### syscall シグネチャ

syscall ID はモジュール名を接頭辞に持つ。
戻り値型が `void` の syscall は命令としてのみ使い、式中で値として利用できない。
戻り値型が `void` 以外の syscall は式中で利用できる。

| syscall ID | 引数 | 戻り値 | 概要 |
|---|---|---|---|
| `core.print` | `text:string` | `void` | デバッグログへ文字列を出力する |
| `core.array_len` | `values:T[]` | `number` | 配列の要素数を返す |
| `core.str_len` | `text:string` | `number` | 文字列の長さを返す |
| `core.bool_to_string` | `value:bool` | `string` | 真偽値を文字列化する |
| `core.number_to_string` | `value:number` | `string` | 数値を文字列化する |
| `scene.rt_back` | なし | `void` | 描画先を裏画面へ切り替える |
| `scene.rt_front` | なし | `void` | 表画面への反映対象を確定する |
| `scene.bg` | `id:string` | `void` | 背景素材を設定する |
| `scene.camera_autofocus` | `enabled:bool` | `void` | オートフォーカスを切り替える |
| `actor.show` | `actor:Actor pos:number face:string layer:number z:number bustup:bool` | `void` | actor を表示する |
| `actor.face` | `actor:Actor exp:string` | `void` | actor の表情を切り替える |
| `actor.move` | `actor:Actor pos:number duration:number` | `void` | actor を移動する |
| `scene.trans` | `effect:string duration:number` | `void` | 画面遷移を実行する |
| `actor.action_jump` | `actor:Actor` | `void` | actor にジャンプ演出を実行する |
| `actor.cast` | `actor:Actor` | `void` | actor をロードする |
| `actor.hide` | `actor:Actor` | `void` | actor を非表示にする |
| `text.p` | なし | `void` | 本文表示中に改ページを挿入する |
| `text.l` | なし | `void` | 本文表示中に行内クリック待ちを挿入する |
| `text.wait_click` | なし | `void` | クリック待ちを行う |
| `text.vo` | `id:string` | `void` | 指定 Voice を再生する |
| `text.r` | なし | `void` | 本文表示中に改行を挿入する |
| `text.cm` | なし | `void` | メッセージウィンドウを非表示にする |
| `audio.vo_auto` | なし | `void` | 現在の `say` / `nar` 文脈から Voice を自動再生する |
| `audio.bgm` | `id:string loop:bool fade:number` | `void` | BGM を再生する |
| `audio.bgm_stop` | `fade:number` | `void` | BGM を停止する |
| `audio.se` | `id:string` | `void` | SE を再生する |
| `audio.se_stop_all` | なし | `void` | 再生中の SE をすべて停止する |
| `audio.se_stop` | `id:string` | `void` | 指定 SE を停止する |
| `audio.voice_stop` | なし | `void` | 再生中 Voice を停止する |
| `state.load` | `slot:number` | `void` | 指定スロットから復元する |
| `state.autosave` | なし | `void` | オートセーブする |
| `state.mark_read` | `tag:string` | `void` | タグを既読にする |
| `state.is_read` | `tag:string` | `bool` | タグの既読状態を返す |
| `state.save` | `slot:number title:string` | `void` | 指定スロットへ保存する |
| `localize.get` | `tag:string` | `string` | ローカライズ辞書から指定タグに対応する文字列を取得する |
| `system.wait` | `seconds:number` | `void` | 指定秒数だけ待機する |
| `system.set_auto` | `enabled:bool` | `void` | オート進行を切り替える |
| `system.set_skip` | `mode:string` | `void` | スキップモードを設定する |
| `system.set_config_string` | `key:string value:string` | `void` | 文字列設定を更新する |
| `system.set_config_number` | `key:string value:number` | `void` | 数値設定を更新する |
| `system.set_config_bool` | `key:string value:bool` | `void` | 真偽値設定を更新する |
| `system.get_config` | `key:string` | `string` | 設定値を文字列として取得する |
| `system.set_param_string` | `key:string value:string` | `void` | ゲーム変数に文字列を格納する |
| `system.set_param_number` | `key:string value:number` | `void` | ゲーム変数に数値を格納する |
| `system.set_param_bool` | `key:string value:bool` | `void` | ゲーム変数に真偽値を格納する |
| `system.get_param` | `key:string` | `string` | ゲーム変数を文字列として取得する |

### STL 実装例

次の例は概念上の STL 実装である。
実際の STL はコンパイラに内蔵しても、標準モジュールとして配布してもよい。

```kes
fn bg(id: string):
    __systemcall__ "scene.bg" id

fn trans(effect: string, duration: number):
    __systemcall__ "scene.trans" effect duration

fn array_len(values: string[]): number:
    return __systemcall__ "core.array_len" values

fn localize.get(tag: string): string:
    return __systemcall__ "localize.get" tag

fn vf(actor: Actor, exp: string):
    vo
    face actor exp
```

上記の `array_len` は例示のため `string[]` を使っている。
実際の `array_len` は `T[]` を受け取る組み込み STL として扱い、ユーザー定義関数でジェネリック関数を表現する必要はない。

### エラー/警告条件

- `__systemcall__` が通常 `.kc` から直接呼び出された場合はコンパイルエラーとする。
- `localize.get` は `__systemcall__ "localize.get" tag` の薄いラッパとして定義する。
- syscall ID が文字列リテラルでない場合はコンパイルエラーとする。
- syscall ID が未定義の場合はコンパイルエラーとする。
- syscall シグネチャと異なる引数数・引数型・戻り値利用はコンパイルエラーとする。
- syscall 実行中に素材欠落、保存失敗、runtime 状態不整合などが発生した場合は、各 STL 関数のエラー/警告条件に従う。

## モジュール一覧

以降の各モジュールで定義する公開命令は、シナリオ作者向けの STL API である。
runtime 側の状態を変更する命令は、STL 実装内で対応する `__systemcall__` を呼び出す。
複数の公開命令を組み合わせて実現できる命令は、直接 syscall を増やさず STL 関数として実装する。

| モジュール | 目的 |
|---|---|
| `core` | 基本的なデバッグ出力、配列操作、型別文字列化、範囲生成、検証 |
| `scene` | 背景、描画先、画面遷移、カメラ補助 |
| `actor` | actor のロード、表示、非表示、表情、位置、簡易アクション |
| `text` | `say` / `nar` と連携するボイス、表情、改ページ、改行、クリック待ち、メッセージウィンドウ制御 |
| `audio` | BGM、SE、Voice の明示制御 |
| `flow` | ラベル、ジャンプ、選択肢構文の runtime 連携 |
| `state` | セーブ、ロード、オートセーブ、既読情報 |
| `system` | 待機、オート、スキップ、ユーザー設定 |

## `core` モジュール

### 目的

`core` は、シナリオ実行とデバッグに必要な基本関数を提供する。
ゲーム演出には直接関与せず、言語処理系または VM 上で完結する処理を中心に扱う。

### 公開命令一覧

| 命令 | シグネチャ | 戻り値 | 概要 |
|---|---|---|---|
| `print` | `print text` | `void` | デバッグログへ文字列を出力する |
| `array_len` | `array_len values` | `number` | 配列の要素数を返す |
| `str_len` | `str_len text` | `number` | 文字列の長さを返す |
| `range` | `range start end` | `number[]` | `start` 以上 `end` 未満の連番配列を返す |
| `number_to_string` | `number_to_string value` | `string` | `number` を表示用文字列へ変換する |
| `bool_to_string` | `bool_to_string value` | `string` | `bool` を表示用文字列へ変換する |
| `assert` | `assert condition message=""` | `void` | 条件が偽の場合、実行時エラーにする |

### 引数・戻り値

- `print text`: `text` は `string` とする。
- `array_len values`: `values` は配列型 `T[]` とする。`T` は配列の要素型である。
- `str_len text`: `text` は `string` とする。
- `range start end`: `start` と `end` は `number` とする。
- `number_to_string value`: `value` は `number` とする。
- `bool_to_string value`: `value` は `bool` とする。
- `assert condition message=""`: `condition` は `bool`、`message` は `string` とする。

### 実行時効果

- `print` は通常ログへ出力する。配布 runtime ではデバッグモード以外で画面表示してはならない。
- `array_len`、`str_len`、`range`、`number_to_string`、`bool_to_string` は副作用を持たない。
- `assert` はデバッグ、テスト、開発中の検証に使う。失敗時は現在の実行位置を含む診断を生成する。

### エラー/警告条件

- `print` に `string` 以外を渡した場合はコンパイルエラーとする。
- `array_len` に配列以外を渡した場合はコンパイルエラーとする。
- `str_len` に `string` 以外を渡した場合はコンパイルエラーとする。
- `range` の `end` が `start` より小さい場合は空配列を返す。
- `assert` の条件が偽の場合は実行時エラーとする。

### 最小サンプル

```kes
var names = ["Riku", "Noa", "Amane"]
print (number_to_string (array_len names))

for i in range 0 (array_len names):
    print names[i]

assert (array_len names > 0) "cast list is empty"
```

## `scene` モジュール

### 目的

`scene` は背景、描画先、画面遷移、カメラ補助を扱う。
具体的な描画方式、トランジション実装、バッファ管理は runtime 側が担当する。

### 公開命令一覧

| 命令 | シグネチャ | 戻り値 | 概要 |
|---|---|---|---|
| `rt_back` | `rt_back` | `void` | 描画先を裏画面へ切り替える |
| `rt_front` | `rt_front` | `void` | 表画面への反映対象を確定する |
| `bg` | `bg id` | `void` | 背景素材を設定する |
| `trans` | `trans effect="crossfade" duration=0.3` | `void` | 画面遷移を実行する |
| `camera_autofocus` | `camera_autofocus enabled` | `void` | オートフォーカスを有効または無効にする |

### 引数・戻り値

- `bg id`: `id` は `string` とし、manifest 上の背景素材 ID を指す。
- `trans effect="crossfade" duration=0.3`: `effect` は `string`、`duration` は秒数の `number` とする。
- `camera_autofocus enabled`: `enabled` は `bool` とする。

### 実行時効果

- `rt_back` 以降の描画命令は裏画面へ適用する。
- `rt_front` は裏画面の内容を次の表画面候補として確定する。
- `bg` は現在の描画先の背景レイヤーを指定素材へ差し替える。
- `trans` は表画面と裏画面、または現在状態と次状態の間で画面遷移を行う。
- `camera_autofocus true` は、表示中 actor のバウンディングボックスに基づく自動カメラ調整を有効にする。

### エラー/警告条件

- `bg` の素材 ID が manifest に存在しない場合は実行時エラーとする。
- `trans` の `effect` が runtime 未対応の場合は実行時エラーとする。
- `duration` が負数の場合はコンパイルエラー、静的に判定できない場合は実行時エラーとする。
- `rt_back` と `rt_front` の厳密な内部状態は runtime 実装に委ねるが、未確定の裏画面がない状態で `trans` しても runtime は破綻してはならない。

### 最小サンプル

```kes
rt_back
bg "bg_living"
show Noa 0 face="normal"
rt_front
trans "crossfade" duration=0.4

camera_autofocus true
```

## `actor` モジュール

### 目的

`actor` は、`actor` 構文で定義されたアクターのロード、表示、非表示、表情差分、位置、簡易アクションを扱う。
1つの actor 定義名に対する実行時インスタンスは常に1つとする。

### 公開命令一覧

| 命令 | シグネチャ | 戻り値 | 概要 |
|---|---|---|---|
| `cast` | `cast actor` | `void` | actor をロードし、実行時に参照可能にする |
| `show` | `show actor pos=0 face="normal" layer=0 z=0 bustup=false` | `void` | actor を指定位置へ表示する |
| `hide` | `hide actor` | `void` | actor を非表示にする |
| `face` | `face actor exp` | `void` | actor の表情差分を切り替える |
| `move` | `move actor pos duration=0.3` | `void` | actor を指定位置へ移動する |
| `action_jump` | `action_jump actor` | `void` | actor に短いジャンプ演出を実行する |

### 引数・戻り値

- `actor` は `Actor` 型の値を受け取る。
- `pos` は `number` とし、ノベルゲーム向けの横位置を表す。`-1`、`0`、`1` を基本位置として扱う。
- `face` / `exp` は `string` とし、actor 素材内の表情差分 ID を指す。
- `layer` と `z` は `number` とし、描画順の補助情報として扱う。
- `bustup` は `bool` とし、立ち絵のバストアップ表示を要求する。
- `duration` は秒数の `number` とする。

### 実行時効果

- `cast` は actor の定義と素材参照を runtime へ登録する。
- `show` は actor を現在の描画先に表示し、位置、表情、レイヤー、奥行き、表示種別を更新する。
- `hide` は actor を非表示にする。ロード済み状態は維持する。
- `face` は表示中またはロード済み actor の表情状態を更新する。
- `move` は actor の表示位置を時間付きで変更する。
- `action_jump` は actor の現在位置を基準に短いジャンプ動作を再生する。

### エラー/警告条件

- 未定義 actor または `cast` されていない actor 参照はコンパイルエラーまたは実行時エラーとする。
- `show` または `face` が要求する actor 素材・表情差分が manifest に存在しない場合は実行時エラーとする。
- `hide` 対象が非表示の場合は何もしない。
- `duration` が負数の場合はコンパイルエラー、静的に判定できない場合は実行時エラーとする。

### 最小サンプル

```kes
cast:
    Riku
    Noa

show Noa 0 face="normal"
face Noa "smile"
action_jump Noa
hide Noa
```

## `text` モジュール

### 目的

`text` は、`say` / `nar` 構文と連携する補助命令を提供する。
台詞本文、ナレーション本文、クリック待ちの構文規則は言語仕様が定義し、本モジュールは本文中で使える標準命令を定義する。

### 公開命令一覧

| 命令 | シグネチャ | 戻り値 | 概要 |
|---|---|---|---|
| `vo` | `vo id=null` | `void` | Voice を明示再生する |
| `vf` | `vf actor=null exp` | `void` | Voice 再生と表情変更を連動させる |
| `p` | `p` | `void` | 現在の本文表示内で改ページを挿入する |
| `r` | `r` | `void` | 現在の本文表示内で改行を挿入する |
| `l` | `l` | `void` | 現在の本文表示内で行内クリック待ちを挿入する |
| `cm` | `cm` | `void` | メッセージウィンドウを非表示にする |
| `wait_click` | `wait_click` | `void` | プレイヤー入力によるクリック待ちを行う |

### 引数・戻り値

- `vo id=null`: `id` は `string` または `null` とする。
- `vf actor=null exp`: `actor` は `Actor` または `null`、`exp` は `string` とする。
- `p`、`r`、`l`、`cm`、`wait_click` は引数を持たない。

`vf` は名前付き引数でも位置引数でも呼び出せる。
`actor` を省略した場合、現在の `say` 話者を使う。
`nar` 内または `say` 外で actor を省略した場合は実行時エラーとする。
位置引数で1つの `string` だけを渡した場合は `exp` と解釈する。
位置引数で `Actor` と `string` を渡した場合は、それぞれ `actor` と `exp` と解釈する。

### 実行時効果

- `vo null` は、現在の `say` / `nar` タグと連番から Voice ID を組み立て、`audio.vo_auto` syscall により再生する。
- `vo id` は指定した Voice ID を `text.vo` syscall により再生する。
- `vf` は `vo` を実行したうえで、対象 actor の `face` を `exp` へ変更する。
- `p` は本文表示中に改ページを挿入する。runtime は現在ページの表示を完了したあとクリック待ちを行い、入力後に同じ `say` / `nar` 文脈の次ページを表示する。
- `r` は本文表示中に改行を挿入する。同じページ内で表示位置を次行へ移し、クリック待ちは発生させない。
- `l` は本文表示中に行内クリック待ちを挿入する。runtime は現在位置までの本文表示を完了したあと入力を待ち、入力後に同じページ内の続きを表示する。
- `cm` はメッセージウィンドウを非表示にする。次に本文を表示する `say` / `nar` が進行した場合、runtime は必要に応じてメッセージウィンドウを再表示する。
- `wait_click` は本文表示外でも明示的なクリック待ちを発生させる。

### エラー/警告条件

- Voice 素材が manifest に存在しない場合は警告を出し、実行は継続する。
- `vo null` をタグなしの `say` / `nar` 外で呼び出した場合は実行時エラーとする。
- `vf` の対象 actor が決定できない場合は実行時エラーとする。
- `vf` が要求する表情差分が存在しない場合は実行時エラーとする。

### 最小サンプル

```kes
say Noa #sy_sample_0003:
    詳しい話を確認できるよう、
    @vf "eye_close"
    一度集まってもらってもいいかもしれないね。{p}
    @vo "voice_extra_001"
    どうかな？{l}{r}
    返事を聞かせて。
    @cm

wait_click
```

## `audio` モジュール

### 目的

`audio` は BGM、SE、Voice の明示制御を提供する。
Voice の自動再生 syscall も本モジュールに属する。
`vo` 公開命令は `text` モジュールに置くが、`vo null` の runtime 呼び出しは `audio.vo_auto` を使う。

### 公開命令一覧

| 命令 | シグネチャ | 戻り値 | 概要 |
|---|---|---|---|
| `bgm` | `bgm id loop=true fade=0.0` | `void` | BGM を再生する |
| `bgm_stop` | `bgm_stop fade=0.0` | `void` | BGM を停止する |
| `se` | `se id` | `void` | SE を再生する |
| `se_stop` | `se_stop id=null` | `void` | SE を停止する |
| `voice_stop` | `voice_stop` | `void` | 再生中 Voice を停止する |

### 引数・戻り値

- `id` は `string` とし、manifest 上の音声素材 ID を指す。
- `loop` は `bool` とする。
- `fade` は秒数の `number` とする。
- `se_stop id=null` の `id` は `string` または `null` とする。

### 実行時効果

- BGM は原則1系統で再生する。新しい `bgm` は現在の BGM を置き換える。
- SE は複数同時再生できる。
- Voice は原則1系統で再生する。
- `se_stop null` は再生中の SE をすべて停止する。
- `voice_stop` は現在再生中の Voice のみを停止し、BGM と SE は停止しない。

### エラー/警告条件

- `bgm` または `se` の素材 ID が manifest に存在しない場合は実行時エラーとする。
- `fade` が負数の場合はコンパイルエラー、静的に判定できない場合は実行時エラーとする。
- `bgm_stop`、`se_stop`、`voice_stop` は対象チャンネルが未再生でも成功する。

### 最小サンプル

```kes
bgm "daily_theme" loop=true fade=0.5
se "door_open"

say Riku:
    誰か来たみたいだ

bgm_stop fade=1.0
```

## `flow` モジュール

### 目的

`flow` は、言語構文として定義される `label`、`jump`、`select`、`case` の runtime 連携上の意味を整理する。
これらは通常命令ではなく、構文として解析・型検査・タグ解決される。

### 公開構文一覧

| 構文 | 形式 | 概要 |
|---|---|---|
| `label` | `label #tag` | VM のジャンプ先を定義する |
| `jump` | `jump #tag` | 指定タグへ実行位置を移す |
| `select` | `select:` / `select #tag:` | 選択肢 UI を表示する |
| `case` | `case "text" #tag` | 選択肢の表示文とジャンプ先を定義する |

### 引数・戻り値

- `#tag` は言語仕様のタグであり、通常命令引数ではない。
- `case` の `"text"` は選択肢に表示する `string` とする。
- いずれの構文も表示値を返さない。

### 実行時効果

- `label` は VM の実行位置として登録され、直接の表示効果を持たない。
- `jump` は指定タグに対応する命令位置へ VM の実行位置を移す。
- `select` は `case` を一覧表示し、プレイヤーの選択を待つ。
- 選択された `case` のタグへ VM の実行位置を移す。

### エラー/警告条件

- 未定義タグへの `jump` または `case` はコンパイルエラーとする。
- 重複タグはコンパイルエラーとする。
- `select` ブロックが `case` を1つも持たない場合はコンパイルエラーとする。
- runtime は選択肢表示中にセーブ、ロード、バックログ、設定 UI を開ける設計としてよいが、VM の進行は選択確定まで停止する。

### 最小サンプル

```kes
select #se_sample_0001:
    case "かぐやに意見を聞く" #choice_kaguya
    case "乃愛に意見を聞く" #choice_noa

label #choice_kaguya
say Riku:
    かぐやはどう思う？
jump #end_choice

label #choice_noa
say Riku:
    乃愛はどう思う？

label #end_choice
```

## `state` モジュール

### 目的

`state` は、通常セーブ、ロード、オートセーブ、既読情報を扱う。
保存先の具体パス、ファイル形式、サムネイル生成、ユーザーデータ領域の選択は runtime 側の責務とする。

### 公開命令一覧

| 命令 | シグネチャ | 戻り値 | 概要 |
|---|---|---|---|
| `save` | `save slot title=""` | `void` | 指定スロットへ現在状態を保存する |
| `load` | `load slot` | `void` | 指定スロットから状態を復元する |
| `autosave` | `autosave` | `void` | オートセーブスロットへ現在状態を保存する |
| `mark_read` | `mark_read tag` | `void` | 指定タグを既読として記録する |
| `is_read` | `is_read tag` | `bool` | 指定タグが既読なら `true` を返す |

### 引数・戻り値

- `slot` は `number` とする。通常セーブスロットは `0` 以上の整数として扱う。
- `title` は `string` とする。
- `tag` は `string` とし、`#` を含めないタグ ID を渡す。

### 実行時効果

- `save` と `autosave` は、VM 状態、制御状態、画面状態、必要な音声状態、既読情報、メタ情報を保存する。
- `load` は保存時点の状態へ復元し、以後の VM 実行位置も保存時点へ戻す。
- `mark_read` は指定タグを既読情報へ追加する。
- `is_read` はスキップ制御や分岐条件に利用できる。

### エラー/警告条件

- `slot` が負数の場合はコンパイルエラー、静的に判定できない場合は実行時エラーとする。
- `load` 対象スロットが存在しない場合は実行時エラーとする。
- セーブデータの読み込みに失敗した場合は実行時エラーとする。
- 配布物ディレクトリが書き込み不可でも、runtime はユーザーデータ領域へ保存できなければならない。

### 最小サンプル

```kes
save 1 title="合流前"

if is_read "choice_noa":
    print "Noa route already read"

mark_read "chapter01_start"
autosave
```

## `system` モジュール

### 目的

`system` は、待機、オート進行、スキップ、ユーザー設定を扱う。
キー割り当てや UI の具体表示は runtime 側で定義する。

### 公開命令一覧

| 命令 | シグネチャ | 戻り値 | 概要 |
|---|---|---|---|
| `wait` | `wait seconds` | `void` | 指定秒数だけ待機する |
| `set_auto` | `set_auto enabled` | `void` | オート進行を有効または無効にする |
| `set_skip` | `set_skip mode` | `void` | スキップモードを設定する |
| `set_config_string` | `set_config_string key value` | `void` | 文字列のユーザー設定値を更新する |
| `set_config_number` | `set_config_number key value` | `void` | 数値のユーザー設定値を更新する |
| `set_config_bool` | `set_config_bool key value` | `void` | 真偽値のユーザー設定値を更新する |
| `get_config` | `get_config key` | `string` | ユーザー設定値を文字列として取得する |
| `set_param_string` | `set_param_string key value` | `void` | ゲーム変数に文字列を格納する |
| `set_param_number` | `set_param_number key value` | `void` | ゲーム変数に数値を格納する |
| `set_param_bool` | `set_param_bool key value` | `void` | ゲーム変数に真偽値を格納する |
| `get_param` | `get_param key` | `string` | ゲーム変数を文字列として取得する |

### 引数・戻り値

- `seconds` は `number` とする。
- `enabled` は `bool` とする。
- `mode` は `string` とし、`"off"`、`"read"`、`"all"` を標準値とする。
- `key` は `string` とする。
- `set_config_string` の `value` は `string` とする。
- `set_config_number` の `value` は `number` とする。
- `set_config_bool` の `value` は `bool` とする。
- `set_param_string` の `value` は `string` とする。
- `set_param_number` の `value` は `number` とする。
- `set_param_bool` の `value` は `bool` とする。

標準設定キーは次の通りとする。

| キー | 値の型 | 概要 |
|---|---|---|
| `masterVolume` | `number` | マスター音量 |
| `bgmVolume` | `number` | BGM 音量 |
| `seVolume` | `number` | SE 音量 |
| `voiceVolume` | `number` | Voice 音量 |
| `textSpeed` | `number` | テキスト表示速度 |
| `autoSpeed` | `number` | オート進行速度 |
| `skipMode` | `string` | スキップ設定 |
| `fullscreen` | `bool` | フルスクリーン状態 |
| `locale` | `string` | 表示ロケール |

### 実行時効果

- `wait` は VM の進行を指定秒数だけ停止する。描画、音声、入力処理は継続する。
- `set_auto` は runtime のオート進行状態を切り替える。
- `set_skip` は runtime のスキップ状態を切り替える。
- `set_config_string`、`set_config_number`、`set_config_bool` は runtime のユーザー設定を更新し、必要に応じて保存対象に含める。
- `get_config` は設定値を取得する。戻り値は文字列であり、数値や真偽値として使う場合はプロジェクト側で変換する。
- `set_param_string`、`set_param_number`、`set_param_bool` は runtime のゲーム変数を書き換える。キー値がない場合は新規作成する。configはアプリケーションに対して1つの値を共有するものであるのに対し、ゲーム変数はセーブデータごとに値を保持する。主にゲームの進行制御に使用する
- `get_param` はゲーム変数を取得する。戻り値は文字列であり、数値や真偽値として使う場合はプロジェクト側で変換する。

### エラー/警告条件

- `wait` の `seconds` が負数の場合はコンパイルエラー、静的に判定できない場合は実行時エラーとする。
- `set_skip` の `mode` が標準値以外の場合は実行時エラーとする。
- 未知の `key` に対する設定更新または `get_config` は実行時エラーとする。
- 未定義のゲーム変数に対する `get_param` は実行時エラーとする。
- 設定キーの定義型と異なる setter を使った場合はコンパイルエラー、静的に判定できない場合は実行時エラーとする。
- 設定保存に失敗した場合は警告を出して実行継続してよい。ただし同一セッション内の設定値は反映されなければならない。

### 最小サンプル

```kes
set_config_number "textSpeed" 1.2
set_auto true
wait 1.0
set_auto false

set_skip "read"
print (get_config "locale")
```

## 最小ノベルゲーム例

次の例は、MVP の標準ライブラリだけで背景、actor、BGM、台詞、選択肢、既読、セーブを扱う最小シナリオである。

```kes
cast:
    Riku
    Noa

bgm "daily_theme"

rt_back
bg "bg_living"
show Noa 0 face="normal"
rt_front
trans "crossfade"

say Noa #chapter01_start:
    おはよう。
    @vf "smile"
    今日もいい天気だね。

mark_read "chapter01_start"
autosave

select:
    case "声をかける" #talk
    case "少し待つ" #wait

label #talk
say Riku:
    おはよう、乃愛。
jump #end

label #wait
wait 0.5
say Noa:
    どうしたの？

label #end
save 1 title="朝のリビング"
```

## MVP 対象外

初期 MVP では、次の機能を STL に含めない。

- Live2D 固有制御
- 高度なカメラパス、シェイク、被写界深度などの演出
- 複数 BGM バスやミキサールーティング
- キーコンフィグ
- ゲームパッド正式対応
- 実績、スクリーンショット、クラウド同期
- Unity / Unreal 固有 API

これらは将来拡張または runtime / engine 拡張側の仕様として扱う。
