# Research & Design Decisions

## Summary

- **Feature**: `kes-import-resolution`
- **Discovery Scope**: Extension
- **Key Findings**:
  - 既存の `KeParser` は `ImportStatementSyntax` を生成し、import 文がファイル先頭にまとまる構文制約も既に検証している。
  - `kes build --check-only` は project/config/entry `.kel`/referenced script parsing の流れを持つため、import 解決は script parsing 後、終了コード決定前に差し込むのが最小変更になる。
  - 仕様文書は `.ke` を入力拡張子として説明する一方、現行 testdata は `.kc` を使っているため、設計では `.ke` を正としつつ `.kc` を既存互換入力として扱う。

## Research Log

### 既存CLIと構文解析の接続点

- **Context**: import 解決をどこに接続するか確認した。
- **Sources Consulted**: `source/cli/KoromoEventScript.Cli/Commands/Build/BuildCheckOnlyCommand.cs`, `source/cli/KoromoEventScript.Cli/Build/SourceFileParser.cs`, `source/cli/KoromoEventScript.Cli/Parsing/KeParser.cs`
- **Findings**:
  - `BuildCheckOnlyCommand` は `.kel` から `chapter` 参照を抽出し、各 script を `SourceFileParser.ParseKe` で構文解析する。
  - `SourceFileParser` は `ScriptSyntax` を返すため、構文解析済みスクリプト群を import 解決へ渡せる。
  - `ImportStatementSyntax` は `ModuleName` を保持している。
- **Implications**: 新しい import stage は parsed script collection を入力にし、`BuildCheckOnlyCommand` が終了コード `4` / `6` を選べる結果を返す形が自然。

### 仕様上の import 規則

- **Context**: import 名の意味と解決範囲を確認した。
- **Sources Consulted**: `docs/spec/kes-language-spec.md`, `docs/spec/cli-tool-spec.md`, `docs/spec/kel-file-spec.md`
- **Findings**:
  - KES の import は `import Common` のように拡張子なしモジュール名で指定し、ファイル名から拡張子を除いた名前として解決する。
  - パスは解決に含めないため、同名ファイルが複数ある状態は想定しない。
  - CLI build は import 解決、依存関係構築、名前解決を build 検証に含める。
  - `.kel` は key/value データ構文であり、キー意味論は後段処理の責務である。
- **Implications**: import 解決は `.ke` 構文解析後の意味解析責務であり、`.kel` 構文拡張として設計しない。

### テストと互換性リスク

- **Context**: 現行ファイル拡張子と fixture の互換性を確認した。
- **Sources Consulted**: `testdata/projects/minimal/events/chapter001.kc`, `testdata/ke/valid/minimal.kc`, `docs/spec/kes-language-spec.md`
- **Findings**:
  - 現行 testdata は `.kc` を使っている。
  - requirement は `.ke` / `.kel` と書かれている。
- **Implications**: Module file discovery は canonical `.ke` と current `.kc` の両方を認識する。ただし ambiguity 判定では拡張子違いの同名ファイルも衝突として扱う。

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| Build command inline | `BuildCheckOnlyCommand` 内で import を直接解決する | 最小ファイル数 | build orchestration が肥大化し、テスト境界が曖昧 | 不採用 |
| Dedicated semantic services | Import graph、definition collection、name resolver を semantic boundary に分ける | import と名前解決の責務が明確でテストしやすい | 初期ファイル数は増える | 採用 |
| Full compiler pipeline | parser 以降を包括する compiler pipeline を新設する | 将来拡張に強い | Issue #17 の範囲を超える | 不採用 |

## Design Decisions

### Decision: import 解決を semantic stage として追加する

- **Context**: import 解決は構文解析ではなく、ファイル関係と名前解決に影響する。
- **Alternatives Considered**:
  1. Parser に import ファイル読込を入れる。
  2. Build command 内で import を直接たどる。
  3. Semantic services として import graph を構築する。
- **Selected Approach**: `SemanticAnalyzer` が `ImportResolver`、`DefinitionCollector`、`NameResolver` を順に使い、診断と exit stage を返す。
- **Rationale**: parser を純粋な構文責務に保ち、build command は orchestration に留められる。
- **Trade-offs**: Semantic layer の初期骨組みが必要になるが、後続の型検査や名前解決拡張に自然につながる。
- **Follow-up**: 名前解決の対象は現行 AST で観測できる identifier 参照から始める。

### Decision: module file index はプロジェクト全体を走査する

- **Context**: import 規則はパスを含めずファイル名から拡張子を除いて解決する。
- **Alternatives Considered**:
  1. import 元ファイルと同じディレクトリだけを見る。
  2. `Paths.Events` 配下だけを走査する。
  3. プロジェクト内の script 入力候補を走査する。
- **Selected Approach**: `Paths.Events` を主探索範囲とし、プロジェクト基準で `.ke` / `.kc` 入力候補を module name へ index する。
- **Rationale**: CLI仕様のプロジェクト基準解決と、標準構成の events 配置に合う。
- **Trade-offs**: 既存の同名ファイルが複数ある場合は ambiguity diagnostic が必要。
- **Follow-up**: 将来 `Paths.Events` 以外の script root が仕様化されたら index 範囲を再検証する。

## Risks & Mitigations

- `.ke` と `.kc` の拡張子揺れ — resolver は両方を認識し、同名衝突を診断する。
- 名前解決の範囲が広がりすぎる — 現行 AST の top-level definitions と identifier references に限定し、型検査は扱わない。
- 循環 import の診断が読みにくい — import path を保持した graph traversal result を返し、診断メッセージに経路を含める。

## References

- `docs/spec/kes-language-spec.md` — import 構文、名前解決、エラー分類。
- `docs/spec/cli-tool-spec.md` — build flow、診断形式、終了コード。
- `docs/spec/kel-file-spec.md` — `.kel` は構文木構築までが parser 責務であること。
