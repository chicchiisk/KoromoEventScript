# Brief: kes-ke-to-k-conversion

## Problem

CLI / compiler 実装者と VM 実装者は、`.kc` の AST と検証済み意味情報から `.klib` 中間表現を生成する実装境界を必要としている。
現状は `.klib` の公開契約が `docs/spec/k-intermediate-representation-spec.md` で定義されている一方、`.kc` AST を `.klib` document へ下げる emitter 実装が未整備であり、Issue #25 の `say`、`nar`、`select`、`jump`、通常命令を golden test 可能な形で出力できない。

## Current State

既存コードには `.kc` lexer / parser、AST (`ScriptSyntax`、`SayStatementSyntax`、`NarStatementSyntax`、`SelectStatementSyntax`、`JumpStatementSyntax`、`CommandStatementSyntax` など)、import / definition / name resolution / type checking / warning diagnostics の基盤がある。
既存仕様 `kes-k-intermediate-representation-spec` は `.klib` の file format、instruction schema、opcode、source mapping、manifest 参照契約を定義済みだが、同仕様では `.klib` emitter 実装を非対象としている。

## Desired Outcome

`.kc` AST から `.klib` document を生成する変換層が追加され、少なくとも `say`、`nar`、`select`、`jump`、通常命令を `.klib` 仕様に沿った安定 JSON 表現へ変換できる。
変換結果は field order、instruction index、label 解決、source mapping の最小情報が安定し、golden test または snapshot test で差分確認できる。

## Approach

選択方針は、既存 `.klib` IR 仕様に従う最小 emitter を CLI 内に追加し、AST から `.klib` document model へ変換してから正規化 serializer で出力する方式とする。
直接文字列連結で JSON を組み立てず、C# の model と serializer option で stable field order / LF / UTF-8 を管理する。
初期スコープは Issue #25 の受け入れ条件に合わせ、VM 実装、manifest 完全生成、式評価や関数呼び出しの完全 lowering には広げない。

検討した代替案:

- AST から JSON 文字列を直接生成する: 実装は小さいが、field order、escaping、将来の opcode 拡張、golden 差分の保守性が弱い。
- `.klib` schema validator や VM loader まで同時に作る: 契約検証は強くなるが、Issue #25 の範囲を超えて PR が大きくなる。
- 既存 semantic model を全面的に再設計してから emitter を作る: 長期的には整理しやすいが、現在の parser/diagnostics の流れと独立した受け入れ条件を満たすには過剰。

## Scope

- **In**: `.kc` AST から `.klib` document model への変換器、`say` / `nar` / `select` / `jump` / 通常命令の opcode 生成、label から instruction index への解決、最小 source mapping、stable JSON 出力、golden test 用 testdata とテスト。
- **Out**: VM interpreter、runtime 実装、manifest 全体の schema / 出力、asset / locale 実体解決、式評価・変数・関数・制御構文全体の完全 lowering、`.klib` schema validator、既存 `.kc` / `.klib` 表記の全面移行。

## Boundary Candidates

- AST lowering: `StatementSyntax` 群を `.klib` instruction sequence と `labels` へ変換する責務。
- `.klib` document model: `.klib` 仕様の top-level field、instruction、value、source mapping、manifest reference の C# 表現。
- Serialization: golden test で比較できる正規化 JSON 出力を担当する責務。
- Build integration: 変換器を `kes build` または build pipeline へ接続する責務。ただし Issue #25 では必要最小限に留める。

## Out of Boundary

- `.klib` を実行する VM / runtime の実装。
- `.kel` entry と manifest scripts から runtime package 全体を生成する完全な publish pipeline。
- import 済み複数 module の完全な artifact 配置、hash、manifest 照合。
- 型推論、名前解決、重複定義、未定義参照など既存 semantic diagnostics の仕様変更。
- `.klib` IR 公開契約そのものの大幅変更。必要な差分が見つかった場合は既存 `kes-k-intermediate-representation-spec` の更新として扱う。

## Upstream / Downstream

- **Upstream**: `.kc` parser / AST、semantic analysis、`docs/spec/k-intermediate-representation-spec.md`、`docs/spec/kes-language-spec.md`、`docs/spec/cli-tool-spec.md`。
- **Downstream**: VM loader / interpreter、runtime package manifest 生成、`kes build` 通常出力、debug / runtime error 表示、将来の `.klib` schema validator。

## Existing Spec Touchpoints

- **Extends**: なし。既存 `kes-k-intermediate-representation-spec` は emitter 実装を非対象としているため、この作業は新規 implementation spec として扱う。
- **Adjacent**: `kes-k-intermediate-representation-spec`、`kes-build-check-only`、`kes-import-resolution`、`kes-definition-collection`、`kes-undefined-reference-diagnostics`、`kes-duplicate-definition-diagnostics`、`kes-minimal-type-checking`。

## Constraints

ドキュメントと仕様成果物は日本語で記述する。
実装は既存の C# / .NET CLI 構成、NUnit テスト、`testdata/` と snapshot / golden test の既存パターンに従う。
`.klib` 出力は golden test に適した deterministic output とし、不要な timestamp、環境依存 path、非決定的順序を含めない。
Issue #25 の範囲を超える architecture 判断が必要になった場合は、実装前に仕様または ADR の要否を再確認する。
