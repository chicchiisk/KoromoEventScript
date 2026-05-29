# Research: kes-k-intermediate-representation-spec

## Summary

- **Feature**: `kes-k-intermediate-representation-spec`
- **Discovery Scope**: Extension
- **Key Findings**:
  - `docs/spec/cli-tool-spec.md` は `.k` を build output として定義済みだが、命令体系とデータ構造は別仕様に委譲している。
  - `docs/spec/windows-runtime-spec.md` は manifest と VM 成果物を読む runtime 前提を持つが、`.kc` / `.klib` 旧称が残る。
  - 現在の C# 実装には lexer/parser/semantic diagnostics の基盤がある一方、`.k` emitter と VM は未実装であるため、今回は implementation ではなく公開契約の先行定義に限定する。

## Research Log

### 既存 CLI build 契約

- **Context**: `.k` ファイル形式の仕様化範囲を決めるため、既存 build output の所有者を確認した。
- **Sources Consulted**: `docs/spec/cli-tool-spec.md`
- **Findings**:
  - `kes build` は `.ke` / `.kel` を解析・検証し、`.ke` ごとに VM 向け `.k` を生成する。
  - build output は `.k`、`diagnostics.json`、`manifest.json` を含む。
  - CLI 仕様は `.k` の命令体系、データ構造、形式詳細を別仕様に委譲している。
- **Implications**: `.k` 詳細契約は CLI 仕様へ埋め込まず、独立した `docs/spec/k-intermediate-representation-spec.md` として追加する。

### Runtime と manifest 契約

- **Context**: `.k` が所有する情報と manifest が所有する情報の境界を明確にする必要がある。
- **Sources Consulted**: `docs/spec/windows-runtime-spec.md`
- **Findings**:
  - runtime は source script を直接実行せず、manifest と VM 成果物を読み込む。
  - manifest は project、entry、scripts、assets、locale、runtime、build 情報を持つ。
  - save/debug には VM file、instruction position、call stack、variables、tag などの識別情報が必要である。
  - 文書内には `.kc` / `.klib` の旧称が残る。
- **Implications**: `.k` は script execution unit と instruction/value/source mapping を所有し、manifest は成果物一覧、entry、asset/locale/runtime metadata を所有する。旧称は互換性注記で扱う。

### 言語/STL/flow 語彙

- **Context**: `.k` instruction schema が表現すべき source language と runtime call の語彙を確認した。
- **Sources Consulted**: `docs/spec/kes-language-spec.md`, `docs/spec/kes-language-stl-spec.md`, `docs/spec/kel-file-spec.md`
- **Findings**:
  - 言語/STL 仕様は `say`、`nar`、`label`、`jump`、`select`、`case`、変数、式、tag、actor などの語彙を持つ。
  - STL 仕様は `__systemcall__` を compiler/runtime 内部の syscall 境界として定義している。
  - `.kel` は entry/chapter 参照を持つが、パース後の意味解釈は処理系側に委ねている。
- **Implications**: `.k` 仕様では source syntax ではなく VM instruction と operand の契約としてこれらを表現する。`.kel` entry/chapter と `.k` 開始位置の対応は manifest と `.k` の参照契約として定義する。

### 現在の実装状態

- **Context**: design が実装変更まで含むべきかを判断した。
- **Sources Consulted**: repository source tree and existing issue specs
- **Findings**:
  - 現在の C# 実装には lexer/parser/semantic diagnostics の基盤がある。
  - `.k` emitter、VM、runtime は未実装であり、既存実装の format を追認する段階ではない。
- **Implications**: 本 Issue は将来の emitter/VM/golden test の基準となる公開仕様を先に定義する。実装アーキテクチャや serializer 選定は後続 Issue で扱う。

### Steering とテンプレート

- **Context**: repository-wide steering と design template への準拠を確認した。
- **Sources Consulted**: `.kiro/settings/templates/specs/design.md`, `.kiro/settings/templates/specs/research.md`, `.agents/skills/kiro-spec-design/rules/*.md`, `AGENTS.md`
- **Findings**:
  - `.kiro/steering/` は存在しない。
  - `docs/` 配下と `.kiro/specs/**/design.md` / `research.md` / `tasks.md` は日本語で記述する方針がある。
  - design template は Boundary Commitments、File Structure Plan、Requirements Traceability、Testing Strategy を明示的に求める。
- **Implications**: design/research は日本語で作成し、boundary-first の構成にする。steering 不在は出力で警告として扱う。

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| A | 独立した `.k` 中間表現仕様を追加する | CLI、VM、runtime、debug tooling の共通参照点になる。詳細 schema を分散させない | 既存仕様への cross-reference 更新が必要 | 選択する |
| B | CLI 仕様の build セクションに `.k` 詳細を埋め込む | `kes build` 利用者には見つけやすい | VM、runtime、debug/source map、manifest との関係まで CLI 仕様に入り責務が肥大化する | 選択しない |
| C | `.k` 仕様と manifest schema 仕様を同時に分割追加する | runtime contract をより厳密にできる | Issue 範囲を超え、asset manifest や locale schema まで過剰に固定する | 選択しない |

## Design Decisions

### Decision: `.k` は独立した公開仕様として定義する

- **Context**: CLI 仕様が `.k` 詳細を別仕様へ委譲しており、VM/runtime が参照できる共通契約が必要である。
- **Alternatives Considered**:
  1. CLI 仕様に詳細 schema を追加する。
  2. runtime 仕様に VM input schema を追加する。
  3. 独立した `.k` 中間表現仕様を追加する。
- **Selected Approach**: `docs/spec/k-intermediate-representation-spec.md` を追加し、file format、top-level document、instruction schema、value model、control flow、source mapping、manifest relation、compatibility policy、minimal sample をまとめる。
- **Rationale**: CLI、VM、runtime、debug tooling の責務を分離しつつ、同じ成果物契約を参照できる。
- **Trade-offs**: 既存仕様への参照更新が必要になる。
- **Follow-up**: 実装 Issue では emitter/VM がこの仕様に従う golden test を追加する。

### Decision: `.ke` / `.k` を現行の正とする

- **Context**: 既存文書には `.kc` / `.klib` が残る一方、CLI 仕様は `.ke` / `.k` を現行成果物として定義している。
- **Alternatives Considered**:
  1. 既存文書全体を今回一括置換する。
  2. 新仕様だけ `.ke` / `.k` とし、既存文書は旧称のまま放置する。
  3. 新仕様で `.ke` / `.k` を正とし、今回触る既存仕様に旧称注記を追加する。
- **Selected Approach**: `.ke` source と `.k` intermediate representation を正とし、`.kc` / `.klib` は旧称または移行前の用語として注記する。
- **Rationale**: Issue 範囲を文書仕様追加に保ちながら、読者が現行用語を判断できる。
- **Trade-offs**: 旧称の全面撤去は残る。
- **Follow-up**: 広範な用語統一は別 Issue として扱える。

### Decision: manifest schema は完全定義せず参照契約に限定する

- **Context**: `.k` は asset ID、locale key、script path を扱うが、manifest の完全 schema は隣接仕様の責務である。
- **Alternatives Considered**:
  1. `.k` 仕様内で manifest schema も完全定義する。
  2. manifest への参照を一切書かない。
  3. `.k` が持つ ID/key/path 参照と manifest 所有情報の境界だけ定義する。
- **Selected Approach**: `.k` は manifest-owned data を複製せず、script id、asset id、locale key、script path などの参照契約を定義する。
- **Rationale**: runtime contract に必要な接続だけを定義し、manifest schema の固定化を避ける。
- **Trade-offs**: manifest 詳細 schema は後続仕様または runtime 仕様に残る。
- **Follow-up**: manifest schema を独立仕様化する場合は `.k` 仕様との revalidation が必要になる。

### Decision: Build vs Adopt は外部依存なしの文書契約とする

- **Context**: 今回は Markdown 仕様追加であり、serializer、schema validator、VM library の選定は不要である。
- **Alternatives Considered**:
  1. JSON Schema などの標準をこの段階で採用する。
  2. C# serializer 実装を前提に schema を固定する。
  3. JSON-style text example と contract prose に留める。
- **Selected Approach**: 文書では JSON-style の正規化例を示し、外部ライブラリや実装形式は採用しない。
- **Rationale**: 現時点の要求は公開仕様であり、実装選定を先行固定する必要がない。
- **Trade-offs**: 機械検証用 schema は後続実装で追加が必要になる可能性がある。
- **Follow-up**: emitter/VM 実装時に JSON Schema、source generator、custom validator の採否を再検討する。

## Synthesis Outcomes

- **Generalization**: `say` / `nar`、式、変数、制御フロー、runtime call は個別構文の羅列ではなく、`Instruction` + `Value` + `SourceMapping` + `ManifestReference` の一般 contract として扱う。
- **Build vs Adopt**: 今回は外部依存を採用しない。仕様文書で JSON-style 正規化例を示し、実装での serializer/schema validator 採用は後続 Issue に委ねる。
- **Simplification**: manifest schema、VM interpreter、runtime save implementation、binary package format は本仕様から除外し、`.k` の公開契約と cross-reference 更新に集中する。

## Risks & Mitigations

- Risk: `.k` schema を早く固定しすぎると将来の VM 実装とずれる。
  - Mitigation: `version`、`features`、unsupported feature handling、revalidation triggers を仕様に含める。
- Risk: manifest と `.k` の責務境界が曖昧になる。
  - Mitigation: `.k` は script execution unit と VM operand を持ち、manifest は成果物一覧、entry、asset/locale/runtime metadata を所有する、と明記する。
- Risk: `.kc` / `.klib` 旧称が混在する。
  - Mitigation: 新仕様と今回触る既存仕様への注記で `.ke` / `.k` を authoritative term とし、広範な用語統一は別作業に分離する。
- Risk: source mapping が VM semantics に影響するよう誤解される。
  - Mitigation: source mapping は debug metadata であり、欠落しても execution semantics を変えないと明記する。

## References

- `docs/spec/cli-tool-spec.md`
- `docs/spec/windows-runtime-spec.md`
- `docs/spec/kes-language-spec.md`
- `docs/spec/kes-language-stl-spec.md`
- `docs/spec/kel-file-spec.md`
- `docs/spec/overview.md`
- `.kiro/settings/templates/specs/design.md`
- `.kiro/settings/templates/specs/research.md`
- `.agents/skills/kiro-spec-design/rules/design-principles.md`
- `.agents/skills/kiro-spec-design/rules/design-discovery-light.md`
- `.agents/skills/kiro-spec-design/rules/design-synthesis.md`
- `.agents/skills/kiro-spec-design/rules/design-review-gate.md`
