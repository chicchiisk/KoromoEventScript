# MVP 実装ロードマップ

このドキュメントは、KoromoEventScript の初期実装を GitHub Issue に分解するための下書きである。

各項目は、原則として1つの Issue として登録する。
実装中に大きすぎると分かった場合は、さらに小さい Issue へ分割する。

## ソース配置方針

開発中のプロジェクトは `source/` 以下へ配置する。

```txt
source/
    cli/
    runtime/
    extension/
        vscode/
        unity/
        unrealengine/
```

| パス | 用途 | 初期 MVP |
|---|---|---|
| `source/cli/` | CLI、言語処理系、VM、ビルド、publish | 対象 |
| `source/runtime/` | Windows 11 向け単体実行アプリ | 対象 |
| `source/extension/vscode/` | VS Code 拡張 | 対象外 |
| `source/extension/unity/` | Unity 組み込み拡張 | 対象外 |
| `source/extension/unrealengine/` | Unreal Engine 組み込み拡張 | 対象外 |

初期 MVP では `source/cli/` と `source/runtime/` に集中する。
`source/extension/` 配下はフォルダのみ先に用意し、本格実装は初期 MVP 後に扱う。

## Phase 0: 開発基盤

### 0-1. 開発ワークフロードキュメントを追加する

目的:
GitHub Issue、ブランチ、Pull Request、CI、人間レビューを使った開発ルールを定義する。

受け入れ条件:

- `docs/development-workflow.md` が存在する。
- 1 Issue = 1 Branch = 1 PR の原則が書かれている。
- Issue、PR、レビュー、merge のルールが書かれている。

### 0-2. Issue テンプレートと PR テンプレートを追加する

目的:
AI が Issue を読んで実装し、人間が PR をレビューしやすい形式を固定する。

受け入れ条件:

- `.github/ISSUE_TEMPLATE/feature.yml` が存在する。
- `.github/PULL_REQUEST_TEMPLATE.md` が存在する。
- 受け入れ条件、必須テスト、対象外を記入できる。

### 0-3. テスト戦略ドキュメントを追加する

目的:
AI 実装の品質を保つため、テスト分類、優先順位、testdata 構成を定義する。

受け入れ条件:

- `docs/testing-strategy.md` が存在する。
- Lexer / Parser / Diagnostic / Golden / VM / CLI / LSP / Runtime のテスト方針が書かれている。
- PR ごとの必須確認が書かれている。

### 0-4. CI の初期構成を追加する

目的:
Pull Request ごとに Markdown と将来の実装テストを自動検証できるようにする。

受け入れ条件:

- `.github/workflows/ci.yml` が存在する。
- Markdown lint が実行される。
- .NET または Node.js の実装が存在する場合にテストを実行できる。
- C# のテストフレームワークとして NUnit を使う方針が明記されている。
- 実装が存在しない初期状態でも CI がスキップ可能である。

### 0-5. 初期 testdata 構成を追加する

目的:
Parser、Diagnostic、CLI 統合テストで使う入力ファイルの置き場を定義する。

受け入れ条件:

- `testdata/` 配下に入力種別ごとのディレクトリがある。
- 最小の正常系 `.ke` と `.kel` がある。
- 最小プロジェクト例がある。

### 0-6. MVP 実装ロードマップを作成する

目的:
初期実装を Issue 化しやすい単位に分解する。

受け入れ条件:

- Phase 0 から Phase 5 までの作業順が書かれている。
- 各 Phase の主要 Issue 候補が書かれている。
- 初期実装で優先する範囲と後回しにする範囲が明確である。

## Phase 1: 言語処理系の最小核

### 1-1. `.ke` Lexer を実装する

配置:
`source/cli/`

対象:

- 識別子
- 予約語
- 数値
- 文字列
- コメント
- タグ
- インデント
- 改行

対象外:

- 意味解析
- 型検査
- `.kel` の完全対応

必須テスト:

- 正常な token stream
- 閉じていない文字列
- 閉じていないブロックコメント
- タブインデント

### 1-2. `.ke` Parser の最小構文を実装する

配置:
`source/cli/`

対象:

- `import`
- `var`
- `say`
- `nar`
- `select`
- `case`
- `label`
- `jump`
- 通常命令
- LESS 構文

必須テスト:

- 正常な AST
- `:` 欠落
- インデント不一致
- 空の `say` / `nar` ブロック

### 1-3. `.kel` Parser の最小構文を実装する

配置:
`source/cli/`

対象:

- エントリポイント
- `.ke` ファイル参照
- イベント ID

詳細文法が未確定の間は、CLI と VS Code 拡張で必要な最小構文に限定する。

### 1-4. 診断モデルを実装する

配置:
`source/cli/`

対象:

- level
- code
- file
- line
- column
- message

必須テスト:

- text 形式
- JSON Lines 形式
- 複数診断の順序

### 1-5. `kes build --check-only` の骨組みを実装する

配置:
`source/cli/`

対象:

- プロジェクトルート解決
- `kes.xml` 読み込み
- `.kel` と `.ke` の解析
- 診断出力
- 終了コード

対象外:

- `.k` 生成
- manifest 生成
- runtime 起動

## Phase 2: 意味解析

- import 解決を実装する。
- label / jump / case のタグ解決を実装する。
- actor / fn / class / enum / var の定義収集を実装する。
- 重複定義診断を実装する。
- 未定義参照診断を実装する。
- 型検査の最小実装を追加する。
- 標準ライブラリの組み込み定義を名前解決と型検査の対象に追加する。
- 予約内部名 `__systemcall__` のユーザーコードからの直接使用を診断する。
- 警告と warnings-as-errors を実装する。

## Phase 3: コンパイルと VM

- 標準ライブラリを実装する。
  - `docs/spec/kes-language-stl-spec.md` に定義された core / scene / actor / text / audio / flow / state / system モジュールを組み込み定義として登録する。
  - STL は runtime 機能を直接持たず、`__systemcall__` の薄いラップ、または他の STL 関数の組み合わせとして実装する。
  - `__systemcall__` は内部命令として実装し、syscall ID、引数型、戻り値型、`void` 戻り値の使用可否をシグネチャ表で検証する。
  - runtime 向けの scene / actor / text / audio / state / system syscall を VM イベントへ変換する。
- 標準ライブラリのテストを追加する。
  - `print`、`array_len`、`str_len`、`range`、`number_to_string`、`bool_to_string`、`assert` の型検査と実行テストを追加する。
  - `p`、`r`、`l`、`cm`、`vo`、`vf`、`wait_click` が text/audio syscall として発行されることを確認する。
  - `bgm`、`se`、`save`、`load`、`set_config` など runtime 連携命令の syscall 発行 golden test を追加する。
  - ユーザーコードからの `__systemcall__` 直接使用、未知の syscall ID、引数数/型不一致を診断するテストを追加する。
- 中間表現 `.k` の仕様書を追加する。
- `.ke` から `.k` への変換を実装する。
- IR golden test を追加する。
- VM のヘッドレス実行を実装する。
- `say`、`nar`、`select`、`jump` の VM テストを追加する。
- セーブ対象となる VM 状態を定義する。

## Phase 4: CLI 完成度向上

- `kes init` を実装する。
- `kes build` の成果物生成を実装する。
- `kes clean` を実装する。
- `kes run` の runtime 起動委譲を実装する。
- `kes publish --target windows` の出力を実装する。
- manifest 生成を実装する。
- JSON Lines ログを実装する。

## Phase 5: Windows 単体実行アプリ

Phase 5 では、Windows 11 向けの単体実行アプリに集中する。
Unity / Unreal 組み込み拡張と VS Code 拡張の本格実装は、初期 MVP の対象外とする。

### 5-1. Windows Runtime プロジェクトを作成する

目的:
WinUI 3 / Windows App SDK / Win2D を使う Windows ランタイムのプロジェクトを作成する。

配置:
`source/runtime/`

受け入れ条件:

- Windows Runtime 用の C# プロジェクトがある。
- .NET 10 系を使用する。
- アプリを起動すると空のウィンドウが表示される。
- NUnit の最小テストプロジェクトがある。
- CI で build と test が実行できる。

### 5-2. ランタイム起動引数を解析する

目的:
`kes run` や配布実行ファイルから渡される runtime options を解析する。

対象:

- `--manifest <PATH>`
- `--debug`
- `--locale <LOCALE>`
- `--start <TAG>`
- `--fullscreen`
- `--width <NUMBER>`
- `--height <NUMBER>`
- `--profile`

受け入れ条件:

- 正常な引数を runtime 設定へ変換できる。
- 不正な引数で終了コード2相当のエラーにできる。
- 引数解析は UI に依存せず NUnit でテストできる。

### 5-3. manifest 読み込みを実装する

目的:
CLI が生成した `manifest.json` を読み込み、ランタイム入力として検証する。

受け入れ条件:

- `project`、`entry`、`scripts`、`assets`、`locale`、`runtime`、`build` を読み込める。
- manifest からの相対パスを manifest 所在ディレクトリ基準で解決できる。
- manifest 不在、必須項目不足、必須 `.k` 不在を診断できる。
- manifest 読み込みは UI に依存せず NUnit でテストできる。

### 5-4. リソース解決を実装する

目的:
manifest に定義された画像、音声、ローカライズ辞書を ID から解決する。

受け入れ条件:

- 背景、actor、BGM、SE、Voice の素材 ID を解決できる。
- manifest にない素材 ID を実行時エラーまたは警告として扱える。
- 暗黙のフォルダ探索を行わない。
- リソース解決は UI に依存せず NUnit でテストできる。

### 5-5. VM 連携アダプタを実装する

目的:
ヘッドレス VM の状態変化を Windows Runtime の表示、音声、入力へ渡す境界を作る。

受け入れ条件:

- VM から発行される `bg`、`show`、`face`、`trans`、`say`、`nar`、`select`、`jump` を受け取れる。
- STL の `__systemcall__` 由来の scene / actor / text / audio / state / system イベントを受け取れる。
- ランタイム側の画面状態に変換できる。
- VM と WinUI 3 の依存方向が分離されている。
- VM 連携アダプタは NUnit でテストできる。

### 5-6. 画面状態モデルを実装する

目的:
背景、actor、表情、位置、テキスト、選択肢、トランジションを復元可能な状態として保持する。

受け入れ条件:

- 現在の背景を保持できる。
- actor の表示状態、位置、表情を保持できる。
- 表示中の話者、本文、選択肢を保持できる。
- セーブ対象にできる形でシリアライズ可能である。

### 5-7. 制作座標系と表示スケーリングを実装する

目的:
1920x1080 の制作座標系をウィンドウ表示へアスペクト比維持で変換する。

受け入れ条件:

- 16:9 では表示領域全体に描画できる。
- 横長または縦長では余白を計算し中央配置できる。
- 表示座標から制作座標へマウス座標を逆変換できる。
- スケーリング計算は WinUI に依存せず NUnit でテストできる。

### 5-8. Win2D 描画の最小実装を追加する

目的:
画面状態をもとに、背景、actor、テキスト、選択肢を描画する。

受け入れ条件:

- 背景画像を描画できる。
- actor 画像をレイヤー順に描画できる。
- メッセージウィンドウ、話者名、本文を描画できる。
- 選択肢一覧を描画できる。
- 素材読み込み失敗を実行時エラーとして扱える。

### 5-9. トランジションを実装する

目的:
`fade`、`crossfade`、`none` の画面遷移を実装する。

受け入れ条件:

- `none` は即時切り替えできる。
- `fade` はフェードイン、フェードアウトを実行できる。
- `crossfade` は前画面と次画面を補間できる。
- 未知のトランジション名を実行時エラーにできる。

### 5-10. 入力操作を実装する

目的:
マウスとキーボード入力をプレイヤー操作へ変換する。

対象:

- 左クリック / Enter / Space
- 右クリック / Esc
- Ctrl
- Tab
- マウスホイール上
- 上下キー
- F11

受け入れ条件:

- テキスト送りが動作する。
- 選択肢移動と決定が動作する。
- バックログ表示を呼び出せる。
- オート、スキップ、フルスクリーン切り替えを呼び出せる。
- 入力変換は UI イベントから分離してテストできる。

### 5-11. 標準 UI の最小セットを実装する

目的:
配布用プレイヤーとして最低限必要な UI を実装する。

初期対象:

- メッセージウィンドウ
- 選択肢
- バックログ
- スキップ
- オート
- システムメニュー
- 本文中の改ページ `p`、改行 `r`、行内クリック待ち `l`、メッセージウィンドウ非表示 `cm`

受け入れ条件:

- `say` / `nar` の本文をクリック待ちで進行できる。
- `p`、`r`、`l`、`cm` の text syscall をメッセージ表示へ反映できる。
- `select` の選択肢を表示し、選択結果を VM に返せる。
- バックログで表示済みテキストを確認できる。
- スキップとオートの状態を切り替えられる。

### 5-12. 音声再生を実装する

目的:
BGM、SE、Voice の再生、停止、音量制御を実装する。

受け入れ条件:

- BGM は原則1系統で再生できる。
- SE は複数同時再生できる。
- Voice は原則1系統で再生できる。
- `bgm`、`bgm_stop`、`se`、`se_stop`、`voice_stop`、`vo_auto` の audio syscall を処理できる。
- クリック進行で Voice を停止できる。
- Voice 素材がない場合は警告として実行継続できる。

### 5-13. セーブ、ロード、設定保存を実装する

目的:
通常セーブ、ロード、オートセーブ、既読情報、ユーザー設定保存を実装する。

受け入れ条件:

- VM 状態を保存、復元できる。
- 画面状態を保存、復元できる。
- BGM など必要な音声状態を保存、復元できる。
- 既読情報を保存できる。
- 音量、テキスト速度、オート速度、フルスクリーン、ウィンドウサイズ、ロケールを保存できる。
- 配布物ディレクトリが書き込み不可でもユーザーデータ領域へ保存できる。

### 5-14. デバッグ表示とログを実装する

目的:
通常配布モードとデバッグモードを分離し、開発中の確認をしやすくする。

受け入れ条件:

- `--debug` ありで FPS、VM 位置、リソース状態、音声状態、入力状態を確認できる。
- `--debug` なしでは内部詳細を画面に出さない。
- `--profile` ありで描画時間、VM 実行時間、素材読み込み時間を記録できる。
- ログ出力は通常ログ、警告、エラーを区別できる。

### 5-15. `kes run` から Windows Runtime を起動する

目的:
CLI の `kes run` から manifest と runtime options を渡して Windows Runtime を起動する。

受け入れ条件:

- `kes run` から `--manifest` を渡して起動できる。
- `kes run events/main.kel --debug` でデバッグモード起動できる。
- `kes run ... -- --profile` のように `--` 以降を runtime へ渡せる。
- runtime の終了コードを CLI の終了コードへ反映できる。

### 5-16. `kes publish --target windows` と配布物を接続する

目的:
Windows Runtime を含む自己完結フォルダと zip を生成し、展開後に実行できるようにする。

受け入れ条件:

- `dist/windows/<ProjectName>/` に実行ファイル、`data/manifest.json`、`.k`、素材、ローカライズ辞書、ライセンスを配置できる。
- `--include-source` なしでは `.ke` / `.kel` の生ファイルを含めない。
- zip を展開し、実行ファイルからプレイを開始できる。
- 配布物の smoke test 手順が PR に記載されている。

## 初期 MVP 対象外

次の作業は初期 MVP では扱わない。

- Unity 組み込み拡張
- Unreal Engine 組み込み拡張
- VS Code 拡張の本格実装
- UI スキン差し替え
- キーコンフィグ
- ゲームパッド正式対応
- 追加トランジション
- 実績、スクリーンショット、クラウド同期

## 初期 MVP の完了条件

初期 MVP は、次を満たした状態とする。

1. 最小 KES プロジェクトを `kes init` で作成できる。
2. `kes build --check-only` で構文エラーと未定義タグを検出できる。
3. `kes build` で `.k` と `manifest.json` を生成できる。
4. ヘッドレス VM で `say`、`nar`、`select`、`jump` を実行できる。
5. `kes run` から Windows Runtime を起動できる。
6. Windows Runtime で背景、actor、テキスト、選択肢を表示し、クリック進行できる。
7. 通常セーブ、ロード、オートセーブ、既読情報、ユーザー設定保存が動作する。
8. `kes publish --target windows` の zip を展開し、実行ファイルからプレイを開始できる。
9. CLI 統合テスト、VM テスト、runtime state test、golden test が CI で通る。

Unity / Unreal 連携と VS Code 拡張は、初期 MVP の後に本格化する。
