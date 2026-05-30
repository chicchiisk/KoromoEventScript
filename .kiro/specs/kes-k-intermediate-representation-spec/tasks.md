# Implementation Plan

- [ ] 1. Foundation: `.k` 仕様文書の骨格と責務境界を作る
- [x] 1.1 `.k` 中間表現仕様の文書骨格と参照導線を作成する
  - `docs/spec/k-intermediate-representation-spec.md` を新規作成し、目的、対象読者、適用範囲、非対象範囲、隣接仕様への参照を配置する。
  - `.ke` / `.k` を現行の正とし、`.kc` / `.klib` は旧称または移行前の表記として扱う方針を明記する。
  - 完了時には、新規仕様文書だけを読んでも、compiler 実装、VM 実装、runtime 実装、圧縮・暗号化が対象外であることを確認できる。
  - _Requirements: 6.1, 6.2, 6.3, 6.4_
  - _Boundary: KIntermediateRepresentationSpec_

- [x] 1.2 `.k` の基本ファイル形式と互換性ポリシーを定義する
  - `.k` の目的、拡張子、文字エンコーディング、改行、top-level document の識別情報を定義する。
  - `version` と `features` に基づく互換性判定を定義し、未知 major version と unsupported feature の期待動作を明記する。
  - 完了時には、VM/runtime が読み込み前に確認すべき version、feature、形式エラーの扱いを仕様文書上で追跡できる。
  - _Requirements: 1.1, 1.3_
  - _Boundary: KIntermediateRepresentationSpec_

- [ ] 2. Core: VM execution contract を `.k` 仕様として定義する
- [x] 2.1 `.k` document、module、import、実行単位の表現を定義する
  - `.k` が単一 `.ke` 入力に対応する基本方針と、複数ファイル project で import 済み `.ke` が実行単位へ反映される方針を定義する。
  - module id、script id、source path、entry label、imports、labels など、VM と manifest が共通で参照する識別情報を整理する。
  - 完了時には、単一ファイルと複数ファイル project のどちらでも `.k` と import 済み module の関係を説明できる。
  - _Requirements: 1.2, 2.5, 5.1, 5.2_
  - _Boundary: KIntermediateRepresentationSpec_

- [x] 2.2 instruction schema と主要 opcode 群を定義する
  - 命令列、instruction index、opcode、引数、戻り値、実行順序の共通 schema を定義する。
  - `say`、`nar`、通常命令、式評価、変数定義、代入を VM が解釈できる形で表現する opcode 方針を定義する。
  - `label`、`jump`、`select`、`case` の制御フローと、解決済みジャンプ先を instruction index で扱う方針を定義する。
  - `__systemcall__` または runtime call 相当の syscall ID、typed args、return value usage を定義する。
  - 完了時には、VM 実装者が主要構文を instruction と operand の契約として読み取れる。
  - _Requirements: 2.1, 2.2, 2.3, 2.4_
  - _Boundary: KIntermediateRepresentationSpec_

- [x] 2.3 value、variable、scope、execution state reference を定義する
  - number、bool、string、null、array、actor reference、tag reference、asset reference、locale key、runtime dynamic value の表現を定義する。
  - 変数の宣言、読み取り、書き込み、scope、初期値に必要な情報を定義する。
  - save/load が参照できる script id、instruction index、call/continuation state、variable state、branch return position を定義する。
  - compile-time に解決済みであるべき名前、型、タグと、runtime に残る動的値の境界を明記する。
  - 完了時には、`.k` が save data ではなく、save/load が参照する安定識別子を提供する契約であることを確認できる。
  - _Requirements: 3.1, 3.2, 3.3, 3.4_
  - _Boundary: KIntermediateRepresentationSpec_

- [x] 2.4 source mapping と debug metadata の方針を定義する
  - 各命令または関連命令群から元 `.ke` の file、line、column を参照できる source mapping 情報を定義する。
  - LESS、`say` / `nar` 本文、`select` / `case` など、1 つの構文が複数命令へ展開される場合の primary source と related source の方針を定義する。
  - runtime error と debug 表示で参照する module/file 名、instruction position、fallback 表示を定義する。
  - 完了時には、source mapping が VM 実行意味を変えない補助情報であることが仕様文書で明記されている。
  - _Requirements: 4.1, 4.2, 4.3, 4.4_
  - _Boundary: KIntermediateRepresentationSpec_

- [x] 2.5 manifest 参照契約と最小正規化例を追加する
  - manifest が所有する entry、scripts、assets、locale、runtime、build metadata と、`.k` が所有する VM execution contract の境界を定義する。
  - `.k` 内の script path、asset ID、locale key が manifest 上の情報へどのように対応するかを定義する。
  - `format`、`version`、`features`、`module`、`instructions`、`labels`、`manifestRefs`、`debug` を含む最小の正規化例を追加する。
  - 完了時には、人間レビューと将来の golden test で使える `.k` サンプルが仕様文書内に存在する。
  - _Requirements: 1.4, 5.1, 5.2, 5.3, 5.4_
  - _Boundary: KIntermediateRepresentationSpec_

- [ ] 3. Integration: 既存仕様から `.k` 仕様へ接続する
- [x] 3.1 (P) CLI 仕様の `.k` 出力説明を新仕様へ接続する
  - `kes build`、`kes run`、`kes publish` の `.k` 出力または入力説明から、新しい `.k` 中間表現仕様へ参照を追加する。
  - CLI 仕様は `.k` を生成する責務を持ち、instruction schema の詳細は `.k` 仕様が所有することを明確にする。
  - 完了時には、CLI 仕様の build 成果物説明から `docs/spec/k-intermediate-representation-spec.md` を辿れる。
  - _Depends: 2.5_
  - _Requirements: 5.1, 6.1, 6.4_
  - _Boundary: CliSpecReferenceUpdate_

- [x] 3.2 (P) Windows runtime 仕様の VM 成果物表記と save/debug 参照を整える
  - Windows runtime 仕様に `.k` 中間表現仕様への参照を追加し、`.klib` は旧称または移行前表記であることを注記する。
  - save/debug に必要な VM file、instruction position、tag、source mapping が `.k` 上の script id と instruction index を参照することを明確にする。
  - manifest が `.k` を列挙または参照し、asset ID、locale key、script path を解決する関係を runtime 仕様から追えるようにする。
  - 完了時には、runtime 仕様の入力ファイル、manifest、save、debug の記述が `.k` 仕様と矛盾しない。
  - _Depends: 2.5_
  - _Requirements: 3.3, 5.1, 5.2, 5.3, 5.4, 6.1, 6.2, 6.4_
  - _Boundary: RuntimeSpecTerminologyNote_

- [x] 3.3 (P) overview から `.k` 仕様と現行用語へ到達できるようにする
  - `docs/spec/overview.md` の workflow または仕様一覧に `.k` 中間表現仕様を追加する。
  - overview に残る `.kc` / `.klib` 表記について、現行仕様では `.ke` / `.k` を正とする注記を追加する。
  - 完了時には、overview を起点に `.ke` / `.k` の現行用語と `.k` 詳細仕様の場所を確認できる。
  - _Depends: 2.5_
  - _Requirements: 6.1, 6.2, 6.4_
  - _Boundary: OverviewIndexUpdate_

- [ ] 4. Validation: 仕様カバレッジと参照整合性を確認する
- [x] 4.1 要求 ID と設計コンポーネントのカバレッジを確認する
  - `docs/spec/k-intermediate-representation-spec.md` が 1.1 から 6.4 までの全要求を節または表で満たすことを確認する。
  - KIntermediateRepresentationSpec、CliSpecReferenceUpdate、RuntimeSpecTerminologyNote、OverviewIndexUpdate の各境界に対応する成果物が存在することを確認する。
  - 完了時には、要求 ID の抜けがなく、各 design component の成果物が差分上で確認できる。
  - _Depends: 3.1, 3.2, 3.3_
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.2, 3.3, 3.4, 4.1, 4.2, 4.3, 4.4, 5.1, 5.2, 5.3, 5.4, 6.1, 6.2, 6.3, 6.4_
  - _Boundary: Regression Validation_

- [x] 4.2 参照リンク、旧称注記、正規化例を検索で検証する
  - `rg "k-intermediate-representation-spec" docs/spec` で CLI、runtime、overview から新仕様への参照が存在することを確認する。
  - `.kc` / `.klib` が今回触る既存仕様に残る場合、`.ke` / `.k` への互換性注記または移行注記があることを確認する。
  - 最小 `.k` サンプルに `format`、`version`、`features`、`module`、`instructions`、`labels`、`manifestRefs`、`debug` が含まれることを確認する。
  - 完了時には、検索結果とサンプル確認により cross-reference と golden test 準備が検証できる。
  - _Depends: 4.1_
  - _Requirements: 1.4, 4.1, 4.2, 4.3, 4.4, 6.1, 6.2, 6.4_
  - _Boundary: Regression Validation_

- [x] 4.3 最終差分品質と ADR 要否を確認する
  - Markdown の見出し、表、Mermaid、コードブロックが読み取り可能で、docs 配下の日本語方針に従っていることを確認する。
  - コード変更を伴わないため `dotnet test` を必須にしない判断を実装証跡へ残せるようにする。
  - 今回の判断が ADR 対象か棚卸しし、必要なら `docs/adr/` に記録し、不要なら完了報告でその理由を明示できる状態にする。
  - 完了時には、差分品質、検証結果、ADR 判断を PR 本文へ転記できる。
  - _Depends: 4.2_
  - _Requirements: 6.1, 6.3, 6.4_
  - _Boundary: Regression Validation_
