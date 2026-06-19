# Research & Design Decisions

## Summary

- **Feature**: `kes-loc-dictionary-export`
- **Discovery Scope**: Extension
- **Key Findings**:
  - 既存 CLI は `CliApplication` で `build` / `correct` / `init` を分岐し、各コマンドは専用 `CommandOptions` と `CommandResult` を持つ。
  - `CorrectCommand` は `ScriptPreparationService`、`TagAssignmentPlanner`、`ScriptRewriteService` を組み合わせてタグ補完と書き戻しを実行しており、`kes loc` でも同じ前処理資産を再利用できる。
  - 公開仕様ではローカライズ辞書 `.csv` の列構成、UTF-8 BOM、既存翻訳保持、`--locale` のマージ規則が明確化されており、`kes loc` はこの契約の唯一の出力責務を持つ。

## Research Log

### CLI コマンド統合ポイント

- **Context**: `kes loc` を既存 CLI にどう追加するかを確認する必要があった。
- **Sources Consulted**:
  - `source/cli/KoromoEventScript.Cli/Commands/CliApplication.cs`
  - `source/cli/KoromoEventScript.Cli/Program.cs`
  - `tests/KoromoEventScript.Cli.Tests/Commands/CliApplicationTests.cs`
- **Findings**:
  - `CliApplication` が引数パース、診断出力形式、コマンド分岐を一元管理している。
  - 各コマンドは `Execute(options, currentDirectory)` で統一されている。
  - 未対応コマンドや不正オプションは `KES9001` の command line diagnostic で失敗する。
- **Implications**:
  - `kes loc` も `Commands/Loc/` 配下に独立コマンドとして追加し、`CliApplication` に分岐とパーサを追加する。
  - 診断の出力形式は既存 `DiagnosticSink` を再利用し、`loc` 専用の出力処理は success message のみとする。

### 既存のタグ補完と解析パイプライン

- **Context**: `kes loc` が `kes correct` 相当の前処理をどう満たすかを把握したかった。
- **Sources Consulted**:
  - `source/cli/KoromoEventScript.Cli/Commands/Correct/CorrectCommand.cs`
  - `source/cli/KoromoEventScript.Cli/Build/ScriptPreparationService.cs`
  - `source/cli/KoromoEventScript.Cli/Localization/TagAssignmentPlanner.cs`
  - `source/cli/KoromoEventScript.Cli/Localization/ScriptRewriteService.cs`
- **Findings**:
  - `ScriptPreparationService` がプロジェクト解決、`kes.xml` 読み込み、entry `.kel` 解析、参照 `.kc` 解析、semantic analysis までを一括で扱う。
  - `TagAssignmentPlanner` は semantic import graph の `OrderedDocuments` を入力にして `say` / `nar` / `select` の未設定タグだけを補完する。
  - `CorrectCommand` は `--check-only` なしで `.kc` に書き戻す設計であり、前処理の責務境界が既に分離されている。
- **Implications**:
  - `kes loc` は独自の解析パイプラインを増やさず、同じ preparation と tag rewrite を呼び出す。
  - ローカライズ辞書抽出は `OrderedDocuments` と更新後のスクリプト整合性を前提に設計する。

### ローカライズ辞書契約

- **Context**: CSV 形式と既存辞書マージ規則を設計に反映する必要があった。
- **Sources Consulted**:
  - `docs/spec/cli-tool-spec.md`
  - `docs/spec/localization-dictionary-spec.md`
  - `docs/spec/kes-language-spec.md`
  - `docs/spec/overview.md`
- **Findings**:
  - `kes loc` は `tag` / `say` / `original` 固定列と可変の言語列を持つ UTF-8 BOM 付き CSV を出力する。
  - 抽出対象は `say` 本文、`nar` 本文、`select` の `case` 文字列であり、`original` には改行やインラインマクロを保持する。
  - 既存辞書がある場合は翻訳列と翻訳内容を保持しつつ、不足行と不足列のみを追加する。
  - `--locale` 省略時は既存辞書の言語列を優先し、既存辞書がなければ基準言語のみを出力する。
- **Implications**:
  - 辞書入出力は専用の document model を持たせ、CSV パースとマージを明示的に扱う。
  - 言語列選択と既存翻訳保持は export service 内で独立した責務として整理する。

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| `LocCommand` から直接 CSV を組み立てる | コマンドに抽出・マージ・保存を集約する | 実装ファイル数が少ない | CLI 境界と辞書ドメイン責務が混ざり、build 連携時に再利用しにくい | 却下 |
| `LocCommand` + Export Service + CSV Repository | コマンドは orchestration、辞書生成はサービス、永続化は repository に分離する | 既存コマンド構造と整合し、テストしやすい | 小さな型が数個増える | 採用 |
| 汎用ローカライズ基盤を先に大きく抽象化する | 将来の `kes build --loc` まで見越した共通化 | 将来拡張の見通しはよい | 現時点では過剰抽象になりやすい | 今回は見送り |

## Design Decisions

### Decision: `kes loc` は既存の script preparation と書き戻し資産を再利用する

- **Context**: requirements 2 系は `kes correct` 相当の前処理を要求している。
- **Alternatives Considered**:
  1. `kes loc` 専用の解析・タグ補完フローを新設する
  2. `CorrectCommand` を内部的に再利用する
  3. `ScriptPreparationService`、`TagAssignmentPlanner`、`ScriptRewriteService` を `LocCommand` から直接再利用する
- **Selected Approach**: `LocCommand` が既存の preparation / planning / rewrite サービスを直接組み合わせ、抽出時には `TagAssignmentPlan` を補助入力として渡す。
- **Rationale**: 既存資産を流用しつつ、`CorrectCommand` の標準出力形式や `--check-only` 契約を loc 側へ持ち込まずに済む。加えて、書き戻し後に全 `.kc` を再 parse しなくても、未設定タグを抽出側で補完済みとして扱える。
- **Trade-offs**: orchestration の重複は一部残るが、不要な共通抽象化と再 parse の追加コストを避けられる。
- **Follow-up**: 将来 `kes build` でも同じ orchestration を使う段階で、共有 coordinator へ昇格する余地を残す。

### Decision: ローカライズ辞書は専用 document model を介してマージする

- **Context**: requirements 3 系と 4 系は CSV 契約と既存翻訳保持を同時に要求している。
- **Alternatives Considered**:
  1. 生の `string[]` / `Dictionary<string,string>` だけで組み立てる
  2. `LocalizationDictionaryDocument` / `LocalizationDictionaryEntry` を導入する
- **Selected Approach**: 辞書全体と行を表す明示的な model を導入し、repository と export service の間の契約にする。
- **Rationale**: 必須列、動的言語列、一意 tag、翻訳保持を型で表現できる。
- **Trade-offs**: 型数は少し増えるが、テストと将来の `build --loc` 連携が容易になる。
- **Follow-up**: `build --loc` 実装時は同じ document model を入力側でも再利用する。

### Decision: 言語列決定は export service 内の policy として閉じる

- **Context**: `--locale` 省略時と指定時で出力対象言語の規則が異なる。
- **Alternatives Considered**:
  1. CLI 引数パース時点で最終言語列を決める
  2. 既存辞書読込後に export service が最終言語列を決める
- **Selected Approach**: 既存辞書の有無を見た後で export service が最終言語列を決める。
- **Rationale**: 既存辞書の列情報を見ないと仕様どおりにマージできないため。
- **Trade-offs**: command layer からは最終言語列が見えにくいが、責務としては自然である。
- **Follow-up**: 成功メッセージに最終出力言語列を含めるかは implementation で判断する。

## Risks & Mitigations

- `kes loc` と `kes correct` が別々にタグ書き戻しを組むことで将来 drift する可能性がある — 同じ planner / rewrite service の再利用を設計上固定する。
- CSV パース不備で既存翻訳を失う可能性がある — repository に必須列検証、一意 tag 検証、保持優先マージのテストを用意する。
- `say` / `nar` / `select` 抽出でテキスト保持仕様を誤る可能性がある — インラインマクロ、改行、複数ページを含む fixture で unit test と command test を用意する。

## References

- [CLI ツール仕様書](../../../docs/spec/cli-tool-spec.md) — `kes loc` のコマンド契約と更新規則
- [ローカライズ辞書仕様書](../../../docs/spec/localization-dictionary-spec.md) — CSV 列構成、文字コード、既存辞書マージ規則
- [KES 言語仕様書](../../../docs/spec/kes-language-spec.md) — `say` / `nar` / `select` の抽出対象とタグ仕様
