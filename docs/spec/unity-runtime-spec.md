# KoromoEventScript Unity 組み込み拡張仕様書

KoromoEventScript Unity は、Unity プロジェクト内で KoromoEventScript のビルド成果物を読み込み、イベントスクリプトを実行するための組み込み拡張である。

本仕様書では、Unity 組み込み拡張の対象環境、入力データ、インポート、ランタイム構成、描画、音声、入力、UI、診断、配布契約を定義する。

## 基本方針

- Unity プロジェクトへ取り込んで使う組み込み拡張として提供する。
- Unity 拡張は `.kc` / `.kel` の生ソースを直接解釈しない。
- Unity 拡張は CLI の `kes build --target unity` が生成した `manifest.kson` と `.klib` を入力として扱う。
- VM が実行する中間表現は `.klib` を正とし、ファイル形式、instruction schema、命令体系は [`.klib` 中間表現仕様](k-intermediate-representation-spec.md)に従う。
- イベント遷移は `.kel` を起点に行う。
- Unity 固有の描画、音声、UI、入力は Unity の標準機能または広く利用される公式パッケージで実装できる設計とする。
- UI は uGUI を標準構成とする。
- v1 は「Unity プロジェクト内に配置した `manifest.kson` と `.klib` を Editor で KES Build Asset へ取り込み、シーン上の KesManager から再生できること」を最優先とする。
- Unity での開発では Editor と CLI を往復することを前提に、`kes build --target unity` の出力先を Unity プロジェクト内ディレクトリに設定する構成を推奨する。
- 生成済み成果物を手動で Unity プロジェクトへコピーまたは移動する運用も許容する。
- KesManager を含む実行用 GameObject 一式は、どのシーンにも配置しやすいようプレハブとして提供する。
- Windows 単体ランタイムと同じシナリオ資産を再利用できるよう、VM の実行意味論は共通に保つ。
- ローカライズ辞書 `.csv` は Unity では直接読まない。ローカライズ済みテキストは CLI build 済みの言語別 `.klib` を利用する。

## 用語

| 用語 | 意味 |
|---|---|
| Unity 拡張 | Unity 上で KoromoEventScript を実行するための組み込み機能一式 |
| KesManager | シーン上で KES Build Asset の読み込み、VM 実行、UI 制御を司る MonoBehaviour |
| Build Output Root | `kes build --target unity` の出力先ディレクトリ |
| KES Build Asset | `manifest.kson` の ScriptedImporter が生成し、manifest内容と参照`.klib`を保持するScriptableObject |
| VM | `.klib` を読み取り、イベントスクリプトを実行する仮想マシン |
| Addressables Resolver | manifestのasset ID、kind、localeからUnity Addressablesを非同期解決する仕組み |
| Entry Event | manifest の `events` と entry 情報に基づき最初に開始されるイベント |

## 対象環境

| 項目 | 内容 |
|---|---|
| エンジン | Unity 6000.5.3f1 以降 |
| スクリプト言語 | C# |
| API 互換性レベル | .NET Standard 2.1 |
| 共通ソース言語 | Unity 6000.5.3f1 が対応するC# 9.0の範囲 |
| レンダリング | Universal Render Pipeline（URP）のみ |
| UI | uGUI を必須、UI Toolkit は将来拡張 |
| 入力 | Input System パッケージを標準とする。ホストプロジェクトが入力を注入できる抽象化も提供する |
| 対応プラットフォーム | Windows, macOS, Linux, iOS, Android, WebGL を将来対象にできる設計とする |

本リポジトリ内の Unity 検証プロジェクトは Unity 6 系を前提とする。
v1 の正式検証対象は Unity Editor 上の Play Mode と Windows 向けビルドとする。
Unity 6 系のマイナーバージョン差に依存する API は避け、サポート下限の Unity 6000.5.3f1 でコンパイルおよび Play Mode 検証を行う。Built-in Render Pipeline、HDRP、カスタム SRP はサポート対象外とする。

## パッケージとアセンブリ境界

Unity 拡張は Unity Package Manager から導入できる package として `source/extension/unity/Package/` 配下に配置する。package 名は `com.koromosoft.koromo-event-script` とし、Runtime、Editor、Tests を分離する。package は GitHub 上の本リポジトリでホストし、利用者は Git URL の `path` query でこのサブフォルダーを指定して導入する。検証用 Unity プロジェクトは `source/extension/unity/SampleProject/` 配下に置き、`Packages/manifest.json` からプロジェクトルート基準の `file:../Package` で package を参照する。

```txt
source/extension/unity/Package
    package.json
    Runtime/
    Editor/
    Tests/
        Runtime/
        Editor/
    Samples~/
source/extension/unity/SampleProject/
    Assets/
    Packages/
    ProjectSettings/
```

- Runtime assembly は `UnityEditor` 名前空間へ依存してはならない。
- `.klib` importer、Inspector、インポート診断は Editor assembly に置く。
- Runtime、Editor、各 Test assembly は asmdef で分離する。
- uGUI、Input System、URP の package 依存は `package.json` に宣言する。
- Runtime assembly は URP を前提とし、Built-in Render Pipeline と HDRP の互換コードを持たない。
- 標準プレハブ、既定Input Actions、Addressables設定例は`Samples~`から導入できるようにする。

### 共通ランタイムソース

- `.klib` loader、VM、runtime manifest model、イベント評価、セーブ状態modelはWindowsランタイムとUnity拡張で同じC#ソースを共有する。
- 共通ソースはUnity 6000.5.3f1のC# compilerでコンパイルできるC# 9.0以下の構文と、Unityの.NET Standard 2.1 API profileで利用できるAPIだけを使う。
- 共通ソースではUnity API、WinUI、Windows固有API、ファイルシステム保存APIへ依存しない。
- Unityで利用できない新しいC#構文やBCL APIをWindows側だけの都合で共通ソースへ追加してはならない。
- collection expression等のC# 10以降の構文、module initializer、init-only setter等のUnity非対応機能を共通ソースで使わない。recordに必要な`System.Runtime.CompilerServices.IsExternalInit`はUnity assembly内の互換shimとして提供する。
- platform固有処理はinterface境界の背後へ置き、Unity用asmdefと.NET projectの双方から同じファイルをコンパイルする。

### GitHub配布契約

既定ブランチのpackageを導入するGit URLは次とする。

```txt
https://github.com/chicchiisk/KoromoEventScript.git?path=/source/extension/unity/Package
```

リリース版は`unity-v{package-version}`形式のGit tagを使用し、URL末尾のrevisionとして固定する。

```txt
https://github.com/chicchiisk/KoromoEventScript.git?path=/source/extension/unity/Package#unity-v0.1.0
```

- `Package/package.json`の`version`とtagのversionは一致しなければならない。
- 公開済みtagは付け替えず、修正版はSemantic Versioningに従って新しいversionとtagを発行する。
- 利用者向けドキュメントではtagまたはcommit hashへの固定を推奨し、既定ブランチ参照は評価用途として扱う。
- Git依存関係は利用側Unityプロジェクトの`Packages/manifest.json`に記録する。package自身の`package.json`にはGit URL依存を記述しない。
- Git URLの`?path=`より後に、必要に応じて`#tag`、`#branch`または`#commit-hash`を指定する。
- 利用環境ではGitクライアントを`PATH`から実行できなければならない。Git LFS管理対象をpackageに含める場合はGit LFSも必要になるため、v1のpackage本体はGit LFSへ依存しない。
- `SampleProject`、`Library`、`UserSettings`、IDE生成物は配布packageの構成要素に含めない。
- リポジトリ内開発では`file:../Package`を使用し、GitHub経由の導入試験では一時的な別Unityプロジェクトから公開URLまたはcommit hashを指定する。

## 拡張構成

Unity 拡張は次の責務を持つモジュールから構成する。

| モジュール | 役割 |
|---|---|
| Build Asset Importer | `manifest.kson` と `.klib` を Editor で検証し、KES Build Asset を生成する |
| Manifest Loader | KES Build Asset から manifest と `.klib` のシリアライズ済み参照を読み込む |
| VM Runner | `.klib` を読み込み、命令列を順次実行する |
| Event Resolver | manifest の `events` に基づくイベント選択と次イベント遷移を解決する |
| Addressables Resolver | 背景、actor、音声などをasset ID、kind、localeから非同期loadする |
| Presentation | 背景、actor、テキスト、選択肢、トランジションを描画する |
| Audio | BGM、SE、Voice の再生と停止を扱う |
| UI | メッセージ、選択肢、スキップ、オート、履歴などを提供する |
| Save System | セーブデータ、既読情報、設定の永続化を扱う |
| Diagnostics | 実行時エラー、警告、デバッグ表示、Editor 上の検証結果を扱う |

## 入力データ

Unity 拡張は、少なくとも次の入力を扱う。

| 入力 | 用途 |
|---|---|
| `manifest.kson` | [ランタイムマニフェスト仕様](runtime-manifest-spec.md)に従うUnity向けmanifest |
| `.klib` | VM 実行用バイナリ |
| `events/loc/<locale>/` 配下の `.klib` | 言語別バリアント |
| Unity アセット | 背景、actor、BGM、SE、Voice、UI 素材 |
| KesManager 設定 | 起動時 locale、開始イベント上書き、UI 設定、デバッグ設定 |

Unity 拡張は `.kc` と翻訳用 `.csv` を必須入力としては扱わない。

## Unity プロジェクトへの配置

Unity での開発では、`kes build --target unity` の出力先を Unity プロジェクト内に設定することを推奨する。

推奨例:

```txt
Assets/
    KoromoEventScript/
        Build/
            manifest.kson
            events/
                chapter001.klib
                loc/
                    en/
                        chapter001.klib
```

| パス | 用途 |
|---|---|
| `manifest.kson` | 実行対象イベント、`.klib`、素材情報の定義 |
| `events/*.klib` | 基準言語の VM 実行成果物 |
| `events/loc/<locale>/*.klib` | 言語別成果物 |

この配置は推奨であり、実際の出力先は `kes build --out-dir` または手動コピーで変更してよい。

Editor の importer は manifest を基準に相対パスで `.klib` を解決する。
そのため、`manifest.kson` と `.klib` 群の相対関係は CLI build の出力構造を維持しなければならない。Player 実行時は `Assets/` のパスや `AssetDatabase` を探索せず、インポート時に作成した KES Build Asset のシリアライズ済み参照を使用する。

## Unity への取り込み

`manifest.kson` と、それが参照する `.klib` 群は Unity プロジェクトの `Assets/` 配下へ配置して取り込む。

### 取り込み時の要件

1. `.kson`専用ScriptedImporterはUTF-8 JSONを[正式schema](runtime-manifest.schema.json)とsemantic validationで検証し、KES Build Assetをmain objectとして生成すること。
2. Unity が標準では TextAsset として扱わない `.klib` は、専用 ScriptedImporter でバイト列を保持する KES Klib Asset として取り込むこと。CLI 成果物の拡張子を `.bytes` へ変更してはならない。
3. `.kson` importerはmanifestが参照する相対パスのKES Klib Assetを解決し、KES Build Assetに直接参照として保存すること。
4. 言語別 `.klib` が存在する場合、locale と script ID をキーとするバリアント参照を KES Build Asset に保存すること。
5. `manifest.kson` または `.klib` の不足、破損、参照不整合はインポートを失敗させ、Console と Inspector に診断表示すること。
6. importer は `.klib` の File Header、format version、必須 section、`scriptId`、manifest 参照、runtime capability を検証すること。
7. manifestまたは参照`.klib`が再インポートされた場合、KES Build Assetの依存関係も再評価されること。KES Build Assetは`.kson`のmain objectなので、再import前後で`.kson`のGUIDを維持すること。

`AssetDatabase` と `ScriptedImporter` は Editor 専用である。Player の起動経路にこれらを含めてはならない。外部配信や DLC のようにビルド後のファイルを読む機能は v1 の非目標とし、将来導入する場合は Addressables、AssetBundle、または StreamingAssets 用の別 loader として定義する。

### 推奨配置

```txt
Assets/
    KoromoEventScript/
        Build/
            manifest.kson
            events/
        Scenes/
        UI/
```

推奨配置はガイドラインであり、実際のフォルダ配置は Unity プロジェクト側で変更してよい。

## KesManager

Unity 拡張は、シーン上に配置する KesManager を標準の起動入口とする。

KesManager を含む実行用オブジェクト群は、標準プレハブ `KesSystem` として提供する。
利用者はこのプレハブを任意のシーンへ配置し、Build Asset フィールドへ KES Build Asset を割り当てることで再生を開始できるものとする。

### KesManager の責務

| 項目 | 内容 |
|---|---|
| Manifest 読み込み | Inspector で指定された KES Build Asset から manifest と `.klib` を読み込む |
| 起動設定 | locale、開始イベント、デバッグ、オート再生などを初期化する |
| VM 実行 | 指定された `.klib` を開始し、完了後に次イベントを決定する |
| Presentation 連携 | 背景、actor、UI、音声へ命令結果を反映する |
| セーブ連携 | 現在状態の保存・復元を行う |
| 診断通知 | 実行中の警告、エラー、未解決 asset を通知する |

### KesManager の主要設定

| 設定 | 意味 |
|---|---|
| Build Asset | 実行対象の KES Build Asset |
| Default Locale | 既定ロケール |
| Start Event Override | manifest の entry を上書きして開始するイベント ID |
| Start Tag Override | 開始 script の Label Map に存在する public label。空の場合は manifest の entry label を使う |
| Play On Start | シーン開始時に自動再生するか |
| Debug Overlay | 実行状態と診断を画面表示するか |

### 標準プレハブ構成

Unity 拡張は、少なくとも次の階層を含む `KesSystem` プレハブを提供する。

```txt
KesSystem
    KesManager
    CanvasRoot
    SpriteRoot
    Presenters
```

| オブジェクト | 役割 |
|---|---|
| `KesSystem` | KES 実行系全体のルート |
| `KesManager` | KesManager コンポーネントがアタッチされ、各モジュール参照を保持する |
| `CanvasRoot` | uGUI ルート。Canvas コンポーネントを持ち、メッセージ、メッセージウィンドウ、選択肢などの UI パーツを配置する |
| `SpriteRoot` | 2D オブジェクトのルート。背景と立ち絵を配置する |
| `Presenters` | `*Presenter` スクリプト群をまとめてアタッチする |

### プレハブ要件

1. `KesSystem` プレハブはどのシーンにもそのまま配置できること。
2. `CanvasRoot` は uGUI ベースの標準 UI レイヤーとして動作すること。
3. `SpriteRoot` は 2D 表示レイヤーとして背景と actor 表示の親になること。
4. `KesManager` は Inspector から Build Asset を設定でき、`CanvasRoot`、`SpriteRoot`、`Presenters` を参照できること。
5. 利用者は標準プレハブをベースに UI 見た目や Presenter 実装を差し替えてよいこと。

### 起動手順

1. シーン上に `KesSystem` プレハブ、または KesManager コンポーネントを含む同等構成の GameObject を配置する。
2. Inspector から KesManager の Build Asset フィールドへ KES Build Asset をアタッチする。
3. 必要に応じて locale、開始イベント上書き、デバッグ表示を設定する。
4. Play Mode またはビルド実行を開始すると、KesManager は manifest を読み込み、再生を開始する。

### ライフサイクルと実行モデル

- `Play On Start` が有効な場合、KesManager は `Start` 以降に初期化し、同一フレーム中に重複起動しない。
- VM は入力待ち、選択肢待ち、演出待ちを明示的な待機状態として扱い、Unity のメインスレッドをブロックしてはならない。
- `UnityEngine.Object`、uGUI、AudioSource、Animator の操作は Unity メインスレッドで行う。
- 非同期処理には KesManager の停止または破棄時にキャンセルされるライフタイムを設ける。破棄後に Presenter を更新してはならない。
- `OnDisable` または `OnDestroy` では入力購読を解除し、演出と音声を停止し、未完了の待機をキャンセルする。
- scene をまたいで継続するかどうかはホストプロジェクトが決定する。標準プレハブは自動で `DontDestroyOnLoad` を呼ばず、同時に複数の KesManager が起動しようとした場合はエラーにする。

## VM命令とSTL実行契約

次の表はUnity runtimeの初期契約である。「VM待機」は命令完了まで次のopcodeへ進まないこと、「演出完了待ち」はanimation、fadeまたは入力の完了まで待つことを表す。キャンセルや失敗時の詳細は実装前レビューで変更してよいが、全項目について最終値を確定してから受け入れテストを作成する。

### VM opcode

| opcode | VM待機 | 演出完了待ち | エラー時停止 |
|---|---:|---:|---:|
| `PUSH_CONST`, `PUSH_TRUE`, `PUSH_FALSE`, `PUSH_NULL`, `PUSH_INT`, `POP`, `DUP` | no | no | schema/stack違反時yes |
| `LOAD_VAR`, `STORE_VAR`, `DEF_VAR` | no | no | index/type違反時yes |
| `ADD`, `SUB`, `MUL`, `DIV`, `NEG` | no | no | type違反、0除算時yes |
| `EQ`, `NEQ`, `LT`, `LE`, `GT`, `GE`, `AND`, `OR`, `NOT` | no | no | type違反時yes |
| `JUMP`, `JUMP_FALSE`, `LABEL`, `END` | no | no | offset/schema違反時yes |
| `SELECT` | yes | ユーザー選択までyes | UI生成、case解決失敗時yes |
| `CALL`, `CALL_VOID` | calleeによる | calleeによる | command失敗時yes |
| `SYSCALL`, `SYSCALL_VOID` | syscallによる | syscallによる | 下表による |
| `ARRAY_NEW`, `ARRAY_GET`, `ARRAY_SET` | no | no | index/type違反時yes |
| `NEW`, `GET_FIELD`, `SET_FIELD`, `CALL_METHOD`, `CALL_METHOD_VOID`, `DISPOSE` | methodによる | methodによる | class/member/type違反時yes |

### シナリオ構文とSTL

| module/命令 | VM待機 | 演出完了待ち | エラー時停止 |
|---|---:|---:|---:|
| `scenario.say` | yes | テキスト送りまでyes | UI失敗時yes。自動Voice欠落はwarning継続 |
| `scenario.nar` | yes | テキスト送りまでyes | UI失敗時yes。自動Voice欠落はwarning継続 |
| `core.print` | no | no | no |
| `core.array_len` | no | no | type違反時yes |
| `core.str_len` | no | no | type違反時yes |
| `core.range` | no | no | 引数不正時yes |
| `core.number_to_string` | no | no | type違反時yes |
| `core.bool_to_string` | no | no | type違反時yes |
| `core.assert` | no | no | 条件false時yes |
| `scene.rt_back` | no | no | render target不整合時yes |
| `scene.rt_front` | no | no | render target不整合時yes |
| `scene.bg` | yes | Addressables loadと表示反映までyes | asset解決失敗時yes |
| `scene.trans` | yes | fade/crossfade完了までyes | effect不明・演出失敗時yes |
| `scene.camera_autofocus` | no | no | 設定不正時yes |
| `actor.cast` | yes | Addressables load完了までyes | asset解決失敗時yes |
| `actor.show` | yes | loadと表示反映までyes | actor/face解決失敗時yes |
| `actor.hide` | no | no | actor未定義時yes |
| `actor.face` | yes | loadと表示反映までyes | face解決失敗時yes |
| `actor.move` | yes | move完了までyes | actor未定義・座標不正時yes |
| `actor.action_jump` | yes | jump animation完了までyes | actor未定義時yes |
| `text.vo` | yes | Voice loadと再生開始までyes | 明示Voice解決失敗時yes |
| `text.vf` | yes | Voice load、再生開始、face反映までyes | Voice/face解決失敗時yes |
| `text.p` | yes | 改ページ後の入力までyes | UI失敗時yes |
| `text.r` | no | no | UI失敗時yes |
| `text.l` | yes | 行内入力までyes | UI失敗時yes |
| `text.cm` | no | no | UI失敗時yes |
| `text.wait_click` | yes | 入力までyes | input source失敗時yes |
| `audio.bgm` | yes | loadと再生開始またはfade完了までyes | asset解決失敗時yes |
| `audio.bgm_stop` | yes | fade指定時は完了までyes | audio失敗時yes |
| `audio.se` | yes | loadと再生開始までyes | asset解決失敗時yes |
| `audio.se_stop` | no | no | no。対象なしはno-op |
| `audio.voice_stop` | no | no | no。対象なしはno-op |
| `flow.label` | no | no | label schema違反時yes |
| `flow.jump` | no | no | label解決失敗時yes |
| `flow.select` | yes | ユーザー選択までyes | UI/case解決失敗時yes |
| `flow.case` | `select`の一部 | `select`の一部 | case不整合時yes |
| `state.save` | yes | host callback完了までyes | host失敗時yes、cancel時継続 |
| `state.load` | yes | host取得と状態復元完了までyes | host/復元失敗時yes、cancel時継続 |
| `state.autosave` | yes | host callback完了までyes | host失敗時yes |
| `state.mark_read` | no | no | no |
| `state.is_read` | no | no | no |
| `system.wait` | yes | 指定時間経過までyes | 負数・非finite値でyes |
| `system.set_auto` | no | no | no |
| `system.set_skip` | no | no | mode不明時yes |
| `system.set_config_string` | no | no | key/type不正時yes |
| `system.set_config_number` | no | no | key/type不正時yes |
| `system.set_config_bool` | no | no | key/type不正時yes |
| `system.get_config` | no | no | key不明時yes |
| `system.set_param_string` | no | no | key/type不正時yes |
| `system.set_param_number` | no | no | key/type不正時yes |
| `system.set_param_bool` | no | no | key/type不正時yes |
| `system.get_param` | no | no | key不明時yes |

## イベント遷移

Unity 拡張は manifest の `events` をイベント目録として解釈する。
`events` の構造と意味論は CLI が `.kel` から生成した runtime 向けイベント情報に従う。

- 初回起動時は manifest の entry に対応するイベントを開始する。
- イベントの `.klib` 実行が完了した場合、直前のイベント ID とゲーム変数を使って次イベントの `trigger` を評価する。
- `trigger` 直下の `from` と `is` は AND 条件として扱う。
- `or` は複数定義でき、いずれかが成立すれば OR 条件部を満たす。
- 複数イベントが成立した場合は `.kel` の定義順で最初のイベントを採用する。
- 成立する次イベントがなければ、現在のセッションを通常終了する。

イベント遷移の意味論は [windows-runtime-spec.md](windows-runtime-spec.md) と整合することを必須とする。

## ローカライズ

Unity 拡張は翻訳用 `.csv` を直接読まず、CLI が生成した言語別 `.klib` を使ってローカライズを行う。

### ローカライズ規則

- locale 指定がない場合は基準言語の `.klib` を使う。
- `events/loc/<locale>/` に対応する `.klib` が存在する場合は、その locale の成果物を優先する。
- 指定 locale が存在しない場合は基準言語へフォールバックする。
- locale は manifest に記録された文字列と ordinal（大文字小文字を区別）で照合し、実行時に独自の言語タグ正規化を行わない。
- locale フォールバックが発生した場合は warning を出す。
- Voice、画像、BGM などの素材ローカライズは v1 では Unity プロジェクト側の asset 差し替え責務とする。

## Addressablesによる素材解決

Unity 拡張は、`.klib` が参照する論理asset IDをUnity Addressablesへ解決する。manifestの`assetId`をAddressables keyとして扱い、`kind`から要求するUnity型を決定し、`locale`でvariantを選択する。

### 対応資産種別

| 種別 | Unity 側の代表型 |
|---|---|
| 背景 | Sprite |
| actor 立ち絵 | Sprite |
| BGM | AudioClip |
| SE | AudioClip |
| Voice | AudioClip |
| UI 補助素材 | Sprite, Prefab |

### 資産解決規則

- asset IDとAddressables addressは同じ文字列とし、ordinal（大文字小文字を区別）で比較する。
- `kind=background`と`kind=actor`は`Addressables.LoadAssetAsync<Sprite>(assetId)`、音声kindは`Addressables.LoadAssetAsync<AudioClip>(assetId)`で解決する。
- locale指定時はmanifestの同一`assetId`から要求localeと一致するentryを優先し、存在しなければ`locale=null`へフォールバックする。
- Addressables keyは要求型について一意に解決できなければならない。複数assetへ解決されるkey、空ID、kindと実asset型の不一致はEditor検証エラーとする。
- load handleはassetの利用中保持し、画面・音声状態から外れて不要になった時点またはKesManager破棄時に`Addressables.Release`する。
- 背景・actor・BGM・SEの必須assetが見つからない場合は実行時エラーとして該当命令を停止する。Voice欠落はwarningとし、台詞表示を継続する。
- Unity targetではmanifestの`assets[].path`を解決に使用しない。

## 描画

Unity 拡張は、ノベルゲーム向けの2D表示を標準の描画モデルとする。URP 2D RendererとUniversal Rendererの双方をサポートする。背景とactorはCanvas配下へ置かず、Orthographic Cameraが描画する`SpriteRenderer`として`SpriteRoot`配下へ配置する。UIだけを`CanvasRoot`配下のuGUIで描画する。

### 論理レイヤー

| レイヤー | 内容 |
|---|---|
| 背景 | `bg` 命令で表示する背景 |
| actor | `show`、`face`、`action_jump` などで制御する actor |
| 効果 | フェード、クロスフェード、簡易画面遷移 |
| テキスト | `say` / `nar` の本文、話者名 |
| 選択肢 | `select` の `case` 一覧 |
| システム UI | バックログ、設定、セーブ、ロード、デバッグ表示 |

### 表示要件

- 制作座標系は1920x1080のフルHDとする。左上をUI座標原点、画面中央をworld座標原点とし、KES座標からworld座標への変換をPresentation層で一元化する。
- 実画面サイズが異なる場合も、その表示領域を1920x1080とみなしてKES座標を正規化する。`x_world = (x / 1920 - 0.5) * visibleWorldWidth`、`y_world = (0.5 - y / 1080) * visibleWorldHeight`を基本変換とする。
- CameraはOrthographic projectionとし、実際のviewport全体を仮想1920x1080へ対応付ける。異なるaspectでもletterboxやcropを標準動作にせず、xとyをそれぞれviewport幅・高さへ正規化する。背景はviewport全体を覆うようscaleし、actorと入力座標には同じ正規化変換を適用する。
- Canvas ScalerのUI Scale Modeは`Constant Physical Size`とする。このmodeにはReference Resolutionがないため、1920x1080はCanvas Scalerの値ではなくKES UI layoutと座標変換の基準として保持する。
- 背景用sorting layerを`KES Background`、actor用sorting layerを`KES Actor`とし、この順で描画する。同一layer内の順序はVMのlayerとzを`sortingOrder`へ決定的に変換する。
- 背景とactorは`SpriteRoot`配下の`SpriteRenderer`で管理する。
- メッセージウィンドウ、話者名、選択肢、システム UI は `CanvasRoot` 配下の uGUI 要素として管理する。
- VolumeとRenderer FeatureはURP projectのdefault設定を使用し、v1 packageは追加のRenderer Featureを要求または自動登録しない。
- トランジションは少なくとも `none`、`fade`、`crossfade` をサポートする。
- 未知のトランジション名は実行時エラーとする。

## 音声

音声は BGM、SE、Voice の 3 系統を標準とする。

| チャンネル | 用途 | 多重再生 |
|---|---|---|
| BGM | 背景音楽 | 原則 1 系統 |
| SE | 効果音 | 複数同時再生可 |
| Voice | 台詞音声 | 原則 1 系統 |

- `say` / `nar` のタグ付きテキストに対する Voice 解決規則は言語仕様に従う。
- クリック送りでテキストをスキップした場合、再生中の Voice は停止してよい。
- BGM はフェード付き切り替えを推奨するが、v1 では即時切り替えでもよい。

## 入力と UI

Unity 拡張は、プレイ可能な最小 UI を内蔵またはサンプル prefab として提供する。
標準 UI 実装は uGUI を用いる。

標準プレハブは Input System の Input Actions と `InputSystemUIInputModule` を使用する。ゲーム進行用 action map と UI 操作用 action map を分け、メニュー表示中や選択肢表示中に同じ入力が複数の操作へ伝播しないよう action map または入力コンテキストを切り替える。旧 Input Manager を使うプロジェクトは、同じ論理操作を `IKesInputSource` 相当の入力境界へ渡す独自 adapter で対応できるが、標準 package は旧 API へ直接依存しない。

### 標準操作

| 操作 | 挙動 |
|---|---|
| 左クリック / Enter / Space | テキスト送り、選択肢決定 |
| 右クリック / Esc | メニュー表示または閉じる |
| Ctrl | 押下中スキップ |
| Tab | オートモード切り替え |
| 上下キー | 選択肢移動 |

入力はボタンの押下開始を 1 回の決定として扱う。Ctrl のスキップだけは押下中の継続状態として扱う。選択肢表示中はテキスト送りを無効化し、現在選択中の項目がない状態で決定入力を受けても分岐してはならない。

### 標準 UI 機能

| UI | 内容 |
|---|---|
| メッセージウィンドウ | 話者名と本文を表示する |
| 選択肢 | `case` 一覧を表示し選択を受け付ける |
| バックログ | 表示済みテキストを確認する |
| スキップ | テキストを高速進行する |
| オート | 一定時間ごとに自動進行する |
| セーブ / ロード | 任意スロットへの保存と復元を行う |
| 設定 | 音量、テキスト速度などを変更する |

UI の見た目は Unity プロジェクト側で差し替え可能とする。
v1 では UI スキンシステムを必須にしない。

## 状態スナップショットと復元

Unity拡張はファイル、PlayerPrefs、クラウド等への保存と読み込みを行わない。ライブラリは現在状態から`KesSaveState`構造体を作成するAPIと、ゲーム側から渡された`KesSaveState`を検証して状態復元するAPIだけを提供する。serialization形式、保存先、slot管理、暗号化、バックアップ、migration UIはゲーム制作者の責務とする。

### KesSaveState schema

| 状態 | 内容 |
|---|---|
| `schemaVersion` | KesSaveState構造のversion |
| `gameId` | manifestのgame ID |
| `buildId` | manifestのbuild ID |
| `locale` | 実行中locale |
| `scenario` | event ID、script ID、bytecode offset、call stack、continuation state |
| `gameVariables` | `set_param_*`で変更されたstring、number、bool、null値のmap |
| `systemVariables` | auto、skip、既読状態などKES実行系の値のmap |
| `presentation` | 背景、actor、UI、音声の論理表示状態 |

`presentation`は少なくとも次を保持する。

- 背景のasset ID、locale、表示中フラグ、KES座標、scale、sorting order。
- actorごとのactor ID、face用asset ID、locale、表示中フラグ、KES座標、scale、sorting order。
- 表示中テキスト、話者、選択肢、メッセージウィンドウ表示状態。
- BGMのasset ID、locale、loop、volume、再生中フラグ。SEとVoiceの途中位置は必須としない。

`CaptureState()`は待機状態を含む一貫したsnapshotを返す。`RestoreState(KesSaveState)`はAddressables assetを非同期に再解決してPresentationを再構築し、完了するまでVMを進めない。復元前に`schemaVersion`、`gameId`、`buildId`、script ID、bytecode offset、変数型を検証する。`buildId`不一致を許可するかはhostから渡す互換性policyで決める。

`state.save`と`state.autosave`はsnapshotとslot情報をhost callbackへ渡して待機し、ライブラリ自身は書き込まない。`state.load`はhostへload要求を通知して待機し、hostが取得した`KesSaveState`を返した後に復元する。hostがcancelした場合は実行を継続し、失敗を返した場合はruntime errorとする。

## 診断

Unity 拡張は、Editor と Play Mode の両方で診断を扱う。

### 診断分類

| 分類 | 例 |
|---|---|
| Import Error | `manifest.kson` のschema違反、必須ファイル不足 |
| Validation Warning | locale 欠落、未使用 asset、Voice 欠落 |
| Runtime Error | `.klib` 読み込み失敗、未知命令、必須 asset 解決失敗 |
| Runtime Warning | locale フォールバック、Voice 欠落、推奨設定不足 |

### 表示要件

- Import 時のエラーは Unity Console と Inspector で確認できること。
- 実行時エラーはゲーム進行を停止し、開発時には詳細メッセージを表示できること。
- 診断には分類、安定した KES 診断コード、メッセージ、可能なら manifest path、script ID、bytecode offset、source mapping の file・line・column を含めること。
- source mapping がない場合は script ID と bytecode offset を表示すること。
- Debug build または Debug Overlay 有効時は、現在イベント、現在タグ、選択 locale、解決 asset ID を画面表示できること。Release build の標準 UI には内部スタックやローカル絶対パスを表示しないこと。

## CLI との連携

Unity 拡張は、CLI の次の成果物契約と整合しなければならない。

- `kes build --target unity` が出力する `.klib` 構成を読めること。
- `kes build --target unity` が出力する `manifest.kson` を読めること。
- `kes build --loc <locale>` の言語別 `.klib` を読めること。
- `kes build --out-dir` により Unity プロジェクト内へ直接出力する運用を許容すること。
- Unity プロジェクト外へ出力した build 成果物を、手動で Unity プロジェクトへ移動またはコピーする運用を許容すること。
- Unity 拡張は `.kc` を前提にしないこと。

## 非目標

v1 では次を必須にしない。

- Unity 上での `.kc` 直接編集とその場ビルド
- Timeline、Cinemachineとの深い自動統合
- 3D 空間演出への最適化
- ネットワーク同期、マルチプレイ
- ランタイム中の翻訳辞書 `.csv` 差し替え
- ビルド後に追加された外部 `.klib` の動的ロード

## 受け入れ条件

v1 の Unity 拡張は、少なくとも次を満たす。

1. Unity 6000.5.3f1 の URP プロジェクトへ package と Sample を導入し、asmdef の参照エラーなくコンパイルできる。
2. GitHub の公開tagまたはcommit hashを指定したGit URLから、空のURPプロジェクトへ`com.koromosoft.koromo-event-script`を導入できる。
3. `kes build --target unity` の出力を `Assets/` 配下へ配置すると、`manifest.kson`と全`.klib`がインポートされ、KES Build Assetが生成される。
4. `.klib` 欠落、破損、script ID 不一致、未対応 format version を Play Mode 開始前に Editor 診断として検出できる。
5. `KesSystem`プレハブへKES Build Assetを設定し、Addressablesをbuildした状態でPlay ModeとWindows Playerからentry eventを開始できる。
6. Windows runtimeと共有するVMソースが[`.klib`中間表現仕様](k-intermediate-representation-spec.md)の全opcodeを実行できる。
7. [KES言語STL仕様](kes-language-stl-spec.md)の`core`、`scene`、`actor`、`text`、`audio`、`flow`、`state`、`system`全公開命令と`scenario.say`、`scenario.nar`を本書の実行契約表どおり実行できる。
8. 基準言語、存在するlocale variant、存在しないlocaleから基準言語へのwarning付きフォールバックを検証できる。
9. Input Systemでテキスト送り、選択肢移動・決定、メニュー、スキップ、オートを操作でき、1入力が重複処理されない。
10. `CaptureState()`でゲーム変数、システム変数、シナリオ位置、画面状態を取得し、hostが保持した構造体を`RestoreState()`へ渡してVM、表示、BGMを復元できる。ライブラリがファイルI/Oを行わない。
11. Runtime assemblyが`UnityEditor`を参照せず、Windows Player buildにEditor専用コードが含まれない。
12. Edit Modeで`.kson` importer・manifest schema・Addressables検証を、Play Modeで起動・入力待ち・選択・state capture/restoreを自動テストできる。

## 将来拡張

将来仕様では、次の項目を拡張候補とする。

- UI Toolkit ベースの標準 UI
- Timeline / Animation / Playables 連携
- モバイル向け入力最適化
- WebGL 向けメモリ制約対応
- Unity Editor 上での KES プレビュー再生

## Unity 公式資料

- [Text assets](https://docs.unity3d.com/Manual/class-TextAsset.html)
- [Scripted Importer](https://docs.unity3d.com/Manual/ScriptedImporters.html)
- [AssetDatabase ワークフローのカスタマイズ](https://docs.unity3d.com/Manual/AssetDatabaseCustomizingWorkflow.html)
- [Input System](https://docs.unity3d.com/Manual/com.unity.inputsystem.html)
- [Input System の UI サポート](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.17/manual/UISupport.html)
- [Git URLからのUPM packageのインストール](https://docs.unity3d.com/Manual/upm-ui-giturl.html)
- [Addressables](https://docs.unity3d.com/Manual/com.unity.addressables.html)
