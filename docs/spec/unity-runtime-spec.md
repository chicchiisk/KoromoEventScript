# KoromoEventScript Unity 組み込み拡張仕様書

KoromoEventScript Unity は、Unity プロジェクト内で KoromoEventScript のビルド成果物を読み込み、イベントスクリプトを実行するための組み込み拡張である。

本仕様書では、Unity 組み込み拡張の対象環境、入力データ、インポート、ランタイム構成、描画、音声、入力、UI、診断、配布契約を定義する。

## 基本方針

- Unity プロジェクトへ取り込んで使う組み込み拡張として提供する。
- Unity 拡張は `.kc` / `.kel` の生ソースを直接解釈しない。
- Unity 拡張は CLI の `kes build` が生成した `manifest.json` と `.klib` を入力として扱う。
- VM が実行する中間表現は `.klib` を正とし、ファイル形式、instruction schema、命令体系は [`.klib` 中間表現仕様](k-intermediate-representation-spec.md)に従う。
- イベント遷移は `.kel` を起点に行う。
- Unity 固有の描画、音声、UI、入力は Unity の標準機能または広く利用される公式パッケージで実装できる設計とする。
- UI は uGUI を標準構成とする。
- v1 は「Unity プロジェクト内に配置した `manifest.json` を、シーン上の KesManager から読み込んで再生できること」を最優先とする。
- Unity での開発では Editor と CLI を往復することを前提に、`kes build --target unity` の出力先を Unity プロジェクト内ディレクトリに設定する構成を推奨する。
- 生成済み成果物を手動で Unity プロジェクトへコピーまたは移動する運用も許容する。
- KesManager を含む実行用 GameObject 一式は、どのシーンにも配置しやすいようプレハブとして提供する。
- Windows 単体ランタイムと同じシナリオ資産を再利用できるよう、VM の実行意味論は共通に保つ。
- ローカライズ辞書 `.csv` は Unity では直接読まない。ローカライズ済みテキストは CLI build 済みの言語別 `.klib` を利用する。

## 用語

| 用語 | 意味 |
|---|---|
| Unity 拡張 | Unity 上で KoromoEventScript を実行するための組み込み機能一式 |
| KesManager | シーン上で `manifest.json` の読み込み、VM 実行、UI 制御を司る MonoBehaviour |
| Build Output Root | `kes build --target unity` の出力先ディレクトリ |
| Manifest Asset | Unity へ取り込まれた `manifest.json` の TextAsset |
| VM | `.klib` を読み取り、イベントスクリプトを実行する仮想マシン |
| Asset Registry | 論理 asset ID と UnityEngine.Object の対応を管理する仕組み |
| Entry Event | manifest の `events` と entry 情報に基づき最初に開始されるイベント |

## 対象環境

| 項目 | 内容 |
|---|---|
| エンジン | Unity 6 系 |
| スクリプト言語 | C# |
| レンダリング | Unity 標準 Render Pipeline または URP |
| UI | uGUI を必須、UI Toolkit は将来拡張 |
| 入力 | Unity Input System を推奨。v1 では旧 Input Manager 相当でも可 |
| 対応プラットフォーム | Windows, macOS, Linux, iOS, Android, WebGL を将来対象にできる設計とする |

本リポジトリ内の Unity 検証プロジェクトは Unity 6 系を前提とする。
v1 の正式検証対象は Unity Editor 上の Play Mode と Windows 向けビルドとする。

## 拡張構成

Unity 拡張は次の責務を持つモジュールから構成する。

| モジュール | 役割 |
|---|---|
| Manifest Loader | `manifest.json` を読み込み、実行対象の `.klib` とイベント情報を解決する |
| VM Runner | `.klib` を読み込み、命令列を順次実行する |
| Event Resolver | manifest の `events` に基づくイベント選択と次イベント遷移を解決する |
| Asset Registry | 背景、actor、音声などの論理 ID から Unity アセットを引く |
| Presentation | 背景、actor、テキスト、選択肢、トランジションを描画する |
| Audio | BGM、SE、Voice の再生と停止を扱う |
| UI | メッセージ、選択肢、スキップ、オート、履歴などを提供する |
| Save System | セーブデータ、既読情報、設定の永続化を扱う |
| Diagnostics | 実行時エラー、警告、デバッグ表示、Editor 上の検証結果を扱う |

## 入力データ

Unity 拡張は、少なくとも次の入力を扱う。

| 入力 | 用途 |
|---|---|
| `manifest.json` | 実行対象のイベント、`.klib`、素材参照、locale 情報 |
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
            manifest.json
            events/
                chapter001.klib
                loc/
                    en/
                        chapter001.klib
```

| パス | 用途 |
|---|---|
| `manifest.json` | 実行対象イベント、`.klib`、素材情報の定義 |
| `events/*.klib` | 基準言語の VM 実行成果物 |
| `events/loc/<locale>/*.klib` | 言語別成果物 |

この配置は推奨であり、実際の出力先は `kes build --out-dir` または手動コピーで変更してよい。

Unity 拡張は、manifest を基準に相対パスで `.klib` を解決する。
そのため、`manifest.json` と `.klib` 群の相対関係は CLI build の出力構造を維持しなければならない。

## Unity への取り込み

`manifest.json` と、それが参照する `.klib` 群は Unity プロジェクトの `Assets/` 配下へ配置して取り込む。

### 取り込み時の要件

1. Unity は `manifest.json` を TextAsset として取り込めること。
2. Unity は `.klib` を TextAsset または専用 importer で認識できること。
3. `manifest.json` が参照する相対パスの `.klib` を Unity プロジェクト内で解決できること。
4. 言語別 `.klib` が存在する場合、Unity 拡張は locale ごとのバリアント一覧を解決できること。
5. `manifest.json` または `.klib` の不足、破損、参照不整合は Editor 上で診断表示できること。

### 推奨配置

```txt
Assets/
    KoromoEventScript/
        Build/
            manifest.json
            events/
        Scenes/
        UI/
```

推奨配置はガイドラインであり、実際のフォルダ配置は Unity プロジェクト側で変更してよい。

## KesManager

Unity 拡張は、シーン上に配置する KesManager を標準の起動入口とする。

KesManager を含む実行用オブジェクト群は、標準プレハブ `KesSystem` として提供する。
利用者はこのプレハブを任意のシーンへ配置し、Manifest フィールドへ `manifest.json` を割り当てることで再生を開始できるものとする。

### KesManager の責務

| 項目 | 内容 |
|---|---|
| Manifest 読み込み | Inspector で指定された `manifest.json` を読み込む |
| 起動設定 | locale、開始イベント、デバッグ、オート再生などを初期化する |
| VM 実行 | 指定された `.klib` を開始し、完了後に次イベントを決定する |
| Presentation 連携 | 背景、actor、UI、音声へ命令結果を反映する |
| セーブ連携 | 現在状態の保存・復元を行う |
| 診断通知 | 実行中の警告、エラー、未解決 asset を通知する |

### KesManager の主要設定

| 設定 | 意味 |
|---|---|
| Manifest | 実行対象の `manifest.json` TextAsset |
| Default Locale | 既定ロケール |
| Start Event Override | manifest の entry を上書きして開始するイベント ID |
| Start Tag Override | `.klib` 内のラベルまたはタグから開始する位置 |
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
4. `KesManager` は Inspector から Manifest を設定でき、`CanvasRoot`、`SpriteRoot`、`Presenters` を参照できること。
5. 利用者は標準プレハブをベースに UI 見た目や Presenter 実装を差し替えてよいこと。

### 起動手順

1. シーン上に `KesSystem` プレハブ、または KesManager コンポーネントを含む同等構成の GameObject を配置する。
2. Inspector から KesManager の Manifest フィールドへ `manifest.json` をアタッチする。
3. 必要に応じて locale、開始イベント上書き、デバッグ表示を設定する。
4. Play Mode またはビルド実行を開始すると、KesManager は manifest を読み込み、再生を開始する。

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
- locale フォールバックが発生した場合は warning を出してよい。
- Voice、画像、BGM などの素材ローカライズは v1 では Unity プロジェクト側の asset 差し替え責務とする。

## Asset Registry

Unity 拡張は、`.klib` が参照する論理 asset ID を Unity アセットへ変換するための Asset Registry を提供する。

### 対応資産種別

| 種別 | Unity 側の代表型 |
|---|---|
| 背景 | Sprite, Texture2D, Prefab |
| actor 立ち絵 | Sprite, Animator, Prefab |
| BGM | AudioClip |
| SE | AudioClip |
| Voice | AudioClip |
| UI 補助素材 | Sprite, Prefab |

### 資産解決規則

- `.klib` 内の asset ID は、Unity 側の Asset Registry で解決する。
- Asset Registry は ScriptableObject、Addressables ラベル、または同等の Unity 標準仕組みで実装してよい。
- v1 では asset ID と UnityEngine.Object の静的マッピングを最小構成とする。
- 必須 asset が見つからない場合、背景・actor は実行時エラー、Voice は warning として扱ってよい。

## 描画

Unity 拡張は、ノベルゲーム向けの 2D 表示を標準の描画モデルとする。
UI は uGUI を標準とし、2D 表示は `SpriteRoot` 配下、UI 表示は `CanvasRoot` 配下へ分離する。

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

- v1 は 16:9 の表示を標準とする。
- 解像度差は uGUI の Canvas Scaler または同等機構で吸収する。
- 背景と actor の前後関係は VM が持つ表示順と Presentation 設定に従う。
- 背景と actor は `SpriteRoot` 配下で管理する。
- メッセージウィンドウ、話者名、選択肢、システム UI は `CanvasRoot` 配下の uGUI 要素として管理する。
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

### 標準操作

| 操作 | 挙動 |
|---|---|
| 左クリック / Enter / Space | テキスト送り、選択肢決定 |
| 右クリック / Esc | メニュー表示または閉じる |
| Ctrl | 押下中スキップ |
| Tab | オートモード切り替え |
| 上下キー | 選択肢移動 |

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

## セーブデータ

Unity 拡張は、少なくとも次の状態を保存できる設計とする。

| 状態 | 内容 |
|---|---|
| 現在イベント | 実行中イベント ID |
| 現在位置 | `.klib` 内の復元可能な位置 |
| ゲーム変数 | `set_param_*` で変更された値 |
| 表示状態 | 背景、actor、選択肢待ち、音声状態 |
| 設定 | 音量、オート速度、テキスト速度 |

保存形式は Unity 標準の永続化手段でよいが、将来的に差し替え可能な抽象化を持つこと。

## 診断

Unity 拡張は、Editor と Play Mode の両方で診断を扱う。

### 診断分類

| 分類 | 例 |
|---|---|
| Import Error | `manifest.json` の破損、必須ファイル不足 |
| Validation Warning | locale 欠落、未使用 asset、Voice 欠落 |
| Runtime Error | `.klib` 読み込み失敗、未知命令、必須 asset 解決失敗 |
| Runtime Warning | locale フォールバック、Voice 欠落、推奨設定不足 |

### 表示要件

- Import 時のエラーは Unity Console と Inspector で確認できること。
- 実行時エラーはゲーム進行を停止し、開発時には詳細メッセージを表示できること。
- デバッグ有効時は、現在イベント、現在タグ、選択 locale、解決 asset ID を画面表示してよい。

## CLI との連携

Unity 拡張は、CLI の次の成果物契約と整合しなければならない。

- `kes build --target unity` が出力する `.klib` 構成を読めること。
- `kes build --target unity` が出力する `manifest.json` を読めること。
- `kes build --loc <locale>` の言語別 `.klib` を読めること。
- `kes build --out-dir` により Unity プロジェクト内へ直接出力する運用を許容すること。
- Unity プロジェクト外へ出力した build 成果物を、手動で Unity プロジェクトへ移動またはコピーする運用を許容すること。
- Unity 拡張は `.kc` を前提にしないこと。

## 非目標

v1 では次を必須にしない。

- Unity 上での `.kc` 直接編集とその場ビルド
- Timeline、Cinemachine、Addressables との深い自動統合
- 3D 空間演出への最適化
- ネットワーク同期、マルチプレイ
- ランタイム中の翻訳辞書 `.csv` 差し替え

## 将来拡張

将来仕様では、次の項目を拡張候補とする。

- Addressables ベースの Asset Registry
- UI Toolkit ベースの標準 UI
- Timeline / Animation / Playables 連携
- モバイル向け入力最適化
- WebGL 向けメモリ制約対応
- Unity Editor 上での KES プレビュー再生
