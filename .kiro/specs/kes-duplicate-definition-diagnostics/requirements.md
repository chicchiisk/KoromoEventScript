# Requirements Document

## Introduction

KoromoEventScript のコンパイラ開発者は、同一スコープ内で `actor`、`fn`、`class`、`enum`、`var` が同じ名前で重複定義された場合に、原因を特定できる診断を提供する必要がある。現状の診断では、同一スコープ重複の対象範囲と、重複元・重複先の位置情報の扱いが Issue #20 の受け入れ条件として明確化されていない。

この仕様では、意味解析時に同一スコープ内の重複定義を検出し、CLI 利用者とテストが重複元および重複先の位置を確認できる診断として表面化する状態を目指す。

Issue: https://github.com/chicchiisk/KoromoEventScript/issues/20

## Boundary Context

- **In scope**: 同一スコープ内の `actor`、`fn`、`class`、`enum`、`var` の重複定義診断、重複元と重複先の位置情報、`kes build --check-only` での compile diagnostic としての表面化。
- **Out of scope**: シャドーイング診断の仕様変更、参照解決、型検査、式評価、IR / `.klib` 生成、runtime 起動、VS Code Language Server 実装、import 解決ルールの変更。
- **Adjacent expectations**: 既存の構文解析、スコープ付き定義収集、CLI 診断形式、JSON Lines 出力、終了コード分類と整合する。異なるスコープに属する同名定義は、この仕様の重複定義診断対象にしない。

## Requirements

### Requirement 1: 同一スコープ重複の検出

**Objective:** As a コンパイラ開発者, I want 同一スコープ内の主要定義名の重複を検出できる, so that 重複定義を後続の意味解析へ進める前に診断できる

#### Acceptance Criteria

1. When 同一モジュールスコープ内に同じ名前の `actor`、`fn`、`class`、`enum`、または `var` 定義が複数存在する, the KES compiler shall report a compile diagnostic for each duplicate definition after the first definition.
2. When 同一クラススコープ内に同じ名前の member `fn` または member `var` 定義が複数存在する, the KES compiler shall report a compile diagnostic for each duplicate definition after the first definition.
3. When 同一関数、メソッド、またはブロックスコープ内に同じ名前の `var` 定義が複数存在する, the KES compiler shall report a compile diagnostic for each duplicate definition after the first definition.
4. If 同じ名前の定義が異なるスコープに属する, then the KES compiler shall not report a duplicate definition diagnostic for that name solely because the names match.
5. The KES compiler shall compare definition names case-sensitively when determining same-scope duplicate definitions.

### Requirement 2: 重複位置情報の診断

**Objective:** As a CLI 利用者, I want 重複元と重複先の位置が診断から分かる, so that どの定義を修正すべきか判断できる

#### Acceptance Criteria

1. When the KES compiler reports a duplicate definition diagnostic, the diagnostic shall identify the duplicated name.
2. When the KES compiler reports a duplicate definition diagnostic, the diagnostic shall include the duplicate definition location as file, line, and column.
3. When the KES compiler reports a duplicate definition diagnostic, the diagnostic shall include the original definition location as file, line, and column.
4. When more than two definitions with the same name exist in the same scope, the KES compiler shall report diagnostics that allow each duplicate definition after the first to be associated with the first original definition.
5. If duplicate definitions are found across files that contribute to the same module scope, then the KES compiler shall preserve file information for both the original definition and the duplicate definition.

### Requirement 3: CLI check-only での表面化

**Objective:** As a CLI 利用者, I want `kes build --check-only` が重複定義を compile error として返す, so that CI とローカル検証で同じ問題を検出できる

#### Acceptance Criteria

1. When `kes build --check-only` validates a project containing same-scope duplicate definitions, the KES CLI shall emit the duplicate definition diagnostics in the existing diagnostic output flow.
2. If duplicate definition diagnostics are emitted during check-only validation, then the KES CLI shall return the compile error exit code.
3. When duplicate definition diagnostics are emitted in text output, the KES CLI shall preserve the diagnostic code, level, duplicate definition location, original definition location, and message fields.
4. When duplicate definition diagnostics are emitted in JSON Lines output, the KES CLI shall include the duplicate definition location and original definition location in machine-readable form.
5. If syntax, file, or import resolution fails before duplicate definition validation can run, then the KES CLI shall report the earlier-stage diagnostics according to the existing stage ordering.

### Requirement 4: 既存診断範囲との分離

**Objective:** As a コンパイラ開発者, I want 重複定義診断の範囲が既存の意味解析診断と分離される, so that Issue #20 の実装範囲を超えずに検証できる

#### Acceptance Criteria

1. The KES compiler shall treat same-scope duplicate definition diagnostics as distinct from shadowing diagnostics.
2. The KES compiler shall not require type checking, overload resolution, expression evaluation, or runtime execution to report same-scope duplicate definition diagnostics.
3. When a script has no same-scope duplicate definitions, the KES compiler shall not emit duplicate definition diagnostics for that script.
4. If another semantic diagnostic is also present, then the KES compiler shall preserve duplicate definition diagnostics without changing the existing diagnostic ordering rules.
