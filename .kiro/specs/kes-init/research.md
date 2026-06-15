# Research & Design Decisions

## Summary

- **Feature**: `kes-init`
- **Discovery Scope**: Extension
- **Key Findings**:
  - 公開 CLI 仕様は `kes init` の引数形、`basic` / `empty` テンプレート、標準ディレクトリ構成、`--force` / `--no-sample` の挙動をすでに定義しているため、設計はこの契約に従う必要がある。
  - 既存 CLI 実装は `CliApplication` が `build` 系だけを手動でパースする薄い構成で、`init` 追加も同じ「薄いルータ + 専用コマンド」パターンで拡張するのが最小変更になる。
  - `testdata/projects/minimal` は `kes.xml` とサンプルイベントを持つが、公開仕様の `assets/` 配下サブディレクトリまでは含んでいないため、`kes init` の雛形は実装側で正規定義し、テストは仕様準拠の期待ツリーを個別に検証する必要がある。

## Research Log

### `kes init` の公開契約

- **Context**: requirements を公開仕様へ合わせたため、設計も同じ契約へ揃える必要がある。
- **Sources Consulted**: `docs/spec/cli-tool-spec.md`, `docs/spec/kes-config.xsd`, `docs/testing-strategy.md`
- **Findings**:
  - `kes init [PROJECT_DIR] [options]` が公開コマンド形である。
  - `--name`、`--template <basic|empty>`、`--force`、`--no-sample` が公開オプションとして定義されている。
  - 標準構成には `kes.xml`、`events/`、`assets/`、`locale/`、`build/`、`dist/` と、`assets/` 配下の `bg/`、`actor/`、`voice/`、`se/`、`bgm/` が含まれる。
  - `basic` テンプレートは `events/main.kel` と `events/chapter001.kc` を生成し、`--force` なしで既存ファイルがあればエラーとする。
- **Implications**:
  - 設計はテンプレート選択、標準ディレクトリ生成、衝突判定、サンプル生成有無を明示的に扱う。
  - `kes.xml` の内容は `kes-config.xsd` と `build --check-only` が期待する構成に揃える必要がある。

### 既存 CLI 実装の拡張点

- **Context**: `kes init` をどの層へ追加するか、既存実装の責務境界を確認したい。
- **Sources Consulted**: `source/cli/KoromoEventScript.Cli/Commands/CliApplication.cs`, `source/cli/KoromoEventScript.Cli/Commands/Build/*`, `source/cli/KoromoEventScript.Cli/Diagnostics/*`, `tests/KoromoEventScript.Cli.Tests/Commands/*`
- **Findings**:
  - `CliApplication` は現在 `build` のみを受け付ける手書きパーサで、`DiagnosticSink` と `CliExitCode` に依存している。
  - `BuildCheckOnlyCommand` / `BuildCommand` は「引数解釈済みの typed options を受け取って処理を実行する」形で統一されている。
  - CLI テストは `CliApplication.Run(...)` を直接呼ぶ統合寄りテストと、個別コマンドの単体テストを併用している。
- **Implications**:
  - `kes init` も `InitCommandOptions` と `InitCommand` を追加し、`CliApplication` はルーティングと引数解釈に留める。
  - 診断出力と終了コードは既存の `DiagnosticSink` / `CliExitCode` を再利用し、新しい出力経路は増やさない。

### 雛形内容と既存 fixture の差分

- **Context**: 生成ファイルをどこから定義するか、既存 fixture をそのまま再利用できるかを確認したい。
- **Sources Consulted**: `testdata/projects/minimal/kes.xml`, `testdata/projects/minimal/events/main.kel`, `testdata/projects/minimal/events/chapter001.kc`, `tests/KoromoEventScript.Cli.Tests/TemporaryProject.cs`
- **Findings**:
  - `testdata/projects/minimal` の `kes.xml` とイベントファイルは `build --check-only` 成功系に使われており、`basic` テンプレートの参考値として有用である。
  - 一方で fixture には `assets/bg` など公開仕様の素材サブディレクトリが存在しない。
  - テスト用の `TemporaryProject` は任意のファイルツリーを簡単に構築できるため、`kes init` でも期待ディレクトリ構成をテスト内で明示できる。
- **Implications**:
  - 実装は fixture ディレクトリをコピーするのではなく、コード内で正規の scaffold を組み立てる。
  - テストは fixture の丸コピー比較ではなく、必要ファイル・必要ディレクトリ・サンプル内容・`build --check-only` 成功を個別に検証する。

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| `CliApplication` に初期化処理を直接書く | 引数解釈とファイル生成を 1 ファイルへ集約する | ファイル数が少ない | ルーティング、雛形生成、衝突判定が密結合になり、テストしづらい | 不採用 |
| 薄い CLI ルータ + 専用 `InitCommand` + scaffold service | `CliApplication` は引数解釈、`InitCommand` は orchestration、scaffold service は生成責務を持つ | 既存 `build` 系パターンと整合し、単体テストしやすい | 少数の新規型が増える | 採用 |
| 外部 CLI パーサ導入 | `System.CommandLine` などへ置き換える | 将来の CLI 拡張に強い | 依存追加と既存 `build` ルートの再設計が必要 | 今回の scope を超えるため不採用 |

## Design Decisions

### Decision: `kes init` は既存 CLI パターンのまま拡張する

- **Context**: 既存 CLI は `CliApplication` が引数を解釈し、typed command へ渡す薄い構成である。
- **Alternatives Considered**:
  1. `CliApplication` にファイル生成まで直接実装する。
  2. `CliApplication` はルーティングのみに留め、`InitCommand` へ委譲する。
- **Selected Approach**: `CliApplication` に `init` ルートと専用引数解釈を追加し、実処理は `InitCommand` と scaffold 関連 service へ委譲する。
- **Rationale**: 既存 `build` 系との一貫性を保ちつつ、引数解釈とファイルシステム副作用を分離できる。
- **Trade-offs**: 新規型は増えるが、テスト境界が明確になる。
- **Follow-up**: 実装時は `build` の既存挙動を壊さないことを `CliApplicationTests` で再確認する。

### Decision: 雛形は in-memory scaffold として組み立ててから一括書き込みする

- **Context**: `basic` / `empty`、`--name`、`--no-sample`、`--force` を組み合わせると生成対象が分岐する。
- **Alternatives Considered**:
  1. 書き込み処理の中で都度文字列とディレクトリを分岐生成する。
  2. 先に scaffold model を組み立て、その後で衝突判定と書き込みを行う。
- **Selected Approach**: `ProjectScaffoldFactory` が最終的なディレクトリ一覧とファイル内容一覧を組み立て、`ProjectScaffoldWriter` がそれを検証・書き込みする。
- **Rationale**: 生成内容の整合性、`--force` 時の差分適用、テストでの期待比較を単純化できる。
- **Trade-offs**: 小さな model 型が追加で必要になる。
- **Follow-up**: scaffold model は「生成対象の物理構造」だけを表し、将来テンプレート管理へ拡張できるようにする。

### Decision: 既定プロジェクト名は target directory のベース名から決める

- **Context**: 公開仕様は `--name` の存在を定義しているが、省略時の既定名は明文化していない。
- **Alternatives Considered**:
  1. 固定名を使う。
  2. 対象ディレクトリのベース名を既定名に使う。
- **Selected Approach**: `--name` 省略時は、解決済み project root のディレクトリ名を `Project.Name` へ使う。
- **Rationale**: ユーザーが `kes init MyGame` や `kes init .` を実行したときの期待に最も自然で、追加入力を要求しない。
- **Trade-offs**: 公開仕様に明文化されていない既定規則を実装側で固定することになる。
- **Follow-up**: 実装 PR では CLI 仕様書側にも既定名規則の追記要否を確認する。

### Decision: `--force` は managed scaffold の上書きに限定し、未知ファイルの削除は行わない

- **Context**: 公開仕様は `--force` を「既存ファイルの上書きを許可する」とだけ定義している。
- **Alternatives Considered**:
  1. 対象ディレクトリをまるごと初期化し直し、未知ファイルも消す。
  2. scaffold が管理するファイルだけを上書きし、未知ファイルは保持する。
- **Selected Approach**: `ProjectScaffoldWriter` は scaffold が所有するファイルの内容だけを上書きし、未知ファイルや未知ディレクトリの削除は行わない。
- **Rationale**: 利用者の既存作業物を壊さず、安全側に倒せる。
- **Trade-offs**: 完全にクリーンな再初期化ではない。
- **Follow-up**: 衝突判定は「要求される directory path に file がある」「要求される file path に既存 file があり force なし」のケースを明示的にテストする。

## Risks & Mitigations

- 公開仕様に `--name` 省略時の既定名規則が明文化されていない — 設計で target directory ベース名へ固定し、仕様追記の再確認を revalidation trigger に含める。
- `testdata/projects/minimal` をそのまま template source に使うと公開仕様の素材サブディレクトリ不足を取り込んでしまう — scaffold content は実装側で正規生成し、fixture は content reference と検証用に限定する。
- `--force` の解釈が広すぎるとユーザーファイル破壊の危険がある — overwrite only の方針に限定し、削除を伴う処理は設計境界外とする。

## References

- `docs/spec/cli-tool-spec.md` — `kes init` の公開コマンド仕様、標準構成、オプション。
- `docs/spec/kes-config.xsd` — 生成する `kes.xml` の必須属性と shape。
- `source/cli/KoromoEventScript.Cli/Commands/CliApplication.cs` — 既存 CLI ルーティングの実装形。
- `testdata/projects/minimal/` — `basic` テンプレート相当の参考イベント内容。
