# Research & Design Decisions

## Summary

- **Feature**: `kes-correct-auto-tagging`
- **Discovery Scope**: Extension
- **Key Findings**:
  - 既存 CLI は `CliApplication` が手動パースで `build` / `init` だけを振り分けており、`kes correct` 追加時は同じ入口にコマンド分岐を追加する必要がある。
  - `BuildPreparationService` は `kes.xml` 解決、entry `.kel` 解決、参照 `.kc` parse、semantic 解析までを既に一括提供しており、`kes correct` の前段解析に近い。
  - 現在の AST は `say` / `nar` / `case` のタグ情報を保持しているが、テキスト書き戻しやタグ補完計画を扱う専用コンポーネントは存在しない。

## Research Log

### 既存 CLI コマンド拡張点

- **Context**: `kes correct` をどこに追加するか確認する必要があった。
- **Sources Consulted**:
  - `source/cli/KoromoEventScript.Cli/Commands/CliApplication.cs`
  - `source/cli/KoromoEventScript.Cli/Commands/Build/BuildCommand.cs`
  - `source/cli/KoromoEventScript.Cli/Commands/Init/InitCommand.cs`
- **Findings**:
  - `CliApplication` が引数の手動パースとコマンドディスパッチを集中管理している。
  - `build --check-only` は独立コマンドではなく `BuildCheckOnlyCommand` への分岐として実装されている。
  - 既存パターンに従うなら、`correct` も `CliApplication` 配下で options を組み立てて専用 command へ委譲する形が自然。
- **Implications**:
  - `kes correct` は `Commands/Correct/` 配下へ新設し、`CliApplication` に parse/dispatch を追加する。
  - コマンドラインエラーは既存の `KES9001` / `CliExitCode.CommandLineError` 契約へ揃える。

### 既存の解析・意味解析パイプライン

- **Context**: `kes correct` が build 相当の事前解析を必要とするため、再利用可能な層を確認したかった。
- **Sources Consulted**:
  - `source/cli/KoromoEventScript.Cli/Build/BuildPreparationService.cs`
  - `source/cli/KoromoEventScript.Cli/Build/SourceFileParser.cs`
  - `source/cli/KoromoEventScript.Cli/Build/KelScriptReferenceResolver.cs`
  - `source/cli/KoromoEventScript.Cli/Semantics/SemanticAnalyzer.cs`
  - `source/cli/KoromoEventScript.Cli/Semantics/SemanticModels.cs`
- **Findings**:
  - `BuildPreparationService` は project root 解決、`kes.xml` load、entry `.kel` parse、chapter 参照解決、`.kc` parse、semantic 解析までを提供している。
  - `SemanticAnalysisResult.ImportGraph.OrderedDocuments` から、参照順に `ScriptDocument` を取得できる。
  - ただし build 専用 options 型に依存しており、`kes correct` からそのまま呼ぶには境界が不自然。
- **Implications**:
  - `kes correct` と将来の `kes build` が共有できるよう、project/script 解析だけを担う共通準備サービスへ抽出する設計が妥当。
  - build 固有の warning policy や artifact 出力は既存 `BuildCommand` 側へ残す。

### AST とタグ補完対象

- **Context**: 自動タグ補完がどの構文を直接扱えるかを確認したかった。
- **Sources Consulted**:
  - `source/cli/KoromoEventScript.Cli/Parsing/SyntaxNodes.cs`
  - `source/cli/KoromoEventScript.Cli/Parsing/KeParser.cs`
  - `source/cli/KoromoEventScript.Cli/Semantics/DefinitionCollector.cs`
  - `docs/spec/cli-tool-spec.md`
  - `docs/spec/localization-dictionary-spec.md`
- **Findings**:
  - AST には `SayStatementSyntax.Tag`、`NarStatementSyntax.Tag`、`CaseClauseSyntax.Tag` がある。
  - `SelectStatementSyntax` 自体のタグは現実装 AST に存在しないが、本 feature の自動補完対象は `select-case` であり、requirements と矛盾しない。
  - `DefinitionCollector` は既存タグを legacy symbol として収集しており、タグ重複や未定義ジャンプとの干渉に注意が必要。
- **Implications**:
  - 自動補完対象は `say` / `nar` / `case` に限定し、`label` / `jump` / 手動タグ全般は正規化対象に含めない。
  - 既存の自動採番パターンに一致するタグだけを番号衝突回避の参照集合として扱う。

### 書き戻しとテスト資産

- **Context**: `.kc` 書き戻しの責務と検証方法を決めるため。
- **Sources Consulted**:
  - `tests/KoromoEventScript.Cli.Tests/TemporaryProject.cs`
  - `tests/KoromoEventScript.Cli.Tests/Commands/CliApplicationTests.cs`
  - `tests/KoromoEventScript.Cli.Tests/Commands/BuildCheckOnlyCommandTests.cs`
  - `tests/KoromoEventScript.Cli.Tests/Build/SourceFileParserTests.cs`
- **Findings**:
  - 一時プロジェクト作成ヘルパーと CLI 呼び出しテスト基盤は既に存在する。
  - 既存テストは command 単位、build preparation 単位、parser 単位で分かれている。
  - `.kc` 書き戻し後の内容比較は `TemporaryProject.SnapshotFiles()` で容易に検証できる。
- **Implications**:
  - `CorrectCommand` の統合テスト、タグ採番器のユニットテスト、書き戻しレンダラの golden 的テストを分離する。
  - `--check-only` ではファイル不変性を直接検証する。

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| `CorrectCommand` が build 用準備サービスを直接流用 | `BuildPreparationService` と `BuildCommandOptions` をそのまま使う | 実装量が少ない | `correct` が build 向け option/責務に依存し、将来の build/correct 共有境界が曖昧になる | 不採用 |
| 共通の script 準備サービスを抽出し、build/correct が共有 | project 解決と `.kel` / `.kc` / semantic 解析だけを共通化する | 既存処理を再利用しつつ責務が明確 | 既存 build 実装に小さなリファクタが必要 | 採用 |
| parser から直接書き戻しまでを `CorrectCommand` に内包 | command が parse・採番・書込を全部持つ | ファイル数は少ない | テストしにくく、build からの再利用点が消える | 不採用 |

## Design Decisions

### Decision: 解析前段は `kes correct` / `kes build` 共通の準備サービスへ抽出する

- **Context**: `kes correct` は build と同等の project/script/semantic 解決を必要とする。
- **Alternatives Considered**:
  1. `BuildPreparationService` を `correct` から直接呼ぶ
  2. 共通の準備サービスを抽出する
- **Selected Approach**: project root 解決、config load、entry `.kel` parse、参照 `.kc` 解決、semantic 解析を返す共通サービスを導入し、build/correct の両方から呼ぶ。
- **Rationale**: requirements 1, 2 を満たしつつ、次の `kes build` 実装で `correct` 相当処理を組み込みやすい境界を先に整えられる。
- **Trade-offs**: 既存 build コードの参照先変更が発生するが、重複解析ロジックを避けられる。
- **Follow-up**: build 系テストが共通準備サービスの抽出後も維持されることを確認する。

### Decision: タグ補完計画と書き戻しを分離する

- **Context**: `--check-only` ではファイルを変更せず、予定タグ一覧だけを出力する必要がある。
- **Alternatives Considered**:
  1. 解析しながら即時に AST とファイルを書き換える
  2. 先に補完計画を作り、適用と出力を後段で切り替える
- **Selected Approach**: `TagAssignmentPlanner` が対象と採番結果を `TagAssignmentPlan` として生成し、`CorrectCommand` はそれを `check-only` 出力または `.kc` 書き戻しへ分岐する。
- **Rationale**: 同じ計画を `check-only` と実書き戻しで共有でき、要求 4 を素直に満たせる。
- **Trade-offs**: 補完対象の位置情報を保持する中間モデルが必要になる。
- **Follow-up**: plan 表現に source line/column と project relative path を含め、診断やレビュー出力に再利用できるようにする。

### Decision: 書き戻しは全文 pretty-print ではなく、局所編集ベースで行う

- **Context**: `kes correct` の責務は不足タグの補完と最小限の整形であり、DSL 全体の formatter ではない。
- **Alternatives Considered**:
  1. AST から `.kc` を全面再生成する
  2. 元ファイルを保持し、タグ不足箇所だけを挿入する
- **Selected Approach**: 元ファイルテキストを読み込み、`say` / `nar` / `case` のタグ挿入位置へ局所編集を適用する書き戻しエンジンを採用する。
- **Rationale**: 既存のコメント、空行、作者の整形意図を壊しにくく、spec の「書き戻し整形」責務を最小コストで満たせる。
- **Trade-offs**: 行・列とテキストオフセットの変換、および複数変更の適用順序管理が必要になる。
- **Follow-up**: source location から offset を引くユーティリティを独立させ、後続の `kes loc` / `kes build` でも再利用可能にする。

## Risks & Mitigations

- `BuildPreparationService` 抽出時に build の回帰を起こすリスク — 共通準備サービスの導入後も既存 build 系 command テストを維持する。
- 局所編集で複数タグを同一ファイルへ適用する際の offset ずれ — 変更計画を降順 offset で適用する設計にする。
- 既存手動タグと自動採番タグの衝突判定が曖昧になるリスク — 自動採番パターンに一致するタグだけを正規化して番号予約集合へ入れる。

## References

- `docs/spec/cli-tool-spec.md` — `kes correct` の公開オプション、挙動、採番規則
- `docs/spec/localization-dictionary-spec.md` — 自動採番タグが downstream の辞書仕様で前提になっていることの確認
- `source/cli/KoromoEventScript.Cli/Commands/CliApplication.cs` — 既存 CLI コマンド分岐
- `source/cli/KoromoEventScript.Cli/Build/BuildPreparationService.cs` — 既存の project/script 準備処理
