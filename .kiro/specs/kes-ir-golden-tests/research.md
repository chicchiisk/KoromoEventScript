# Research & Design Decisions

## Summary

- **Feature**: `kes-ir-golden-tests`
- **Discovery Scope**: Extension
- **Key Findings**:
  - 既存コードベースには `BuildCommand` と `KlibArtifactWriter` を通じて `.klibtxt` を生成する経路がすでにあり、新しい production 向け出力機構は不要である。
  - `tests/KoromoEventScript.Cli.Tests/Compilation/KlibCompilerTests.cs` には broad surface 向けの比較テストが存在し、Issue #26 はこの系統を golden test として整理・拡張する設計が最も小さい。
  - `docs/testing-strategy.md` と `testdata/README.md` は golden test の期待値を `testdata/snapshots/ir/` に置く方針を定義しており、期待値資産の配置規約は既存ルールに従える。

## Research Log

### 既存の IR 出力経路

- **Context**: IR golden test 追加で production code の新規境界が必要か確認したかった。
- **Sources Consulted**:
  - `source/cli/KoromoEventScript.Cli/Commands/Build/BuildCommand.cs`
  - `source/cli/KoromoEventScript.Cli/Commands/Build/BuildCommandOptions.cs`
  - `source/cli/KoromoEventScript.Cli/Commands/CliApplication.cs`
- **Findings**:
  - `BuildCommand` は `EmitTextIr` が有効なときに `.klibtxt` を build 出力へ書き出す。
  - CLI 解析は `--txt-il` を既にサポートしている。
  - Issue #26 のために CLI 契約を増やす必要はない。
- **Implications**:
  - 設計の責務は新規 CLI 機能追加ではなく、既存出力契約をテスト固定へ接続することに限定できる。
  - 変更境界は主にテストコードと snapshot 資産に置ける。

### 既存テストパターンと資産配置

- **Context**: どのテスト層に golden test を置くべきか、期待値管理の既存規約を確認したかった。
- **Sources Consulted**:
  - `tests/KoromoEventScript.Cli.Tests/Compilation/KlibCompilerTests.cs`
  - `tests/KoromoEventScript.Cli.Tests/Commands/BuildCommandTests.cs`
  - `tests/KoromoEventScript.Cli.Tests/TemporaryProject.cs`
  - `docs/testing-strategy.md`
  - `testdata/README.md`
- **Findings**:
  - 既存の `KlibCompilerTests` は `.klibtxt` の全文比較を already 使っている。
  - `BuildCommandTests` は存在確認と部分文字列検証に留まり、IR 全体の固定には向いていない。
  - snapshot 期待値は `testdata/snapshots/ir/` に置く方針が明文化されている。
  - `TemporaryProject` は改行を `\n` に正規化して書き出すため、テスト側で改行差異を吸収しやすい。
- **Implications**:
  - IR golden test の主戦場は `Compilation` テスト層が適切である。
  - 比較対象は `.klibtxt` の全文とし、期待値は `testdata/snapshots/ir/` に置く。

### 仕様整合性

- **Context**: 比較対象を `.k` にするか `.klibtxt` にするかを既存仕様と整合させたかった。
- **Sources Consulted**:
  - `docs/spec/k-intermediate-representation-spec.md`
  - `docs/spec/cli-tool-spec.md`
- **Findings**:
  - IR 仕様は golden test と差分確認向けの人間可読形式として `.klibtxt` を定義している。
  - CLI 仕様は `--txt-il` により `.klibtxt` を `.klib` と併置する契約を持つ。
- **Implications**:
  - 可読差分という要件は `.klibtxt` で満たすのが正道であり、バイナリ `.klib` の直接比較は設計対象外とする。

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| 既存 `KlibCompilerTests` を拡張 | 既存の compilation テストで `.klibtxt` 全文を snapshot 比較する | 最小変更、既存責務と一致、差分可読性が高い | 代表入力が増えるとテストファイルが大きくなる | 採用 |
| `BuildCommandTests` に移す | CLI 統合テストで `.klibtxt` 全文比較する | CLI 契約に近い | セットアップが重く、責務が混ざる | 不採用 |
| 新規スナップショットフレームワーク導入 | 専用ライブラリで golden 管理 | 機能は豊富 | 新依存、学習コスト、Issue 範囲超過 | 不採用 |

## Design Decisions

### Decision: 既存 compilation テストを IR golden test の責務境界として使う

- **Context**: IR 全文比較をどのテスト層へ置くかを決める必要があった。
- **Alternatives Considered**:
  1. `KlibCompilerTests` を拡張する
  2. `BuildCommandTests` を拡張する
  3. 新規の snapshot テスト基盤を導入する
- **Selected Approach**: `KlibCompilerTests` を IR golden test のアンカーとして扱い、代表入力の build 実行後に `.klibtxt` 全文と snapshot を比較する。
- **Rationale**: 既に broad surface の全文比較があり、比較責務と fixture 構築責務が自然に収まっているため。
- **Trade-offs**: CLI 引数の解析自体はこのテストでは直接検証しないが、Issue #26 の核心である IR 固定と可読差分には最短距離で届く。
- **Follow-up**: 実装時にテスト名・補助メソッド名を golden test 目的に合わせて整理する。

### Decision: 可読差分の契約は `.klibtxt` 全文比較で表現する

- **Context**: 失敗時に読みやすい差分をどう担保するかを決める必要があった。
- **Alternatives Considered**:
  1. `.klibtxt` 全文を文字列比較する
  2. 特定命令だけ `Contains` で検証する
  3. `.klib` バイナリを比較する
- **Selected Approach**: 改行正規化後の `.klibtxt` 全文を `Assert.That(..., Is.EqualTo(...))` で比較する。
- **Rationale**: NUnit の失敗表示で全文差分が確認でき、仕様が想定する人間可読 IR に一致するため。
- **Trade-offs**: 期待値更新は慎重さを要するが、差分レビュー性は最も高い。
- **Follow-up**: 実装時に snapshot ファイル命名と取得メソッドを統一する。

### Decision: 新規 production dependency や abstraction は追加しない

- **Context**: 将来拡張を見越した共通 abstraction を先に入れるべきかを判断したかった。
- **Alternatives Considered**:
  1. テスト専用ヘルパーを最小限で足す
  2. 専用 snapshot service を導入する
  3. 外部 golden test ライブラリを採用する
- **Selected Approach**: 既存 helper とファイル配置規約の範囲で完結させる。
- **Rationale**: 現要件は 1 系統の IR snapshot 比較で完結し、抽象化を増やすと task 境界だけがぼやけるため。
- **Trade-offs**: 将来 snapshot 種別が増えた場合は再整理が必要になる可能性がある。
- **Follow-up**: diagnostics や manifest へ共通化が波及する時点で再評価する。

## Risks & Mitigations

- 代表入力が狭すぎると回帰検知力が不足する — broad surface 入力を基準にし、主要言語表面を requirement と design の双方で明記する。
- 期待値更新の理由が不明瞭になる — PR と task で snapshot 更新理由の記録を必須にする。
- 改行や環境差異で不要な失敗が出る — 比較前に改行正規化を維持し、テキスト比較契約を明文化する。

## References

- `docs/testing-strategy.md` — golden test と snapshot 配置方針
- `testdata/README.md` — testdata 配置ルール
- `docs/spec/k-intermediate-representation-spec.md` — `.klibtxt` の用途と可読表現契約
- `docs/spec/cli-tool-spec.md` — `--txt-il` と build 成果物契約
