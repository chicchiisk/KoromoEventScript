# Implementation Plan

- [ ] 1. Foundation: `kes run` の共有契約を整える
- [x] 1.1 Run option model と終了コードを project-first 仕様へ更新する
  - `RunBuildMode` で `Always`、`Never`、`IfStale` を表現し、`RunCommandOptions` から manifest 直接指定を取り除く。
  - `RunCommandOptions` に target と build mode を持たせ、runtime arguments は既存と同じ順序保持の契約にする。
  - `CliExitCode` に runtime 起動失敗用の `7` を追加し、既存終了コードの値を変えない。
  - 完了時には `kes run` の内部 option が `ProjectDirectory`、`Target`、`BuildMode`、runtime options だけで実行条件を表せる。
  - _Requirements: 2.4, 4.1, 4.3, 4.4, 7.3_
  - _Boundary: RunCommandOptions, RunBuildMode, CliExitCode_

- [x] 1.2 CLI parser を `kes run [PROJECT_DIR]` の仕様へ合わせる
  - `--target windows` と target 省略を受け付け、`windows` 以外は command line diagnostic にする。
  - `--build` と `--no-build` を排他として解析し、同時指定時は runtime を起動しない parse error にする。
  - `--manifest` は unsupported option として扱い、project-first 実行へ暗黙変換しない。
  - `--` 以降の runtime arguments は CLI で解釈せず、`RunCommandOptions` に順序どおり保持される。
  - 完了時には `CliApplicationTests` で parser の成功・失敗ケースを直接検証できる。
  - _Requirements: 2.4, 4.1, 4.2, 4.5, 6.7_
  - _Boundary: CliApplication.ParseRun_

- [ ] 2. Core: run 用の解決・検証コンポーネントを実装する
- [x] 2.1 (P) project root と entry 解決を実装する
  - `PROJECT_DIR` 省略時は現在ディレクトリから親方向に `kes.xml` を探索する。
  - `PROJECT_DIR` 指定時は指定ディレクトリ直下の `kes.xml` を使い、`.kc` / `.kel` / その他ファイル指定は廃止済み入力として診断する。
  - `Project.Entry` の未指定、不正 `kes.xml`、entry ファイル不在を runtime 起動前の diagnostic として返す。
  - 完了時には成功結果が project root、`ProjectConfig`、entry path、entry full path、Windows manifest path を保持する。
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 2.1, 2.2, 2.3, 3.1, 3.2, 3.3_
  - _Boundary: RunProjectInputResolver, RunProjectInput_
  - _Depends: 1.1_

- [x] 2.2 (P) manifest 読み取りと成果物存在検証を実装する
  - build が生成した `manifest.json` を run 側で読み取り、JSON 不正や読み取り不能を diagnostic に変換する。
  - Windows target の manifest であることを確認し、manifest directory から `scripts[].klibPath` を解決する。
  - manifest 不在または `.klib` 不在のときは build を行わず runtime 起動前に停止できる結果を返す。
  - 完了時には writer が生成した manifest を reader が読み、必要 `.klib` の存在を検証できる。
  - _Requirements: 5.1, 5.2, 5.3, 5.5_
  - _Boundary: BuildManifestReader, RunArtifactValidator_
  - _Depends: 1.1_

- [x] 2.3 stale 判定を実装する
  - manifest 不在または `.klib` 不足を stale として扱う。
  - `kes.xml`、entry `.kel`、`EventsPath` 配下 `.kc`、`AssetsPath`、`LocalePath` 配下ファイルを入力候補として列挙する。
  - 入力候補が manifest または `.klib` より新しい場合だけ既定 run の build が必要であると判定する。
  - 読み取り不能な入力は stale ではなく file diagnostic として返す。
  - 完了時には fresh / stale / file error の 3 状態を unit test で区別できる。
  - _Requirements: 4.6, 5.4_
  - _Boundary: RunStalenessChecker_
  - _Depends: 2.1, 2.2_

- [ ] 2.4 (P) runtime 起動対象解決と起動引数構築を分離する
  - 既存の Windows runtime exe / csproj 探索順を専用 resolver に移す。
  - runtime 引数は `--manifest <path>` を先頭にし、locale/start/fullscreen/width/height/debug/profile を指定時のみ追加する。
  - `--` 以降の runtime arguments を順序どおり末尾へ渡す。
  - csproj 起動時は既存の `dotnet run --project ... -- --args <serialized>` 形式と escaping を維持する。
  - 完了時には exe 起動と csproj 起動の `ProcessLaunchRequest` を runtime process なしで検証できる。
  - _Requirements: 5.1, 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7, 7.3_
  - _Boundary: RuntimeCommandResolver, RuntimeLaunchAdapter_
  - _Depends: 1.1_

- [ ] 3. Integration: `RunCommand` に実行フローを統合する
- [ ] 3.1 build 方針と成果物検証を `RunCommand` に統合する
  - project input 解決後、`Always` では必ず Windows build、`Never` では build せず artifact validation、`IfStale` では stale のときだけ build を行う。
  - build failure は `BuildPipelineService` の diagnostics と exit code をそのまま返し、runtime を起動しない。
  - fresh 判定後または `--no-build` 時には manifest と `.klib` 検証を通過した場合だけ runtime 起動へ進む。
  - 完了時には build mode ごとの到達先が process launcher stub で観測できる。
  - _Requirements: 4.3, 4.4, 4.6, 4.7, 5.1, 5.5, 7.4, 7.5_
  - _Boundary: RunCommand_
  - _Depends: 2.1, 2.2, 2.3, 2.4_

- [ ] 3.2 runtime 起動結果と起動失敗の終了コードを統合する
  - process が起動した後は正常・非ゼロを問わず runtime の終了コードを CLI の終了コードとして返す。
  - process start failure、runtime executable 起動不能、csproj 起動不能は runtime 起動エラー diagnostic と終了コード `7` に変換する。
  - project / build / artifact の起動前 error はそれぞれの処理段階の終了コードを維持する。
  - 完了時には runtime 起動失敗と runtime 非ゼロ終了が別の結果としてテストで区別できる。
  - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_
  - _Boundary: RunCommand, ProcessLauncher_
  - _Depends: 3.1_

- [ ] 4. Validation: CLI 仕様差分を自動テストで固定する
- [ ] 4.1 parser と廃止済み入力の CLI テストを追加・更新する
  - `--target windows`、unknown target、`--build` / `--no-build` 同時指定、`--manifest` unsupported を検証する。
  - `.kc`、`.kel`、その他ファイル指定が project root として扱われず診断されることを検証する。
  - `--` 以降の runtime arguments が CLI parse で保持されることを検証する。
  - 完了時には `CliApplicationTests` が project-first run の parse 境界を固定している。
  - _Requirements: 2.4, 3.1, 3.2, 3.3, 4.1, 4.2, 4.5, 6.7_
  - _Boundary: CliApplicationTests_
  - _Depends: 1.2, 2.1_

- [ ] 4.2 run 用 core component の unit test を追加する
  - project root 探索、明示 project root、`kes.xml` 不在、不正 `kes.xml`、entry 不在を検証する。
  - manifest 読み取り、manifest 不在、target mismatch、`.klib` 不足、writer 出力 manifest の round-trip を検証する。
  - fresh / stale / file error の stale 判定を検証する。
  - 完了時には `RunProjectInputResolverTests`、`RunArtifactValidatorTests`、`RunStalenessCheckerTests` が各 component 境界を単独で検証している。
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 2.1, 2.2, 2.3, 5.2, 5.3, 5.4, 5.5_
  - _Boundary: Commands.Run unit tests_
  - _Depends: 2.1, 2.2, 2.3_

- [ ] 4.3 runtime adapter と process 起動境界の unit test を追加する
  - locale/start/fullscreen/width/height/debug/profile が指定時だけ runtime args に現れることを検証する。
  - runtime arguments passthrough の順序保持を検証する。
  - exe 起動と csproj 起動の request、空白・引用符・backslash を含む serialized args を検証する。
  - 完了時には runtime process を起動せずに全 runtime 起動引数契約を検証できる。
  - _Requirements: 5.1, 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7, 7.3_
  - _Boundary: RuntimeLaunchAdapterTests, RuntimeCommandResolverTests_
  - _Depends: 2.4_

- [ ] 4.4 `RunCommand` の integration test を project-first 仕様へ更新する
  - `--build` が build 後に runtime を起動すること、`--no-build` が build せず既存成果物を検証することを確認する。
  - 既定 fresh では build しないこと、既定 stale では build することを確認する。
  - build failure、artifact failure、runtime launch failure、runtime 非ゼロ終了の終了コードを確認する。
  - 完了時には `RunCommandTests` が `--manifest` 直接指定に依存せず、project-first flow を end-to-end に近い形で固定している。
  - _Requirements: 4.3, 4.4, 4.6, 4.7, 5.1, 5.2, 5.3, 5.5, 7.1, 7.2, 7.3, 7.4, 7.5_
  - _Boundary: RunCommandTests_
  - _Depends: 3.1, 3.2_

- [ ] 4.5 full-command-sample の smoke 経路を確認する
  - `testdata/projects/full-command-sample` を project root として、既存成果物ありの `--no-build` 経路が runtime launch request へ到達することを確認する。
  - build 成果物がない既定実行で build 後 runtime launch request へ到達することを確認する。
  - 完了時には sample project を使った `kes run` の代表経路が自動テストで回帰検出できる。
  - _Requirements: 1.1, 1.2, 2.1, 4.3, 4.4, 4.6, 5.1, 7.1_
  - _Boundary: CLI smoke tests_
  - _Depends: 4.4_
