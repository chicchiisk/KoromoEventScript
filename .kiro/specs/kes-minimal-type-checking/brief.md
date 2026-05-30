# Brief: kes-minimal-type-checking

## Problem
KoromoEventScript の CLI 利用者は、定義収集、import 解決、未定義参照診断が整っても、代入、式、命令引数の型不一致を `kes build --check-only` で検出できない。`string`、`number`、`bool`、配列、`Actor` のような MVP 型の誤用が見逃されると、IR 生成や runtime 実行に近い段階で原因を切り分ける必要があり、CI での早期検出もできない。

Issue: https://github.com/chicchiisk/KoromoEventScript/issues/22

## Current State
既存仕様では、`kes-definition-collection` が `actor`、`fn`、`class`、`enum`、`var` の主要定義とスコープ付き定義情報を収集し、`kes-import-resolution` が import 済み定義を後続の名前解決で利用できる状態にする。`kes-undefined-reference-diagnostics` は未定義の変数、actor、label、関数参照を診断するが、参照先の型、関数引数数、戻り値、式評価は範囲外としている。

公開言語仕様では、変数定義の型注釈と初期値推論、算術・比較・論理演算、配列型 `T[]`、`say` の `Actor` 話者、`if` / `while` の `bool` 条件、通常命令の引数型が定義されている。STL 仕様では、`print`、`array_len`、`str_len`、`range`、`number_to_string`、`bool_to_string`、`cast`、`show`、`face` など MVP 命令の基本シグネチャが定義されている。

## Desired Outcome
意味解析は、代入、式、命令引数における基本的な型不一致を compile diagnostic として報告する。対象は MVP に必要な `string`、`number`、`bool`、配列型、`Actor` を中心とし、各診断は不一致が発生した式、引数、代入先または代入値の位置を指す。`kes build --check-only` は型不一致を既存の診断形式と compile error 終了コードに反映する。

## Approach
既存の semantic validation に、名前解決後の最小型検査レイヤーを追加する。型検査は定義収集結果、import 解決結果、未定義参照診断で利用した参照分類を入力にし、構文上の式、変数定義、代入、通常命令、LESS 展開相当の命令呼び出し、組み込み構文を走査する。

型表現は MVP 型に限定した軽量な semantic type とし、`number`、`bool`、`string`、`Actor`、配列型、`null`、`void`、不明型を扱う。既存定義から変数・引数・関数戻り値・actor 定義の型を読み取り、STL / 組み込み命令については最小シグネチャ表を用意する。未定義参照や前段診断がある場合は、既存の stage ordering を尊重し、型検査が重複診断を増やさないようにする。

## Scope
- **In**: 変数定義の型注釈と初期値の整合、代入先と代入値の整合、算術・比較・論理演算の基本型検査、配列リテラルと配列要素アクセスの最小検査、`say` 話者と actor 系命令引数の `Actor` 検査、通常命令 / LESS / 関数呼び出しの MVP シグネチャ検査、`if` / `while` 条件の `bool` 検査、既存 CLI 診断出力と `kes build --check-only` への統合。
- **Out**: 完全な型システム、ジェネリック関数の一般実装、オーバーロード解決、ユーザー定義クラスのメンバーアクセス完全解決、enum の詳細検査、制御フローに基づく戻り値網羅検査、初期化済み状態検査、素材・manifest・runtime 状態検証、IR / `.klib` 生成、VS Code Language Server 実装、新しい構文の追加。

## Boundary Candidates
- 型検査は、定義収集や名前解決の責務を変更せず、解決済み定義の種別と型注釈を読む後段として実装する。
- STL / 組み込み命令シグネチャは、最小型検査に必要な公開命令だけを扱う。STL の完全登録や `__systemcall__` の内部検査は別仕様に委ねる。
- `array_len` や `range` など、MVP に必要な配列関連の組み込み関数は特例的な最小シグネチャとして扱う。
- `null` は `string`、`Actor`、配列型などの参照型へ代入可能とし、`number`、`bool` には代入不可とする。
- 型が前段エラーのため不明な式は、型検査で追加の派生診断を出さない。
- 診断出力は既存の diagnostic contract を使い、出力形式や終了コード分類を増やさない。

## Out of Boundary
- 関数や命令の探索規則、未定義参照、import 衝突、重複定義は既存または隣接仕様に委ねる。
- 実行時にしか確定しない値の範囲、素材 ID の存在、actor の cast 済み状態、設定キーの実在性は扱わない。
- 文字列連結、暗黙変換、数値から bool への truthy 判定など、公開仕様にない型変換は追加しない。
- メンバーアクセス、コンストラクタ、dispose、デストラクタ、アクセス修飾子の完全検査は扱わない。
- Lint 的な未使用定義、未使用 import、冗長な型注釈診断は扱わない。

## Upstream / Downstream
- **Upstream**: `.kc` 構文解析、`kes-definition-collection` のスコープ付き定義情報、`kes-import-resolution` の import 依存関係、`kes-undefined-reference-diagnostics` の参照分類と stage ordering、既存 CLI 診断基盤、`docs/spec/kes-language-spec.md`、`docs/spec/kes-language-stl-spec.md`。
- **Downstream**: IR / `.klib` 生成、runtime 連携、STL 組み込み定義の完全登録、`__systemcall__` 検査、Language Server の型診断、補完や hover 表示。

## Existing Spec Touchpoints
- **Extends**: なし。新しい最小型検査仕様として扱う。
- **Adjacent**: `kes-definition-collection`、`kes-import-resolution`、`kes-duplicate-definition-diagnostics`、`kes-undefined-reference-diagnostics`、`kes-build-check-only`。

## Constraints
ドキュメントは日本語で記述する。Issue #22 の範囲を超えて完全な型システムや runtime 検証を実装しない。診断は既存の compile diagnostic、JSON Lines 出力、終了コード分類と整合させる。既存公開仕様と矛盾する場合は、実装前に仕様側で調整する。1 Issue につき 1 ブランチ / 1 Pull Request の開発方針に従う。
