# Requirements Document

## Introduction

KoromoEventScript Windows runtime は、CLI が生成した Windows 向けビルド成果物を Windows 11 上で単体実行するための配布用プレイヤーである。制作者は `kes run` で開発中の成果物を確認でき、一般ユーザーは `kes publish --target windows` で作られた配布フォルダまたは zip を展開して実行ファイルからシナリオをプレイできる必要がある。

本仕様では、Windows runtime が `manifest.json`、`.klib`、素材、ユーザー設定、セーブデータを扱い、描画、音声、入力、標準 UI、診断、終了コードをユーザーまたは開発者に観測可能な形で提供する要求を定義する。

## Boundary Context

- **In scope**: Windows 11 向け runtime の起動、manifest 読み込み、`.klib` 読み込み、素材解決、VM 実行、`.klib` の全命令実行、STL / runtime syscall 実行、描画、音声、入力、標準 UI、セーブ/ロード、既読情報、ユーザー設定、診断、終了コード、`kes run` と `kes publish --target windows` から見える接続挙動。
- **Out of scope**: `.kc` / `.kel` の直接実行、`.klib` instruction schema や binary format の再設計、CLI の一般的な build / publish 仕様全体の再設計、Unity / Unreal runtime、VS Code 拡張、Windows 10 以前、macOS、Linux、UI スキン差し替え、キーコンフィグ、正式なゲームパッド対応、MSIX 配布、クラウドセーブ、runtime プラグイン。
- **Adjacent expectations**: CLI は `manifest.json`、`.klib`、素材、配布フォルダを生成し、runtime はそれらを入力として扱う。`.klibtxt` は補助成果物であり、runtime 入力ではない。ローカライズ済み本文は build 時に対象言語向け `.klib` として解決され、runtime は翻訳作業用 `.csv` を直接読まない。

## Requirements

### Requirement 1: 起動と実行入口

**Objective:** As a 制作者, I want Windows runtime を `kes run` と配布実行ファイルの両方から起動できる, so that 開発時確認と配布後プレイを同じ runtime 契約で行える

#### Acceptance Criteria

1. When `kes run` が runtime を起動する, the KoromoEventScript Windows runtime shall 指定された `manifest.json` と runtime 引数を受け取って実行を開始する
2. When 配布フォルダ内の実行ファイルが直接起動される, the KoromoEventScript Windows runtime shall 実行ファイルと同じディレクトリまたは `data/` 配下から既定の `manifest.json` を探索する
3. When `--manifest <PATH>` が指定される, the KoromoEventScript Windows runtime shall 指定された manifest を既定探索より優先して読み込む
4. When `--locale`, `--start`, `--fullscreen`, `--width`, or `--height` が指定される, the KoromoEventScript Windows runtime shall 起動時のロケール、開始位置、表示状態、ウィンドウサイズに反映する
5. If runtime 引数が不正である, then the KoromoEventScript Windows runtime shall ランタイム引数エラーとして終了する

### Requirement 2: manifest と実行入力

**Objective:** As a 制作者, I want runtime が CLI のビルド成果物だけを入力として扱う, so that 配布物の内容と実行時の素材解決が予測可能になる

#### Acceptance Criteria

1. The KoromoEventScript Windows runtime shall `manifest.json` を実行に必要な `.klib`、素材、ロケール、runtime 設定、build 情報の入口として扱う
2. When manifest 内に相対パスが含まれる, the KoromoEventScript Windows runtime shall manifest が置かれたディレクトリを基準にそのパスを解決する
3. When `.klib` を読み込む, the KoromoEventScript Windows runtime shall `module.scriptId` と manifest の script entry の対応を検証する
4. If manifest が存在しない、または読み込めない, then the KoromoEventScript Windows runtime shall ランタイム起動エラーとして終了する
5. If manifest に記載された必須 `.klib` が存在しない, then the KoromoEventScript Windows runtime shall ファイルまたはディレクトリの入出力エラーとして終了する
6. The KoromoEventScript Windows runtime shall `.kc`、`.kel`、翻訳作業用 `.csv`、`.klibtxt` を runtime 実行入力として扱わない

### Requirement 3: 画面表示と描画結果

**Objective:** As a プレイヤー, I want シナリオ画面がウィンドウ表示とフルスクリーン表示で正しく表示される, so that 制作者が意図した画面構成でプレイできる

#### Acceptance Criteria

1. The KoromoEventScript Windows runtime shall 1920x1080 の制作座標系をシナリオ演出と UI 配置の基準として扱う
2. When 表示領域が 16:9 である, the KoromoEventScript Windows runtime shall 制作座標系を表示領域全体へ拡大縮小する
3. When 表示領域が 16:9 ではない, the KoromoEventScript Windows runtime shall アスペクト比を維持して中央配置し、余白を表示する
4. When マウス操作が発生する, the KoromoEventScript Windows runtime shall 表示座標系の位置を制作座標系へ変換して選択肢や UI の判定に使用する
5. When 背景、actor、効果、テキスト、選択肢、システム UI が同時に表示される, the KoromoEventScript Windows runtime shall 仕様で定義された論理レイヤー順に画面へ反映する
6. When `fade`, `crossfade`, or `none` のトランジションが要求される, the KoromoEventScript Windows runtime shall 対応する画面遷移を表示する
7. If 未知のトランジション名が要求される, then the KoromoEventScript Windows runtime shall 実行時エラーとして扱う

### Requirement 4: `.klib` 全命令に基づく VM 実行

**Objective:** As a 制作者, I want `.klib` に含まれる全命令が runtime で抜け漏れなく実行される, so that CLI が生成したビルド済みシナリオを Windows プレイヤーで完全に再生できる

#### Acceptance Criteria

1. The KoromoEventScript Windows runtime shall `docs/spec/k-intermediate-representation-spec.md` で定義された `.klib` 命令セットを抜け漏れなく実行対象として扱う
2. When stack、定数、変数、演算、比較、制御フロー、配列、class、field、method、call、syscall、label、select、end に属する `.klib` 命令が実行される, the KoromoEventScript Windows runtime shall `.klib` 中間表現仕様に従って VM 状態と実行位置を更新する
3. When `.klib` 命令が描画、音声、入力待ち、UI、セーブ、設定、診断に関わる runtime 効果を要求する, the KoromoEventScript Windows runtime shall プレイヤーまたは制作者に観測可能な状態へその効果を反映する
4. When `select` に対応する命令が実行される, the KoromoEventScript Windows runtime shall 選択肢を表示し、選択結果に応じた進行先へ移動する
5. When `jump` または label に基づく制御フローが実行される, the KoromoEventScript Windows runtime shall ビルド時に解決された実行位置へ移動する
6. When `END` に対応する命令が実行される, the KoromoEventScript Windows runtime shall シナリオ実行を完了状態として扱う
7. If 未対応 opcode、未対応 feature、命令 schema 違反、または VM 状態不整合が検出される, then the KoromoEventScript Windows runtime shall 実行時エラーまたは読み込みエラーとして扱う

### Requirement 5: STL と runtime syscall

**Objective:** As a 制作者, I want KES 標準ライブラリが Windows runtime 上で実行時効果を持つ, so that STL を使った MVP シナリオを追加実装なしで配布できる

#### Acceptance Criteria

1. The KoromoEventScript Windows runtime shall `docs/spec/kes-language-stl-spec.md` で定義された STL の runtime 側効果を実行対象として扱う
2. When `core` モジュールの STL が実行される, the KoromoEventScript Windows runtime shall デバッグ出力、配列長、文字列長、範囲生成、文字列化、assert の効果または結果を仕様に従って提供する
3. When `scene` モジュールの STL が実行される, the KoromoEventScript Windows runtime shall 裏画面、表画面、背景、トランジション、カメラ補助の効果を仕様に従って反映する
4. When `actor` モジュールの STL が実行される, the KoromoEventScript Windows runtime shall actor のロード、表示、非表示、表情、移動、簡易アクションの効果を仕様に従って反映する
5. When `text` モジュールの STL が `say` or `nar` 文脈で実行される, the KoromoEventScript Windows runtime shall Voice、表情変更、改ページ、改行、行内クリック待ち、メッセージウィンドウ制御、クリック待ちを仕様に従って反映する
6. When `audio` モジュールの STL が実行される, the KoromoEventScript Windows runtime shall BGM、SE、Voice の再生、停止、多重再生、フェード、チャンネル制御を仕様に従って反映する
7. When `flow` に属する label、jump、select、case の runtime 連携が実行される, the KoromoEventScript Windows runtime shall VM の進行、選択肢表示、選択確定待ちを仕様に従って扱う
8. When `state` モジュールの STL が実行される, the KoromoEventScript Windows runtime shall save、load、autosave、mark_read、is_read の効果または結果を仕様に従って提供する
9. When `system` モジュールの STL が実行される, the KoromoEventScript Windows runtime shall wait、auto、skip、ユーザー設定の更新と取得を仕様に従って提供する
10. If STL または runtime syscall の実行中に素材欠落、保存失敗、未知の設定キー、不正な skip mode、または runtime 状態不整合が発生する, then the KoromoEventScript Windows runtime shall STL 仕様で定義された警告または実行時エラーとして扱う

### Requirement 6: 音声再生

**Objective:** As a プレイヤー, I want BGM、SE、Voice がシナリオと操作に合わせて再生される, so that ノベルゲームとして自然な音響体験を得られる

#### Acceptance Criteria

1. When BGM が要求される, the KoromoEventScript Windows runtime shall BGM チャンネルで背景音楽を再生する
2. When SE が要求される, the KoromoEventScript Windows runtime shall SE チャンネルで効果音を再生する
3. When Voice が要求される, the KoromoEventScript Windows runtime shall Voice チャンネルで台詞音声を再生する
4. When `say` or `nar` のタグ付きテキストに対応する Voice 素材が manifest に存在する, the KoromoEventScript Windows runtime shall 対応する Voice を再生する
5. If 対応する Voice 素材が manifest に存在しない, then the KoromoEventScript Windows runtime shall 警告として扱い、シナリオ実行を継続する
6. When プレイヤーが現在のテキストをスキップして進行する, the KoromoEventScript Windows runtime shall 再生中の Voice を停止し、BGM を継続する
7. When 音量設定が変更される, the KoromoEventScript Windows runtime shall マスター音量、BGM 音量、SE 音量、Voice 音量へ変更を反映する

### Requirement 7: 標準入力操作と標準 UI

**Objective:** As a プレイヤー, I want 標準的なノベルゲーム操作と UI を使える, so that 追加設定なしでシナリオを読める

#### Acceptance Criteria

1. When 左クリック、Enter、or Space が入力される, the KoromoEventScript Windows runtime shall テキスト送りまたは選択肢決定を行う
2. When 右クリック or Esc が入力される, the KoromoEventScript Windows runtime shall システムメニューを表示または閉じる
3. While Ctrl が押下されている, the KoromoEventScript Windows runtime shall スキップ進行を行う
4. When Tab が入力される, the KoromoEventScript Windows runtime shall オートモードを切り替える
5. When マウスホイール上が入力される, the KoromoEventScript Windows runtime shall バックログを表示する
6. When 上下キーが入力される, the KoromoEventScript Windows runtime shall 選択肢の選択位置を移動する
7. When F11 が入力される, the KoromoEventScript Windows runtime shall フルスクリーン状態を切り替える
8. The KoromoEventScript Windows runtime shall メッセージウィンドウ、選択肢、バックログ、スキップ、オート、セーブ、ロード、設定、タイトル、終了を標準 UI として提供する

### Requirement 8: セーブ、ロード、既読情報、ユーザー設定

**Objective:** As a プレイヤー, I want プレイ状態と設定を保存して復元できる, so that 中断後も同じ状態から再開できる

#### Acceptance Criteria

1. When プレイヤーが通常セーブを実行する, the KoromoEventScript Windows runtime shall VM 状態、制御状態、画面状態、音声状態、既読情報、メタ情報を含むセーブデータを保存する
2. When オートセーブ条件が満たされる, the KoromoEventScript Windows runtime shall オートセーブデータを保存する
3. When プレイヤーがロードを実行する, the KoromoEventScript Windows runtime shall 保存時点の画面、実行位置、選択状態、ロケール、必要な音声状態を復元する
4. When セーブデータが実行位置を保持する, the KoromoEventScript Windows runtime shall 配布物上のファイルパスだけに依存せず、`.klib` 上の script id と instruction index を安定参照として扱う
5. If セーブデータが参照する script id または instruction index が現在の manifest と `.klib` で有効ではない, then the KoromoEventScript Windows runtime shall ロード失敗としてプレイヤーに通知する
6. When ユーザー設定が変更される, the KoromoEventScript Windows runtime shall 音量、テキスト速度、オート速度、スキップモード、フルスクリーン状態、ウィンドウサイズ、ロケールを保存する
7. While 配布物ディレクトリが書き込み不可である, the KoromoEventScript Windows runtime shall 通常プレイ、セーブ、ロード、ユーザー設定保存を継続できる
8. The KoromoEventScript Windows runtime shall 複数ゲーム間でセーブデータとユーザー設定が衝突しないように保存情報を区別する

### Requirement 9: 診断、エラー表示、終了コード

**Objective:** As a 制作者, I want 通常配布モードとデバッグモードで適切な診断を得られる, so that 一般ユーザーには過剰な内部情報を出さず、開発時には原因を追跡できる

#### Acceptance Criteria

1. While 通常配布モードで実行している, the KoromoEventScript Windows runtime shall 一般ユーザーに必要な範囲のエラー表示のみを行う
2. While 通常配布モードで実行している, the KoromoEventScript Windows runtime shall 詳細な VM 位置、内部スタック、素材解決ログを画面上に表示しない
3. When `--debug` が指定される, the KoromoEventScript Windows runtime shall FPS、VM 位置、リソース状態、音声状態、入力、実行時警告、エラーを表示またはログ出力できる
4. When `--profile` が指定される, the KoromoEventScript Windows runtime shall 描画時間、VM 実行時間、素材読み込み時間を含むプロファイル情報を収集できる
5. When デバッグ表示またはログで元ソース位置を示せる, the KoromoEventScript Windows runtime shall `.klib` の source mapping を参照して file、line、column を表示する
6. If source mapping が存在しない, then the KoromoEventScript Windows runtime shall script id と instruction index を fallback 表示として扱う
7. When runtime が終了する, the KoromoEventScript Windows runtime shall 正常終了、一般エラー、ランタイム引数エラー、実行時エラー、入出力エラー、ランタイム起動エラーを CLI の終了コード体系と整合する終了コードで返す

### Requirement 10: Windows 配布成果物

**Objective:** As a 制作者, I want Windows 向け publish 成果物に runtime と必要資産が含まれる, so that 一般ユーザーへ自己完結した配布物を渡せる

#### Acceptance Criteria

1. When `kes publish --target windows` の成果物が作成される, the KoromoEventScript Windows runtime shall 配布フォルダ内の実行ファイルから `data/manifest.json` と実行資産を読み込んでプレイを開始できる
2. When Windows 向け配布 zip が展開される, the KoromoEventScript Windows runtime shall 展開後の配布フォルダ内の実行ファイルからプレイを開始できる
3. The KoromoEventScript Windows runtime shall 配布成果物内の `data/events/` にある `.klib` と `data/assets/` にある素材を manifest に基づいて使用する
4. Where 言語別 `.klib` バリアントが配布成果物に含まれる, the KoromoEventScript Windows runtime shall 選択されたロケールに対応する `.klib` を実行する
5. The KoromoEventScript Windows runtime shall `--include-source` が指定されていない Windows 配布物で `.kc` / `.kel` の存在を実行条件にしない
