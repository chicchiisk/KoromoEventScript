# Requirements Document

## Introduction

KoromoEventScript の CLI 利用者とコンパイラ開発者は、`actor`、`fn`、`class`、`enum`、`var` の主要定義を意味解析で一貫して扱う基盤がまだないため、後続の参照解決、型検査、Language Server 連携で同じ定義情報を再利用できない。現状の `DefinitionCollector` は import/name 解決のために一部トップレベル定義を収集するに留まり、スコープ単位の定義表、重複検出、シャドーイング検出、主要宣言構文の定義情報が不足している。

この仕様では、意味解析が主要定義をスコープごとに収集し、後続の参照解決が利用できる定義情報として保持できる状態を目指す。

## Boundary Context

- **In scope**: `actor`、`fn`、`class`、`enum`、`var` の定義認識、モジュール/クラス/関数またはメソッド/ブロックのスコープ単位の定義収集、同一スコープ重複とシャドーイングの診断、import 済みモジュールを含む後続参照解決向けの定義情報提供。
- **Out of scope**: 完全な型検査、式評価、IR / `.k` 生成、runtime 起動、STL 組み込み定義の完全登録、VS Code Language Server 実装、素材や manifest の検証。
- **Adjacent expectations**: 既存の `.ke` 構文解析、import 解決、タグ解決、CLI 診断形式、終了コード分類と整合する。import モジュール探索、循環 import、`label` / `jump` / `case` の制御フロー検査は既存仕様の責務を変更しない。

## Requirements

### Requirement 1: 主要定義の認識

**Objective:** As a コンパイラ開発者, I want 主要な言語定義が意味解析入力として認識される, so that 後続の定義収集が同じ構文情報を利用できる

#### Acceptance Criteria

1. When `.ke` script contains top-level `actor`, `fn`, `class`, `enum`, or `var` definitions, the KES compiler shall recognize each definition name and source location as a semantic definition candidate.
2. When `.ke` script contains class members declared by `var` or `fn`, the KES compiler shall recognize each member name and source location as a class-scope definition candidate.
3. When `.ke` script contains function or method parameters, the KES compiler shall recognize each parameter name and source location as a function-or-method-scope definition candidate.
4. When `.ke` script contains local `var` definitions inside function, method, or block bodies, the KES compiler shall recognize each local variable name and source location as a scoped definition candidate.
5. If a supported definition form is syntactically incomplete, the KES compiler shall report syntax diagnostics using the existing diagnostic contract instead of producing partial definition information for that form.

### Requirement 2: スコープ単位の定義収集

**Objective:** As a コンパイラ開発者, I want 定義情報がスコープごとに整理される, so that 参照解決が言語仕様の探索順序を再現できる

#### Acceptance Criteria

1. When meaning analysis processes a syntax-valid script, the KES compiler shall collect top-level `actor`, `fn`, `class`, `enum`, and `var` definitions into the module scope for that script.
2. When meaning analysis processes a class definition, the KES compiler shall collect member `var` and member `fn` definitions into the class scope for that class.
3. When meaning analysis processes a function or method definition, the KES compiler shall collect parameters and directly contained local `var` definitions into that function-or-method scope.
4. When meaning analysis processes nested blocks that can contain local definitions, the KES compiler shall associate collected local definitions with the block scope where they are declared.
5. The KES compiler shall preserve parent-child relationships between module, class, function-or-method, and block scopes in the collected definition information.

### Requirement 3: 重複定義とシャドーイング診断

**Objective:** As a CLI 利用者, I want 名前衝突がビルド時に診断される, so that 意図しない定義の衝突を実行前に修正できる

#### Acceptance Criteria

1. If multiple definitions with the same name are declared in the same scope, the KES compiler shall report a compile diagnostic at the duplicate definition location.
2. If a definition declares the same name as a visible definition in an outer scope, the KES compiler shall report a compile diagnostic for disallowed shadowing.
3. When top-level `actor`, `fn`, `class`, `enum`, and `var` definitions share the same module scope name, the KES compiler shall treat them as duplicate module-scope definitions.
4. When different classes contain members with the same name, the KES compiler shall allow those member definitions because they belong to different class scopes.
5. When duplicate or shadowing diagnostics are reported, the KES compiler shall include file, line, column, diagnostic code, and a message that identifies the conflicting name.

### Requirement 4: 後続参照解決向けの定義情報

**Objective:** As a コンパイラ開発者, I want 収集済み定義を後続の参照解決で利用できる, so that 名前解決と型検査を同じ定義基盤の上に実装できる

#### Acceptance Criteria

1. When definition collection succeeds for a script, the KES compiler shall make collected definitions available to subsequent semantic validation for that script.
2. When import resolution has built reachable module information, the KES compiler shall keep imported module definitions distinguishable from local module definitions for subsequent reference resolution.
3. When collected definitions include type-like declarations such as `class`, `enum`, or `actor`, the KES compiler shall identify those definitions as type-capable definitions for later semantic checks.
4. When collected definitions include callable declarations such as `fn` or methods, the KES compiler shall identify those definitions as callable definitions for later semantic checks.
5. If definition collection fails with compile diagnostics, the KES compiler shall not treat the affected definition collection result as successful input for subsequent reference resolution.

### Requirement 5: CLI check-only 統合

**Objective:** As a CLI 利用者, I want `kes build --check-only` が主要定義の意味解析結果を反映する, so that CI で定義衝突を検出できる

#### Acceptance Criteria

1. When `kes build --check-only` validates a project with supported major definitions, the KES CLI shall include definition collection in the semantic validation stage.
2. When supported major definitions are valid and no other diagnostics occur, the KES CLI shall return the success exit code.
3. If definition collection reports duplicate or shadowing diagnostics, the KES CLI shall return the compile error exit code.
4. When definition collection diagnostics are emitted in text output, the KES CLI shall preserve the existing diagnostic fields and ordering.
5. When definition collection diagnostics are emitted in JSON Lines output, the KES CLI shall include file, line, column, code, level, and message fields for each diagnostic.
