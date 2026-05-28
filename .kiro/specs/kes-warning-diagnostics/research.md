# Research & Design Decisions

## Summary

- **Feature**: `kes-warning-diagnostics`
- **Discovery Scope**: Extension
- **Key Findings**:
  - `DiagnosticLevel.Warning` と formatter / sink の warning 出力は既に存在する。
  - `CliExitCode` は `0` から `6` までしか持たず、仕様上の warning-as-error 終了コード `9` は未実装である。
  - `BuildCommandOptions` は project directory と output format のみを持ち、`--warnings-as-errors` と `kes.xml` の `Build.WarningsAsErrors` はまだ build flow に渡っていない。

## Research Log

### 既存診断出力

- **Context**: 1.1-1.5 の warning 出力が既存基盤で満たせるか確認した。
- **Sources Consulted**: `DiagnosticLevel.cs`, `DiagnosticFormatter.cs`, `DiagnosticSink.cs`, `DiagnosticFormatterTests.cs`, `DiagnosticSinkTests.cs`
- **Findings**:
  - `DiagnosticLevel.Warning` は存在する。
  - text 出力は `warning`、JSON Lines は `"level":"warning"` を既に生成できる。
  - `DiagnosticSink` は diagnostic level に関係なく指定 writer へ出力する。
- **Implications**: formatter / sink の contract は維持し、build check-only が warning diagnostic を渡せるようにする。

### CLI と build check-only の設定伝搬

- **Context**: 2.1-2.4 の warnings-as-errors をどこで受け取り、どこで適用するか確認した。
- **Sources Consulted**: `CliApplication.cs`, `BuildCommandOptions.cs`, `ProjectConfigLoader.cs`, `ProjectConfig.cs`, `docs/spec/cli-tool-spec.md`, `docs/spec/kes-config.xsd`
- **Findings**:
  - `CliApplication.Parse` は `--warnings-as-errors` を未対応 option として扱う。
  - `ProjectConfigLoader` は `Build.WarningsAsErrors` を読んでいない。
  - `BuildCheckOnlyCommand.Execute` は `BuildCommandOptions` と `ProjectConfig` を受け取るため、CLI option と project config の合成点として自然である。
- **Implications**: CLI option と project config の boolean を `BuildCheckOnlyCommand` で OR 合成し、warning policy へ渡す。

### 終了コード集約

- **Context**: 3.1-4.4 の終了コード優先順位を既存 flow にどう接続するか確認した。
- **Sources Consulted**: `CliExitCode.cs`, `BuildCheckOnlyCommand.cs`, `SemanticModels.cs`, `BuildCheckOnlyCommandTests.cs`
- **Findings**:
  - 既存 flow は stage failure の結果を `BuildCheckOnlyResult.ExitCode` で返す。
  - syntax / file I/O / compile error は warning policy より前に非 `Success` として返る。
  - warning-only を `Success` として保持し、最後に policy で `WarningsAsErrors` へ昇格する形が既存 stage ordering を壊さない。
- **Implications**: `WarningPolicy` は `ExitCode == Success` の場合だけ `WarningAsError` へ昇格し、それ以外の既存エラー終了コードを上書きしない。

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| Build flow 内に直接分岐 | `BuildCheckOnlyCommand` に warning 判定を直接書く | 変更ファイルが少ない | 終了コード規則の単体テストがしづらく、後続 feature が再利用しにくい | 却下 |
| Warning policy service | 診断列、現在の終了コード、設定から最終終了コードを決定する小さな service を置く | 責務が明確で単体テストしやすい | 新規ファイルが増える | 採用 |
| Diagnostic level を Error へ変換 | warnings-as-errors 時に warning diagnostic 自体を error に変える | 一見単純 | 要件 3.4 / 5.2 に反する | 却下 |

## Design Decisions

### Decision: 警告昇格は診断変換ではなく終了コード変換に限定する

- **Context**: warnings-as-errors は警告を失敗扱いにするが、出力される診断 level は `warning` のまま維持する必要がある。
- **Alternatives Considered**:
  1. warning diagnostic を error diagnostic に変換する。
  2. diagnostic は不変のまま、最終終了コードだけを `9` に昇格する。
- **Selected Approach**: `WarningPolicy` が warning diagnostic の有無と warnings-as-errors flag を見て、`Success` のみを `WarningsAsErrors` に変換する。
- **Rationale**: 既存 formatter と downstream diagnostics の互換性を維持できる。
- **Trade-offs**: CLI の戻り値だけを見た利用者には失敗として見えるが、診断本文は warning 表記のままになる。
- **Follow-up**: JSON Lines と text の両方で `warning` が維持されることをテストする。

### Decision: 最小の warning producer として空 `.ke` ドキュメント警告を追加する

- **Context**: build check-only 統合テストで warning-only path を観測するには、実際の semantic warning source が必要である。
- **Alternatives Considered**:
  1. test 専用 fake diagnostic を build flow に注入する。
  2. 素材参照や未使用変数のような広い静的解析を実装する。
  3. 空 `.ke` ドキュメントを warning とする最小 semantic analyzer を追加する。
- **Selected Approach**: `WarningAnalyzer` が空の `.ke` ドキュメントに `KES4001` を出す。
- **Rationale**: 追加範囲が小さく、runtime / 素材 manifest / 完全な lint へ踏み込まず warning 出力を実証できる。
- **Trade-offs**: 将来の lint rule と比べると限定的だが、Issue #23 の warning pipeline 検証には十分である。
- **Follow-up**: 追加 warning が compile error を上書きしないことを確認する。

## Risks & Mitigations

- `CliExitCode` に `9` を追加すると数値順の「小さい exit code 優先」実装と相性が悪い — warning policy は既存 error を上書きせず、`Success` の場合だけ昇格する。
- `Build.WarningsAsErrors` の XML 読み取りで既存 config 読み込みを壊す可能性がある — 未指定は `false` として扱い、既存 fixture の互換性を保つ。
- warning producer が広がりすぎる可能性がある — この仕様では空 `.ke` ドキュメントの `KES4001` に限定する。

## References

- `docs/spec/cli-tool-spec.md` — warning diagnostics、`--warnings-as-errors`、終了コード `9` の公開仕様。
- `docs/spec/kes-config.xsd` — `Build.WarningsAsErrors` の XML schema。
