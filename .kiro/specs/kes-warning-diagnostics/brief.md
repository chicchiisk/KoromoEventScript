# Brief: kes-warning-diagnostics

## Problem
CLI 利用者と CI は、警告診断を通常のエラー診断とは区別して確認したい。また、品質ゲートでは警告を失敗として扱いたい場合がある。現状は診断レベルとして `Warning` は存在するが、`kes build --check-only` の処理で warning level の診断を扱う契約、`warnings-as-errors` の適用、終了コード `9` への反映が実装範囲として固定されていない。

## Current State
`docs/spec/cli-tool-spec.md` は `Build.WarningsAsErrors`、`--warnings-as-errors`、警告診断 `KES4xxx`、終了コード `9` を定義している。既存実装には `DiagnosticLevel.Warning` と診断 formatter の warning 表示があり、テストでも formatter / sink レベルの警告出力は確認されている。一方で、build 検証フローが警告診断を成功扱いで返すのか、warnings-as-errors 有効時にどの段階で失敗へ昇格するのか、CLI オプションと `kes.xml` 設定の優先順位をどう扱うのかが未整備である。

## Desired Outcome
`kes build --check-only` は warning level の診断を既存の text / JSON Lines 形式で出力できる。警告だけの場合、warnings-as-errors が無効なら終了コード `0` を返し、有効なら警告を失敗として扱って終了コード `9` を返す。エラー診断と警告診断が同時に存在する場合は、既存の「最も早い処理段階のエラー分類」規則を維持し、警告昇格が既存の構文エラー、コンパイルエラー、ファイル I/O エラーを上書きしない。

## Approach
既存の `Diagnostic` / `DiagnosticLevel` / formatter / sink を拡張せずに利用し、build check-only の結果集約層で warning policy を適用する。`kes.xml` の `Build.WarningsAsErrors` と CLI の `--warnings-as-errors` を入力設定として読み取り、診断の level と既存 exit code を見て最終終了コードを決定する。警告を生成する最小の検証項目は、この仕様内で限定的に定義し、将来のリソース検証や runtime 警告とは責務を分ける。

## Scope
- **In**: warning level 診断の build check-only 出力、`--warnings-as-errors` の受付、`Build.WarningsAsErrors` の反映、警告のみを終了コード `9` に昇格する集約規則、text / JSON Lines / exit code のテスト。
- **Out**: runtime 実行中の警告、素材 manifest の完全検証、VS Code 拡張の warning 表示、警告コード体系全体の再設計、既存 compile error の診断コード変更。

## Boundary Candidates
- 診断生成: 既存 semantic pipeline または build validation から `DiagnosticLevel.Warning` を出す責務。
- 警告ポリシー: `BuildCheckOnlyCommand` 周辺で warnings-as-errors と終了コードを決定する責務。
- 設定入力: CLI option と `kes.xml` の build 設定を読み取り、検証設定へ渡す責務。

## Out of Boundary
- `KES4xxx` のすべての将来警告をこの仕様で実装しきること。
- runtime / publish / clean / init の警告ポリシーを変更すること。
- 警告を `DiagnosticLevel.Error` に書き換えて出力表記を `error` にすること。
- エラー診断が存在する場合に終了コードを `9` へ置き換えること。

## Upstream / Downstream
- **Upstream**: `kes-build-check-only`、既存 `Diagnostic` / `DiagnosticFormatter` / `DiagnosticSink`、`ProjectConfigLoader`、`docs/spec/cli-tool-spec.md`、`docs/spec/kes-config.xsd`。
- **Downstream**: 将来の素材参照検証、ローカライズ検証、VS Code diagnostics、CI 向け品質ゲート、runtime / publish の警告取り扱い。

## Existing Spec Touchpoints
- **Extends**: `kes-build-check-only` の CLI オプション、診断出力、終了コード集約。
- **Adjacent**: `kes-minimal-type-checking`、`kes-undefined-reference-diagnostics`、`kes-duplicate-definition-diagnostics` は error level の compile diagnostics を扱うため、警告昇格で既存エラー分類を壊さない。

## Constraints
GitHub Issue #23 の範囲に限定し、警告診断と warnings-as-errors の最小実装を行う。ドキュメントは日本語で記述する。既存の CLI 診断形式、JSON Lines 出力、終了コード分類、1 Issue / 1 branch / 1 PR の開発方針に従う。新しい外部依存は追加しない。
