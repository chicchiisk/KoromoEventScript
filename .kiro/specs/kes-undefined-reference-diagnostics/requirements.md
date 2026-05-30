# Requirements Document

## Introduction

KoromoEventScript の CLI 利用者は、スクリプト内の参照名が定義されていない場合でも、現状の意味解析では未定義参照として十分に診断できない。未定義の変数、actor、label、関数参照が見逃されると、実行前の `kes build --check-only` や CI で誤りを発見できず、後続の型検査や生成段階で原因の切り分けが難しくなる。

既存仕様では、`kes-definition-collection` が主要定義とスコープ付き定義表を収集し、`kes-import-resolution` が import 済み定義を後続の名前解決で利用できる状態にする。`kes-duplicate-definition-diagnostics` は同一スコープ重複定義を診断するが、参照箇所から見える定義が存在するかを検証する未定義参照診断は独立した仕様としてまだ定義されていない。

意味解析は、未定義の変数参照、actor 参照、label 参照、関数参照を compile diagnostic として報告する。各診断は参照箇所の file、line、column を指し、既存の CLI 診断形式、JSON Lines 出力、compile error 終了コードと整合する。

Issue: https://github.com/chicchiisk/KoromoEventScript/issues/21

## Boundary Context

- **In scope**: 未定義の変数参照、actor 参照、label 参照、関数参照の診断、参照箇所の位置情報、`kes build --check-only` の compile diagnostic としての表面化、import 済み定義を含む参照可否の扱い。
- **Out of scope**: 型検査、式評価、オーバーロード解決、関数引数数検査、actor のロード済み状態検査、素材や manifest の検証、IR / `.klib` 生成、runtime 起動、VS Code Language Server 実装、新しい参照構文の追加、import 解決ルールの変更、重複定義診断の仕様変更。
- **Adjacent expectations**: 既存の構文解析、定義収集、import 解決、重複定義診断、CLI 診断形式、JSON Lines 出力、終了コード分類と整合する。構文エラー、import エラー、重複定義、シャドーイングのような前段診断がある場合は、既存の stage ordering を変更しない。

## Requirements

### Requirement 1: 変数参照の未定義診断

**Objective:** As a CLI 利用者, I want 未定義の変数参照が診断される, so that 実行前に名前の誤りや不足している定義を修正できる

#### Acceptance Criteria

1. When `.kc` script contains an identifier expression that is used as a variable reference and no visible variable, parameter, local variable, member, or imported definition can satisfy that reference, the KES compiler shall report a compile diagnostic for the undefined variable reference.
2. When a variable reference resolves to a visible local, parameter, member, module-scope, or imported definition, the KES compiler shall not report an undefined variable diagnostic for that reference.
3. When a variable reference uses the same spelling as a definition that is not visible from the reference location, the KES compiler shall report the reference as undefined.
4. When a variable reference is undefined, the KES compiler shall include the referenced name in the diagnostic message.
5. When a variable reference is checked, the KES compiler shall compare names case-sensitively.

### Requirement 2: actor 参照の未定義診断

**Objective:** As a CLI 利用者, I want 未定義の actor 参照が診断される, so that `say` や actor 系命令で存在しない actor 名を使わないようにできる

#### Acceptance Criteria

1. When `.kc` script contains an actor position such as `say <actor_identifier>:` and the referenced actor name has no visible actor definition, the KES compiler shall report a compile diagnostic for the undefined actor reference.
2. When `.kc` script contains a command or expression argument that is resolved as an actor reference and no visible actor definition can satisfy that reference, the KES compiler shall report a compile diagnostic for the undefined actor reference.
3. When an actor reference resolves to a visible local or imported actor definition, the KES compiler shall not report an undefined actor diagnostic for that reference.
4. If an actor name exists only in a file that is not reachable through the active import graph, then the KES compiler shall report references to that actor as undefined.
5. When an actor reference is undefined, the KES compiler shall point the diagnostic location at the actor identifier token used by the reference.

### Requirement 3: label 参照の未定義診断

**Objective:** As a CLI 利用者, I want 未定義の label 参照が診断される, so that `jump` や `case` の遷移先を実行前に修正できる

#### Acceptance Criteria

1. When `.kc` script contains `jump #tag` and the same document has no jump target matching `#tag`, the KES compiler shall report a compile diagnostic for the undefined label reference.
2. When `.kc` script contains `case "..." #tag` and the same document has no jump target matching `#tag`, the KES compiler shall report a compile diagnostic for the undefined label reference.
3. When a `jump` or `case` tag resolves to a `label #tag`, tagged `say`, or tagged `nar` jump target in the same document, the KES compiler shall not report an undefined label diagnostic for that reference.
4. If a matching tag exists only in an imported document, then the KES compiler shall report the local `jump` or `case` reference as undefined.
5. When a label reference is undefined, the KES compiler shall point the diagnostic location at the referenced tag token.

### Requirement 4: 関数参照の未定義診断

**Objective:** As a CLI 利用者, I want 未定義の関数参照が診断される, so that 通常命令、LESS 構文、式中の関数呼び出しで存在しない関数名を使わないようにできる

#### Acceptance Criteria

1. When `.kc` script contains a normal command call and no visible function or callable built-in definition can satisfy the command name, the KES compiler shall report a compile diagnostic for the undefined function reference.
2. When `.kc` script contains a LESS call and no visible function or callable built-in definition can satisfy the LESS call name, the KES compiler shall report a compile diagnostic for the undefined function reference.
3. When `.kc` script contains a function call expression and no visible function or callable built-in definition can satisfy the function name, the KES compiler shall report a compile diagnostic for the undefined function reference.
4. When a function reference resolves to a visible local module, imported module, or built-in callable definition, the KES compiler shall not report an undefined function diagnostic for that reference.
5. When a function reference is undefined, the KES compiler shall point the diagnostic location at the function name token.

### Requirement 5: 診断出力と CLI check-only 統合

**Objective:** As a CLI 利用者, I want 未定義参照が既存の診断出力と終了コードに反映される, so that CI とローカル検証で同じ問題を検出できる

#### Acceptance Criteria

1. When `kes build --check-only` validates a project containing undefined references, the KES CLI shall emit undefined reference diagnostics in the existing diagnostic output flow.
2. If undefined reference diagnostics are emitted during check-only validation, then the KES CLI shall return the compile error exit code.
3. When undefined reference diagnostics are emitted in text output, the KES CLI shall include file, line, column, level, diagnostic code, and message fields.
4. When undefined reference diagnostics are emitted in JSON Lines output, the KES CLI shall include file, line, column, code, level, and message fields for each diagnostic.
5. When multiple undefined reference diagnostics are emitted, the KES CLI shall preserve the existing deterministic diagnostic ordering used by semantic validation.

### Requirement 6: 隣接診断範囲との分離

**Objective:** As a コンパイラ開発者, I want 未定義参照診断の責務が隣接する意味解析診断と分離される, so that Issue #21 の実装範囲を超えずに検証できる

#### Acceptance Criteria

1. If syntax parsing fails before semantic validation can run, then the KES compiler shall report the syntax diagnostics according to the existing stage ordering instead of producing undefined reference diagnostics for the affected syntax.
2. If import resolution fails before reference validation can run, then the KES compiler shall report import diagnostics according to the existing stage ordering instead of treating imported definitions as undefined references.
3. If definition collection reports duplicate definition or disallowed shadowing diagnostics before reference validation can run, then the KES compiler shall preserve those diagnostics according to the existing stage ordering.
4. The KES compiler shall not require type checking, overload resolution, expression evaluation, IR generation, or runtime execution to report undefined reference diagnostics.
5. When a script contains no undefined variable, actor, label, or function references, the KES compiler shall not emit undefined reference diagnostics for that script.
