# Implementation Plan

- [ ] 1. Runtime 基盤と WinUI 前提を整える
- [x] 1.1 WinUI 3 開発前提を検出し、ビルド手順を固定する
  - .NET SDK、WinApp CLI、WinUI 3 template、Developer Mode の検出結果を開発者が一度に確認できる手順を追加する
  - Developer Mode が無効な場合は実装を進めず、WinUI setup 手順で解消する運用にする
  - Windows runtime のビルドと起動は `winapp run` または `BuildAndRun.ps1` 相当の手順で確認でき、packaged exe を直接起動しないことが分かる
  - _Requirements: 1.1, 1.5, 10.1, 10.2_
  - _Boundary: Runtime prerequisites_

- [x] 1.2 Runtime Core、Windows app、テスト project を solution に追加する
  - runtime core、WinUI 3 Windows app、core tests、Windows host tests の project を追加する
  - Windows app は x64 または ARM64 を対象にし、AnyCPU に依存しない構成にする
  - `dotnet build KoromoEventScript.slnx` で新規 project を含む solution が復元とコンパイルの入口を持つ
  - _Requirements: 1.1, 3.1, 10.1_
  - _Boundary: Solution structure_

- [x] 1.3 `.klib` モデルと headless VM 資産を Runtime Core へ共有化する
  - CLI に閉じている `.klib` 型と VM の再利用可能部分を Runtime Core から参照できる境界へ移す
  - CLI 側は新しい共有境界を参照し、既存 CLI build と headless 実行のテストを保つ
  - Runtime Core だけを参照するテストで `.klib` 型を構築できる
  - _Requirements: 2.3, 4.1, 4.2_
  - _Boundary: Runtime Core, CLI shared compilation_

- [x] 1.4 Runtime 共通の effect、diagnostic、exit code 契約を定義する
  - 描画、音声、入力待ち、UI、保存、設定、診断の runtime effect を VM から Windows host へ渡せる形にする
  - warning、runtime error、startup error、IO error、argument error を終了コードへ写像できる契約を用意する
  - VM または syscall が生成した effect と diagnostic を fake host で観測できる
  - _Requirements: 4.3, 5.10, 9.1, 9.2, 9.7_
  - _Boundary: Runtime Core contracts_

- [ ] 2. manifest、package、素材解決を実装する
- [x] 2.1 Runtime manifest の読み込みと相対パス解決を実装する
  - `manifest.json` を runtime 入力の入口として読み込み、schema version、game id、locale、scripts、assets、runtime defaults、build 情報を検証する
  - 相対パスは manifest の配置ディレクトリ基準で解決する
  - manifest が存在しない、読めない、必須項目が欠ける場合に起動エラーとして観測できる
  - _Requirements: 2.1, 2.2, 2.4, 9.7_
  - _Boundary: Runtime Core manifest_

- [x] 2.2 (P) `.klib` package resolver と script id 検証を実装する
  - manifest の script entry と `.klib` の `scriptId` 対応を検証する
  - 必須 `.klib` が欠ける場合は IO error として返す
  - `.kc`、`.kel`、翻訳作業用 `.csv`、`.klibtxt` が runtime 実行入力にならないことをテストで確認できる
  - _Requirements: 2.3, 2.5, 2.6, 10.3, 10.5_
  - _Boundary: Runtime Core package resolver_
  - _Depends: 2.1_

- [x] 2.3 (P) locale variant と素材 catalog を解決する
  - 選択 locale に応じた `.klib` と素材 entry を runtime package から選べる
  - `data/events/` と `data/assets/` の配布 layout を manifest に基づいて解決する
  - locale が切り替わる test package で、選択された `.klib` が実行候補になる
  - _Requirements: 1.4, 2.1, 10.3, 10.4_
  - _Boundary: Runtime Core resource catalog_
  - _Depends: 2.1_

- [ ] 3. `.klib` VM の全命令実行を完成させる
- [x] 3.1 VM session と save snapshot の基本状態を実装する
  - instruction pointer、stack、variables、call context、await 状態を Runtime Core で保持する
  - `scriptId` と `instructionIndex` を安定した実行位置として snapshot に含める
  - VM session の状態を capture / restore できることを unit test で確認できる
  - _Requirements: 4.2, 8.1, 8.4_
  - _Boundary: Runtime Core VM session_

- [x] 3.2 stack、定数、変数、演算、比較命令を実行する
  - stack 操作、literal、変数定義/読取/書込、算術、論理、比較を仕様どおりに処理する
  - schema 違反または VM 状態不整合は runtime error または load error として返す
  - 命令ごとの前後 stack と変数状態が unit test で観測できる
  - _Requirements: 4.1, 4.2, 4.7_
  - _Boundary: Runtime Core VM executor_

- [x] 3.3 (P) label、jump、select、end の制御フローを実行する
  - build 済み label と instruction index に基づいて jump する
  - select は選択肢表示 effect と選択確定待ちを生成し、結果に応じて進行先へ移動する
  - END 命令で session が完了状態になることをテストで確認できる
  - _Requirements: 4.4, 4.5, 4.6, 5.7, 7.6_
  - _Boundary: Runtime Core VM control flow_
  - _Depends: 3.1_

- [x] 3.4 (P) 配列、class、field、method、call 命令を実行する
  - array new/get/set、object field、method call、call/call void を VM 状態へ反映する
  - 不正な index、field、method、call target を runtime error として扱う
  - 配列と object を使う `.klib` fixture が Runtime Core の VM で完走する
  - _Requirements: 4.1, 4.2, 4.7_
  - _Boundary: Runtime Core VM object model_
  - _Depends: 3.1_

- [x] 3.5 opcode coverage gate を追加する
  - `.klib` opcode enum の全値が executor dispatch の対象であることを自動検証する
  - 未対応 opcode が追加された場合にテストが失敗する
  - coverage test の失敗メッセージから不足 opcode が分かる
  - _Requirements: 4.1, 4.7_
  - _Boundary: Runtime Core tests_

- [ ] 4. STL と runtime syscall を実装する
- [x] 4.1 core STL syscall を実装する
  - debug 出力、配列長、文字列長、range、stringify、assert の結果または effect を提供する
  - assert 失敗は diagnostic と runtime error の契約に従う
  - core syscall の成功/失敗が unit test で観測できる
  - _Requirements: 5.1, 5.2, 9.3_
  - _Boundary: Runtime Core STL core_

- [x] 4.2 (P) scene と actor STL syscall を runtime effect へ変換する
  - 裏画面、表画面、背景、transition、camera 補助を scene state effect として表現する
  - actor の load、show、hide、expression、move、簡易 action を actor state effect として表現する
  - fake host が scene / actor effect を順序付きで受け取れる
  - _Requirements: 3.5, 3.6, 3.7, 5.3, 5.4_
  - _Boundary: Runtime Core STL scene actor_
  - _Depends: 1.4_

- [x] 4.3 (P) text と flow STL syscall を runtime effect へ変換する
  - `say` / `nar` 文脈の voice、表情変更、改ページ、改行、行内クリック待ち、メッセージウィンドウ制御、クリック待ちを effect として表現する
  - label、jump、select、case の runtime 連携を VM の進行と選択待ちへ接続する
  - text と flow の fixture が選択待ち、クリック待ち、進行再開を再現できる
  - _Requirements: 4.4, 5.5, 5.7, 7.1, 7.8_
  - _Boundary: Runtime Core STL text flow_
  - _Depends: 3.3_

- [x] 4.4 (P) audio、state、system STL syscall を runtime effect へ変換する
  - BGM、SE、Voice、channel、fade、save、load、autosave、mark_read、is_read、wait、auto、skip、設定取得/更新を effect または return value として提供する
  - 未知の設定 key、不正な skip mode、保存失敗を warning または runtime error として扱う
  - fake host で audio/state/system effect の入出力が確認できる
  - _Requirements: 5.6, 5.8, 5.9, 5.10, 6.1, 6.2, 6.3, 6.7, 8.2, 8.6_
  - _Boundary: Runtime Core STL audio state system_
  - _Depends: 1.4_

- [ ] 4.5 STL syscall coverage gate を追加する
  - STL 仕様から作成した syscall fixture と registry の対応を自動検証する
  - 未登録 syscall、未分類 syscall、誤った module 名がテストで検出される
  - STL 追加時に runtime 側実装漏れがテストで分かる
  - _Requirements: 5.1, 5.10_
  - _Boundary: Runtime Core tests_

- [ ] 5. Windows host の起動、描画、標準 UI を実装する
- [ ] 5.1 WinUI app の bootstrap と起動引数処理を実装する
  - `--manifest`、既定 manifest 探索、`--locale`、`--start`、`--fullscreen`、`--width`、`--height`、`--debug`、`--profile` を解釈する
  - 不正引数は runtime argument error として終了する
  - `BuildAndRun.ps1` または `winapp run` で sample manifest を渡して MainWindow が起動する
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 9.3, 9.4_
  - _Boundary: Windows Runtime bootstrap_

- [ ] 5.2 Win2D scene renderer と 1920x1080 座標系を実装する
  - 1920x1080 制作座標を 16:9 では全体拡大、非 16:9 では中央配置と余白表示に変換する
  - 背景、actor、効果、テキスト、選択肢、システム UI を論理レイヤー順に描画する
  - renderer test または screenshot で座標変換とレイヤー順を確認できる
  - _Requirements: 3.1, 3.2, 3.3, 3.5_
  - _Boundary: Windows Runtime rendering_
  - _Depends: 4.2, 4.3_

- [ ] 5.3 入力座標変換と transition controller を実装する
  - mouse 表示座標を制作座標へ変換し、選択肢や UI の hit test に使う
  - `fade`、`crossfade`、`none` の transition を表示し、未知 transition を runtime error にする
  - 16:9 と非 16:9 の hit test、および未知 transition の失敗がテストで確認できる
  - _Requirements: 3.4, 3.6, 3.7, 4.3_
  - _Boundary: Windows Runtime rendering controls_
  - _Depends: 5.2_

- [ ] 5.4 (P) 標準 UI の message、choice、backlog、menu shell を実装する
  - WinUI の標準 control と theme resource を使い、message window、choice list、backlog、system menu の表示状態を ViewModel で管理する
  - すべての interactive control に AutomationId または AutomationProperties.Name を設定する
  - UI automation で message、choice、backlog、menu の主要要素を検索できる
  - _Requirements: 4.4, 7.5, 7.8, 9.1, 9.2_
  - _Boundary: Windows Runtime standard UI_
  - _Depends: 4.3_

- [ ] 6. 音声、入力、保存 UI を Windows host に接続する
- [ ] 6.1 Audio channel service を実装する
  - BGM、SE、Voice を別 channel として再生、停止、fade、音量変更できる
  - `say` / `nar` の voice 素材が存在する場合は再生し、欠ける場合は warning のみで継続する
  - skip 時に Voice が停止し、BGM が継続することを fake または host test で確認できる
  - _Requirements: 5.6, 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7_
  - _Boundary: Windows Runtime audio_
  - _Depends: 4.4_

- [ ] 6.2 Windows input router を実装する
  - 左クリック、Enter、Space、右クリック、Esc、Ctrl、Tab、mouse wheel、上下キー、F11 を runtime input に変換する
  - text advance、choice decision、system menu、skip、auto、backlog、choice navigation、fullscreen の状態変化を発生させる
  - keyboard と mouse の入力ごとに期待する runtime input がテストで確認できる
  - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.7_
  - _Boundary: Windows Runtime input_
  - _Depends: 5.1, 5.4_

- [ ] 6.3 Save/load/settings UI と user data store を接続する
  - save、load、settings、title、exit を system menu から操作できる
  - save/settings は配布物ディレクトリではなく game id ごとの user data 領域へ保存する
  - 書き込み不可の配布物 layout でも save、load、settings 保存が継続できる
  - _Requirements: 7.8, 8.1, 8.2, 8.6, 8.7, 8.8_
  - _Boundary: Windows Runtime persistence UI_
  - _Depends: 5.4_

- [ ] 6.4 load 復元と不正 save のプレイヤー通知を実装する
  - 保存時点の画面、実行位置、選択状態、locale、必要な音声状態を復元する
  - 無効な `scriptId` または `instructionIndex` を参照する save は load 失敗として UI に通知する
  - 正常 load と invalid load の両方を Windows host test で確認できる
  - _Requirements: 8.3, 8.4, 8.5_
  - _Boundary: Windows Runtime persistence_
  - _Depends: 3.1, 6.1, 6.3_

- [ ] 7. 診断、profile、エラー表示を統合する
- [ ] 7.1 debug overlay と runtime log を実装する
  - `--debug` で FPS、VM 位置、resource state、audio state、input、warning、error を overlay または log に出す
  - 通常配布モードでは VM stack、素材探索詳細、内部位置を画面に出さない
  - debug mode と通常 mode の出力差がテストまたは UI 確認で観測できる
  - _Requirements: 9.1, 9.2, 9.3_
  - _Boundary: Windows Runtime diagnostics_

- [ ] 7.2 profile 計測と source mapping 表示を実装する
  - `--profile` で draw、VM、asset load の時間を収集する
  - source mapping がある場合は file、line、column を表示し、ない場合は script id と instruction index を fallback 表示する
  - profile log に timing と source location fallback が記録される
  - _Requirements: 9.4, 9.5, 9.6_
  - _Boundary: Windows Runtime diagnostics_

- [ ] 7.3 起動、IO、実行時エラーの表示と process exit を統合する
  - manifest、`.klib`、素材、opcode、transition、syscall の fatal error を統一的に UI と exit code へ変換する
  - CLI の終了コード体系と整合した process result を返す
  - representative fatal error fixture が期待 exit code を返す
  - _Requirements: 2.4, 2.5, 3.7, 4.7, 5.10, 9.7_
  - _Boundary: Windows Runtime error handling_

- [ ] 8. CLI run と publish を Windows runtime に接続する
- [ ] 8.1 build manifest を runtime manifest へ拡張する
  - CLI build が runtime に必要な script、asset、locale、runtime defaults、build 情報を manifest に含める
  - 既存 build manifest の互換性を壊さず、Windows runtime が必要な情報を読める
  - sample project の build 出力に runtime manifest fields が現れる
  - _Requirements: 2.1, 10.3, 10.4_
  - _Boundary: CLI build manifest_

- [ ] 8.2 `kes run` から Windows runtime を起動する
  - `kes run` が build 済み manifest と runtime 引数を Windows runtime process へ渡す
  - `--manifest`、`--locale`、`--start`、`--fullscreen`、`--width`、`--height`、`--debug`、`--profile` が runtime に届く
  - fake process launcher test で起動 command line と終了コード伝播を確認できる
  - _Requirements: 1.1, 1.3, 1.4, 1.5, 9.7_
  - _Boundary: CLI run integration_
  - _Depends: 5.1_

- [ ] 8.3 `kes publish --target windows` の folder layout を実装する
  - self-contained Windows runtime、`data/manifest.json`、`data/events/**/*.klib`、`data/assets/**` を配布 folder に配置する
  - `--include-source` がない場合に `.kc` / `.kel` を runtime 実行条件にしない
  - 展開済み folder の exe から `data/manifest.json` を探索できる layout が integration test で確認できる
  - _Requirements: 1.2, 10.1, 10.3, 10.5_
  - _Boundary: CLI publish integration_
  - _Depends: 8.1_

- [ ] 8.4 Windows 配布 zip と locale variant を検証可能にする
  - publish 成果物を zip 化し、展開後も実行ファイルから manifest と資産を読める
  - locale 別 `.klib` variant を含む成果物で選択 locale の script が使われる
  - zip 展開先から runtime package resolver が同じ manifest を解決できる
  - _Requirements: 10.1, 10.2, 10.4_
  - _Boundary: CLI publish packaging_
  - _Depends: 2.3, 8.3_

- [ ] 9. 自動テストと WinUI UI 検証を整備する
- [ ] 9.1 Runtime Core の manifest、VM、STL 自動テストを追加する
  - manifest reader、package resolver、opcode behavior、opcode coverage、STL syscall coverage を NUnit で検証する
  - `testdata/projects/full-command-sample` を runtime core の package として読み込める
  - `dotnet test` で core tests が CLI tests と同じ solution から実行される
  - _Requirements: 2.1, 2.2, 2.3, 4.1, 4.2, 4.7, 5.1, 5.10_
  - _Boundary: Runtime Core tests_

- [ ] 9.2 (P) Windows host service tests を追加する
  - coordinate mapper、transition controller、input router、save store、diagnostics mapper を UI automation なしで検証する
  - 16:9/非 16:9、invalid transition、input mapping、user data separation、debug/profile をテストする
  - Windows host service tests が headless CI で実行可能な範囲を明確に分けて通る
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.6, 3.7, 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.7, 8.7, 8.8, 9.1, 9.2, 9.3, 9.4_
  - _Boundary: Windows Runtime tests_
  - _Depends: 5.3, 6.2, 7.2_

- [ ] 9.3 (P) CLI run/publish integration tests を追加する
  - `kes run` の process 起動引数、manifest 受け渡し、終了コード伝播を fake で検証する
  - `kes publish --target windows` の folder と zip に runtime、manifest、`.klib`、assets が含まれることを検証する
  - source 非同梱の publish 成果物でも runtime 入力が揃うことを確認できる
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 10.1, 10.2, 10.3, 10.4, 10.5_
  - _Boundary: CLI integration tests_
  - _Depends: 8.2, 8.4_

- [ ] 9.4 WinUI UI automation script と screenshot 確認を追加する
  - `winapp ui` を使う batch UI test script を追加し、message、choice、system menu、backlog、save/load、settings、fullscreen、debug overlay を確認する
  - UI test は AutomationId を使い、主要状態の screenshot を保存して視覚崩れを確認できる
  - UI test 結果の JSON と screenshot で pass/fail が判断できる
  - _Requirements: 3.5, 7.1, 7.2, 7.5, 7.7, 7.8, 8.1, 8.3, 8.5, 8.6, 9.1, 9.2, 9.3_
  - _Boundary: WinUI UI testing_
  - _Depends: 5.4, 6.3, 7.1_

- [ ] 9.5 Audio と end-to-end sample の手動検証手順を自動化寄りに整える
  - sample project で BGM、SE、Voice、voice 欠落 warning、skip 時 voice stop、volume 変更を確認できる test fixture または scripted check を追加する
  - full-command sample を build、run、publish の流れで検証できる手順を CI またはローカル test artifact として残す
  - 音声と sample end-to-end の確認結果がテストログまたは検証ログに残る
  - _Requirements: 5.6, 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7, 10.1, 10.2_
  - _Boundary: Runtime validation_
  - _Depends: 6.1, 8.4_

- [ ] 10. 最終統合と完了検証を行う
- [ ] 10.1 Runtime Core、Windows app、CLI を統合して full-command sample を実行する
  - build 済み sample manifest から runtime が entry `.klib` を読み込み、標準 UI で進行できる
  - VM、STL effect、Windows host、audio、save、diagnostics、CLI run が同じ実行契約で接続される
  - sample の通常起動、debug 起動、profile 起動がそれぞれ期待する観測結果を返す
  - _Requirements: 1.1, 2.1, 3.5, 4.3, 5.1, 7.8, 8.1, 9.3, 9.4_
  - _Boundary: Runtime integration_
  - _Depends: 3.5, 4.5, 5.4, 6.4, 7.3, 8.2_

- [ ] 10.2 Release build と Windows publish 成果物を検証する
  - WinUI plugin の手順に沿って Release build は `BuildAndRun.ps1 -SkipRun` 相当で確認し、runtime launch は `winapp run` を使う
  - publish folder と zip を展開し、実行ファイルから `data/manifest.json` と資産を読み込める
  - Release build、publish layout、zip 展開後 package 解決が検証ログで確認できる
  - _Requirements: 1.2, 10.1, 10.2, 10.3, 10.4, 10.5_
  - _Boundary: Release validation_
  - _Depends: 8.4, 9.3_

- [ ] 10.3 全テスト、UI 検証、ADR 棚卸しを完了する
  - `dotnet test` で CLI、Runtime Core、Windows host の自動テストを実行する
  - WinUI UI automation と screenshot checklist の結果を確認し、失敗があれば修正対象として戻す
  - 実装判断が ADR 対象か棚卸しし、必要なら `docs/adr/` に記録する
  - _Requirements: 1.1, 2.4, 3.7, 4.7, 5.10, 6.5, 8.5, 9.7, 10.2_
  - _Boundary: Completion verification_
  - _Depends: 9.1, 9.2, 9.3, 9.4, 9.5, 10.2_
