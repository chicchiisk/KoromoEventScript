# Requirements Document

## Introduction

KoromoEventScript の CLI 利用者は、定義収集、import 解決、未定義参照診断が整っても、代入、式、命令引数の型不一致を `kes build --check-only` で検出できない。`string`、`number`、`bool`、配列、`Actor` のような MVP 型の誤用が見逃されると、IR 生成や runtime 実行に近い段階で原因を切り分ける必要があり、CI での早期検出もできない。

既存仕様では、`kes-definition-collection` が `actor`、`fn`、`class`、`enum`、`var` の主要定義とスコープ付き定義情報を収集し、`kes-import-resolution` が import 済み定義を後続の名前解決で利用できる状態にする。`kes-undefined-reference-diagnostics` は未定義の変数、actor、label、関数参照を診断するが、参照先の型、関数引数数、戻り値、式評価は範囲外としている。

この仕様では、意味解析が代入、式、命令引数における基本的な型不一致を compile diagnostic として報告できる状態を目指す。対象は MVP に必要な `string`、`number`、`bool`、配列型、`Actor` を中心とし、`kes build --check-only` の既存診断形式、JSON Lines 出力、compile error 終了コードと整合させる。

Issue: https://github.com/chicchiisk/KoromoEventScript/issues/22

## Boundary Context

- **In scope**: `string`、`number`、`bool`、配列型、`Actor`、`null`、`void` の最小型判定、変数定義と代入の整合、算術・比較・論理演算の基本型検査、配列リテラルと配列要素アクセスの最小検査、`say` 話者、`if` / `while` / `for`、通常命令、LESS、式中関数呼び出しの MVP 型検査、既存 CLI 診断出力と `kes build --check-only` への統合。
- **Out of scope**: 完全な型システム、暗黙型変換、オーバーロード解決、ユーザー定義クラスのメンバーアクセス完全解決、enum の詳細検査、制御フローに基づく戻り値網羅検査、初期化済み状態検査、素材・manifest・runtime 状態検証、IR / `.k` 生成、VS Code Language Server 実装、新しい構文の追加。
- **Adjacent expectations**: 既存の構文解析、定義収集、import 解決、重複定義診断、未定義参照診断、CLI 診断形式、終了コード分類と整合する。構文エラー、import エラー、重複定義、未定義参照のような前段診断がある場合は、既存の stage ordering を変更しない。

## Requirements

### Requirement 1: MVP 型の認識

**Objective:** As a コンパイラ開発者, I want MVP 型が意味解析で一貫して扱われる, so that 代入、式、命令引数を同じ型規則で検査できる

#### Acceptance Criteria

1. When `.ke` script contains a supported type annotation using `number`, `bool`, `string`, `Actor`, or `T[]`, the KES compiler shall recognize the annotated type for semantic type checking.
2. When `.ke` script contains `actor` declarations, the KES compiler shall treat references to those declarations as values assignable to `Actor`-typed positions.
3. When `.ke` script contains string, number, boolean, or `null` literals, the KES compiler shall classify each literal as `string`, `number`, `bool`, or `null` for semantic type checking.
4. When `.ke` script contains a function declaration with parameter and return type annotations, the KES compiler shall use those annotations when checking calls to that function.
5. If a type annotation names a type outside the MVP type set that cannot be resolved as a supported type, the KES compiler shall report a compile diagnostic for the unsupported or unknown type.

### Requirement 2: 変数定義と代入の型検査

**Objective:** As a CLI 利用者, I want 変数の型不一致がビルド時に診断される, so that 誤った値を変数へ入れる前に修正できる

#### Acceptance Criteria

1. When `.ke` script contains a variable declaration with both a type annotation and an initializer, the KES compiler shall verify that the initializer type is assignable to the annotated type.
2. When `.ke` script contains a variable declaration without a type annotation and with an initializer, the KES compiler shall infer the variable type from the initializer when the initializer has a supported MVP type.
3. If a variable declaration initializer is not assignable to the annotated type, the KES compiler shall report a compile diagnostic at the initializer or variable declaration location.
4. When `.ke` script contains an assignment to a variable with a known MVP type, the KES compiler shall verify that the assigned expression type is assignable to the variable type.
5. If an assignment stores a value whose type is not assignable to the target variable type, the KES compiler shall report a compile diagnostic that identifies the expected and actual types.

### Requirement 3: 式演算の型検査

**Objective:** As a CLI 利用者, I want 式中の型不一致が診断される, so that 計算、比較、条件判定の誤りを実行前に発見できる

#### Acceptance Criteria

1. When `.ke` script contains arithmetic operators `+`, `-`, `*`, or `/`, the KES compiler shall require each operand to be `number` and shall classify the expression result as `number`.
2. If an arithmetic expression uses a non-`number` operand, the KES compiler shall report a compile diagnostic at the incompatible operand or operator location.
3. When `.ke` script contains comparison operators `<`, `<=`, `>`, or `>=`, the KES compiler shall require each operand to be `number` and shall classify the expression result as `bool`.
4. When `.ke` script contains equality operators `==` or `!=`, the KES compiler shall require operands to have the same type except for valid comparisons between `null` and supported reference types.
5. When `.ke` script contains logical operators `&&`, `||`, or `!`, the KES compiler shall require each operand to be `bool` and shall classify the expression result as `bool`.

### Requirement 4: 配列と制御構文の最小型検査

**Objective:** As a CLI 利用者, I want 配列と条件式の基本型が検査される, so that 反復や分岐の誤用を実行前に修正できる

#### Acceptance Criteria

1. When `.ke` script contains a non-empty array literal, the KES compiler shall require all elements to be assignable to one common supported element type and shall classify the literal as that element array type.
2. If an array literal contains elements with incompatible MVP types, the KES compiler shall report a compile diagnostic at the incompatible element or array literal location.
3. When `.ke` script contains an empty array literal assigned to or passed into a position with a known array type, the KES compiler shall treat the empty array as assignable to that known array type.
4. When `.ke` script contains array element access, the KES compiler shall require the accessed value to be an array type and the index expression to be `number`.
5. When `.ke` script contains `if`, `else if`, or `while` conditions, the KES compiler shall require each condition expression to be `bool`.
6. When `.ke` script contains `for <name> in <expr>`, the KES compiler shall require the right-hand expression to be a supported iterable type, including at least `T[]`, and shall treat the loop variable as the element type within the loop body.

### Requirement 5: 命令引数と関数呼び出しの型検査

**Objective:** As a CLI 利用者, I want 通常命令や関数呼び出しの引数型不一致が診断される, so that シナリオ命令の誤用を CI とローカル検証で発見できる

#### Acceptance Criteria

1. When `.ke` script contains a call to a visible user-defined function, the KES compiler shall verify that each supplied argument is assignable to the corresponding parameter type.
2. When `.ke` script contains a call to a supported MVP built-in command or function, the KES compiler shall verify that supplied positional and named arguments match the documented command signature.
3. When `.ke` script contains LESS syntax that expands to calls of a supported command or function, the KES compiler shall verify the common arguments and item arguments against the same command signature used for normal calls.
4. When `.ke` script contains `say <actor_identifier>:`, the KES compiler shall require the speaker identifier to resolve to an `Actor` value.
5. If a command, LESS item, or function call supplies an argument whose type is not assignable to the required parameter type, the KES compiler shall report a compile diagnostic at the incompatible argument location.
6. If a call uses a `void` result where a value is required, the KES compiler shall report a compile diagnostic for invalid value usage.

### Requirement 6: 診断出力と隣接診断範囲との分離

**Objective:** As a CLI 利用者, I want 型不一致が既存の診断出力と終了コードに反映される, so that CI とローカル検証で同じ問題を検出できる

#### Acceptance Criteria

1. When `kes build --check-only` validates a project containing MVP type mismatches, the KES CLI shall emit type diagnostics in the existing diagnostic output flow.
2. If type diagnostics are emitted during check-only validation, the KES CLI shall return the compile error exit code.
3. When type diagnostics are emitted in text output, the KES CLI shall include file, line, column, level, diagnostic code, and message fields.
4. When type diagnostics are emitted in JSON Lines output, the KES CLI shall include file, line, column, code, level, and message fields for each diagnostic.
5. If syntax parsing, import resolution, definition collection, or undefined reference validation fails before type checking can run, the KES compiler shall preserve the existing stage ordering instead of producing guessed type diagnostics for affected syntax or references.
6. The KES compiler shall not require IR generation, `.k` output, manifest validation, runtime execution, or Language Server execution to report MVP type diagnostics.
