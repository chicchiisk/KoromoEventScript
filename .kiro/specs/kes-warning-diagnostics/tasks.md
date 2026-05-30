# Implementation Plan

- [x] 1. Foundation: warnings-as-errors の設定と終了コードを扱える状態にする
- [x] 1.1 終了コードと警告ポリシーの基盤を追加する
  - 警告をエラーとして扱った失敗を表す終了コードを既存の終了コード体系に追加する。
  - warning-only、error coexist、warnings-as-errors 有効/無効を判定する警告ポリシーを追加する。
  - 完了時には、警告だけなら設定に応じて `0` または `9` になり、既存 error exit code は上書きされないことを単体テストで確認できる。
  - _Requirements: 2.4, 3.1, 3.2, 3.3, 3.4, 4.1, 4.2, 4.3, 4.4, 5.2, 5.5_
  - _Boundary: WarningPolicy, CliExitCode_

- [x] 1.2 CLI option と project config の warnings-as-errors 設定を読み取る
  - `--warnings-as-errors` を build check-only の supported option として受け取れるようにする。
  - `kes.xml` の `Build.WarningsAsErrors` を読み取り、未指定時は無効として扱う。
  - 完了時には、CLI option、config true、config false、未指定の各ケースをテストから判定できる。
  - _Requirements: 2.1, 2.2, 2.3, 2.4_
  - _Boundary: Build option/config integration_

- [x] 2. Core warning diagnostics: warning を生成し集約できる状態にする
- [x] 2.1 (P) 最小 warning producer を追加する
  - 空の `.kc` ドキュメントを warning level 診断として報告する semantic warning 検査を追加する。
  - warning diagnostic は `KES4xxx`、warning level、file、line、column、message を保持する。
  - 完了時には、空 `.kc` 入力で `KES4001` warning が生成され、非空入力では不要な warning が出ないことを単体テストで確認できる。
  - _Requirements: 1.1, 1.4, 5.1_
  - _Boundary: WarningAnalyzer_

- [x] 2.2 (P) warning diagnostics を semantic result に含める
  - semantic success path に warning 検査を接続し、warning-only の場合は成功 exit code のまま diagnostics へ含める。
  - import、definition、name、type checking の error がある場合は既存 stage ordering を維持する。
  - 完了時には、semantic analyzer の結果に warning が含まれ、compile error path の終了コードが変わらないことをテストで確認できる。
  - _Depends: 2.1_
  - _Requirements: 1.1, 1.4, 3.4, 4.2, 5.1, 5.5_
  - _Boundary: SemanticAnalyzer, SemanticModels_

- [x] 3. Integration: build check-only と diagnostic output を接続する
- [x] 3.1 warnings-as-errors 設定を build check-only の最終結果へ反映する
  - CLI option と project config の設定を合成し、warning policy に渡す。
  - warning-only project は warnings-as-errors 無効時に成功、設定有効時に終了コード `9` になる。
  - 完了時には、同じ warning-only fixture で設定差による exit code の違いを build command test から確認できる。
  - _Depends: 1.1, 1.2, 2.2_
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 3.1, 3.2, 3.3_
  - _Boundary: BuildCheckOnlyCommand, WarningPolicy_

- [x] 3.2 warning diagnostics を既存の text / JSON Lines 出力に通す
  - warning diagnostic を既存 sink から標準エラーへ出力できるように build flow を通す。
  - text と JSON Lines の level は `warning` のまま維持し、diagnostic fields を欠落させない。
  - 完了時には、CLI 実行に近いテストで標準出力が空、標準エラーに warning diagnostic が出ることを確認できる。
  - _Depends: 3.1_
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 3.4, 5.2_
  - _Boundary: CliApplication, Diagnostic output_

- [x] 3.3 既存 error と warning の同居時の優先順位を固定する
  - syntax error、compile error、file I/O error と warning が同じ検証に存在しても既存 error exit code を優先する。
  - warnings-as-errors が有効でも error level diagnostic の終了コードを `9` へ置き換えない。
  - 完了時には、warning と既存 error が同居する fixture で `3` / `4` / `6` の分類が維持されることを統合テストで確認できる。
  - _Depends: 3.1_
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 5.5_
  - _Boundary: BuildCheckOnlyCommand, WarningPolicy_

- [x] 4. Validation: CLI 統合と回帰を固める
- [x] 4.1 process-level の warnings-as-errors 挙動を検証する
  - `kes build --check-only --warnings-as-errors` を実プロセスに近い形で実行し、warning-only project が exit code `9` を返すことを確認する。
  - process result の標準エラーに warning diagnostic が出力され、標準出力が空であることを確認する。
  - 完了時には、CI から観測できる process exit code と stderr の warning 表示がテストで固定されている。
  - _Depends: 3.2_
  - _Requirements: 1.2, 1.5, 2.1, 3.2, 5.2_
  - _Boundary: CLI process integration_

- [x] 4.2 warning diagnostics の範囲外コマンドと既存 compile error 回帰を確認する
  - publish、run、clean、init の警告ポリシーをこの実装で変更しないことを既存 unsupported command behavior と合わせて確認する。
  - 既存 compile error diagnostic の level、code、exit code が warning 実装で変わらないことを回帰テストで確認する。
  - 完了時には、既存 check-only の syntax / compile / file I/O / success テストと新規 warning tests が同時に通る。
  - _Depends: 3.3_
  - _Requirements: 5.3, 5.4, 5.5_
  - _Boundary: CLI regression validation_
