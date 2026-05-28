# Brief: kes-undefined-reference-diagnostics

## Problem
KoromoEventScript の CLI 利用者は、スクリプト内の参照名が定義されていない場合でも、現状の意味解析では未定義参照として十分に診断できない。未定義の変数、actor、label、関数参照が見逃されると、実行前の `kes build --check-only` や CI で誤りを発見できず、後続の型検査や生成段階で原因の切り分けが難しくなる。

Issue: https://github.com/chicchiisk/KoromoEventScript/issues/21

## Current State
既存仕様では、`kes-definition-collection` が主要定義とスコープ付き定義表を収集し、`kes-import-resolution` が import 済み定義を後続の名前解決で利用できる状態にする。`kes-duplicate-definition-diagnostics` は同一スコープ重複定義を診断するが、参照箇所から見える定義が存在するかを検証する未定義参照診断は独立した仕様としてまだ定義されていない。

## Desired Outcome
意味解析は、未定義の変数参照、actor 参照、label 参照、関数参照を compile diagnostic として報告する。各診断は参照箇所の file、line、column を指し、既存の CLI 診断形式、JSON Lines 出力、compile error 終了コードと整合する。

## Approach
既存の定義収集結果と import 解決結果を入力に、参照式または参照構文を走査して、参照種別ごとの探索規則に従って名前を解決する。解決できない参照だけを未定義参照診断として出力し、重複定義、import エラー、型検査、式評価とは責務を分離する。

この方式は既存の semantic validation の流れに沿っており、定義表を再利用できるため実装範囲を Issue #21 の受け入れ条件に抑えやすい。探索規則の詳細は後続の requirements/design で、変数、actor、label、関数の各参照種別ごとに明確化する。

## Scope
- **In**: 未定義の変数、actor、label、関数参照の診断、参照箇所を指す位置情報、既存 CLI 診断出力と `kes build --check-only` への統合、import 済み定義を含む既存定義情報の利用。
- **Out**: 型検査、式評価、オーバーロード解決、IR / `.k` 生成、runtime 起動、VS Code Language Server 実装、新しい参照構文の追加、import 解決ルールの変更、重複定義診断の仕様変更。

## Boundary Candidates
- 定義収集結果を読む参照解決レイヤーと、定義収集そのものの責務を分ける。
- import 解決済みモジュールを参照可能集合として扱うが、import の存在確認や循環検出は `kes-import-resolution` に委ねる。
- label 参照は制御フロー検査全体ではなく、同一 actor または許可されたラベル可視範囲での存在確認に限定する。
- 診断出力は既存の diagnostic contract を使い、出力形式の拡張は最小限にする。

## Out of Boundary
- 参照先の型適合性、関数引数数、戻り値、actor の実行可能性は扱わない。
- あいまい参照や import 済み定義の衝突は、既存または別仕様の名前衝突診断に委ねる。
- 未使用定義、未使用 import、到達不能 label などの lint 的診断は扱わない。
- 構文エラーや import エラーが先に発生する場合の stage ordering は既存仕様に従い、この仕様では変更しない。

## Upstream / Downstream
- **Upstream**: `.ke` 構文解析、`kes-definition-collection` のスコープ付き定義情報、`kes-import-resolution` の import 依存関係と import 済み定義、既存 CLI 診断基盤。
- **Downstream**: 型検査、関数呼び出し検査、Language Server の参照診断、将来の補完や go-to-definition。

## Existing Spec Touchpoints
- **Extends**: なし。新しい未定義参照診断仕様として扱う。
- **Adjacent**: `kes-definition-collection`、`kes-import-resolution`、`kes-duplicate-definition-diagnostics`、`kes-build-check-only`。

## Constraints
ドキュメントは日本語で記述する。Issue #21 の範囲を超えて型検査や実行時検証を実装しない。診断は既存の compile diagnostic、JSON Lines 出力、終了コード分類と整合させる。既存公開仕様と矛盾する場合は、実装前に仕様側で調整する。
