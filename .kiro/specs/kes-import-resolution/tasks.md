# Implementation Plan

- [ ] 1. Foundation: 意味解析ステージの入力と検証素材を整える
- [x] 1.1 意味解析で共有するスクリプト文書、解析結果、依存関係、シンボル結果の最小モデルを用意する
  - 構文解析済みスクリプトをプロジェクト相対パス、モジュール名、構文情報として扱えるようにする
  - import 解決、名前解決、診断、終了コード分類を1つの結果として後続ステージへ渡せるようにする
  - 完了時には、意味解析サービス群の単体テストから入力文書と結果を生成し、診断順序と終了コード分類を検証できる
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 3.5, 5.3, 5.4, 5.5_

- [x] 1.2 import 成功、未存在、あいまい、循環、構文エラー、名前解決失敗を表すテストプロジェクトを追加する
  - `.ke` を正規入力として含め、既存互換の `.kc` 入力も検証できる fixture を用意する
  - 複数ディレクトリ、同名ファイル、複数 import 経路、未 import 定義参照を再現できる構成にする
  - 完了時には、単体テストと build check-only 統合テストが同じ fixture を読み込み、各失敗分類を再現できる
  - _Requirements: 1.2, 1.3, 1.5, 2.2, 2.3, 3.1, 3.3, 3.4, 4.3, 4.5, 5.1, 5.4_

- [ ] 2. Core: import 対象解決と依存関係構築を実装する
- [x] 2.1 (P) プロジェクト基準で import モジュール名から入力ファイルを特定する
  - `Paths.Events` 配下の `.ke` と既存互換 `.kc` を走査し、拡張子なしファイル名をモジュールキーとして扱う
  - 一致なし、単一一致、複数一致を区別し、複数一致では project-relative path を診断材料として保持する
  - 完了時には、異なるディレクトリの import 先が project root 基準で解決され、同名 `.ke` / `.kc` はあいまいとして検出される
  - _Requirements: 1.1, 1.2, 1.3, 1.5_
  - _Boundary: ModuleFileIndex_

- [x] 2.2 (P) import 依存関係を安定順序で表現し、重複と循環を識別する
  - 直接 import と transitive import の到達関係を保持する
  - 同じファイルへ複数経路から到達しても1つの依存関係として扱う
  - 完了時には、循環 import の経路が診断メッセージへ渡せる形で保持され、到達可能ファイルの順序が実行ごとに安定する
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 3.3_
  - _Boundary: ImportGraph_

- [x] 2.3 import 文をたどって未解析ファイルを読み込み、import グラフまたは import 診断を返す
  - 構文解析済み root から import 文を走査し、未解析の import 先だけを読み込む
  - 未存在、あいまい、読み取り不可、import 先構文エラー、循環を既存診断形式へ分類する
  - 完了時には、複数の import 診断が検査順序に従って返り、`.k`、manifest、runtime 成果物がなくても解析が完了する
  - _Requirements: 1.1, 1.4, 1.5, 2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.2, 3.3, 3.4, 3.5_
  - _Depends: 2.1, 2.2_

- [ ] 3. Core: import 済み定義を名前解決で利用できるようにする
- [x] 3.1 (P) 構文木から名前解決に公開するトップレベル定義を収集する
  - 現行 AST で観測できるトップレベル定義を source location 付きで収集する
  - 同一モジュール内の重複定義を compile 診断として報告できるようにする
  - 完了時には、収集結果がファイル、行、列を保持し、将来のトップレベル構文追加にも import グラフへ影響せず拡張できる
  - _Requirements: 4.1, 4.4, 4.5_
  - _Boundary: DefinitionCollector_

- [x] 3.2 import グラフに基づき、local 定義と import 済み定義で名前参照を解決する
  - import 元から到達可能な定義だけを参照可能にし、未 import ファイルの定義は不可視にする
  - local/import 衝突と複数 import 先の同名定義を区別して診断する
  - 完了時には、import 済み定義への参照は未定義診断にならず、未 import 定義、衝突、あいまい参照は source location 付きで診断される
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_
  - _Depends: 2.2, 3.1_

- [x] 3.3 import 解決、定義収集、名前解決を意味解析ステージとして統合する
  - root スクリプトごとに import グラフを構築し、成功したグラフだけを名前解決へ渡す
  - import ファイル I/O、import 先構文エラー、compile 診断を stage 別の終了コード分類へ集約する
  - 完了時には、意味解析ステージ単体で成功、file I/O、syntax、compile の分類と ordered diagnostics を返せる
  - _Requirements: 1.1, 1.4, 1.5, 2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.2, 3.3, 3.4, 3.5, 4.1, 4.2, 4.3, 4.4, 4.5, 5.3, 5.4, 5.5_
  - _Depends: 2.3, 3.2_

- [ ] 4. Integration: `kes build --check-only` に import/name 検証を接続する
- [x] 4.1 build check-only の構文解析後に意味解析ステージを実行する
  - `.kel` から参照された script の構文解析成功後、import 解決と名前解決を検証に含める
  - 既存の project/config/source read failures は意味解析より前に終了させ、import 関連でも earliest stage wins を維持する
  - 完了時には、import と名前解決が成功するプロジェクトで終了コード `0`、compile 診断で `4`、import ファイル I/O で `6`、import 先構文エラーで `3` が返る
  - _Requirements: 2.5, 3.4, 5.1, 5.2, 5.3, 5.4, 5.5_
  - _Depends: 3.3_

- [ ] 4.2 CLI 診断出力で import/name 診断の順序とフィールドを保持する
  - import 元位置、import 先位置、循環経路、名前衝突対象が既存の診断出力形式で確認できるようにする
  - JSON Lines 出力でも file、line、column、code、message が欠落しないようにする
  - 完了時には、複数 import 診断が検査順序で出力され、missing import の診断が import 元の source location を示す
  - _Requirements: 1.4, 3.1, 3.3, 3.5, 5.1_
  - _Depends: 4.1_

- [ ] 4.3 import なし既存プロジェクトと既存 `.kc` fixture の互換性を維持する
  - import 文を持たない最小プロジェクトでは意味解析追加後も成功終了する
  - `.ke` を正規として扱いつつ、現行 testdata の `.kc` script も build check-only 入力として扱える状態を保つ
  - 完了時には、既存の minimal project と既存 parser/CLI fixture が import 成果物なしで成功する
  - _Requirements: 1.2, 2.5, 5.2_
  - _Depends: 4.1_

- [ ] 5. Validation: 単体・統合・回帰検証で受け入れ条件を固定する
- [ ] 5.1 semantic services の単体テストを追加する
  - module discovery、missing/ambiguous lookup、direct/transitive imports、重複抑制、stable order、cycle、import 先構文エラーを検証する
  - definition collection、duplicate definitions、imported lookup、unimported unresolved、local/import collision、ambiguous imported names を検証する
  - 完了時には、semantic services の各責務が単体テストで失敗分類と診断内容まで確認される
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 2.2, 2.3, 2.4, 3.1, 3.2, 3.3, 3.4, 3.5, 4.1, 4.2, 4.3, 4.4, 4.5_

- [ ] 5.2 build check-only の統合テストを追加する
  - imported definition success、missing import、ambiguous import、cycle、import 先 syntax error、name resolution failure を CLI 終了コード込みで検証する
  - JSON Lines 診断の順序と必須フィールドを検証する
  - 完了時には、`kes build --check-only` が import/name 検証を含む成功・失敗ケースをすべて自動テストで再現する
  - _Requirements: 1.1, 1.4, 1.5, 2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.2, 3.3, 3.4, 3.5, 4.1, 4.2, 4.3, 4.4, 4.5, 5.1, 5.2, 5.3, 5.4, 5.5_
  - _Depends: 4.1, 4.2, 4.3_

- [ ] 5.3 全体回帰と差分品質チェックを実行する
  - 既存 lexer/parser/diagnostic/build tests が意味解析追加後も通ることを確認する
  - `dotnet test`、必要な build check、`git diff --check` を実行し、失敗があれば原因を修正する
  - 完了時には、変更済み source、test、testdata、spec files に空白差分問題がなく、全関連テスト結果を implementation evidence として提示できる
  - _Requirements: 2.5, 5.1, 5.2, 5.3, 5.4, 5.5_
  - _Depends: 5.1, 5.2_
